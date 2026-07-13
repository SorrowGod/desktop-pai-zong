using System.Reflection;
using System.Runtime.InteropServices;

namespace DesktopPet.App.Infrastructure;

public sealed class DesktopShortcutService
{
    public string Create()
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法读取当前可执行文件路径。");
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var shortcutPath = Path.Combine(desktop, "桌面猫咪.lnk");
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("系统不支持 Windows Script Host 快捷方式接口。");
        var shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("无法创建 Windows Script Host 对象。");

        try
        {
            var shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);
            if (shortcut is null)
            {
                throw new InvalidOperationException("无法创建快捷方式对象。");
            }

            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [executable]);
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [Path.GetDirectoryName(executable)!]);
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, [$"{executable},0"]);
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, ["桌面猫咪"]);
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            Marshal.FinalReleaseComObject(shortcut);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }

        return shortcutPath;
    }
}
