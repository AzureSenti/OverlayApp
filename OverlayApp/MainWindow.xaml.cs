using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OverlayApp.Helpers;
using OverlayApp.Models;
using OverlayApp.Services;

namespace OverlayApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppConfigService _configService = new();
    private readonly GlobalHotkeyHelper _hotkeyHelper = new();
    private AppSettings _settings = new();

    // Icon cache to avoid re-extracting
    private readonly Dictionary<string, ImageSource?> _iconCache = new();

    // Active embedded tabs
    private readonly Dictionary<string, EmbeddedTabInfo> _embeddedTabs = new();

    // WebView2 environment (shared, created once for InPrivate mode)
    private CoreWebView2Environment? _webViewEnvironment;
    private readonly string _webViewDataFolder = Path.Combine(
        Path.GetTempPath(), "OverlayApp_Private_" + Guid.NewGuid().ToString("N")[..8]);

    public MainWindow()
    {
        InitializeComponent();

        // Load settings
        _settings = _configService.Load();
        ApplySettings();

        // Setup events
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
    }

    // ===================================================================
    // INITIALIZATION
    // ===================================================================

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Register global hotkey
        RegisterHotkey();

        // Build app list UI
        RebuildAppList();

        // Play fade-in animation
        var fadeIn = (Storyboard)FindResource("FadeIn");
        fadeIn.Begin();

        SetStatus("Ready — Press Ctrl+Space to toggle overlay");
    }

    private void ApplySettings()
    {
        Left = _settings.WindowLeft;
        Top = _settings.WindowTop;
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;
    }

    private void RegisterHotkey()
    {
        _hotkeyHelper.HotkeyPressed += (s, e) => ToggleOverlay();
        bool registered = _hotkeyHelper.Register(this, _settings.HotkeyModifier, _settings.HotkeyKey);

        if (!registered)
        {
            SetStatus("⚠ Failed to register hotkey. It may be in use by another app.");
        }
        else
        {
            string modText = _settings.HotkeyModifier.ToString().Replace(",", " +");
            string keyText = _settings.HotkeyKey.ToString();
            HotkeyHintText.Text = $"{modText} + {keyText} to toggle";
        }
    }

    // ===================================================================
    // TOGGLE OVERLAY (SHOW/HIDE)
    // ===================================================================

    private void ToggleOverlay()
    {
        Dispatcher.Invoke(() =>
        {
            if (Visibility == Visibility.Visible)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fadeOut.Completed += (s, e) => Visibility = Visibility.Hidden;
                RootBorder.BeginAnimation(OpacityProperty, fadeOut);
            }
            else
            {
                Visibility = Visibility.Visible;
                Activate();

                var fadeIn = (Storyboard)FindResource("FadeIn");
                fadeIn.Begin();

                SearchBox.Focus();
            }
        });
    }

    // ===================================================================
    // TITLE BAR EVENTS
    // ===================================================================

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ===================================================================
    // SEARCH — EMBEDDED WEBVIEW2 (InPrivate)
    // ===================================================================

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            string query = SearchBox.Text.Trim();
            OpenSearchTab(query);
            SearchBox.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Clear();
        }
    }

    private async void OpenSearchTab(string query)
    {
        string searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
        string tabTitle = $"🔍 {TruncateName(query, 18)}";

        SetStatus($"Opening search: {query}...");

        try
        {
            // Create WebView2 environment for InPrivate mode (once)
            _webViewEnvironment ??= await CoreWebView2Environment.CreateAsync(
                null, _webViewDataFolder);

            var webView = new WebView2();

            // Create the tab
            var tabInfo = new EmbeddedTabInfo
            {
                Title = tabTitle,
                Type = EmbedType.Browser,
                WebView = webView
            };

            var tabItem = CreateTabItem(tabInfo);
            tabItem.Content = webView;

            EmbedTabControl.Items.Add(tabItem);
            EmbedTabControl.SelectedItem = tabItem;
            ShowTabControl();

            // Initialize WebView2 and navigate
            await webView.EnsureCoreWebView2Async(_webViewEnvironment);
            webView.CoreWebView2.Navigate(searchUrl);

            // Update tab title when page loads
            webView.CoreWebView2.DocumentTitleChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    string newTitle = "🔍 " + TruncateName(webView.CoreWebView2.DocumentTitle, 18);
                    tabInfo.Title = newTitle;
                    tabItem.Header = newTitle;
                });
            };

            SetStatus($"🔍 Search: {query}");
            UpdateTabCount();
        }
        catch (Exception ex)
        {
            SetStatus($"❌ Failed to open search: {ex.Message}");
            // Fallback to external browser
            BrowserSearchHelper.SearchIncognito(query);
        }
    }

    // ===================================================================
    // APP LIST MANAGEMENT
    // ===================================================================

    private void RebuildAppList()
    {
        var toRemove = AppListPanel.Children.OfType<WpfButton>()
            .Where(b => b != AddAppButton && b != CaptureWindowBtn)
            .ToList();

        foreach (var btn in toRemove)
            AppListPanel.Children.Remove(btn);

        foreach (var app in _settings.Apps)
        {
            var card = CreateAppCard(app);
            int insertIndex = AppListPanel.Children.IndexOf(CaptureWindowBtn);
            if (insertIndex < 0) insertIndex = AppListPanel.Children.IndexOf(AddAppButton);
            AppListPanel.Children.Insert(insertIndex, card);
        }
    }

    private WpfButton CreateAppCard(AppEntry app)
    {
        if (!_iconCache.TryGetValue(app.ExePath, out var icon))
        {
            icon = IconExtractor.GetIconFromExe(app.ExePath);
            _iconCache[app.ExePath] = icon;
        }

        var iconImage = new WpfImage
        {
            Width = 32,
            Height = 32,
            Margin = new Thickness(0, 0, 0, 4),
            Source = icon ?? CreateDefaultIcon()
        };

        var nameText = new TextBlock
        {
            Text = TruncateName(app.Name, 10),
            FontSize = 10,
            Foreground = (WpfBrush)FindResource("TextBrush"),
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 68
        };

        var stack = new StackPanel
        {
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(iconImage);
        stack.Children.Add(nameText);

        var btn = new WpfButton
        {
            Style = (Style)FindResource("AppCardStyle"),
            Width = 80,
            Height = 80,
            Margin = new Thickness(4),
            Content = stack,
            Tag = app,
            ToolTip = $"{app.Name}\n{app.ExePath}"
        };

        btn.Click += AppCard_Click;
        btn.ContextMenu = CreateAppContextMenu(app);

        return btn;
    }

    private ContextMenu CreateAppContextMenu(AppEntry app)
    {
        var menu = new ContextMenu();

        var launchEmbed = new MenuItem { Header = "▶ Embed (launch or capture)" };
        launchEmbed.Click += (s, e) => LaunchOrCaptureAndEmbed(app);
        menu.Items.Add(launchEmbed);

        var launchNormal = new MenuItem { Header = "🚀 Launch Normally" };
        launchNormal.Click += (s, e) => LaunchNormally(app);
        menu.Items.Add(launchNormal);

        menu.Items.Add(new Separator());

        var toggleProfile = new MenuItem 
        { 
            Header = app.UseIsolatedProfile ? "✅ Use Isolated Profile (Data Separation)" : "❌ Use Isolated Profile (Data Separation)",
            ToolTip = "If enabled, browsers and electron apps will use an isolated profile to avoid conflicts."
        };
        toggleProfile.Click += (s, e) => 
        {
            app.UseIsolatedProfile = !app.UseIsolatedProfile;
            SaveSettings();
            RebuildAppList(); // To refresh the context menu state
            SetStatus($"Isolated Profile for {app.Name}: {(app.UseIsolatedProfile ? "Enabled" : "Disabled")}");
        };
        menu.Items.Add(toggleProfile);

        menu.Items.Add(new Separator());

        var openFolder = new MenuItem { Header = "📂 Open Folder" };
        openFolder.Click += (s, e) =>
        {
            string? dir = Path.GetDirectoryName(app.ExePath);
            if (dir != null)
                Process.Start("explorer.exe", dir);
        };
        menu.Items.Add(openFolder);

        menu.Items.Add(new Separator());

        var remove = new MenuItem { Header = "🗑 Remove" };
        remove.Click += (s, e) =>
        {
            _settings.Apps.RemoveAll(a => a.Id == app.Id);
            _iconCache.Remove(app.ExePath);
            RebuildAppList();
            SaveSettings();
            SetStatus($"Removed: {app.Name}");
        };
        menu.Items.Add(remove);

        return menu;
    }

    private void AddApp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = "Select an application",
            Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (string file in dialog.FileNames)
            {
                if (_settings.Apps.Any(a => a.ExePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var entry = new AppEntry
                {
                    Name = IconExtractor.GetDisplayName(file),
                    ExePath = file
                };

                _settings.Apps.Add(entry);
            }

            RebuildAppList();
            SaveSettings();
            SetStatus($"Added {dialog.FileNames.Length} app(s)");
        }
    }

    private void AppCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton btn && btn.Tag is AppEntry app)
        {
            LaunchOrCaptureAndEmbed(app);
        }
    }

    // ===================================================================
    // CROSSHAIR WINDOW CAPTURE
    // ===================================================================

    private bool _isCapturingWindow = false;

    private void CaptureWindowBtn_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isCapturingWindow = true;
        Mouse.Capture(CaptureWindowBtn);
        Cursor = System.Windows.Input.Cursors.Cross;
        SetStatus("🎯 Drag crosshair to a target window and release...");
    }

    private void CaptureWindowBtn_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isCapturingWindow)
        {
            // Optional: highlight window under cursor could go here
        }
    }

    private void CaptureWindowBtn_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isCapturingWindow) return;

        _isCapturingWindow = false;
        Mouse.Capture(null);
        Cursor = System.Windows.Input.Cursors.Arrow;

        var pt = GetCursorPos();
        IntPtr targetHwnd = WindowCaptureHelper.GetRootWindowFromPoint(pt.X, pt.Y);

        if (targetHwnd == IntPtr.Zero)
        {
            SetStatus("❌ No window captured.");
            return;
        }

        var helper = new WindowInteropHelper(this);
        if (targetHwnd == helper.Handle)
        {
            SetStatus("❌ Cannot embed OverlayApp into itself.");
            return;
        }

        string title = WindowCaptureHelper.GetWindowTitle(targetHwnd);
        int pid = WindowCaptureHelper.GetProcessIdFromWindow(targetHwnd);

        if (string.IsNullOrWhiteSpace(title)) title = $"Window: {targetHwnd}";

        try
        {
            var proc = Process.GetProcessById(pid);
            
            var app = new AppEntry
            {
                Name = title,
                ExePath = proc.MainModule?.FileName ?? ""
            };

            EmbedExistingWindow(app, targetHwnd);
        }
        catch (Exception)
        {
            // If we can't access process module due to permissions, just embed it anyway
            var fallbackApp = new AppEntry { Name = title, ExePath = "" };
            EmbedExistingWindow(fallbackApp, targetHwnd);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out WindowCaptureHelper.POINT lpPoint);

    private WindowCaptureHelper.POINT GetCursorPos()
    {
        GetCursorPos(out var p);
        return p;
    }

    // ===================================================================
    // LAUNCH, CAPTURE, & EMBED (Multi-Tab)
    // ===================================================================

    /// <summary>
    /// Main entry point: tries to find an already-running instance first,
    /// otherwise launches a new one. Embeds in a new tab.
    /// </summary>
    private async void LaunchOrCaptureAndEmbed(AppEntry app)
    {
        if (!File.Exists(app.ExePath))
        {
            SetStatus($"❌ File not found: {app.ExePath}");
            return;
        }

        SetStatus($"Looking for running instance of {app.Name}...");

        try
        {
            // Step 1: Try to find an already-running process
            IntPtr existingHandle = FindRunningProcessWindow(app.ExePath);

            if (existingHandle != IntPtr.Zero)
            {
                // Found a running instance — embed its existing window
                EmbedExistingWindow(app, existingHandle);
                return;
            }

            // Step 2: No running instance found — launch new and embed
            await LaunchNewAndEmbed(app);
        }
        catch (Exception ex)
        {
            SetStatus($"❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Scans running processes to find one matching the given exe path.
    /// Returns the MainWindowHandle if found, IntPtr.Zero otherwise.
    /// </summary>
    private static IntPtr FindRunningProcessWindow(string exePath)
    {
        string targetName = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant();

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                if (proc.MainWindowHandle == IntPtr.Zero) continue;

                // Try exact path match first
                try
                {
                    if (proc.MainModule?.FileName != null &&
                        proc.MainModule.FileName.Equals(exePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return proc.MainWindowHandle;
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Access denied — fall back to process name match
                }

                // Fallback: match by process name
                if (proc.ProcessName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return proc.MainWindowHandle;
                }
            }
            catch
            {
                // Skip inaccessible processes
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Embeds an already-running window into a new tab.
    /// </summary>
    private void EmbedExistingWindow(AppEntry app, IntPtr windowHandle)
    {
        var embedder = new WindowEmbedder();

        var panel = new System.Windows.Forms.Panel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(34, 34, 34)
        };

        var formsHost = new WindowsFormsHost { Child = panel };

        var tabInfo = new EmbeddedTabInfo
        {
            Title = $"▶ {app.Name}",
            Type = EmbedType.Application,
            Embedder = embedder,
            HostPanel = panel,
            FormsHost = formsHost,
            EmbeddedHandle = windowHandle,
            SourceApp = app
        };

        var tabItem = CreateTabItem(tabInfo);
        tabItem.Content = formsHost;

        EmbedTabControl.Items.Add(tabItem);
        EmbedTabControl.SelectedItem = tabItem;
        ShowTabControl();

        // Embed the existing window into our panel
        int width = (int)Math.Max(panel.Width, 400);
        int height = (int)Math.Max(panel.Height, 300);
        bool success = embedder.EmbedWindow(windowHandle, width, height);

        if (success)
        {
            SetStatus($"✅ Captured & embedded: {app.Name}");
            // Resize after layout
            _ = Dispatcher.InvokeAsync(() =>
            {
                embedder.ResizeEmbedded((int)formsHost.ActualWidth, (int)formsHost.ActualHeight);
            }, System.Windows.Threading.DispatcherPriority.Render);
        }
        else
        {
            SetStatus($"⚠ Could not embed running {app.Name}. Launching new instance...");
            RemoveTabSilently(tabInfo);
            LaunchNormally(app);
        }

        UpdateTabCount();
    }

    /// <summary>
    /// Launches a new process and embeds its window in a new tab.
    /// </summary>
    private async Task LaunchNewAndEmbed(AppEntry app)
    {
        var embedder = new WindowEmbedder();

        var panel = new System.Windows.Forms.Panel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(34, 34, 34)
        };

        var formsHost = new WindowsFormsHost { Child = panel };

        var tabInfo = new EmbeddedTabInfo
        {
            Title = $"⏳ {app.Name}...",
            Type = EmbedType.Application,
            Embedder = embedder,
            HostPanel = panel,
            FormsHost = formsHost,
            SourceApp = app
        };

        var tabItem = CreateTabItem(tabInfo);
        tabItem.Content = formsHost;

        EmbedTabControl.Items.Add(tabItem);
        EmbedTabControl.SelectedItem = tabItem;
        ShowTabControl();

        SetStatus($"Launching {app.Name}...");

        IntPtr hostHandle = panel.Handle;
        int width = (int)Math.Max(formsHost.ActualWidth, 400);
        int height = (int)Math.Max(formsHost.ActualHeight, 300);

        string finalArgs = ProfileInjectorHelper.GetLaunchArguments(app);

        bool success = await embedder.LaunchAndEmbed(
            app.ExePath, finalArgs, hostHandle, width, height);

        if (success)
        {
            tabInfo.Title = $"▶ {app.Name}";
            tabItem.Header = tabInfo.Title;
            SetStatus($"✅ Embedded: {app.Name}");

            // Resize after layout settles
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (formsHost.ActualWidth > 0)
                    embedder.ResizeEmbedded((int)formsHost.ActualWidth, (int)formsHost.ActualHeight);
            }, System.Windows.Threading.DispatcherPriority.Render);
        }
        else
        {
            SetStatus($"⚠ Could not embed {app.Name}. Launching normally...");
            RemoveTabSilently(tabInfo);
            LaunchNormally(app);
        }

        UpdateTabCount();
    }

    private void LaunchNormally(AppEntry app)
    {
        try
        {
            string finalArgs = ProfileInjectorHelper.GetLaunchArguments(app);

            Process.Start(new ProcessStartInfo
            {
                FileName = app.ExePath,
                Arguments = finalArgs,
                UseShellExecute = true
            });
            SetStatus($"🚀 Launched: {app.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"❌ Failed to launch: {ex.Message}");
        }
    }

    // ===================================================================
    // TAB MANAGEMENT
    // ===================================================================

    private TabItem CreateTabItem(EmbeddedTabInfo tabInfo)
    {
        _embeddedTabs[tabInfo.Id] = tabInfo;

        var tabItem = new TabItem
        {
            Header = tabInfo.Title,
            Tag = tabInfo.Id,
            Style = (Style)FindResource("EmbedTabItem")
        };

        return tabItem;
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton closeBtn && closeBtn.Tag is TabItem tabItem)
        {
            CloseTabByItem(tabItem);
        }
    }

    private void CloseTabByItem(TabItem tabItem)
    {
        if (tabItem.Tag is string tabId && _embeddedTabs.TryGetValue(tabId, out var tabInfo))
        {
            // Cleanup resources
            tabInfo.Dispose();
            _embeddedTabs.Remove(tabId);
        }

        EmbedTabControl.Items.Remove(tabItem);
        UpdateTabCount();

        if (EmbedTabControl.Items.Count == 0)
        {
            HideTabControl();
        }

        SetStatus("Tab closed");
    }

    private void RemoveTabSilently(EmbeddedTabInfo tabInfo)
    {
        if (_embeddedTabs.ContainsKey(tabInfo.Id))
        {
            _embeddedTabs.Remove(tabInfo.Id);
        }

        var tabToRemove = EmbedTabControl.Items.OfType<TabItem>()
            .FirstOrDefault(t => t.Tag is string id && id == tabInfo.Id);

        if (tabToRemove != null)
            EmbedTabControl.Items.Remove(tabToRemove);

        if (EmbedTabControl.Items.Count == 0)
            HideTabControl();
    }

    private void EmbedTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EmbedTabControl.SelectedItem is TabItem selected && selected.Tag is string tabId)
        {
            if (_embeddedTabs.TryGetValue(tabId, out var info))
            {
                SetStatus($"Tab: {info.Title}");
            }
        }
    }

    private void ShowTabControl()
    {
        EmbedTabControl.Visibility = Visibility.Visible;
        EmbedPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void HideTabControl()
    {
        EmbedTabControl.Visibility = Visibility.Collapsed;
        EmbedPlaceholder.Visibility = Visibility.Visible;
    }

    private void UpdateTabCount()
    {
        int count = _embeddedTabs.Count;
        TabCountText.Text = count > 0 ? $"{count} tab(s) open" : "";
    }

    // ===================================================================
    // RESIZE HANDLING
    // ===================================================================

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Resize ALL embedded windows in all tabs
        foreach (var kvp in _embeddedTabs)
        {
            var info = kvp.Value;
            if (info.Type == EmbedType.Application && info.Embedder?.IsEmbedded == true)
            {
                if (info.FormsHost != null && info.FormsHost.ActualWidth > 0)
                {
                    info.Embedder.ResizeEmbedded(
                        (int)info.FormsHost.ActualWidth,
                        (int)info.FormsHost.ActualHeight);
                }
            }
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            RootBorder.CornerRadius = new CornerRadius(0);
            RootBorder.Margin = new Thickness(0);
        }
        else
        {
            RootBorder.CornerRadius = new CornerRadius(14);
            RootBorder.Margin = new Thickness(8);
        }
    }

    // ===================================================================
    // SAVE & CLOSE
    // ===================================================================

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Close all embedded tabs
        foreach (var kvp in _embeddedTabs)
        {
            kvp.Value.Dispose();
        }
        _embeddedTabs.Clear();

        // Save window position/size
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
        }

        SaveSettings();

        // Cleanup
        _hotkeyHelper.Dispose();

        // Clean up WebView2 temp data
        try
        {
            if (Directory.Exists(_webViewDataFolder))
                Directory.Delete(_webViewDataFolder, true);
        }
        catch { /* Best effort cleanup */ }
    }

    private void SaveSettings()
    {
        _configService.Save(_settings);
    }

    // ===================================================================
    // UTILITY
    // ===================================================================

    private void SetStatus(string text)
    {
        Dispatcher.Invoke(() => StatusText.Text = text);
    }

    private static string TruncateName(string name, int maxLength)
    {
        if (name.Length <= maxLength) return name;
        return name[..(maxLength - 1)] + "…";
    }

    private static ImageSource CreateDefaultIcon()
    {
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            var gradient = new LinearGradientBrush(
                WpfColor.FromRgb(233, 69, 96),
                WpfColor.FromRgb(60, 60, 60),
                45);

            ctx.DrawRoundedRectangle(gradient, null,
                new Rect(0, 0, 32, 32), 6, 6);

            ctx.DrawText(
                new FormattedText("?",
                    System.Globalization.CultureInfo.CurrentCulture,
                    WpfFlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    18, WpfBrushes.White,
                    VisualTreeHelper.GetDpi(visual).PixelsPerDip),
                new WpfPoint(10, 4));
        }

        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}