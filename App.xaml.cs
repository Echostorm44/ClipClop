using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace ClipClop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static string? RootFolderPath { get; set; }
    public static string? LogFolderPath { get; set; }
    public static string? ImageFolderPath { get; set; }
    public static string? SavedPinsPath { get; set; }
    public static string? SettingsPath { get; set; }

    public App()
    {
        RootFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            + "\\ClipClop\\";
        LogFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            + "\\ClipClop\\Logs\\";
        ImageFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            + "\\ClipClop\\Images\\";
        SavedPinsPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            + "\\ClipClop\\pins.json";
        SettingsPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
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
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
    }

    private void CloseWindow_Event(object sender, RoutedEventArgs e)
    {
        if(e.Source != null)
        {
            this.CloseWind(Window.GetWindow((FrameworkElement)e.Source));
        }
    }

    private void AutoMinimize_Event(object sender, RoutedEventArgs e)
    {
        if(e.Source != null)
        {
            this.MaximizeRestore(Window.GetWindow((FrameworkElement)e.Source));
        }
    }

    private void Minimize_Event(object sender, RoutedEventArgs e)
    {
        if(e.Source != null)
        {
            this.MinimizeWind(Window.GetWindow((FrameworkElement)e.Source));
        }
    }

    public void CloseWind(Window window)
    {
        window?.Close();
    }

    public void MaximizeRestore(Window window)
    {
        if(window == null)
        {
            return;
        }

        switch(window.WindowState)
        {
            case WindowState.Normal:
                window.WindowState = WindowState.Maximized;
                break;
            case WindowState.Minimized:
            case WindowState.Maximized:
                window.WindowState = WindowState.Normal;
                break;
        }
    }

    public void MinimizeWind(Window window)
    {
        if(window != null)
        {
            window.WindowState = WindowState.Minimized;
        }
    }
}

public static class CornerRadiusHelper
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(CornerRadius), typeof(CornerRadiusHelper), new PropertyMetadata(new CornerRadius(0)));

    public static void SetValue(DependencyObject element, CornerRadius value)
    {
        element.SetValue(ValueProperty, value);
    }

    public static CornerRadius GetValue(DependencyObject element)
    {
        return (CornerRadius)element.GetValue(ValueProperty);
    }
}