﻿#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
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
        var altKeyList = Enum.GetNames(typeof(Key)).ToList();
        ddlKey.ItemsSource = altKeyList;
        var modList = Enum.GetValues(typeof(KeyModifier)).Cast<KeyModifier>().ToList();
        ddlMod.ItemsSource = modList;
        ddlKey.SelectedItem = parent.ShowHotKey.Key.ToString();
        ddlMod.SelectedItem = parent.ShowHotKey.KeyModifiers;
        chkLaunchAtStartup.IsChecked = parent.Settings.LaunchAtStartup;
        chkOpenAtMouse.IsChecked = parent.Settings.OpenAtMousePointer;
        initializing = false;
    }

    private void HotkeySelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if(initializing)
        {
            return;
        }
        if(ddlKey.SelectedItem == null || ddlMod.SelectedItem == null)
        {
            return;
        }
        var key = (Key)Enum.Parse(typeof(Key), ddlKey.SelectedItem.ToString());
        parent.SetNewHotkey(key, (KeyModifier)ddlMod.SelectedItem);
        parent.Settings.ShowHotKey = key;
        parent.Settings.ShowHotKeyMod = (KeyModifier)ddlMod.SelectedItem;
        parent.SaveAppSettings();
    }

    private void chkLaunchAtStartup_Checked(object sender, RoutedEventArgs e)
    {
        if(initializing)
        {
            return;
        }
        parent.Settings.LaunchAtStartup = chkLaunchAtStartup.IsChecked.Value;
        parent.SaveAppSettings();
        if(chkLaunchAtStartup.IsChecked.Value)
        {
            Helpers.SetStartup();
        }
        else
        {
            Helpers.RemoveStartup();
        }
    }

    private void chkOpenAtMouse_Checked(object sender, RoutedEventArgs e)
    {
        if(initializing)
        {
            return;
        }
        parent.Settings.OpenAtMousePointer = chkOpenAtMouse.IsChecked.Value;
        parent.SaveAppSettings();
    }

    private void btnSource_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/Echostorm44/ClipClop") 
            { UseShellExecute = true });
    }
}
