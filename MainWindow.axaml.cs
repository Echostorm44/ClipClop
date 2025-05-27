using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ClipClop;

public partial class MainWindow : Window
{
	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

	private const byte VK_MENU = 0x12;
	private const byte VK_TAB = 0x09;
	private const byte VK_CONTROL = 0x11;
	private const byte VK_V = 0x56;
	private const uint KEYEVENTF_KEYUP = 0x0002;

	[StructLayout(LayoutKind.Sequential)]

	private struct POINT
	{
		public int X;
		public int Y;
	}

	public ObservableCollection<ClipItem> VisibleClipItems { get; set; }
	public List<ClipItem> AllClipItems { get; set; }
	string searchText;
	public string SearchText
	{
		get => searchText;
		set
		{
			if(searchText == value)
			{
				return;
			}
			searchText = value;
			Debouncer.Debounce("Search", () =>
			{
				ReSortClipItems();
			}, 300);
		}
	}

	public MainWindow()
	{
		InitializeComponent();
		this.DataContext = this;
		Opened += (_, _) => WindowPositionManager.RestoreWindowPosition(this);
		Closing += (_, _) => WindowPositionManager.SaveWindowPosition(this);
	}

	private void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		try
		{
			Program.Settings = Helpers.ReadSettings();
			VisibleClipItems = new ObservableCollection<ClipItem>();
			AllClipItems = Helpers.ReadSavedPins();
			ReSortClipItems();
			lbItems.ItemsSource = VisibleClipItems;

			Program.ShowHotkeyManager = new HotKeyManager(this, Program.Settings.ShowHotKey,
				Program.Settings.ShowHotKeyMod, () =>
			{
				if(Program.Settings.OpenAtMousePointer)
				{
					var pos = GetMousePosition();
					this.Position = new PixelPoint(pos.X, pos.Y);
				}
				txtSearchBox.Text = "";
				lbItems.Scroll.Offset = new Vector(0, 0);
				this.Show();
				this.Activate();
				lbItems.Focus();
			});

			Program.MainClipboardWatcher = new ClipboardWatcher((obj) =>
			{
				if(obj is string text)
				{
					if(string.IsNullOrEmpty(text))
					{
						return;
					}
					var existing = AllClipItems.SingleOrDefault(a => a.Text == text);
					if(existing != null)
					{
						existing.DateTimeAdded = DateTime.Now;
						ReSortClipItems();
						return;
					}
					AllClipItems.Add(new ClipItem()
					{
						DateTimeAdded = DateTime.Now,
						IsImage = false,
						Pinned = false,
						Text = text,
						VisibleText = text.Trim()
					});
					ReSortClipItems();
				}
				else if(obj is Bitmap img)
				{
					var existing = AllClipItems.SingleOrDefault(a => a.IsImage && CompareBitmaps(a.Image, img));
					if(existing != null)
					{
						existing.DateTimeAdded = DateTime.Now;
					}
					else
					{
						AllClipItems.Add(new ClipItem()
						{
							DateTimeAdded = DateTime.Now,
							IsImage = true,
							Image = img,
							Pinned = false,
							Text = "",
							VisibleText = ""
						});
					}
					ReSortClipItems();
				}
			});

			this.KeyDown += (_, e) =>
			{
				switch(e.Key)
				{
					case Key.Escape:
					{
						this.Close();
					}
						break;
				}
			};
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	public static bool CompareBitmaps(Bitmap b1, Bitmap b2)
	{
		if(b1 == null || b2 == null)
		{
			return false;
		}

		if(b1.PixelSize != b2.PixelSize || b1.Dpi != b2.Dpi)
		{
			return false;
		}

		int width = b1.PixelSize.Width;
		int height = b1.PixelSize.Height;
		int stride = width * 4; // assuming Bgra8888 (4 bytes per pixel)
		int size = stride * height;

		byte[] buffer1 = new byte[size];
		byte[] buffer2 = new byte[size];

		var handle1 = GCHandle.Alloc(buffer1, GCHandleType.Pinned);
		var handle2 = GCHandle.Alloc(buffer2, GCHandleType.Pinned);

		try
		{
			b1.CopyPixels(new PixelRect(0, 0, width, height), handle1.AddrOfPinnedObject(), size, stride);
			b2.CopyPixels(new PixelRect(0, 0, width, height), handle2.AddrOfPinnedObject(), size, stride);
		}
		finally
		{
			handle1.Free();
			handle2.Free();
		}

		return buffer1.SequenceEqual(buffer2);
	}

	private void Window_Closing(object? sender, WindowClosingEventArgs e)
	{
		try
		{
			WindowPositionManager.SaveWindowPosition(this);
			this.Hide();
			e.Cancel = true;
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	private void ReSortClipItems()
	{
		var filtered = AllClipItems
			.Where(a => string.IsNullOrEmpty(searchText) || (!string.IsNullOrEmpty(a.Text) && 
				a.Text.ToLowerInvariant().Contains(searchText.ToLowerInvariant())))
			.OrderByDescending(a => a.Pinned)
			.ThenByDescending(a => a.DateTimeAdded)
			.ToList();
		VisibleClipItems.Clear();
		foreach(var item in filtered)
		{
			VisibleClipItems.Add(item);
		}
	}

	public static PixelPoint GetMousePosition()
	{
		if(GetCursorPos(out POINT point))
		{
			return new PixelPoint(point.X, point.Y);
		}
		return new PixelPoint(0, 0);
	}

	private void DeleteClipItem_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
	{
		if(lbItems.SelectedItem == null)
		{
			return;
		}
		AllClipItems.Remove((ClipItem)lbItems.SelectedItem);
		ReSortClipItems();
	}

	private void ClearItems_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
	{
		AllClipItems = AllClipItems.Where(a => a.Pinned).ToList();
		ReSortClipItems();
	}

	bool initializing = true;

	private async void SettingButton_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
	{
		try
		{
			settingPanel.IsPaneOpen = true;
			initializing = true;
			var altKeyList = Enum.GetNames(typeof(Key));
			var modList = Enum.GetValues(typeof(KeyModifier));
			ddlMod.ItemsSource = modList;
			ddlMod.SelectedItem = Program.Settings.ShowHotKeyMod;
			txtHotKey.Text = Program.Settings.ShowHotKey.ToString();
			chkLaunchAtStartup.IsChecked = Program.Settings.LaunchAtStartup;
			chkOpenAtMouse.IsChecked = Program.Settings.OpenAtMousePointer;
			initializing = false;
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	private void PinButton_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
	{
		try
		{
			if(lbItems.SelectedItem == null)
			{
				return;
			}
			var item = (ClipItem)lbItems.SelectedItem;
			item.Pinned = !item.Pinned;

			var unsavedImages = AllClipItems.Where(a => a.IsImage && a.Pinned &&
					string.IsNullOrEmpty(a.ImageFilePath)).ToList();
			foreach(var unsaved in unsavedImages)
			{
				var fileName = unsaved.DateTimeAdded.ToString("yyyy-MM-dd-HH-mm-ss") + ".png";
				var filePath = System.IO.Path.Combine(Program.ImageFolderPath, fileName);
				unsaved.Image.Save(filePath);
				unsaved.ImageFilePath = filePath;
			}

			var toDelete = AllClipItems.Where(a => a.IsImage && !a.Pinned &&
					!string.IsNullOrEmpty(a.ImageFilePath)).ToList();
			foreach(var dieImageDie in toDelete)
			{
				System.IO.File.Delete(dieImageDie.ImageFilePath);
				dieImageDie.ImageFilePath = "";
			}
			var serializedPins = JsonSerializer.Serialize(AllClipItems.Where(a => a.Pinned).ToList(),
					SourceGenerationContext.Default.ListClipItem);
			serializedPins = Helpers.Encrypt(serializedPins);
			System.IO.File.WriteAllText(Program.SavedPinsPath, serializedPins);
			ReSortClipItems();
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	private void ListBox_KeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
	{
		try
		{
			if(e.Key == Key.Enter)
			{
				if(lbItems.SelectedItem == null)
				{
					return;
				}
				PasteSelection((ClipItem)lbItems.SelectedItem);
			}
			else if(e.Key == Key.S)
			{
				txtSearchBox.Focus();
				txtSearchBox.Text = "";
				searchText = "";
			}
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	void PasteSelection(ClipItem clip)
	{
		if(clip.IsImage)
		{
			ClipboardHelper.SetBitmapToClipboard(clip.Image);
		}
		else
		{
			this.Clipboard.SetTextAsync(clip.Text).Wait();
		}
		this.Hide();
		System.Threading.Thread.Sleep(150); // Wait for the focus to move back
		keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);

		// Press V key
		keybd_event(VK_V, 0, 0, UIntPtr.Zero);

		// Release V key
		keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

		// Release Ctrl key
		keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
	}

	private void txtHotKey_PreviewKeyUp(object? sender, KeyEventArgs e)
	{
		try
		{
			if(e.Key == null)
			{
				return;
			}
			Program.Settings.ShowHotKey = e.Key;
			Helpers.SaveSettings();
			txtHotKey.Text = e.Key.ToString();
			Program.ShowHotkeyManager.Dispose();
			Program.ShowHotkeyManager = null;
			System.Threading.Thread.Sleep(100);
			Program.ShowHotkeyManager = new HotKeyManager(this, Program.Settings.ShowHotKey,
			Program.Settings.ShowHotKeyMod, () =>
			{
				if(Program.Settings.OpenAtMousePointer)
				{
					var pos = GetMousePosition();
					this.Position = new PixelPoint(pos.X, pos.Y);
				}
				txtSearchBox.Text = "";
				lbItems.Scroll.Offset = new Vector(0, 0);
				this.Show();
				this.Activate();
				lbItems.Focus();
			});
			ddlMod.Focus();
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	private void txtHotKey_GotFocus(object sender, RoutedEventArgs e)
	{
		txtHotKey.Text = "";
	}

	private void HotkeySelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		try
		{
			if(initializing)
			{
				return;
			}
			if(ddlMod.SelectedItem == null)
			{
				return;
			}
			var modKey = (KeyModifier)ddlMod.SelectedItem;
			if(modKey == null)
			{
				return;
			}
			Program.Settings.ShowHotKeyMod = modKey;
			Program.ShowHotkeyManager.Dispose();
			Program.ShowHotkeyManager = null;
			System.Threading.Thread.Sleep(100);
			Program.ShowHotkeyManager = new HotKeyManager(this, Program.Settings.ShowHotKey,
			Program.Settings.ShowHotKeyMod, () =>
			{
				if(Program.Settings.OpenAtMousePointer)
				{
					var pos = GetMousePosition();
					this.Position = new PixelPoint(pos.X, pos.Y);
				}
				txtSearchBox.Text = "";
				lbItems.Scroll.Offset = new Vector(0, 0);
				this.Show();
				this.Activate();
				lbItems.Focus();
			});
			Helpers.SaveSettings();
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	private void chkLaunchAtStartup_Checked(object sender, RoutedEventArgs e)
	{
		try
		{
			if(initializing)
			{
				return;
			}
			Program.Settings.LaunchAtStartup = chkLaunchAtStartup.IsChecked.Value;
			Helpers.SaveSettings();
			if(chkLaunchAtStartup.IsChecked.Value)
			{
				Helpers.SetStartup();
			}
			else
			{
				Helpers.RemoveStartup();
			}
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	private void chkOpenAtMouse_Checked(object sender, RoutedEventArgs e)
	{
		try
		{
			if(initializing)
			{
				return;
			}

			Program.Settings.OpenAtMousePointer = chkOpenAtMouse.IsChecked.Value;
			Helpers.SaveSettings();
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	private void btnSource_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Process.Start(new ProcessStartInfo("https://github.com/Echostorm44/ClipClop")
			{ UseShellExecute = true });
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	private void OpenLog(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		try
		{
			Process.Start("explorer.exe", Program.LogFolderPath);
		}
		catch(Exception ex)
		{
			Helpers.WriteLogEntry(ex.ToString());
		}
	}

	private void Clip_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
	{
		if(lbItems.SelectedItem == null)
		{
			return;
		}
		PasteSelection((ClipItem)lbItems.SelectedItem);
	}
}

[JsonSerializable(typeof(ClipItem))]
public class ClipItem : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	public string? Text { get; set; }
	public string? VisibleText { get; set; }
	[JsonIgnore]
	public Bitmap? Image { get; set; }
	public string? ImageFilePath { get; set; }
	DateTime dateTimeAdded;
	public DateTime DateTimeAdded
	{
		get => dateTimeAdded;
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
		get => pinned;
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

#region Converters

public class PinIconConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		bool isPinned = (bool)value;
		if(isPinned)
		{
			return "★";
		}
		else
		{
			return "☆";
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}

public class FalseBoolToVisiblityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (bool)value ? false : true;
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

#endregion

