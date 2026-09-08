using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Tests.Infrastructure;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class CharacterIdentityTests
{
    // Entirely synthetic claims. No launch token or account identifier from a real client is a fixture.
    private static string Token(string payload) => "e30." + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".synthetic-signature";

    [Theory]
    [InlineData("/ssoToken=")]
    [InlineData("--ssoToken ")]
    [InlineData("/SSOTOKEN=\"")]
    public void ReadsOnlyTheNumericUserSubjectAndAcceptsExpiredLaunchMetadata(string argument)
    {
        string token = Token("{\"sub\":\"USER:EVE:12345678\",\"exp\":1,\"name\":\"Fake account\",\"irrelevant\":\"never retain\"}");
        Assert.Equal(12345678L, EveClientUserIdReader.ExtractUserId("ExeFile.exe /noconsole " + argument + token + "\" /other=value"));
        Assert.Null(EveClientUserIdReader.ExtractUserId("ExeFile.exe /other=" + token));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"sub\":\"CHARACTER:EVE:12345678\"}")]
    [InlineData("{\"sub\":\"USER:EVE:0\"}")]
    [InlineData("{\"sub\":\"USER:EVE:-5\"}")]
    [InlineData("{\"sub\":\"USER:EVE:999999999999999999999999\"}")]
    [InlineData("{\"sub\":\"USER:EVE:5x\"}")]
    [InlineData("{\"nested\":{\"sub\":\"USER:EVE:12345678\"}}")]
    [InlineData("{\"sub\":\"USER:EVE:12\",\"sub\":\"USER:EVE:13\"}")]
    [InlineData("not-json")]
    public void MalformedOrWrongSubjectDoesNotCreateAnAccountAssociation(string payload) =>
        Assert.Null(EveClientUserIdReader.ExtractUserId("ExeFile.exe /ssoToken=" + Token(payload)));

    [Fact]
    public void MalformedAndOversizedLaunchDataFailsClosed()
    {
        foreach (string token in new[] { "", "no-dots", "header.%invalid.signature", "h.e30.", new string('x', 70000) })
            Assert.Null(EveClientUserIdReader.ExtractUserId("ExeFile.exe /ssoToken=" + token));
    }

    [Fact]
    public async Task ConcurrentLookupStoresOnlyDerivedIdsAndRestartReusesThem()
    {
        using var fixture = new Fixture();
        var reply = new TaskCompletionSource<HttpResponseMessage>();
        fixture.Handler.Reply = _ => reply.Task;
        using var cache = fixture.Create();
        cache.ObserveProcesses([Client("EVE - Test Pilot", 42)]);
        var first = cache.GetCharacterAsync("EVE - Test Pilot");
        var second = cache.GetCharacterAsync("EVE - Test Pilot");
        reply.SetResult(Ok("{\"characters\":[{\"name\":\"Test Pilot\",\"id\":90000001}],\"corporations\":[{\"name\":\"Test Pilot\",\"id\":80000001}]}"));
        Assert.Equal(90000001L, (await first).CharacterId);
        Assert.Equal(12345678L, (await second).EveUserId);
        Assert.Equal(1, fixture.Handler.Requests);
        Assert.Equal(1, fixture.UserReads);
        Assert.Equal("[\"Test Pilot\"]", fixture.Handler.Body);
        Assert.Equal("https://esi.evetech.net/universe/ids", fixture.Handler.Uri);
        Assert.False(fixture.Handler.HasAuthorization);
        Assert.Equal("2026-09-07", fixture.Handler.CompatibilityDate);
        using var saved = JsonDocument.Parse(File.ReadAllText(fixture.Path));
        Assert.Equal(new[] { "Name", "CharacterId", "EveUserId", "CharacterCheckedAt", "UserCheckedAt" },
            saved.RootElement[0].EnumerateObject().Select(p => p.Name));
        Assert.DoesNotContain("sso", File.ReadAllText(fixture.Path), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("never retain", File.ReadAllText(fixture.Path));
        using var restarted = fixture.Create();
        Assert.Equal(90000001L, (await restarted.GetCharacterAsync("EVE - Test Pilot")).CharacterId);
        Assert.Equal(12345678L, restarted.KnownCharacters["Test Pilot"].EveUserId);
        Assert.Equal(1, fixture.Handler.Requests);
    }

    [Fact]
    public async Task AnOfflineIdentityGetsItsUserOnFirstDiscoveryAndRefreshesAfterTransfer()
    {
        using var fixture = new Fixture();
        using var cache = fixture.Create();
        Assert.Null((await cache.GetCharacterAsync("EVE - Test Pilot")).EveUserId);
        Assert.Equal(0, fixture.UserReads);
        var changed = NextChange(cache);
        cache.ObserveProcesses([Client("EVE - Test Pilot", 42)]);
        await changed.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(12345678L, cache.KnownCharacters["Test Pilot"].EveUserId);
        Assert.Equal(1, fixture.Handler.Requests);
        for (int i = 0; i < 20; i++) cache.ObserveProcesses([Client("EVE - Test Pilot", 42)]);
        Assert.Equal(1, fixture.UserReads);
        fixture.Now = fixture.Now.AddDays(8);
        fixture.UserId = 23456789;
        changed = NextChange(cache);
        Assert.Equal(90000001L, (await cache.GetCharacterAsync("EVE - Test Pilot")).CharacterId);
        await changed.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(23456789L, cache.KnownCharacters["Test Pilot"].EveUserId);
        Assert.Equal(2, fixture.Handler.Requests);
        Assert.Equal(2, fixture.UserReads);
    }

    [Fact]
    public async Task OfflineFailuresRetainKnownIdsAndRespectGlobalRetryAfter()
    {
        using var fixture = new Fixture();
        using var cache = fixture.Create();
        await cache.GetCharacterAsync("EVE - Test Pilot");
        fixture.Now = fixture.Now.AddDays(8);
        var requested = new TaskCompletionSource();
        fixture.Handler.Reply = _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(System.TimeSpan.FromHours(4));
            requested.TrySetResult();
            return Task.FromResult(response);
        };
        Assert.Equal(90000001L, (await cache.GetCharacterAsync("EVE - Test Pilot")).CharacterId);
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await cache.GetCharacterAsync("EVE - Another Pilot"); // Queued work observes the global cooldown.
        Assert.Equal(2, fixture.Handler.Requests);
        Assert.Equal(90000001L, cache.KnownCharacters["Test Pilot"].CharacterId);
        Assert.Null((await cache.GetCharacterAsync("EVE - Another Pilot")).CharacterId);
    }

    [Fact]
    public async Task NonCharacterMatchesAndLoginWindowsDoNotCreatePortraitIdentities()
    {
        using var fixture = new Fixture();
        fixture.Handler.Reply = _ => Task.FromResult(Ok("{\"corporations\":[{\"name\":\"Test Pilot\",\"id\":80000001}],\"characters\":[{\"name\":\"Different Pilot\",\"id\":90000002}]}"));
        using var cache = fixture.Create();
        Assert.Null(await cache.GetCharacterAsync("EVE"));
        Assert.Null(await cache.GetCharacterAsync("not an EVE window"));
        Assert.Null((await cache.GetCharacterAsync("EVE - Test Pilot")).CharacterId);
        Assert.Null((await cache.GetCharacterAsync("EVE - Test Pilot")).CharacterId);
        Assert.Equal(1, fixture.Handler.Requests);
        Assert.False(File.Exists(fixture.Path));
    }

    [Fact]
    public async Task AChangedProcessAssociationCannotCommitAnOldUserId()
    {
        using var fixture = new Fixture();
        var reply = new TaskCompletionSource<HttpResponseMessage>();
        fixture.Handler.Reply = _ => reply.Task;
        using var cache = fixture.Create();
        cache.ObserveProcesses([Client("EVE - Test Pilot", 42)]);
        var pending = cache.GetCharacterAsync("EVE - Test Pilot");
        cache.ObserveProcesses([]);
        reply.SetResult(Ok("{\"characters\":[{\"name\":\"Test Pilot\",\"id\":90000001}]}"));
        Assert.Null((await pending).EveUserId);
    }

    [Fact]
    public async Task UserIdentityIsAvailableWithoutEsiAndNewProcessesRetryMissingSubjects()
    {
        using var fixture = new Fixture();
        fixture.Handler.Reply = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        fixture.UserId = null;
        using var cache = fixture.Create();
        cache.ObserveProcesses([Client("EVE - Test Pilot", 42)]);
        Assert.Null((await cache.GetCharacterAsync("EVE - Test Pilot")).EveUserId);
        cache.ObserveProcesses([Client("EVE - Test Pilot", 42)]);
        Assert.Equal(1, fixture.UserReads);
        fixture.UserId = 12345678;
        cache.ObserveProcesses([Client("EVE - Test Pilot", 43)]);
        var identity = await cache.GetCharacterAsync("EVE - Test Pilot");
        Assert.Equal(12345678L, identity.EveUserId);
        Assert.Null(identity.CharacterId);
        Assert.Equal(1, fixture.Handler.Requests);
        Assert.Equal(2, fixture.UserReads);
    }

    private static Task NextChange(CharacterIdentityCache cache)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed() { cache.Changed -= Changed; done.TrySetResult(); }
        cache.Changed += Changed;
        return done.Task;
    }
    private static IProcessInfo Client(string title, int pid) => Stub.Create<IProcessInfo>((method, _) => method.Name switch
        { "get_Title" => title, "get_ProcessId" => pid, _ => Stub.Default(method.ReturnType) });
    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Reply = _ => Task.FromResult(Ok("{\"characters\":[{\"name\":\"Test Pilot\",\"id\":90000001}]}"));
        public int Requests; public string Uri, Body, CompatibilityDate; public bool HasAuthorization;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Requests);
            Uri = request.RequestUri.AbsoluteUri; Body = await request.Content.ReadAsStringAsync(token);
            HasAuthorization = request.Headers.Authorization is not null;
            CompatibilityDate = request.Headers.GetValues("X-Compatibility-Date").Single();
            return await Reply(request);
        }
    }
    private sealed class Fixture : IDisposable
    {
        public string Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EveOIdentity-" + Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(Root, "Characters.json");
        public Handler Handler = new();
        public DateTimeOffset Now = new(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);
        public int UserReads; public long? UserId = 12345678;
        private readonly Serilog.Core.Logger _logger = new Serilog.LoggerConfiguration().CreateLogger();
        private readonly HttpClient _client;
        public Fixture() { _client = new(Handler); Directory.CreateDirectory(Root); }
        public CharacterIdentityCache Create() => new(Path, _client, (_, _) =>
        {
            Interlocked.Increment(ref UserReads);
            return UserId is long userId ? EveClientUserIdReader.ExtractUserId("ExeFile.exe /ssoToken=" + Token($"{{\"sub\":\"USER:EVE:{userId}\",\"exp\":1,\"extra\":\"never retain\"}}")) : null;
        }, _logger, () => Now);
        public void Dispose() { _client.Dispose(); _logger.Dispose(); Directory.Delete(Root, true); }
    }
}
