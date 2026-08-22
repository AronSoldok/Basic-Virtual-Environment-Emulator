using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ClosedEnv.Models;
using ClosedEnv.Services;
using Microsoft.Web.WebView2.Core;

namespace ClosedEnv;

public partial class WebWindow : Window
{
    private const int MaxBodyChars = 2048;
    private const int MaxMemoryEntries = 3000;

    private readonly AppProfile _profile;
    private readonly bool _allowCamera;
    private readonly bool _allowMicrophone;
    private readonly List<RequestLogEntry> _all = new();
    private readonly ObservableCollection<RequestLogEntry> _visible = new();
    private bool _logVisible = true;
    private int _blocked;

    public WebWindow(AppProfile profile, bool allowCamera, bool allowMicrophone)
    {
        _profile = profile;
        _allowCamera = allowCamera;
        _allowMicrophone = allowMicrophone;
        InitializeComponent();
        LogList.ItemsSource = _visible;
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
            core.NewWindowRequested += OnNewWindowRequested;

            var url = string.IsNullOrWhiteSpace(_profile.WebUrl) ? "https://web.max.ru/" : _profile.WebUrl;
            core.Navigate(url);
            UpdateHeaderStatus();
            ApplyFilter();
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

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        var allowed = Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) &&
                      DomainAllowlist.IsAllowedUri(uri, _profile.Allowlist);
        AddEntry(new RequestLogEntry
        {
            Method = "WINDOW",
            Host = uri?.Host ?? "",
            Url = args.Uri,
            Allowed = allowed
        });
        if (allowed && uri is not null)
        {
            Browser.CoreWebView2.Navigate(args.Uri);
        }
        else
        {
            _blocked++;
            UpdateHeaderStatus();
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri))
        {
            return;
        }

        var allowed = DomainAllowlist.IsAllowedUri(uri, _profile.Allowlist);
        AddEntry(new RequestLogEntry
        {
            Method = string.IsNullOrWhiteSpace(e.Request.Method) ? "GET" : e.Request.Method,
            Host = uri.Host,
            Url = e.Request.Uri,
            Allowed = allowed,
            Headers = ReadHeaders(e.Request),
            BodyPreview = ReadBodyPreview(e.Request)
        });

        if (allowed)
        {
            return;
        }

        _blocked++;
        UpdateHeaderStatus();
        e.Response = Browser.CoreWebView2.Environment.CreateWebResourceResponse(
            null, 403, "Blocked", "Content-Type: text/plain");
    }

    private static string ReadHeaders(CoreWebView2WebResourceRequest request)
    {
        try
        {
            var builder = new StringBuilder();
            foreach (var header in request.Headers)
            {
                builder.Append(header.Key).Append(": ").Append(header.Value).AppendLine();
            }

            return builder.ToString().TrimEnd();
        }
        catch
        {
            return "";
        }
    }

    private static string ReadBodyPreview(CoreWebView2WebResourceRequest request)
    {
        try
        {
            var stream = request.Content;
            if (stream is null || !stream.CanRead || !stream.CanSeek)
            {
                return "";
            }

            var position = stream.Position;
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var buffer = new char[MaxBodyChars];
            var read = reader.Read(buffer, 0, buffer.Length);
            stream.Position = position;
            return read <= 0 ? "" : new string(buffer, 0, read);
        }
        catch
        {
            return "";
        }
    }

    private void AddEntry(RequestLogEntry entry)
    {
        try
        {
            RequestLogStore.Append(_profile.Id, entry);
        }
        catch
        {
            // disk log is best-effort
        }

        void Apply()
        {
            _all.Add(entry);
            if (_all.Count > MaxMemoryEntries)
            {
                _all.RemoveRange(0, _all.Count - MaxMemoryEntries);
            }

            ApplyFilter(keepSelection: true);
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(Apply);
        }
        else
        {
            Apply();
        }
    }

    private bool MatchesFilter(RequestLogEntry entry)
    {
        var kind = (KindFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        if (kind == "allow" && !entry.Allowed)
        {
            return false;
        }

        if (kind == "block" && entry.Allowed)
        {
            return false;
        }

        var query = SearchBox.Text?.Trim() ?? "";
        if (query.Length == 0)
        {
            return true;
        }

        return Contains(entry.Url, query) ||
               Contains(entry.Host, query) ||
               Contains(entry.Method, query) ||
               Contains(entry.BodyPreview, query) ||
               Contains(entry.Headers, query);
    }

    private static bool Contains(string? value, string query) =>
        !string.IsNullOrEmpty(value) &&
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void ApplyFilter(bool keepSelection = false)
    {
        var selected = keepSelection ? LogList.SelectedItem as RequestLogEntry : null;
        _visible.Clear();
        foreach (var entry in _all)
        {
            if (MatchesFilter(entry))
            {
                _visible.Add(entry);
            }
        }

        CountText.Text = $"показано {_visible.Count} из {_all.Count}";
        if (selected is not null && _visible.Contains(selected))
        {
            LogList.SelectedItem = selected;
        }
    }

    private void FilterChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void SearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ToggleLog_Click(object sender, RoutedEventArgs e)
    {
        _logVisible = !_logVisible;
        LogRow.Height = _logVisible ? new GridLength(260) : new GridLength(0);
        LogRow.MinHeight = _logVisible ? 120 : 0;
        LogSplitter.Visibility = _logVisible ? Visibility.Visible : Visibility.Collapsed;
        LogPanel.Visibility = _logVisible ? Visibility.Visible : Visibility.Collapsed;
        ToggleLogButton.Content = _logVisible ? "Скрыть журнал" : "Журнал";
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        _all.Clear();
        _visible.Clear();
        _blocked = 0;
        DetailBox.Text = "Журнал очищен. Новые запросы появятся здесь.";
        try
        {
            RequestLogStore.ClearFile(_profile.Id);
        }
        catch
        {
            // ignore
        }

        UpdateHeaderStatus();
        ApplyFilter();
    }

    private void OpenLogFile_Click(object sender, RoutedEventArgs e)
    {
        var path = AppPaths.RequestLog(_profile.Id);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, string.Empty);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ClosedEnv", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LogList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogList.SelectedItem is not RequestLogEntry entry)
        {
            return;
        }

        var text = new StringBuilder();
        text.AppendLine(entry.Time.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        text.AppendLine(entry.StatusText + "  " + entry.Method + "  " + entry.Host);
        text.AppendLine(entry.Url);
        if (!string.IsNullOrWhiteSpace(entry.Headers))
        {
            text.AppendLine();
            text.AppendLine("Заголовки:");
            text.AppendLine(entry.Headers);
        }

        if (!string.IsNullOrWhiteSpace(entry.BodyPreview))
        {
            text.AppendLine();
            text.AppendLine("Тело (превью):");
            text.AppendLine(entry.BodyPreview);
        }

        DetailBox.Text = text.ToString();
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

    private void UpdateHeaderStatus()
    {
        void Apply()
        {
            FilterStatus.Text = _blocked == 0
                ? "фильтр доменов включён · кадры WebSocket после рукопожатия часто не видны"
                : "отклонено: " + _blocked + " · кадры WebSocket после рукопожатия часто не видны";
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(Apply);
        }
        else
        {
            Apply();
        }
    }
}
