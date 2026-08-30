using System.Net;
using System.Net.Http;
using System.Windows;
using DesktopPet.App.Chat;
using DesktopPet.App.Infrastructure;
using DesktopPet.App.Models;
using DesktopPet.App.Pet;
using DesktopPet.App.Security;
using DesktopPet.App.Services;
using DesktopPet.App.Windows;

namespace DesktopPet.App;

public partial class App : System.Windows.Application
{
    private readonly AppPaths _paths = new();
    private SingleInstanceService? _singleInstance;
    private AppLogger? _logger;
    private SettingsService? _settingsService;
    private ChatHistoryService? _historyService;
    private AutoStartManager? _autoStartManager;
    private DesktopShortcutService? _shortcutService;
    private HttpClient? _httpClient;
    private IChatCompletionClient? _chatClient;
    private AppSettings? _settings;
    private PetWindow? _petWindow;
    private BubbleWindow? _bubbleWindow;
    private ChatWindow? _chatWindow;
    private SettingsWindow? _settingsWindow;
    private TrayIconService? _trayIcon;
    private PetStateMachine? _stateMachine;
    private SpriteAnimator? _animator;
    private AffectionMeter? _affection;
    private bool _exiting;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        _paths.EnsureCreated();
        _logger = new AppLogger(_paths);
        DispatcherUnhandledException += (_, args) =>
        {
            _logger.Error("Unhandled UI exception.", args.Exception);
            System.Windows.MessageBox.Show("桌面派总遇到错误，详情已写入日志。", "桌面派总", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _singleInstance = new SingleInstanceService();
        var startupCommand = GetStartupCommand(eventArgs.Args);
        if (!_singleInstance.IsFirstInstance)
        {
            try
            {
                await _singleInstance.SignalPrimaryAsync(startupCommand);
            }
            catch (Exception exception)
            {
                _logger.Error("Unable to notify primary instance.", exception);
            }

            _singleInstance.Dispose();
            Shutdown();
            return;
        }

        _singleInstance.StartListening(command => Dispatcher.InvokeAsync(() => HandleInstanceCommand(command)).Task);
        _ = _singleInstance.ListenerTask?.ContinueWith(
            task => _logger.Error("Single-instance pipe listener stopped unexpectedly.", task.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        _settingsService = new SettingsService(_paths, new DpapiSecretProtector());
        _historyService = new ChatHistoryService(_paths);
        _autoStartManager = new AutoStartManager();
        _shortcutService = new DesktopShortcutService();
        _settings = await _settingsService.LoadAsync();
        _settings.AutoStart = _autoStartManager.IsEnabled();

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };
        _httpClient = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopPet/1.0");
        _chatClient = new OpenAiCompatibleChatClient(_httpClient);
        _affection = new AffectionMeter(_settings.Affection);
        _stateMachine = new PetStateMachine();
        var catalog = new SpriteCatalog();
        _animator = new SpriteAnimator(catalog);
        _petWindow = new PetWindow();
        _bubbleWindow = new BubbleWindow();

        WirePetEvents();
        _stateMachine.StateChanged += (_, state) => Dispatcher.Invoke(() => _animator.SetState(state));
        _animator.FrameChanged += (_, frame) => _petWindow.SetFrame(frame);
        _petWindow.SetPetScale(_settings.PetScale);
        _petWindow.Show();
        _animator.Start(PetAnimationState.Idle);
        _stateMachine.Start();
        _ = _petWindow.Dispatcher.BeginInvoke(() => RestorePetPosition());

        _trayIcon = new TrayIconService(
            TogglePet,
            ShowPet,
            OpenChat,
            OpenSettings,
            ShowAbout,
            () => _ = ExitAsync());
        HandleInstanceCommand(startupCommand);
        _logger.Info("DesktopPet started.");
    }

    private void WirePetEvents()
    {
        if (_petWindow is null)
        {
            return;
        }

        _petWindow.SingleClicked += (_, _) =>
        {
            _logger?.Info("Pet single click received.");
            HandleInteraction(1, PetAnimationState.Reaction, "哇哦！是你！", "派大星在这里！", "今天也要开心呀~");
        };
        _petWindow.DoubleClicked += (_, _) =>
        {
            _logger?.Info("Pet double click received.");
            OpenChat();
        };
        _petWindow.FeedRequested += (_, _) => HandleInteraction(5, PetAnimationState.Happy, "好吃到转圈圈！", "谢谢你投喂派大星~", "肚肚圆滚滚啦！");
        _petWindow.PetRequested += (_, _) => HandleInteraction(3, PetAnimationState.Happy, "软绵绵地抱一下~", "派大星喜欢被摸摸！", "再来一下嘛~");
        _petWindow.PlayRequested += (_, _) => HandleInteraction(8, PetAnimationState.Walk, "一起去比奇堡散步！", "派大星要开始摇摆啦！", "看我的海星步！");
        _petWindow.ChatRequested += (_, _) => OpenChat();
        _petWindow.SettingsRequested += (_, _) => OpenSettings();
        _petWindow.HideRequested += (_, _) => HidePet();
        _petWindow.ExitRequested += (_, _) => _ = ExitAsync();
        _petWindow.Dragging += (_, _) =>
        {
            if (_stateMachine?.State != PetAnimationState.Drag)
            {
                _logger?.Info("Pet drag started.");
                _stateMachine?.Enter(PetAnimationState.Drag);
            }

            if (_bubbleWindow?.IsVisible == true)
            {
                _bubbleWindow.Follow(_petWindow.GetPhysicalBounds());
            }
        };
        _petWindow.DragEnded += (_, _) =>
        {
            _logger?.Info("Pet drag ended.");
            _stateMachine?.NotifyInteraction();
        };
        _petWindow.ContextMenuOpened += (_, _) => _logger?.Info("Pet context menu opened.");
        _petWindow.PositionCommitted += (_, position) =>
        {
            if (_settings is null)
            {
                return;
            }

            _settings.PetLeft = position.X;
            _settings.PetTop = position.Y;
            _ = _settingsService?.SaveAsync(_settings);
        };
    }

    private void HandleInteraction(int affection, PetAnimationState state, params string[] bubbles)
    {
        if (_settings is null || _affection is null || _petWindow is null)
        {
            return;
        }

        _settings.Affection = _affection.Add(affection);
        _stateMachine?.Enter(state, TimeSpan.FromMilliseconds(state == PetAnimationState.Walk ? 1_300 : 850));
        _bubbleWindow?.ShowMessage(
            bubbles[Random.Shared.Next(bubbles.Length)],
            _petWindow.GetPhysicalBounds(),
            TimeSpan.FromSeconds(2.8));
        _ = _settingsService?.SaveAsync(_settings);
    }

    private void RestorePetPosition()
    {
        if (_petWindow is null || _settings is null)
        {
            return;
        }

        var bounds = _petWindow.GetPhysicalBounds();
        var position = PositionService.GetInitialPosition(_settings, bounds);
        _petWindow.MovePhysical(position.X, position.Y);
        if (_settings.PetLeft != position.X || _settings.PetTop != position.Y)
        {
            _settings.PetLeft = position.X;
            _settings.PetTop = position.Y;
            _ = _settingsService?.SaveAsync(_settings);
        }
    }

    private void TogglePet()
    {
        if (_petWindow?.IsVisible == true)
        {
            HidePet();
        }
        else
        {
            ShowPet();
        }
    }

    private void ShowPet()
    {
        if (_petWindow is null)
        {
            return;
        }

        _petWindow.Show();
        RestorePetPosition();
    }

    private void HidePet()
    {
        _bubbleWindow?.Stop();
        _petWindow?.Hide();
    }

    private void OpenChat()
    {
        if (_chatClient is null || _historyService is null || _settings is null || _settingsService is null || _logger is null)
        {
            return;
        }

        if (_chatWindow is null)
        {
            _chatWindow = new ChatWindow(
                _chatClient,
                _historyService,
                () => _settings,
                () => _settingsService.GetApiKey(_settings),
                _logger);
            _chatWindow.Closed += (_, _) => _chatWindow = null;
        }

        _chatWindow.BringToFront();
    }

    private void OpenSettings()
    {
        if (_settings is null
            || _settingsService is null
            || _historyService is null
            || _chatClient is null
            || _autoStartManager is null
            || _shortcutService is null)
        {
            return;
        }

        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(
                _settings,
                _settingsService,
                _historyService,
                _chatClient,
                _autoStartManager,
                _shortcutService,
                _paths,
                ApplySettings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.BringToFront();
    }

    private void ApplySettings()
    {
        if (_settings is null || _petWindow is null)
        {
            return;
        }

        _petWindow.SetPetScale(_settings.PetScale);
        _affection = new AffectionMeter(_settings.Affection);
        RestorePetPosition();
    }

    private void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            "桌面派总 1.0\n.NET 8 · WPF · 原生透明窗口\n\nAPI Key 仅使用 Windows DPAPI CurrentUser 加密保存在本机。",
            "关于桌面派总",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void HandleInstanceCommand(string command)
    {
        switch (command.ToLowerInvariant())
        {
            case "chat":
                OpenChat();
                break;
            case "settings":
                OpenSettings();
                break;
            case "hide":
                HidePet();
                break;
            case "exit":
                _ = ExitAsync();
                break;
            default:
                ShowPet();
                break;
        }
    }

    private static string GetStartupCommand(IReadOnlyList<string> arguments)
    {
        var command = arguments.FirstOrDefault(argument => argument.StartsWith("--", StringComparison.Ordinal));
        return command?.TrimStart('-').ToLowerInvariant() switch
        {
            "chat" => "chat",
            "settings" => "settings",
            "hide" => "hide",
            "exit" => "exit",
            _ => "show"
        };
    }

    private async Task ExitAsync()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        try
        {
            if (_settings is not null && _petWindow is not null)
            {
                var bounds = _petWindow.GetPhysicalBounds();
                _settings.PetLeft = bounds.Left;
                _settings.PetTop = bounds.Top;
                _settings.Affection = _affection?.Value ?? _settings.Affection;
                if (_settingsService is not null)
                {
                    await _settingsService.SaveAsync(_settings);
                }
            }
        }
        catch (Exception exception)
        {
            _logger?.Error("Unable to save settings during shutdown.", exception);
        }

        _chatWindow?.StopAndClose();
        _settingsWindow?.Close();
        _bubbleWindow?.Stop();
        _bubbleWindow?.Close();
        _petWindow?.ClosePermanently();
        _trayIcon?.Dispose();
        _animator?.Dispose();
        _stateMachine?.Dispose();
        _singleInstance?.Dispose();
        _httpClient?.Dispose();
        _logger?.Info("DesktopPet stopped.");
        Shutdown();
    }
}
