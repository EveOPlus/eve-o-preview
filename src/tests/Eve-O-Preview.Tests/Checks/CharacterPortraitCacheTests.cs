using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Services.Implementation;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class CharacterPortraitCacheTests
{
    [Fact]
    public async Task ConcurrentRequestsShareDownloadAndRestartUsesCharacterIdFile()
    {
        using var fixture = new Fixture();
        var response = new TaskCompletionSource<HttpResponseMessage>();
        fixture.Handler.Reply = _ => response.Task;
        var cache = fixture.CreateCache();
        var first = cache.GetCharacterPortraitAsync(95465272);
        var second = cache.GetCharacterPortraitAsync(95465272);
        Assert.False(first.IsCompleted);
        response.SetResult(Ok(fixture.Jpeg));
        Assert.Equal(fixture.Jpeg, await first);
        Assert.Equal(fixture.Jpeg, await second);
        Assert.Equal(1, fixture.Handler.Count);
        Assert.Equal("https://images.evetech.net/characters/95465272/portrait?size=128", fixture.Handler.LastUri);
        Assert.Equal(fixture.Jpeg, File.ReadAllBytes(Path.Combine(fixture.Root, "95465272.jpg")));
        Assert.Equal(fixture.Jpeg, await fixture.CreateCache().GetCharacterPortraitAsync(95465272));
        Assert.Equal(1, fixture.Handler.Count);
        Assert.Single(Directory.GetFiles(fixture.Root));
        var preferences = new ApplicationPreferences(Path.Combine(fixture.Root, "EVE-O Preview.settings.json"), fixture.Logger);
        Assert.Equal(Path.Combine(fixture.Root, "Cache", "Portraits"), new CharacterPortraitCache(preferences, fixture.Logger).DirectoryPath);
    }

    [Fact]
    public async Task OldPortraitIsImmediatelyAvailableWhileItsWeeklyRefreshRuns()
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Root, "95465272.jpg");
        File.WriteAllBytes(path, fixture.Jpeg);
        var oldTime = DateTime.UtcNow.AddDays(-8);
        File.SetLastWriteTimeUtc(path, oldTime);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        fixture.Handler.Reply = _ => response.Task;
        var cache = fixture.CreateCache();
        Assert.Equal(fixture.Jpeg, await cache.GetCharacterPortraitAsync(95465272).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(response.Task.IsCompleted);
        Assert.Equal(1, fixture.Handler.Count);
        response.SetResult(Ok(fixture.Jpeg));
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (File.GetLastWriteTimeUtc(path) <= oldTime.AddSeconds(1) && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(File.GetLastWriteTimeUtc(path) > oldTime.AddSeconds(1));
        Assert.Equal(fixture.Jpeg, await cache.GetCharacterPortraitAsync(95465272));
        Assert.Equal(1, fixture.Handler.Count);
    }

    [Fact]
    public async Task FailedOrInvalidResponsesKeepTheOldImageAndDoNotRetryContinuously()
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Root, "95465272.jpg");
        File.WriteAllBytes(path, fixture.Jpeg);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-8));
        fixture.Handler.Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var cache = fixture.CreateCache();
        Assert.Equal(fixture.Jpeg, await cache.GetCharacterPortraitAsync(95465272));
        Assert.Equal(fixture.Jpeg, await cache.GetCharacterPortraitAsync(95465272));
        Assert.Equal(fixture.Jpeg, File.ReadAllBytes(path));
        Assert.Equal(1, fixture.Handler.Count);

        fixture.Handler.Reply = _ => Task.FromResult(Ok("not an image"u8.ToArray()));
        Assert.Null(await cache.GetCharacterPortraitAsync(123));
        Assert.Null(await cache.GetCharacterPortraitAsync(123));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "123.jpg")));
        Assert.Equal(2, fixture.Handler.Count);
    }

    private static HttpResponseMessage Ok(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Reply { get; set; }
        public int Count;
        public string LastUri;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Count);
            LastUri = request.RequestUri.AbsoluteUri;
            return Reply(request);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "EveOPreviewPortraits-" + Guid.NewGuid().ToString("N"));
        public Handler Handler { get; } = new();
        public Serilog.Core.Logger Logger { get; } = new LoggerConfiguration().CreateLogger();
        public byte[] Jpeg { get; }
        private readonly HttpClient _client;
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            _client = new HttpClient(Handler);
            using var stream = typeof(Fixture).Assembly.GetManifestResourceStream("EveOPreview.Tests.Portrait.jpg");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            Jpeg = buffer.ToArray();
        }
        public CharacterPortraitCache CreateCache() => new(Root, _client, Logger);
        public void Dispose()
        {
            _client.Dispose();
            Logger.Dispose();
            Directory.Delete(Root, recursive: true);
        }
    }
}
