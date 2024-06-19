using Microsoft.VisualBasic;
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
    [DllImport("msvcrt.dll")]
    private static extern int memcmp(IntPtr b1, IntPtr b2, long count);

    ClipboardWatcher watcher;
    Progress<RawClipboardItem> pro;
    public ObservableCollection<ClipItem> ClipItems { get; set; }
    private ListCollectionView MyCollectionView;

    public ICommand TogglePinCommand { get; set; }
    public ICommand DeleteClipCommand { get; set; }

    public MainWindow()
    {
        this.TogglePinCommand = new RelayCommand((a) => TogglePin((ClipItem)a));
        this.DeleteClipCommand = new RelayCommand((a) => DeleteClip((ClipItem)a));
        ClipItems = new ObservableCollection<ClipItem>();
        MyCollectionView = CollectionViewSource.GetDefaultView(ClipItems) as ListCollectionView;
        MyCollectionView.IsLiveSorting = true;
        MyCollectionView.SortDescriptions.Add(new SortDescription("Pinned", ListSortDirection.Descending));
        MyCollectionView.SortDescriptions.Add(new SortDescription("DateTimeAdded", ListSortDirection.Descending));

        InitializeComponent();
        this.DataContext = this;
        lvMain.ItemsSource = MyCollectionView;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        watcher.Dispose();
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
                    CompareMemCmp(BitmapFromSource(img), BitmapFromSource(a.Image)));
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
        watcher = new ClipboardWatcher(this, pro);
    }

    private void TogglePin(ClipItem item)
    {
        if(item != null)
        {
            item.Pinned = !item.Pinned;
        }
    }

    void DeleteClip(ClipItem item)
    {
        ClipItems.Remove(item);
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
    {
        return (BitmapSource)new BitmapImage(new Uri(filePath));
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
}

public class ClipItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Text { get; set; }
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