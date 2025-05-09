using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Windows.Input;

namespace ClipClop;

public partial class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow();
		}

		base.OnFrameworkInitializationCompleted();
	}

	private void QuitClick(object? sender, System.EventArgs e)
	{
		Program.MainClipboardWatcher?.Dispose();
		Program.ShowHotkeyManager?.Dispose();
		var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
		desktop.Shutdown();
	}

	private void TrayIcon_Clicked(object? sender, System.EventArgs e)
	{
		var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
		desktop.MainWindow.Show();
	}
}
