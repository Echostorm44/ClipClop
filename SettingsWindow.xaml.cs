using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ClipClop;

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : Window
{
    MainWindow parent;
    bool initializing = true;
    public ICommand EscapeCommand { get; set; }

    public SettingsWindow(MainWindow main)
    {
        this.EscapeCommand = new RelayCommand((a) => EscapeButtonClick());
        InitializeComponent();
        this.DataContext = this;
        parent = main;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    void EscapeButtonClick()
    {
        this.Close();
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        initializing = true;
        var keyList = Enum.GetValues(typeof(Key)).Cast<Key>().ToList();
        ddlKey.ItemsSource = keyList;
        var modList = Enum.GetValues(typeof(KeyModifier)).Cast<KeyModifier>().ToList();
        ddlMod.ItemsSource = modList;
        ddlKey.SelectedItem = parent.ShowHotKey.Key;
        ddlMod.SelectedItem = parent.ShowHotKey.KeyModifiers;
        initializing = false;
    }

    private void HotkeySelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if(initializing)
        {
            return;
        }
        if(ddlKey.SelectedItem != null && ddlKey.SelectedItem is Key && 
            ddlMod.SelectedItem != null && ddlMod.SelectedItem is KeyModifier)
        {
            parent.SetNewHotkey((Key)ddlKey.SelectedItem, (KeyModifier)ddlMod.SelectedItem);
            parent.Settings.ShowHotKey = (Key)ddlKey.SelectedItem;
            parent.Settings.ShowHotKeyMod = (KeyModifier)ddlMod.SelectedItem;
            parent.SaveAppSettings();
        }
    }
}
