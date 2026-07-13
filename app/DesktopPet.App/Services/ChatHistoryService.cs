using DesktopPet.App.Infrastructure;
using DesktopPet.App.Models;

namespace DesktopPet.App.Services;

public sealed class ChatHistoryService(AppPaths paths)
{
    public const int MaximumMessages = 20;
    private readonly AtomicJsonStore<List<ChatMessage>> _store = new(paths.ChatHistoryFile);

    public async Task<IReadOnlyList<ChatMessage>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var messages = await _store.LoadOrDefaultAsync(() => [], cancellationToken);
        return Normalize(messages);
    }

    public Task SaveAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        return _store.SaveAsync(Normalize(messages), cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) => _store.SaveAsync([], cancellationToken);

    internal static List<ChatMessage> Normalize(IEnumerable<ChatMessage> messages)
    {
        return messages
            .Where(message =>
                (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase)
                 || message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new ChatMessage(message.Role.ToLowerInvariant(), message.Content))
            .TakeLast(MaximumMessages)
            .ToList();
    }
}
