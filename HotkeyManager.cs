using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Win32.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ClipClop;

public class HotKeyManager : IDisposable
{
	private IntPtr OriginalWndProc = IntPtr.Zero;
	private IntPtr ClipClopWindowHandle;
	private static HotKeyManager Instance = null;

	[DllImport("user32.dll")]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll")]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProc newProc);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	private const int GWL_WNDPROC = -4;
	private const int WM_HOTKEY = 0x0312;

	public int ID { get; private set; }
	public Key Key { get; private set; }
	public KeyModifier Modifiers { get; private set; }
	public Action Callback { get; private set; }

	public HotKeyManager(Window window, Key key, KeyModifier modifiers, Action callback)
	{
		Key = key;
		Modifiers = modifiers;
		Callback = callback;
		ID = (int)key + ((int)modifiers * 0x10000);

		ClipClopWindowHandle = window.TryGetPlatformHandle()?.Handle ??
			throw new InvalidOperationException("Failed to get window handle.");

		if(ClipClopWindowHandle == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to get window handle.");
		}

		Instance = this;
		OriginalWndProc = SetWindowLongPtr(ClipClopWindowHandle, GWL_WNDPROC, WndProcHook);

		if(!RegisterHotKey(ClipClopWindowHandle, ID, (uint)Modifiers, (uint)KeyInterop.VirtualKeyFromKey(Key)))
		{
			Instance = null;
			SetWindowLongPtr(ClipClopWindowHandle, GWL_WNDPROC, OriginalWndProc);
			throw new InvalidOperationException("Failed to register hotkey.");
		}
	}

	private static IntPtr WndProcHook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
		if(msg == WM_HOTKEY)
		{
			int caughtID = wParam.ToInt32();
			if(caughtID == Instance.ID)
			{
				Instance.Callback?.Invoke();
				return IntPtr.Zero;
			}
		}
		return CallWindowProc(Instance.OriginalWndProc, hWnd, msg, wParam, lParam);
	}

	public void Dispose()
	{
		if(ClipClopWindowHandle != IntPtr.Zero)
		{
			UnregisterHotKey(ClipClopWindowHandle, ID);
			SetWindowLongPtr(ClipClopWindowHandle, GWL_WNDPROC, OriginalWndProc);
			Instance = null;
			ClipClopWindowHandle = IntPtr.Zero;
		}
	}
}

[Flags]
public enum KeyModifier
{
	None = 0x0000,
	Alt = 0x0001,
	Ctrl = 0x0002,
	NoRepeat = 0x4000,
	Shift = 0x0004,
	Win = 0x0008
}
