﻿using Microsoft.VisualBasic;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace ClipClop;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("msvcrt.dll")]
    private static extern int memcmp(IntPtr b1, IntPtr b2, long count);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint wCmd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

    private enum ShowWindowCommands : int
    {
        /// <summary>
        /// Activates and displays the window. If the window is minimized or  maximized, the system restores it to its
        /// original size and position.  An application should specify this flag when restoring a minimized window.
        /// </summary>
        Restore = 9,
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_MENU = 0x12;
    private const byte VK_TAB = 0x09;
    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    ClipboardWatcher MyWatcher;
    Progress<RawClipboardItem> pro;
    public ObservableCollection<ClipItem> ClipItems { get; set; }
    private ListCollectionView MyCollectionView;

    public ICommand TogglePinCommand { get; set; }
    public ICommand DeleteClipCommand { get; set; }
    public ICommand PasteClipCommand { get; set; }
    public ICommand ListViewEnterCommand { get; set; }
    public ICommand EscapeCommand { get; set; }
    public HotKey ShowHotKey;

    public MainWindow()
    {
        this.TogglePinCommand = new RelayCommand((a) => TogglePin((ClipItem)a));
        this.DeleteClipCommand = new RelayCommand((a) => DeleteClip((ClipItem)a));
        this.PasteClipCommand = new RelayCommand((a) => PasteClip((ClipItem)a));
        this.ListViewEnterCommand = new RelayCommand((a) => ListViewHitEnter());
        this.EscapeCommand = new RelayCommand((a) => EscapeButtonClick());
        ClipItems = new ObservableCollection<ClipItem>();
        MyCollectionView = CollectionViewSource.GetDefaultView(ClipItems) as ListCollectionView;
        MyCollectionView.IsLiveSorting = true;
        MyCollectionView.SortDescriptions.Add(new SortDescription("Pinned", ListSortDirection.Descending));
        MyCollectionView.SortDescriptions.Add(new SortDescription("DateTimeAdded", ListSortDirection.Descending));

        InitializeComponent();
        ShowHotKey = new HotKey(Key.W, KeyModifier.Ctrl, RestoreMe);
        this.DataContext = this;
        lvMain.ItemsSource = MyCollectionView;
    }

    public void SetNewHotkey(Key key, KeyModifier mod)
    {
        ShowHotKey.Unregister();
        ShowHotKey.Dispose();
        ShowHotKey = new HotKey(key, mod, RestoreMe);
    }

    void ListViewHitEnter()
    {
        if(lvMain.SelectedItem == null)
        {
            return;
        }
        var foo = (ClipItem)lvMain.SelectedItem;
        PasteClip(foo);
    }

    public void RestoreMe(HotKey hotKey)
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        var loo = ShowWindow(MyWatcher.MyWindowHandle, ShowWindowCommands.Restore);
        SetForegroundWindow(MyWatcher.MyWindowHandle);
        lvMain.Focus();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        WindowPlacement.SavePlacement(this);
        MyWatcher.Dispose();
        ShowHotKey.Unregister();
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        pro = new Progress<RawClipboardItem>();
        pro.ProgressChanged += (s, e) =>
        {
            if(e.Format == ClipboardFormat.Bitmap)
            {
                var img = (BitmapSource)e.Data;
                var existing = ClipItems.Where(b => b.IsImage).SingleOrDefault(a => 
                    CompareMemCmp(Helpers.BitmapFromSource(img), Helpers.BitmapFromSource(a.Image)));
                if(existing == null)
                {
                    ClipItems.Add(new ClipItem()
                    {
                        Text = "",
                        DateTimeAdded = DateTime.Now,
                        IsImage = true,
                        Pinned = false,
                        Image = img,
                    });
                }
                else
                {
                    existing.DateTimeAdded = DateTime.Now;
                }
            }
            else if(e.Format == ClipboardFormat.Text || e.Format == ClipboardFormat.UnicodeText)
            {
                var text = e.Data?.ToString() ?? "";
                var existingItem = ClipItems.SingleOrDefault(a => a.Text == text);
                if(existingItem == null)
                {
                    ClipItems.Add(new ClipItem()
                    {
                        Text = text,
                        DateTimeAdded = DateTime.Now,
                        IsImage = false,
                        Pinned = false,
                    });
                }
                else
                {
                    existingItem.DateTimeAdded = DateTime.Now;
                }
            }
        };
        MyWatcher = new ClipboardWatcher(this, pro);
        WindowPlacement.ApplyPlacement(this);
        var savedPins = Helpers.GetFileContents(App.SavedPinsPath);
        if(!string.IsNullOrEmpty(savedPins))
        {
            var pins = JsonSerializer.Deserialize<List<ClipItem>>(savedPins);
            foreach(var pin in pins)
            {
                if(pin.IsImage)
                {
                    pin.Image = Helpers.LoadBitmapSourceFromFile(pin.ImageFilePath);
                }
                ClipItems.Add(pin);
            }
        }
        lvMain.Focus();
    }

    private void TogglePin(ClipItem item)
    {
        if(item != null)
        {
            item.Pinned = !item.Pinned;
            // Save pinned images, delete unpinned images, overwrite saved pins file            
            var unsavedImages = ClipItems.Where(a => a.IsImage && string.IsNullOrEmpty(a.ImageFilePath)).ToList();
            foreach(var unsaved in unsavedImages)
            {
                var fileName = unsaved.DateTimeAdded.ToString("yyyy-MM-dd-HH-mm-ss") + ".png";
                var filePath = System.IO.Path.Combine(App.ImageFolderPath, fileName);
                Helpers.SaveBitmapSourceToFile(filePath, unsaved.Image);
                unsaved.ImageFilePath = filePath;
            }
            var pins = ClipItems.Where(a => a.Pinned).ToList();

            var toDelete = ClipItems.Where(a => !a.Pinned && !string.IsNullOrEmpty(a.ImageFilePath)).ToList();
            foreach(var dieImgDie in toDelete)
            {
                File.Delete(dieImgDie.ImageFilePath);
                dieImgDie.ImageFilePath = "";
            }

            var serializedPins = JsonSerializer.Serialize(pins);
            Helpers.WriteFile(App.SavedPinsPath, serializedPins);
        }
    }

    void DeleteClip(ClipItem item)
    {
        ClipItems.Remove(item);
    }

    void PasteClip(ClipItem item)
    {
        if(item == null)
        {
            return;
        }
        if(item.IsImage)
        {
            Clipboard.SetImage(item.Image);
        }
        else
        {
            Clipboard.SetText(item.Text);
        }
        SendAltTabAndPaste();
        this.WindowState = WindowState.Minimized;
    }

    public static void SendAltTabAndPaste()
    {
        // Press Alt key
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);

        // Press Tab key
        keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);

        // Release Tab key
        keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        // Release Alt key
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        // Wait for a short delay to allow the window to switch
        System.Threading.Thread.Sleep(500);

        // Press Ctrl key
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);

        // Press V key
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);

        // Release V key
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        // Release Ctrl key
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public static bool CompareMemCmp(Bitmap b1, Bitmap b2)
    {
        if(b1 == null != (b2 == null))
        {
            return false;
        }

        if(b1.Size != b2.Size)
        {
            return false;
        }

        var bd1 = b1.LockBits(new System.Drawing.Rectangle(new System.Drawing.Point(0, 0), b1.Size), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var bd2 = b2.LockBits(new System.Drawing.Rectangle(new System.Drawing.Point(0, 0), b2.Size), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            IntPtr bd1scan0 = bd1.Scan0;
            IntPtr bd2scan0 = bd2.Scan0;

            int stride = bd1.Stride;
            int len = stride * b1.Height;

            return memcmp(bd1scan0, bd2scan0, len) == 0;
        }
        finally
        {
            b1.UnlockBits(bd1);
            b2.UnlockBits(bd2);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    void ExitButton(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    void EscapeButtonClick()
    {
        this.WindowState = WindowState.Minimized;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SettingsWindow settings = new SettingsWindow(this);
        settings.ShowDialog();
        //myTaskBarIcon.ShowBalloonTip("Settings", "Settings are not yet implemented", 
        //    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Warning);
    }
}

public class ClipItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Text { get; set; }
    [JsonIgnore]
    public BitmapSource Image { get; set; }
    public string ImageFilePath { get; set; }
    DateTime dateTimeAdded;
    public DateTime DateTimeAdded
    {
        get
        {
            return dateTimeAdded;
        }
        set
        {
            if(dateTimeAdded == value)
            {
                return;
            }

            dateTimeAdded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DateTimeAdded)));
        }
    }
    bool pinned;
    public bool Pinned
    {
        get
        {
            return pinned;
        }
        set
        {
            if(pinned == value)
            {
                return;
            }

            pinned = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Pinned)));
        }
    }
    public bool IsImage { get; set; }
}

public class PinIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isPinned = (bool)value;
        if(isPinned)
        {
            return App.Current.FindResource("FontAwesomeSolid");
        }
        else
        {
            return App.Current.FindResource("FontAwesomeReg");
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FalseBoolToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentageConverter : MarkupExtension, IValueConverter
{
    private static PercentageConverter _instance;

    #region IValueConverter Members

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Convert.ToDouble(value) * System.Convert.ToDouble(parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    #endregion

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return _instance ?? (_instance = new PercentageConverter());
    }
}

public class RelayCommand : ICommand
{
    private Action<object> execute;
    private Func<object, bool> canExecute;

    public event EventHandler CanExecuteChanged
    {
        add
        {
            CommandManager.RequerySuggested += value;
        }
        remove
        {
            CommandManager.RequerySuggested -= value;
        }
    }

    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public bool CanExecute(object parameter)
    {
        return this.canExecute == null || this.canExecute(parameter);
    }

    public void Execute(object parameter)
    {
        this.execute(parameter);
    }
}