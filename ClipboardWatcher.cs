using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace ClipClop;

public class ClipboardWatcher : Control, IDisposable
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public const int WM_CLIPBOARDUPDATE = 0x031D;
    static readonly string[] formats = Enum.GetNames(typeof(ClipboardFormat));
    IProgress<RawClipboardItem> ProgressReporter;
    public IntPtr MyWindowHandle = IntPtr.Zero;
    private static readonly IntPtr WndProcSuccess = IntPtr.Zero;
    HwndSource source;

    public ClipboardWatcher(Window windowSource, IProgress<RawClipboardItem> progress)
    {
        ProgressReporter = progress;
        source = PresentationSource.FromVisual(windowSource) as HwndSource;
        if(source == null)
        {
            throw new ArgumentException(
                "Window source MUST be initialized first, such as in the Window's OnSourceInitialized handler."
                , nameof(windowSource));
        }

        source.AddHook(WndProc);
        MyWindowHandle = new WindowInteropHelper(windowSource).Handle;
        AddClipboardFormatListener(MyWindowHandle);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        try
        {
            if(msg == WM_CLIPBOARDUPDATE)
            {
                ClipChanged();
                handled = true;
            }
        }
        catch(Exception ex)
        {
            var looo = ex;
        }

        return WndProcSuccess;
    }

    private void ClipChanged()
    {
        int maxRetries = 10;
        while(maxRetries-- > 0)
        {
            try
            {
                var iData = Clipboard.GetDataObject(); // Forms version had better error handing but we're trying to get away from that dependancy so we'll do the lifting ourselves System.Windows.Forms.Clipboard.GetDataObject();
                if(iData == null)
                {
                    return;
                }

                ClipboardFormat? format = null;
                object data = null;

                foreach(var f in formats)
                {
                    if(iData.GetDataPresent(f))
                    {
                        format = (ClipboardFormat)Enum.Parse(typeof(ClipboardFormat), f);
                        data = iData.GetData(f);
                        if(format == ClipboardFormat.Text && string.IsNullOrEmpty(data?.ToString()))
                        {
                            continue;
                        }
                        break;
                    }
                }

                if(data == null || format == null)
                {
                    return;
                }
                ProgressReporter.Report(new RawClipboardItem() { Data = data, Format = format.Value });
                return;
            }
            catch(Exception ex)
            {
                /*
                   The the problem here is that using the WPF version of Clipboard sometimes gets blocked from grabbing
                the information from the clipboard because another application is trying at the moment && the WPF
                version doesn't handle the contention as well as it should.  So we will test to see if the error code
                is the OpenClipboard Failed (0x800401D0 (CLIPBRD_E_CANT_OPEN))
                & if it is we will just wait a sec for the other application to finish && try again.  I haven't seen
                this need more than 1 try yet but I'm giving it 10 just in case.  More info here:
                https://stackoverflow.com/questions/12769264/openclipboard-failed-when-copy-pasting-data-from-wpf-datagrid
                
                 */
                if(((System.Runtime.InteropServices.COMException)ex).ErrorCode == -2147221040)
                {
                    Task.Delay(20).Wait();
                }
            }
        }
    }

    public void Dispose()
    {
        RemoveClipboardFormatListener(MyWindowHandle);
        source.RemoveHook(WndProc);
    }
}

public class RawClipboardItem
{
    public object Data { get; set; }
    public ClipboardFormat Format { get; set; }
}

#region Alternate Method Using System.Forms

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Runtime.InteropServices;
//using System.Text;
//using System.Threading.Tasks;

//namespace ClipClop;

//public class ClipboardWatcher : Control, IDisposable
//{
//    [DllImport("user32.dll", CharSet = CharSet.Auto)]
//    public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

//    [DllImport("user32.dll", SetLastError = true)]
//    [return: MarshalAs(UnmanagedType.Bool)]
//    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

//    [DllImport("user32.dll", SetLastError = true)]
//    [return: MarshalAs(UnmanagedType.Bool)]
//    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

//    public const int WM_CLIPBOARDUPDATE = 0x031D;
//    static readonly string[] formats = Enum.GetNames(typeof(ClipboardFormat));
//    IProgress<RawClipboardItem> ProgressReporter;

//    public ClipboardWatcher(IProgress<RawClipboardItem> progress)
//    {
//        ProgressReporter = progress;
//        AddClipboardFormatListener(this.Handle);
//    }

//    protected override void WndProc(ref Message m)
//    {
//        switch(m.Msg)
//        {
//            case WM_CLIPBOARDUPDATE:
//            {
//                ClipChanged();
//            }
//                break;

//            default:
//            {
//                base.WndProc(ref m);
//            }
//                break;
//        }
//    }

//    private void ClipChanged()
//    {
//        var iData = Clipboard.GetDataObject();
//        if(iData == null)
//        {
//            return;
//        }

//        ClipboardFormat? format = null;
//        object data = null;
//        foreach(var f in formats)
//        {
//            if(iData.GetDataPresent(f))
//            {
//                format = (ClipboardFormat)Enum.Parse(typeof(ClipboardFormat), f);
//                data = iData.GetData(f);
//                if(format == ClipboardFormat.Text && string.IsNullOrEmpty(data?.ToString()))
//                {
//                    continue;
//                }
//                break;
//            }
//        }

//        if(data == null || format == null)
//        {
//            return;
//        }
//        ProgressReporter.Report(new RawClipboardItem() { Data = data, Format = format.Value });
//    }

//    protected override void Dispose(bool disposing)
//    {
//        RemoveClipboardFormatListener(this.Handle);
//        base.Dispose(disposing);
//    }

//    public void Dispose()
//    {
//        throw new NotImplementedException();
//    }
//}

//public class RawClipboardItem
//{
//    public object Data { get; set; }
//    public ClipboardFormat Format { get; set; }
//}

#endregion

