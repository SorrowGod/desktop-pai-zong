using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DesktopPet.App.Chat;
using DesktopPet.App.Infrastructure;
using DesktopPet.App.Models;
using DesktopPet.App.Services;

namespace DesktopPet.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly ChatHistoryService _historyService;
    private readonly IChatCompletionClient _chatClient;
    private readonly AutoStartManager _autoStartManager;
    private readonly DesktopShortcutService _shortcutService;
    private readonly AppPaths _paths;
    private readonly Action _settingsChanged;
    private CancellationTokenSource? _testCancellation;

    public SettingsWindow(
        AppSettings settings,
        SettingsService settingsService,
        ChatHistoryService historyService,
        IChatCompletionClient chatClient,
        AutoStartManager autoStartManager,
        DesktopShortcutService shortcutService,
        AppPaths paths,
        Action settingsChanged)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;
        _historyService = historyService;
        _chatClient = chatClient;
        _autoStartManager = autoStartManager;
        _shortcutService = shortcutService;
        _paths = paths;
        _settingsChanged = settingsChanged;
        LoadSettings();
        Closed += (_, _) => _testCancellation?.Cancel();
    }

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
    }

    private void LoadSettings()
    {
        PetNameBox.Text = _settings.PetName;
        ApiBaseBox.Text = _settings.ApiBase;
        ApiKeyBox.Password = _settingsService.GetApiKey(_settings);
        ModelBox.Text = _settings.Model;
        AutoStartBox.IsChecked = _autoStartManager.IsEnabled();

        foreach (var item in PetScaleBox.Items.OfType<ComboBoxItem>())
        {
            if (double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
                && Math.Abs(scale - _settings.PetScale) < 0.01)
            {
                PetScaleBox.SelectedItem = item;
                break;
            }
        }

        PetScaleBox.SelectedIndex = PetScaleBox.SelectedIndex < 0 ? 2 : PetScaleBox.SelectedIndex;
    }

    private async void Save_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (!TryApplyForm(out var error))
        {
            StatusText.Text = error;
            return;
        }

        try
        {
            _autoStartManager.SetEnabled(_settings.AutoStart);
            await _settingsService.SaveAsync(_settings);
            _settingsChanged();
            StatusText.Text = "设置已保存";
            Close();
        }
        catch (Exception exception)
        {
            StatusText.Text = $"保存失败：{exception.Message}";
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs) => Close();

    private async void TestApi_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (!TryReadApiForm(out var apiBase, out var apiKey, out var model, out var error))
        {
            ApiTestText.Text = error;
            return;
        }

        _testCancellation?.Cancel();
        _testCancellation?.Dispose();
        _testCancellation = new CancellationTokenSource();
        TestButton.IsEnabled = false;
        ApiTestText.Text = "正在测试…";
        try
        {
            var result = await _chatClient.TestAsync(
                apiBase,
                apiKey,
                model,
                TimeSpan.FromSeconds(20),
                _testCancellation.Token);
            ApiTestText.Text = result.Latency is null
                ? result.Message
                : $"{result.Message}（{result.Latency.Value.TotalMilliseconds:F0} ms）";
        }
        catch (OperationCanceledException)
        {
            ApiTestText.Text = "测试已取消";
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void CreateShortcut_Click(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            var path = _shortcutService.Create();
            StatusText.Text = $"快捷方式已创建：{path}";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"创建快捷方式失败：{exception.Message}";
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs eventArgs)
    {
        _paths.EnsureCreated();
        Process.Start(new ProcessStartInfo("explorer.exe", _paths.LogsDirectory) { UseShellExecute = true });
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (System.Windows.MessageBox.Show(this, "确定清空最近聊天记录吗？", "清空历史", MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes)
        {
            await _historyService.ClearAsync();
            StatusText.Text = "聊天历史已清空";
        }
    }

    private async void ClearLocalData_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (System.Windows.MessageBox.Show(
                this,
                "这会重置设置、清空聊天历史和日志，但不会删除程序。确定继续吗？",
                "清除本地数据",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning)
            != MessageBoxResult.Yes)
        {
            return;
        }

        _autoStartManager.SetEnabled(false);
        await _historyService.ClearAsync();
        var defaults = new AppSettings();
        _settings.PetName = defaults.PetName;
        _settings.PetScale = defaults.PetScale;
        _settings.Affection = defaults.Affection;
        _settings.PetLeft = null;
        _settings.PetTop = null;
        _settings.ApiBase = defaults.ApiBase;
        _settings.Model = defaults.Model;
        _settings.ProtectedApiKey = string.Empty;
        _settings.AutoStart = false;
        await _settingsService.SaveAsync(_settings);

        if (Directory.Exists(_paths.LogsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(_paths.LogsDirectory))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }

        _settingsChanged();
        LoadSettings();
        StatusText.Text = "本地数据已重置";
    }

    private bool TryApplyForm(out string error)
    {
        if (!TryReadApiForm(out var apiBase, out var apiKey, out var model, out error, requireApiKey: false))
        {
            return false;
        }

        var selectedScale = (PetScaleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!double.TryParse(selectedScale, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
        {
            error = "请选择有效的猫咪大小。";
            return false;
        }

        _settings.PetName = PetNameBox.Text;
        _settings.PetScale = scale;
        _settings.ApiBase = apiBase;
        _settings.Model = model;
        _settings.AutoStart = AutoStartBox.IsChecked == true;
        _settingsService.SetApiKey(_settings, apiKey);
        _settings.Normalize();
        return true;
    }

    private bool TryReadApiForm(
        out string apiBase,
        out string apiKey,
        out string model,
        out string error,
        bool requireApiKey = true)
    {
        apiBase = ApiBaseBox.Text.Trim();
        apiKey = ApiKeyBox.Password.Trim();
        model = ModelBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            error = "模型名称不能为空。";
            return false;
        }

        var validation = ApiEndpointNormalizer.Normalize(apiBase, requireApiKey, apiKey);
        error = validation.Error;
        return validation.IsValid;
    }
}
