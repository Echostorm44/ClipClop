using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClipClop;

public class ClipboardWatcher : IDisposable
{
	private readonly Thread MessageLoopThread;
	private readonly Action<object> OnClipboardChange;
	private IntPtr WindowPointer;
	private bool IsRunning;
	private const int WM_CLIPBOARDUPDATE = 0x031D;
	private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);
	private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
	WndProcDelegate wndProc;

	public ClipboardWatcher(Action<object> onClipboardChange)
	{
		try
		{
			OnClipboardChange = onClipboardChange;
			wndProc = WndProc;

			var wc = new WNDCLASS
			{
				lpszClassName = "ClipboardWatcherWindow",
				lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc)
			};

			ushort classAtom = RegisterClass(ref wc);
			if(classAtom == 0)
			{
				throw new Exception("Failed to register window class");
			}

			WindowPointer = CreateWindowEx(
				0, wc.lpszClassName, string.Empty, 0,
				0, 0, 0, 0,
				HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

			if(WindowPointer == IntPtr.Zero)
			{
				throw new Exception("Failed to create message-only window");
			}

			AddClipboardFormatListener(WindowPointer);
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
		try
		{
			if(msg == WM_CLIPBOARDUPDATE)
			{
				Debouncer.Debounce("ClipboardUpdate", () =>
				{
					DoTheWork();
				}, 200);
			}
			return DefWindowProc(hwnd, msg, wParam, lParam);
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
		Helpers.WriteLogEntry("Returned an empty pointer in WndProc Clipboard Watcher");
		return IntPtr.Zero;
	}

	private void DoTheWork()
	{
		Dispatcher.UIThread.Post(async () =>
		{
			try
			{
				#region Avalonia Clipboard API Way That Doesn't Work Well For Posterity

				// This looks nice && compact using Avalonia's Clipboard API but when you try to read the format
				// you get an almost infinite number of variations that make it really hard to be sure you're
				// going to get the right one. So we go the hard way.
				//var clipboard = _window.Clipboard;
				//var formats = await clipboard.GetFormatsAsync();
				//if(formats.Any(a => a.ToLower().Contains("text")))
				//{
				//	var result = await _window.Clipboard.GetTextAsync();
				//	if(!string.IsNullOrEmpty(result))// comes back null for web image
				//	{
				//		_onClipboardChange?.Invoke(result);
				//		return;
				//	}
				//}
				//var imageArray = await clipboard.GetDataAsync("PNG");
				//if(imageArray != null)
				//{
				//	var bitmap = new Bitmap(new MemoryStream((byte[])imageArray));
				//	_onClipboardChange?.Invoke(bitmap);
				//}
				//return;

				#endregion

				// The hard way but it works
				if(!OpenClipboard(WindowPointer))
				{
					return;
				}
				uint nextFormat = EnumClipboardFormats(0);
				while(nextFormat != 0)
				{
					if(nextFormat == 0x00000001) // CF_TEXT
					{
						var rawTextPointer = GetClipboardData(13);
						if(rawTextPointer != IntPtr.Zero)
						{
							var text = Marshal.PtrToStringAuto(rawTextPointer);
							if(!string.IsNullOrEmpty(text))
							{
								OnClipboardChange?.Invoke(text);
								return;
							}
						}
					}
					else if(nextFormat == 0x00000002)
					{
						IntPtr bitmapPointer = IntPtr.Zero;
						IntPtr deviceContextPointer = IntPtr.Zero;
						try
						{
							bitmapPointer = GetClipboardData(2); // CF_BITMAP
							if(bitmapPointer == IntPtr.Zero)
							{
								continue;
							}

							BITMAP bmp = new BITMAP();
							if(GetObject(bitmapPointer, Marshal.SizeOf<BITMAP>(), out bmp) == 0)
							{
								continue;
							}

							deviceContextPointer = CreateCompatibleDC(IntPtr.Zero);
							var info = new BITMAPINFO
							{
								bmiHeader = new BITMAPINFOHEADER
								{
									biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
									biWidth = bmp.bmWidth,
									biHeight = -bmp.bmHeight,
									biPlanes = 1,
									biBitCount = bmp.bmBitsPixel,
									biCompression = 0 //BI_PNG = 0x0005,
								}
							};

							var pixelData = GetPixelData(deviceContextPointer, bitmapPointer, ref info);
							var finalBitmap = CreateAvaloniaBitmap(pixelData, ref info);
							OnClipboardChange?.Invoke(finalBitmap);
							return;
						}
						finally
						{
							if(deviceContextPointer != IntPtr.Zero)
							{
								DeleteDC(deviceContextPointer);
							}
							if(bitmapPointer != IntPtr.Zero)
							{
								DeleteObject(bitmapPointer);
							}
						}
					}
					nextFormat = EnumClipboardFormats(nextFormat);
				}
			}
			catch(Exception ex)
			{
				Helpers.WriteLogEntry(ex.ToString());
				Console.WriteLine("Clipboard read failed: " + ex.Message);
			}
			finally
			{
				CloseClipboard();
			}
		});
	}

	private static byte[] GetPixelData(IntPtr hdc, IntPtr hBitmap, ref BITMAPINFO info)
	{
		// First call to GetDIBits to get buffer size
		GetDIBits(hdc, hBitmap, 0, 0, IntPtr.Zero, ref info, 0);

		int pixelDataSize = (int)info.bmiHeader.biSizeImage;
		byte[] pixelData = new byte[pixelDataSize];

		IntPtr pixels = Marshal.AllocHGlobal(pixelDataSize);
		try
		{
			if(GetDIBits(hdc, hBitmap, 0, (uint)Math.Abs(info.bmiHeader.biHeight),
				pixels, ref info, 0) == 0)
			{
				throw new InvalidOperationException("Failed to get DIBits");
			}

			Marshal.Copy(pixels, pixelData, 0, pixelDataSize);
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
		finally
		{
			Marshal.FreeHGlobal(pixels);
		}

		return pixelData;
	}

	private static Bitmap CreateAvaloniaBitmap(byte[] pixelData, ref BITMAPINFO info)
	{
		var bitmap = new WriteableBitmap(
			new PixelSize(info.bmiHeader.biWidth, Math.Abs(info.bmiHeader.biHeight)),
			new Vector(96, 96),
			PixelFormat.Bgra8888,
			AlphaFormat.Opaque);

		using(var buffer = bitmap.Lock())
		{
			Marshal.Copy(pixelData, 0, buffer.Address, pixelData.Length);
		}

		return bitmap;
	}

	public void Dispose()
	{
		try
		{
			IsRunning = false;
			RemoveClipboardFormatListener(WindowPointer);
			DestroyWindow(WindowPointer);
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	#region Win32 API

	[DllImport("gdi32.dll")]
	private static extern int GetDIBits(
		IntPtr hdc, IntPtr hbm, uint start, uint lines,
		IntPtr pixels, ref BITMAPINFO info, uint usage);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr hObject);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	private static extern int GetObject(IntPtr hObject, int nCount, out BITMAP lpObject);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr GetClipboardData(uint uFormat);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool OpenClipboard(IntPtr hWndNewOwner);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool CloseClipboard();

	[DllImport("user32.dll", SetLastError = true)]
	public static extern uint EnumClipboardFormats(uint format);

	[DllImport("user32.dll")]
	private static extern bool AddClipboardFormatListener(IntPtr hwnd);

	[DllImport("user32.dll")]
	private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr CreateWindowEx(
		int dwExStyle, string lpClassName, string lpWindowName,
		int dwStyle, int x, int y, int nWidth, int nHeight,
		IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool DestroyWindow(IntPtr hwnd);

	[DllImport("user32.dll")]
	private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

	[DllImport("user32.dll")]
	private static extern bool TranslateMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	private static extern IntPtr DispatchMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern ushort RegisterClass([In] ref WNDCLASS lpWndClass);

	[StructLayout(LayoutKind.Sequential)]
	private struct WNDCLASS
	{
		public uint style;
		public IntPtr lpfnWndProc;
		public int cbClsExtra;
		public int cbWndExtra;
		public IntPtr hInstance;
		public IntPtr hIcon;
		public IntPtr hCursor;
		public IntPtr hbrBackground;
		public string lpszMenuName;
		public string lpszClassName;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MSG
	{
		public IntPtr hwnd;
		public uint message;
		public IntPtr wParam;
		public IntPtr lParam;
		public uint time;
		public POINT pt;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int x;
		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct BITMAPINFO
	{
		public BITMAPINFOHEADER bmiHeader;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public byte[] bmiColors;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct BITMAPINFOHEADER
	{
		public int biSize;
		public int biWidth;
		public int biHeight;
		public ushort biPlanes;
		public ushort biBitCount;
		public uint biCompression;
		public uint biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public uint biClrUsed;
		public uint biClrImportant;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct BITMAP
	{
		public int bmType;
		public int bmWidth;
		public int bmHeight;
		public int bmWidthBytes;
		public ushort bmPlanes;
		public ushort bmBitsPixel;
		public IntPtr bmBits;
	}

	#endregion
}

