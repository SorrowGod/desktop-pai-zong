using System.Text;
using DesktopPet.App.Chat;
using DesktopPet.App.Models;

namespace DesktopPet.Tests;

[TestClass]
public sealed class SseChatParserTests
{
    [TestMethod]
    public async Task ParsesArbitraryNetworkChunksAndDone()
    {
        var payload = "data: {\"choices\":[{\"delta\":{\"content\":\"你\"}}]}\n\n"
            + "data: {\"choices\":[{\"delta\":{\"content\":\"好\"}}]}\n\n"
            + "data: [DONE]\n\n";
        var chunks = SplitEveryBytePattern(Encoding.UTF8.GetBytes(payload), [1, 2, 5, 3, 1, 7]);
        await using var stream = new ChunkedReadStream(chunks);

        var deltas = await CollectAsync(SseChatParser.ParseAsync(stream));

        CollectionAssert.AreEqual(
            new[] { "你", "好", "[DONE]" },
            deltas.Select(delta => delta.IsDone ? "[DONE]" : delta.Content).ToArray());
    }

    [TestMethod]
    public async Task AcceptsEmptyDelta()
    {
        var payload = "data: {\"choices\":[{\"delta\":{}}]}\n\ndata: [DONE]\n\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var deltas = await CollectAsync(SseChatParser.ParseAsync(stream));

        Assert.AreEqual(1, deltas.Count);
        Assert.IsTrue(deltas[0].IsDone);
    }

    [TestMethod]
    public async Task CancellationStopsBlockedRead()
    {
        await using var stream = new BlockingReadStream();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));

        try
        {
            await foreach (var _ in SseChatParser.ParseAsync(stream, cancellation.Token))
            {
            }
            Assert.Fail("Expected the blocked SSE read to be canceled.");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task<List<ChatDelta>> CollectAsync(IAsyncEnumerable<ChatDelta> source)
    {
        var result = new List<ChatDelta>();
        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }

    private static IReadOnlyList<byte[]> SplitEveryBytePattern(byte[] bytes, int[] pattern)
    {
        var result = new List<byte[]>();
        var offset = 0;
        var patternIndex = 0;
        while (offset < bytes.Length)
        {
            var length = Math.Min(pattern[patternIndex++ % pattern.Length], bytes.Length - offset);
            result.Add(bytes[offset..(offset + length)]);
            offset += length;
        }

        return result;
    }

    private sealed class ChunkedReadStream(IReadOnlyList<byte[]> chunks) : Stream
    {
        private int _chunkIndex;
        private int _chunkOffset;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_chunkIndex >= chunks.Count)
            {
                return ValueTask.FromResult(0);
            }

            var chunk = chunks[_chunkIndex];
            var count = Math.Min(buffer.Length, chunk.Length - _chunkOffset);
            chunk.AsMemory(_chunkOffset, count).CopyTo(buffer);
            _chunkOffset += count;
            if (_chunkOffset >= chunk.Length)
            {
                _chunkIndex++;
                _chunkOffset = 0;
            }

            return ValueTask.FromResult(count);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
