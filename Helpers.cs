using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using HarfBuzzSharp;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ClipClop;

public static class Helpers
{
	public static object lockLog = true;

	public static void OpenLogFile()
	{
		var fileName = $"log-{DateTime.Now:yyyy-MM-dd}.txt";
		var path = Path.Combine(Program.LogFolderPath, fileName);
		if(!File.Exists(path))
		{
			return;
		}
		Process.Start("notepad.exe", path);
	}

	public static void WriteLogEntry(string entry)
	{
		lock(lockLog)
		{
			var fileName = $"log-{DateTime.Now:yyyy-MM-dd}.txt";
			using(TextWriter tw = new StreamWriter(Path.Combine(Program.LogFolderPath, fileName), true))
			{
				tw.WriteLine(entry);
			}
		}
		var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
		WindowNotificationManager wnm = new WindowNotificationManager(TopLevel.GetTopLevel(desktop.MainWindow));
		Notification not = new Notification("Error", entry, NotificationType.Error,
			TimeSpan.FromMinutes(1), () =>
		{
			OpenLogFile();
		});
		desktop.MainWindow.Show();
		wnm.Show(not);
	}

	public static string Encrypt(string clearText)
	{
		var inputBytes = Encoding.Unicode.GetBytes(clearText);
		var outputBytes = AesConvert(inputBytes, aes => aes.CreateEncryptor());
		return Convert.ToBase64String(outputBytes);
	}

	public static string Decrypt(string cipherText)
	{
		var inputBytes = Convert.FromBase64String(cipherText.Replace(" ", "+"));
		var outputBytes = AesConvert(inputBytes, aes => aes.CreateDecryptor());
		return Encoding.Unicode.GetString(outputBytes);
	}

	private static byte[] AesConvert(byte[] inputBytes, Func<Aes, ICryptoTransform> convert)
	{
		using var aes = Aes.Create();
		var key = "populateThisFromAppSettings";
		var salt = new byte[] { 0x52, 0x79, 0x62, 0x6e, 0x20, 0x4e, 0x61, 0x63, 0x62, 0x72, 0x62, 0x71, 0x6f };
		var derivedBytes = new Rfc2898DeriveBytes(key, salt, 1000, HashAlgorithmName.SHA1);
		aes.Key = derivedBytes.GetBytes(32);
		aes.IV = derivedBytes.GetBytes(16);
		using var ms = new MemoryStream();
		using var cs = new CryptoStream(ms, convert(aes), CryptoStreamMode.Write);
		cs.Write(inputBytes, 0, inputBytes.Length);
		cs.Close();
		return ms.ToArray();
	}

	public static string GetRootFileContents(string fileName)
	{
		var filePath = Path.Combine(Program.RootFolderPath, fileName);
		if(!File.Exists(filePath))
		{
			return "";
		}
		else
		{
			var fileText = File.ReadAllText(filePath);
			return fileText;
		}
	}

	public static void WriteFileToRoot(string filename, string contents)
	{
		if(!Directory.Exists(Program.RootFolderPath))
		{
			Directory.CreateDirectory(Program.RootFolderPath);
		}
		var filePath = Path.Combine(Program.RootFolderPath, filename);
		File.WriteAllText(filePath, contents);
	}

	public static void SetStartup()
	{
		using RegistryKey? rk = Registry.CurrentUser.OpenSubKey
			(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
		var launcherPath = Path.Combine(AppContext.BaseDirectory, "launcher.exe");
		if(rk == null)
		{
			return;
		}
		rk.SetValue("ClipClop", launcherPath);
	}

	public static void RemoveStartup()
	{
		using RegistryKey? rk = Registry.CurrentUser.OpenSubKey
			("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
		if(rk == null)
		{
			return;
		}
		rk.DeleteValue("ClipClop", false);
	}

	public static void SaveSettings()
	{
		try
		{
			var serializedSettings = JsonSerializer.Serialize(Program.Settings,
				SourceGenerationContext.Default.MySettings);
			File.WriteAllText(Program.SettingsPath, serializedSettings);
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	public static MySettings ReadSettings()
	{
		var result = new MySettings();
		try
		{
			if(File.Exists(Program.SettingsPath))
			{
				var fileText = File.ReadAllText(Program.SettingsPath);
				if(string.IsNullOrEmpty(fileText))
				{
					SaveSettings();
				}
				result = JsonSerializer.Deserialize<MySettings>(fileText,
					SourceGenerationContext.Default.MySettings) ?? new MySettings();
			}
			else
			{
				SaveSettings();
			}
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
		return result;
	}

	public static List<ClipItem> ReadSavedPins()
	{
		var result = new List<ClipItem>();
		try
		{
			if(File.Exists(Program.SavedPinsPath))
			{
				var fileText = File.ReadAllText(Program.SavedPinsPath);
				fileText = Decrypt(fileText);
				result = JsonSerializer.Deserialize<List<ClipItem>>(fileText,
					SourceGenerationContext.Default.ListClipItem) ?? new List<ClipItem>();
				foreach(var item in result)
				{
					if(item.IsImage)
					{
						item.Image = new Bitmap(item.ImageFilePath);
					}
				}
			}
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
		return result;
	}
}

public static class Debouncer
{
	static ConcurrentDictionary<string, CancellationTokenSource> tokens = new();

	public static void Debounce(string uniqueKey, Action action, int milliSeconds)
	{
		var token = tokens.AddOrUpdate(uniqueKey,
			(key) => //key not found - create new
			{
				return new CancellationTokenSource();
			},
			(key, existingToken) => //key found - cancel task and recreate
			{
				existingToken.Cancel(); //cancel previous
				return new CancellationTokenSource();
			});

		//schedule execution after pause
		Task.Delay(milliSeconds, token.Token).ContinueWith(task =>
		{
			if(!task.IsCanceled)
			{
				action(); //run
				if(tokens.TryRemove(uniqueKey, out var cts))
				{
					cts.Dispose(); //cleanup
				}
			}
		}, token.Token);
	}
}

public static class WindowPositionManager
{
	private const string SaveFilename = "window_position.txt";

	public static void SaveWindowPosition(Window window)
	{
		if(window == null || !window.IsVisible)
		{
			return;
		}

		var position = window.Position;
		var size = window.Bounds.Size;

		var data = $"{position.X},{position.Y},{size.Width},{size.Height}";
		Helpers.WriteFileToRoot(SaveFilename, data);
	}

	public static void RestoreWindowPosition(Window window)
	{
		var data = Helpers.GetRootFileContents(SaveFilename);
		if(window == null || string.IsNullOrEmpty(data))
		{
			return;
		}

		try
		{
			var parts = data.Split(',');

			if(parts.Length >= 4 &&
				int.TryParse(parts[0], out int x) &&
				int.TryParse(parts[1], out int y) &&
				double.TryParse(parts[2], out double width) &&
				double.TryParse(parts[3], out double height))
			{
				window.Position = new PixelPoint(x, y);
				window.Width = width;
				window.Height = height;
			}
		}
		catch
		{
		}
	}
}

public static class ClipboardHelper
{
	const uint CF_DIB = 8;
	const uint GMEM_MOVEABLE = 0x0002;

	[DllImport("user32.dll", SetLastError = true)]
	static extern bool OpenClipboard(IntPtr hWndNewOwner);

	[DllImport("user32.dll", SetLastError = true)]
	static extern bool CloseClipboard();

	[DllImport("user32.dll", SetLastError = true)]
	static extern bool EmptyClipboard();

	[DllImport("user32.dll", SetLastError = true)]
	static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern IntPtr GlobalAlloc(uint uFlags, int dwBytes);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern IntPtr GlobalLock(IntPtr hMem);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool GlobalUnlock(IntPtr hMem);

	public static void SetBitmapToClipboard(Bitmap bitmap)
	{
		using var dibStream = new MemoryStream();
		WriteDib(bitmap, dibStream);
		var dib = dibStream.ToArray();

		IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, dib.Length);
		if(hGlobal == IntPtr.Zero)
		{
			throw new Exception("GlobalAlloc failed.");
		}

		IntPtr ptr = GlobalLock(hGlobal);
		if(ptr == IntPtr.Zero)
		{
			throw new Exception("GlobalLock failed.");
		}

		Marshal.Copy(dib, 0, ptr, dib.Length);
		GlobalUnlock(hGlobal);

		if(!OpenClipboard(IntPtr.Zero))
		{
			throw new Exception("OpenClipboard failed.");
		}

		try
		{
			if(!EmptyClipboard())
			{
				throw new Exception("EmptyClipboard failed.");
			}

			if(SetClipboardData(CF_DIB, hGlobal) == IntPtr.Zero)
			{
				throw new Exception("SetClipboardData failed.");
			}

			hGlobal = IntPtr.Zero; // ownership transferred
		}
		finally
		{
			CloseClipboard();
		}
	}

	private static void WriteDib(Bitmap bitmap, Stream stream)
	{
		var pixelSize = bitmap.PixelSize;
		var width = pixelSize.Width;
		var height = pixelSize.Height;
		const int bpp = 32; // Avalonia always outputs BGRA8888

		int rowBytes = width * (bpp / 8);
		int imageSize = rowBytes * height;
		const int headerSize = 40;
		int stride = width * 4; // assuming Bgra8888 (4 bytes per pixel)
		int size = stride * height;

		byte[] pixelData = new byte[imageSize];
		var handle1 = GCHandle.Alloc(pixelData, GCHandleType.Pinned);
		try
		{
			bitmap.CopyPixels(new PixelRect(0, 0, width, height), handle1.AddrOfPinnedObject(), size, stride);

			using var writer = new BinaryWriter(stream);

			// BITMAPINFOHEADER (40 bytes)
			writer.Write(headerSize);       // biSize
			writer.Write(width);            // biWidth
			writer.Write(height);           // biHeight (positive = bottom-up)
			writer.Write((short)1);         // biPlanes
			writer.Write((short)32);        // biBitCount
			writer.Write(0);                // biCompression = BI_RGB
			writer.Write(imageSize);        // biSizeImage
			writer.Write(0);                // biXPelsPerMeter
			writer.Write(0);                // biYPelsPerMeter
			writer.Write(0);                // biClrUsed
			writer.Write(0);                // biClrImportant

			// Write pixel rows bottom-up (flip vertically)
			for(int y = height - 1; y >= 0; y--)
			{
				int offset = y * rowBytes;
				writer.Write(pixelData, offset, rowBytes);
			}
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
		finally
		{
			handle1.Free();
		}
	}
}

// We need this for AOT && tree shaking to work correctly
[JsonSerializable(typeof(MySettings))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(Key))]
[JsonSerializable(typeof(KeyModifier))]
[JsonSerializable(typeof(ClipItem))]
[JsonSerializable(typeof(List<ClipItem>))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(MySettings))]
public class MySettings
{
	public Key ShowHotKey { get; set; }
	public KeyModifier ShowHotKeyMod { get; set; }
	public bool LaunchAtStartup { get; set; }
	public bool OpenAtMousePointer { get; set; }

	public MySettings()
	{
		ShowHotKey = Key.OemTilde;
		ShowHotKeyMod = KeyModifier.Ctrl;
		LaunchAtStartup = false;
		OpenAtMousePointer = false;
	}
}