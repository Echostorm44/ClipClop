﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ClipClop;

public class HotKey : IDisposable
{
    private static Dictionary<int, HotKey> HotKeyCallbackLookup;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, UInt32 fsModifiers, UInt32 vlc);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WmHotKey = 0x0312;

    private bool isDisposed = false;

    public Key Key { get; private set; }
    public KeyModifier KeyModifiers { get; private set; }
    public Action<HotKey> Action { get; private set; }
    public int Id { get; set; }

    public HotKey(Key k, KeyModifier keyModifiers, Action<HotKey> action, bool register = true)
    {
        Key = k;
        KeyModifiers = keyModifiers;
        Action = action;
        if(register)
        {
            Register();
        }
    }

    public bool Register()
    {
        int virtualKeyCode = KeyInterop.VirtualKeyFromKey(Key);
        Id = virtualKeyCode + ((int)KeyModifiers * 0x10000);
        bool result = RegisterHotKey(IntPtr.Zero, Id, (UInt32)KeyModifiers, (UInt32)virtualKeyCode);

        if(HotKeyCallbackLookup == null)
        {
            HotKeyCallbackLookup = new Dictionary<int, HotKey>();
            ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);
        }

        HotKeyCallbackLookup.Add(Id, this);

        Debug.Print(result.ToString() + ", " + Id + ", " + virtualKeyCode);
        return result;
    }

    public void Unregister()
    {
        HotKey hotKey;
        if(HotKeyCallbackLookup.TryGetValue(Id, out hotKey))
        {
            UnregisterHotKey(IntPtr.Zero, Id);
            HotKeyCallbackLookup.Remove(Id);
        }
    }

    private static void ComponentDispatcherThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        if(!handled)
        {
            if(msg.message == WmHotKey)
            {
                HotKey hotKey;

                if(HotKeyCallbackLookup.TryGetValue((int)msg.wParam, out hotKey))
                {
                    if(hotKey.Action != null)
                    {
                        hotKey.Action.Invoke(hotKey);
                    }
                    handled = true;
                }
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if(!this.isDisposed)
        {
            if(disposing)
            {
                Unregister();
            }
            isDisposed = true;
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