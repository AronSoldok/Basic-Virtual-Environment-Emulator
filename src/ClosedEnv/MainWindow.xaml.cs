using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClosedEnv.Models;
using ClosedEnv.Services;
using Microsoft.Win32;

namespace ClosedEnv;

public partial class MainWindow : Window
{
    private IReadOnlyList<AppProfile> _profiles = Array.Empty<AppProfile>();
    private string? _payloadPath;

    public MainWindow()
    {
        InitializeComponent();
        AppPaths.EnsureLayout();
        AppPaths.SyncScripts();
        BindThemeButton();
        ThemeService.Changed += BindThemeButton;
        Closed += (_, _) => ThemeService.Changed -= BindThemeButton;
        LoadProfiles();
        RefreshSandboxStatus();
    }

    private void BindThemeButton()
    {
        ThemeToggleButton.Content = ThemeService.ToggleLabel;
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e) => ThemeService.Toggle();

    private void LoadProfiles()
    {
        _profiles = ProfileStore.Load();
        ProfileList.ItemsSource = _profiles;
        var webIndex = -1;
        for (var i = 0; i < _profiles.Count; i++)
        {
            if (string.Equals(_profiles[i].Id, "max-web", StringComparison.OrdinalIgnoreCase))
            {
                webIndex = i;
                break;
            }
        }

        if (_profiles.Count > 0)
        {
            ProfileList.SelectedIndex = webIndex >= 0 ? webIndex : 0;
        }
    }

    private AppProfile? SelectedProfile => ProfileList.SelectedItem as AppProfile;

    private void ProfileList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        ProfileTitle.Text = profile.Name;
        ProfileDescription.Text = profile.Description;
        ClipboardToggle.IsChecked = profile.Clipboard;
        AudioToggle.IsChecked = profile.AudioInput;
        CameraToggle.IsChecked = profile.VideoInput;
        FirewallToggle.IsChecked = profile.GuestFirewall;
        FirewallToggle.Visibility = !profile.IsWeb && profile.Allowlist.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        PayloadPanel.Visibility = profile.RequiresPayload ? Visibility.Visible : Visibility.Collapsed;
        SandboxLogPanel.Visibility = profile.IsWeb ? Visibility.Collapsed : Visibility.Visible;
        DataPathText.Text = profile.IsWeb
            ? AppPaths.WebViewData(profile.Id)
            : AppPaths.ProfileData(profile.Id);

        HonestyNote.Text = profile.Id.StartsWith("max", StringComparison.OrdinalIgnoreCase)
            ? "MAX остаётся обычным мессенджером и ходит в свою сеть. Изоляция закрывает файлы и устройства этого компьютера."
            : "Программа увидит только каталог данных профиля. Документы, рабочий стол и загрузки хоста не монтируются.";
        RefreshSandboxStatus();
    }

    private void OpenGuestLog_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null || profile.IsWeb)
        {
            return;
        }

        var path = AppPaths.GuestLog(profile.Id);
        try
        {
            if (!File.Exists(path))
            {
                MessageBox.Show(
                    this,
                    "Лога ещё нет. Запустите профиль в песочнице, затем откройте снова.",
                    "ClosedEnv",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ClosedEnv", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BrowsePayload_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Программы и установщики (*.exe;*.msi)|*.exe;*.msi|Все файлы (*.*)|*.*",
            Title = "Файл для замкнутой среды"
        };
        if (dialog.ShowDialog(this) == true)
        {
            _payloadPath = dialog.FileName;
            PayloadPathText.Text = dialog.FileName;
        }
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        try
        {
            if (FirewallToggle.IsVisible)
            {
                profile.GuestFirewall = FirewallToggle.IsChecked == true;
            }

            ProfileSession.Start(
                profile,
                camera: CameraToggle.IsChecked == true,
                audio: AudioToggle.IsChecked == true,
                clipboard: ClipboardToggle.IsChecked == true,
                payload: profile.RequiresPayload ? _payloadPath : null,
                owner: this);

            if (!profile.IsWeb)
            {
                StatusText.Text = "Песочница запущена.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ClosedEnv", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void EnableSandbox_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SandboxFeature.RequestEnable();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ClosedEnv", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshSandboxStatus()
    {
        if (SelectedProfile is { IsWeb: true })
        {
            StatusText.Text = "Веб-режим: Windows Sandbox и BIOS не нужны. Нужен WebView2 (на Windows 11 обычно уже есть).";
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
            EnableSandboxButton.Visibility = Visibility.Collapsed;
            return;
        }

        var status = SandboxFeature.Detect();
        StatusText.Text = status.Summary + (string.IsNullOrWhiteSpace(status.Edition) ? "" : "  ·  " + status.Edition);
        EnableSandboxButton.Visibility = status.CanLaunchSandbox || status.IsHomeEdition
            ? Visibility.Collapsed
            : Visibility.Visible;
        StatusText.Foreground = status.CanLaunchSandbox
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : (System.Windows.Media.Brush)FindResource("WarnBrush");
    }
}
