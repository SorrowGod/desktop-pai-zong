using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using DesktopPet.App.Chat;
using DesktopPet.App.Infrastructure;
using DesktopPet.App.Models;
using DesktopPet.App.Services;

namespace DesktopPet.App.Windows;

public partial class ChatWindow : Window
{
    private readonly IChatCompletionClient _chatClient;
    private readonly ChatHistoryService _historyService;
    private readonly Func<AppSettings> _getSettings;
    private readonly Func<string> _getApiKey;
    private readonly IAppLogger _logger;
    private CancellationTokenSource? _requestCancellation;
    private bool _loaded;

    public ChatWindow(
        IChatCompletionClient chatClient,
        ChatHistoryService historyService,
        Func<AppSettings> getSettings,
        Func<string> getApiKey,
        IAppLogger logger)
    {
        InitializeComponent();
        _chatClient = chatClient;
        _historyService = historyService;
        _getSettings = getSettings;
        _getApiKey = getApiKey;
        _logger = logger;
        DataContext = this;
        Loaded += OnLoaded;
        Closed += (_, _) => CancelRequest();
    }

    public ObservableCollection<ChatDisplayMessage> Messages { get; } = [];

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        InputBox.Focus();
    }

    public void StopAndClose()
    {
        CancelRequest();
        Close();
    }

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var settings = _getSettings();
        TitleText.Text = $"和 {settings.PetName} 聊天";
        var history = await _historyService.LoadAsync();
        foreach (var message in history)
        {
            Messages.Add(new ChatDisplayMessage(message.Role, message.Content));
        }

        ScrollToEnd();
        InputBox.Focus();
    }

    private async void Send_Click(object sender, RoutedEventArgs eventArgs) => await SendAsync();

    private async void InputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            eventArgs.Handled = true;
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        var content = InputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(content) || _requestCancellation is not null)
        {
            return;
        }

        var settings = _getSettings();
        var apiKey = _getApiKey();
        var validation = ApiEndpointNormalizer.Normalize(settings.ApiBase, true, apiKey);
        if (!validation.IsValid)
        {
            StatusText.Text = validation.Error;
            return;
        }

        InputBox.Clear();
        var userMessage = new ChatDisplayMessage("user", content);
        var assistantMessage = new ChatDisplayMessage("assistant", string.Empty);
        Messages.Add(userMessage);
        Messages.Add(assistantMessage);
        ScrollToEnd();
        SetGenerating(true);

        _requestCancellation = new CancellationTokenSource();
        try
        {
            var requestMessages = new List<ChatMessage>
            {
                new("system", BuildSystemPrompt(settings.PetName))
            };
            requestMessages.AddRange(Messages
                .Where(message => message != assistantMessage)
                .TakeLast(ChatHistoryService.MaximumMessages)
                .Select(message => new ChatMessage(message.Role, message.Content)));
            var request = new ChatRequest(settings.Model, requestMessages, true);

            await foreach (var delta in _chatClient.StreamAsync(
                settings.ApiBase,
                apiKey,
                request,
                TimeSpan.FromSeconds(90),
                _requestCancellation.Token))
            {
                if (delta.IsDone)
                {
                    break;
                }

                if (!string.IsNullOrEmpty(delta.Content))
                {
                    assistantMessage.Content += delta.Content;
                    ScrollToEnd();
                }
            }

            if (string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                assistantMessage.Content = "（服务没有返回可显示的内容）";
            }

            await SaveHistoryAsync();
            StatusText.Text = "完成";
        }
        catch (OperationCanceledException)
        {
            if (string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                Messages.Remove(assistantMessage);
            }

            StatusText.Text = "已停止";
        }
        catch (TimeoutException exception)
        {
            assistantMessage.Content = "请求超时，请稍后重试。";
            StatusText.Text = assistantMessage.Content;
            _logger.Error("Chat request timed out.", exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or ArgumentException or JsonException)
        {
            assistantMessage.Content = "请求失败，请检查 AI 地址、Key、模型和网络设置。";
            StatusText.Text = assistantMessage.Content;
            _logger.Error("Chat request failed.", exception);
        }
        finally
        {
            _requestCancellation?.Dispose();
            _requestCancellation = null;
            SetGenerating(false);
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs eventArgs) => CancelRequest();

    private async void ClearHistory_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (System.Windows.MessageBox.Show(this, "确定清空最近聊天记录吗？", "清空历史", MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes)
        {
            return;
        }

        CancelRequest();
        Messages.Clear();
        await _historyService.ClearAsync();
        StatusText.Text = "历史已清空";
    }

    private void CancelRequest() => _requestCancellation?.Cancel();

    private void SetGenerating(bool generating)
    {
        SendButton.IsEnabled = !generating;
        StopButton.IsEnabled = generating;
        InputBox.IsEnabled = !generating;
        if (generating)
        {
            StatusText.Text = "正在生成…";
        }
    }

    private Task SaveHistoryAsync()
    {
        return _historyService.SaveAsync(
            Messages.Select(message => new ChatMessage(message.Role, message.Content)));
    }

    private void ScrollToEnd()
    {
        if (Messages.Count > 0)
        {
            MessagesList.ScrollIntoView(Messages[^1]);
        }
    }

    private static string BuildSystemPrompt(string petName)
    {
        return $"你是一只住在用户桌面上的可爱猫咪桌宠。你的名字叫{petName}。"
            + "你性格活泼可爱，喜欢撒娇，会用“喵~”“呜呜”“嗷~”等语气词。"
            + "回复要简短可爱，一般 1-3 句话，偶尔关心用户有没有好好休息。";
    }
}

public sealed class ChatDisplayMessage : INotifyPropertyChanged
{
    private string _content;

    public ChatDisplayMessage(string role, string content)
    {
        Role = role;
        _content = content;
    }

    public string Role { get; }
    public string Speaker => Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "你" : "猫咪";

    public string Content
    {
        get => _content;
        set
        {
            if (_content == value)
            {
                return;
            }

            _content = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
