using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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