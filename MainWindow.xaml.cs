using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Hoi4ModOverlay.Services;

namespace Hoi4ModOverlay;

public partial class MainWindow : Window
{
    private const double DefaultWidth = 560;
    private const double DefaultHeight = 650;

    private readonly ModCatalogService _catalog = new();
    private readonly ModSummaryEnrichmentService _summaryEnricher = new();
    private readonly OverlaySettingsService _settingsService = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _boundsSaveTimer;
    private readonly bool _previewMode;
    private readonly OverlayPositionSettings _positionSettings;
    private DateTime _lastDlcWriteUtc = DateTime.MinValue;
    private DateTime _nextEnrichmentAttemptUtc = DateTime.MinValue;
    private DateTime _nextAllKnownModsRefreshUtc = DateTime.MinValue;
    private bool _everSawGame;
    private bool _summaryEnrichmentRunning;
    private DateTime? _gameMissingSince;
    private string? _lastRuntimeState;
    private IReadOnlyList<Hoi4ModOverlay.Models.ModInfo> _currentMods = [];
    private IReadOnlyList<Hoi4ModOverlay.Models.ModInfo> _allKnownMods = [];

    public MainWindow()
    {
        InitializeComponent();
        _positionSettings = _settingsService.Load();
        var args = Environment.GetCommandLineArgs();
        _previewMode = args.Any(arg => arg.Equals("--preview", StringComparison.OrdinalIgnoreCase));

        if (args.Any(arg => arg.Equals("--launch-game", StringComparison.OrdinalIgnoreCase)))
        {
            TryLaunchHoi4();
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += OnTimerTick;

        _boundsSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _boundsSaveTimer.Tick += (_, _) =>
        {
            _boundsSaveTimer.Stop();
            SaveStandaloneBounds();
        };

        Loaded += OnLoaded;
        Closed += OnClosed;
        LocationChanged += (_, _) => ScheduleBoundsSave();
        SizeChanged += (_, _) => ScheduleBoundsSave();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReloadMods(force: true);
        ExpandedContent.Visibility = Visibility.Visible;
        HoverHintText.Text = _previewMode ? "预览 · 15 秒后自动关闭" : "独立窗口 · 可拖动 / 可调整大小";
        ApplyStandaloneBounds();
        TraceState($"loaded preview={_previewMode} standalone=true bounds={Left:F0},{Top:F0},{Width:F0}x{Height:F0}");

        if (_previewMode)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = DefaultWidth;
            Height = DefaultHeight;
            Activate();

            var previewAutoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            previewAutoCloseTimer.Tick += (_, _) =>
            {
                previewAutoCloseTimer.Stop();
                Close();
            };
            previewAutoCloseTimer.Start();
        }

        _timer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _boundsSaveTimer.Stop();
        if (!_previewMode)
        {
            SaveStandaloneBounds();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        ReloadMods(force: false);
        StartSummaryEnrichmentIfNeeded();
        if (_previewMode)
        {
            return;
        }

        var gameProcessExists = Process.GetProcessesByName("hoi4").Any();
        if (gameProcessExists)
        {
            _everSawGame = true;
            _gameMissingSince = null;
            if (!IsVisible)
            {
                Show();
            }
            TraceState($"standalone gameRunning=true visible={IsVisible} minimized={WindowState == WindowState.Minimized}");
            return;
        }

        TraceState($"standalone gameRunning=false everSaw={_everSawGame} visible={IsVisible}");
        if (_everSawGame)
        {
            _gameMissingSince ??= DateTime.UtcNow;
            if (DateTime.UtcNow - _gameMissingSince.Value > TimeSpan.FromSeconds(4))
            {
                Application.Current.Shutdown();
            }
        }
    }

    private void ApplyStandaloneBounds()
    {
        if (_previewMode)
        {
            Width = DefaultWidth;
            Height = DefaultHeight;
            return;
        }

        Width = Math.Clamp(_positionSettings.WindowWidth, MinWidth, 1400);
        Height = Math.Clamp(_positionSettings.WindowHeight, MinHeight, 1200);

        if (!_positionSettings.HasStandaloneBounds)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        var minLeft = SystemParameters.VirtualScreenLeft;
        var minTop = SystemParameters.VirtualScreenTop;
        var maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Math.Min(Width, 120);
        var maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Math.Min(Height, 80);
        Left = Math.Clamp(_positionSettings.WindowLeft, minLeft, maxLeft);
        Top = Math.Clamp(_positionSettings.WindowTop, minTop, maxTop);
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_previewMode || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // Ignore aborted drag gestures.
        }
        finally
        {
            ScheduleBoundsSave();
        }

        e.Handled = true;
    }

    private void ScheduleBoundsSave()
    {
        if (_previewMode || !IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        _boundsSaveTimer.Stop();
        _boundsSaveTimer.Start();
    }

    private void SaveStandaloneBounds()
    {
        if (_previewMode || WindowState != WindowState.Normal || double.IsNaN(Left) || double.IsNaN(Top))
        {
            return;
        }

        _positionSettings.HasStandaloneBounds = true;
        _positionSettings.WindowLeft = Left;
        _positionSettings.WindowTop = Top;
        _positionSettings.WindowWidth = ActualWidth > 0 ? ActualWidth : Width;
        _positionSettings.WindowHeight = ActualHeight > 0 ? ActualHeight : Height;
        _settingsService.Save(_positionSettings);
        TraceState($"standalone bounds saved {Left:F0},{Top:F0},{_positionSettings.WindowWidth:F0}x{_positionSettings.WindowHeight:F0}");
    }

    private void ReloadMods(bool force)
    {
        DateTime writeTime;
        try
        {
            writeTime = File.Exists(_catalog.DlcLoadPath)
                ? File.GetLastWriteTimeUtc(_catalog.DlcLoadPath)
                : DateTime.MinValue;
        }
        catch
        {
            writeTime = DateTime.MinValue;
        }

        if (!force && writeTime == _lastDlcWriteUtc)
        {
            return;
        }

        _lastDlcWriteUtc = writeTime;
        var mods = _catalog.LoadEnabledMods();
        _currentMods = mods;
        ModsItemsControl.ItemsSource = mods;
        ModCountText.Text = $"{mods.Count:00} MODS";
        UpdatedText.Text = DateTime.Now.ToString("HH:mm:ss");
        StartSummaryEnrichmentIfNeeded();
    }

    private void StartSummaryEnrichmentIfNeeded()
    {
        var now = DateTime.UtcNow;
        if (_summaryEnrichmentRunning || now < _nextEnrichmentAttemptUtc)
        {
            return;
        }

        if (_allKnownMods.Count == 0 || now >= _nextAllKnownModsRefreshUtc)
        {
            _allKnownMods = _catalog.LoadAllKnownMods();
            _nextAllKnownModsRefreshUtc = now.AddMinutes(10);
            TraceState($"catalog allKnown={_allKnownMods.Count} missing={_allKnownMods.Count(mod => !mod.HasSummary)}");
        }

        var missing = _allKnownMods.Where(mod => !mod.HasSummary).ToArray();
        if (missing.Length == 0)
        {
            _nextEnrichmentAttemptUtc = now.AddMinutes(10);
            return;
        }

        _summaryEnrichmentRunning = true;
        _nextEnrichmentAttemptUtc = now.AddMinutes(5);
        _ = EnrichSummariesAsync(missing);
    }

    private async Task EnrichSummariesAsync(IReadOnlyList<Hoi4ModOverlay.Models.ModInfo> mods)
    {
        try
        {
            var changed = await _summaryEnricher.EnrichMissingAsync(mods);
            if (changed)
            {
                _allKnownMods = [];
                _nextAllKnownModsRefreshUtc = DateTime.MinValue;
                await Dispatcher.InvokeAsync(() => ReloadMods(force: true));
            }
        }
        catch
        {
            // Description enrichment is best-effort and retries later.
        }
        finally
        {
            _summaryEnrichmentRunning = false;
        }
    }

    private void OpenSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string url } || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Keep the overlay usable even if the browser cannot be opened.
        }
    }

    private static void TryLaunchHoi4()
    {
        try
        {
            if (Process.GetProcessesByName("hoi4").Any())
            {
                return;
            }

            var configuredExe = Environment.GetEnvironmentVariable("HOI4_EXE");
            var candidates = new[]
            {
                configuredExe,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Hearts of Iron IV", "hoi4.exe")
            };
            var gameExe = candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
            if (gameExe is not null)
            {
                Process.Start(new ProcessStartInfo(gameExe)
                {
                    WorkingDirectory = Path.GetDirectoryName(gameExe)!,
                    UseShellExecute = true
                });
                return;
            }

            Process.Start(new ProcessStartInfo("steam://rungameid/394360") { UseShellExecute = true });
        }
        catch
        {
            // The overlay can still wait for a manually launched game.
        }
    }

    private void TraceState(string state)
    {
        if (string.Equals(_lastRuntimeState, state, StringComparison.Ordinal))
        {
            return;
        }

        _lastRuntimeState = state;
        try
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(
                Path.Combine(logDir, "runtime.log"),
                $"{DateTime.Now:O} {state}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never break the overlay.
        }
    }

}
