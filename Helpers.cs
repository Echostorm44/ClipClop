using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Serialization;

namespace ClipClop;

public static class Helpers
{
    public static object lockLog = true;

    public static void WriteLogEntry(string entry)
    {
        lock(lockLog)
        {
            if(!Directory.Exists(App.LogFolderPath))
            {
                Directory.CreateDirectory(App.LogFolderPath);
            }
            var fileName = $"log-{DateTime.Now:yyyy-MM-dd}.txt";
            using(TextWriter tw = new StreamWriter(System.IO.Path.Combine(App.LogFolderPath, fileName), true))
            {
                tw.Write(entry);
            }
        }
    }

    public static string GetFileContents(string fileName)
    {
        var filePath = System.IO.Path.Combine(App.RootFolderPath, fileName);
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

    public static void WriteFile(string filename, string contents)
    {
        if(!Directory.Exists(App.RootFolderPath))
        {
            Directory.CreateDirectory(App.RootFolderPath);
        }
        var filePath = System.IO.Path.Combine(App.RootFolderPath, filename);
        File.WriteAllText(filePath, contents);
    }

    public static void SaveBitmapSourceToFile(string filePath, BitmapSource image)
    {
        using(var fileStream = new FileStream(filePath, FileMode.Create))
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image as BitmapSource));
            encoder.Save(fileStream);
        }
    }

    public static BitmapSource LoadBitmapSourceFromFile(string filePath)
    {// We load it this way so we don't retain a lock on the file in case we need to delete it later
        var bytes = File.ReadAllBytes(filePath);
        using var ms = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = ms;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return (BitmapSource)bitmap;
    }

    public static Bitmap BitmapFromSource(BitmapSource bitmapsource)
    {
        Bitmap bitmap;
        using(var outStream = new MemoryStream())
        {
            BitmapEncoder enc = new BmpBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bitmapsource));
            enc.Save(outStream);
            bitmap = new Bitmap(outStream);
        }
        return bitmap;
    }
}

public static class WindowPlacement
{
    private static readonly Encoding UTF8Encoder = new UTF8Encoding();
    private static readonly XmlSerializer WinPlacementXmlSerializer = new XmlSerializer(typeof(WINDOWPLACEMENT));

    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

    private const int SW_SHOWNORMAL = 1;
    private const int SW_SHOWMINIMIZED = 2;

    private static void SetPlacement(IntPtr windowHandle, string placementXml)
    {
        if(string.IsNullOrEmpty(placementXml))
        {
            return;
        }

        byte[] xmlBytes = UTF8Encoder.GetBytes(placementXml);

        try
        {
            WINDOWPLACEMENT placement;
            using(MemoryStream memoryStream = new MemoryStream(xmlBytes))
            {
                placement = (WINDOWPLACEMENT)WinPlacementXmlSerializer.Deserialize(memoryStream);
            }

            placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            placement.flags = 0;
            placement.showCmd = placement.showCmd == SW_SHOWMINIMIZED ? SW_SHOWNORMAL : placement.showCmd;
            SetWindowPlacement(windowHandle, ref placement);
        }
        catch(InvalidOperationException)
        {
            // Parsing placement XML failed. Fail silently.
        }
    }

    private static string GetPlacement(IntPtr windowHandle)
    {
        WINDOWPLACEMENT placement;
        GetWindowPlacement(windowHandle, out placement);

        using(MemoryStream memoryStream = new MemoryStream())
        {
            using(XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
            {
                WinPlacementXmlSerializer.Serialize(xmlTextWriter, placement);
                byte[] xmlBytes = memoryStream.ToArray();
                return UTF8Encoder.GetString(xmlBytes);
            }
        }
    }

    public static void ApplyPlacement(this Window window)
    {
        var className = window.GetType().Name;
        try
        {
            var pos = Helpers.GetFileContents(className + ".pos");
            if(string.IsNullOrEmpty(pos))
            {
                return;
            }
            SetPlacement(new WindowInteropHelper(window).Handle, pos);
        }
        catch(Exception ex)
        {
            Helpers.WriteLogEntry(ex.ToString());
        }
    }

    public static void SavePlacement(this Window window)
    {
        var className = window.GetType().Name;
        var pos = GetPlacement(new WindowInteropHelper(window).Handle);
        try
        {
            Helpers.WriteFile(className + ".pos", pos);
        }
        catch(Exception ex)
        {
            Helpers.WriteLogEntry(ex.ToString());
        }
    }
}

// RECT structure required by WINDOWPLACEMENT structure
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public RECT(int left, int top, int right, int bottom)
    {
        this.Left = left;
        this.Top = top;
        this.Right = right;
        this.Bottom = bottom;
    }
}

// POINT structure required by WINDOWPLACEMENT structure
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;

    public POINT(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
}

// WINDOWPLACEMENT stores the position, size, and state of a window
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct WINDOWPLACEMENT
{
    public int length;
    public int flags;
    public int showCmd;
    public POINT minPosition;
    public POINT maxPosition;
    public RECT normalPosition;
}

