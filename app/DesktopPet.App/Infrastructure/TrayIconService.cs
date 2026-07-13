using System.Drawing;
using System.Windows.Forms;

namespace DesktopPet.App.Infrastructure;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayIconService(
        Action togglePet,
        Action showPet,
        Action openChat,
        Action openSettings,
        Action showAbout,
        Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示/隐藏猫咪", null, (_, _) => togglePet());
        menu.Items.Add("聊天", null, (_, _) => openChat());
        menu.Items.Add("设置", null, (_, _) => openSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("关于", null, (_, _) => showAbout());
        menu.Items.Add("退出", null, (_, _) => exit());

        var icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
            ?? SystemIcons.Application;
        _notifyIcon = new NotifyIcon
        {
            Text = "桌面猫咪",
            Icon = icon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => showPet();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
