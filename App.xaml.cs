using System.Configuration;
using System.Data;
using System.Windows;

namespace ClipClop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static string? AppFolderPath { get; set; }
    public static string? AppLogFolderPath { get; set; }

    public App()
    {
        AppFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            + "\\ClipClop\\";
        AppLogFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            + "\\ClipClop\\Logs\\";
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
    }
}

