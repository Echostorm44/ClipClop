using Avalonia;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ClipClop;

class Program
{
	public static string? LogFolderPath { get; set; }
	public static string? RootFolderPath { get; set; }
	public static string? ImageFolderPath { get; set; }
	public static string? SavedPinsPath { get; set; }
	public static string? SettingsPath { get; set; }
	public static MySettings Settings { get; set; }
	// We are making these global as a workaround because we can't hide the top bar buttons (Minimize,
	// Maximize, Close) in Avalonia
	// so we're overriding the close button to hide the window instead of closing it.  A real close is
	// done from the tray icon but that gets called from App, we need to make sure we release our handles
	// to the hotkeys and clipboard watcher so we need them to be well accessable && thus this.
	public static HotKeyManager ShowHotkeyManager { get; set; }
	public static ClipboardWatcher MainClipboardWatcher { get; set; }

	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static void Main(string[] args)
	{
		try
		{
			RootFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
			+ "\\ClipClop\\";
			LogFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
				+ "\\ClipClop\\Logs\\";
			ImageFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
				+ "\\ClipClop\\Images\\";
			SavedPinsPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
				+ "\\ClipClop\\pins.json";
			SettingsPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
				+ "\\ClipClop\\settings.json";

			if(!Directory.Exists(RootFolderPath))
			{
				Directory.CreateDirectory(RootFolderPath);
			}
			if(!Directory.Exists(LogFolderPath))
			{
				Directory.CreateDirectory(LogFolderPath);
			}
			if(!Directory.Exists(ImageFolderPath))
			{
				Directory.CreateDirectory(ImageFolderPath);
			}
			Settings = new MySettings();
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.WithInterFont()
				.LogToTrace();
	}
}