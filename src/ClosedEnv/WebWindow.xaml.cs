using System.IO;
using System.Windows;
using ClosedEnv.Models;
using ClosedEnv.Services;
using Microsoft.Web.WebView2.Core;

namespace ClosedEnv;

public partial class WebWindow : Window
{
    private readonly AppProfile _profile;
    private readonly bool _allowCamera;
    private readonly bool _allowMicrophone;
    private int _blocked;

    public WebWindow(AppProfile profile, bool allowCamera, bool allowMicrophone)
    {
        _profile = profile;
        _allowCamera = allowCamera;
        _allowMicrophone = allowMicrophone;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var data = AppPaths.WebViewData(_profile.Id);
            Directory.CreateDirectory(data);
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: data);
            await Browser.EnsureCoreWebView2Async(env);

            var core = Browser.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;

            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += OnWebResourceRequested;
            core.PermissionRequested += OnPermissionRequested;
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) &&
                    DomainAllowlist.IsAllowedUri(uri, _profile.Allowlist))
                {
                    core.Navigate(args.Uri);
                }
                else
                {
                    _blocked++;
                    UpdateFilterStatus();
                }
            };

            var url = string.IsNullOrWhiteSpace(_profile.WebUrl) ? "https://web.max.ru/" : _profile.WebUrl;
            core.Navigate(url);
            UpdateFilterStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "WebView2 недоступен. Установите Microsoft Edge WebView2 Runtime.\n\n" + ex.Message,
                "ClosedEnv",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Close();
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (DomainAllowlist.IsAllowedUri(uri, _profile.Allowlist))
        {
            return;
        }

        _blocked++;
        UpdateFilterStatus();
        e.Response = Browser.CoreWebView2.Environment.CreateWebResourceResponse(
            null, 403, "Blocked", "Content-Type: text/plain");
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        if (e.PermissionKind == CoreWebView2PermissionKind.Camera)
        {
            e.State = _allowCamera
                ? CoreWebView2PermissionState.Allow
                : CoreWebView2PermissionState.Deny;
            return;
        }

        if (e.PermissionKind == CoreWebView2PermissionKind.Microphone)
        {
            e.State = _allowMicrophone
                ? CoreWebView2PermissionState.Allow
                : CoreWebView2PermissionState.Deny;
            return;
        }

        e.State = CoreWebView2PermissionState.Deny;
    }

    private void UpdateFilterStatus()
    {
        FilterStatus.Text = _blocked == 0
            ? "фильтр доменов включён"
            : "отклонено запросов: " + _blocked;
    }
}
