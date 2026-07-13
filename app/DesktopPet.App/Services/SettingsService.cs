using DesktopPet.App.Infrastructure;
using DesktopPet.App.Models;
using DesktopPet.App.Security;

namespace DesktopPet.App.Services;

public sealed class SettingsService(AppPaths paths, ISecretProtector secretProtector)
{
    private readonly AtomicJsonStore<AppSettings> _store = new(paths.SettingsFile);

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _store.LoadOrDefaultAsync(() => new AppSettings(), cancellationToken);
        settings.Normalize();
        return settings;
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Normalize();
        return _store.SaveAsync(settings, cancellationToken);
    }

    public string GetApiKey(AppSettings settings)
    {
        try
        {
            return secretProtector.Unprotect(settings.ProtectedApiKey);
        }
        catch (Exception exception) when (exception is FormatException or System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }

    public void SetApiKey(AppSettings settings, string apiKey)
    {
        settings.ProtectedApiKey = secretProtector.Protect(apiKey.Trim());
    }
}
