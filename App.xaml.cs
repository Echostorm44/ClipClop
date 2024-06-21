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
}

