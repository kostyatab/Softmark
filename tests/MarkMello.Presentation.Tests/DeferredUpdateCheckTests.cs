using MarkMello.Application.Updates;
using MarkMello.Presentation.Services;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Отложенная фоновая проверка (ADR-0004, «Update Model»): одна на запуск, общая для всех
/// окон, в сеть — только после паузы, выход отменяет её до запроса.
/// </summary>
public sealed class DeferredUpdateCheckTests
{
    [Fact]
    public async Task StartupDelayDoesNotCallTheServiceImmediately()
    {
        var service = new StubUpdateService();
        using var check = new DeferredUpdateCheck(service);
        using var observer = new CancellationTokenSource();

        var pending = check.WaitAsync(observer.Token);

        Assert.False(pending.IsCompleted);
        Assert.Equal(0, service.CheckCount);
        await observer.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task MultipleWindowsShareOneCheckAndItsResult()
    {
        var service = new StubUpdateService();
        using var check = new DeferredUpdateCheck(service, TimeSpan.FromMilliseconds(20));

        var first = check.WaitAsync(CancellationToken.None);
        var second = check.WaitAsync(CancellationToken.None);
        var results = await Task.WhenAll(first, second);

        Assert.Same(results[0], results[1]);
        Assert.Same(results[0], await check.WaitAsync(CancellationToken.None));
        Assert.Equal(1, service.CheckCount);
    }

    /// <summary>
    /// Проверка висит, пока тест не завершит её сам: с одной паузой в 20 мс медленная
    /// машина успевала закончить проверку раньше, чем окно закрывалось.
    /// </summary>
    [Fact]
    public async Task ClosingOneWindowDoesNotCancelAnotherObserver()
    {
        var service = new StubUpdateService { PendingCheck = new TaskCompletionSource<UpdateCheckResult>() };
        using var check = new DeferredUpdateCheck(service, TimeSpan.FromMilliseconds(1));
        using var observer = new CancellationTokenSource();
        var closed = check.WaitAsync(observer.Token);
        var remaining = check.WaitAsync(CancellationToken.None);

        await observer.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closed);
        Assert.False(remaining.IsCompleted);
        service.PendingCheck.SetResult(service.NextCheckResult);
        Assert.IsType<UpdateCheckResult.SourceNotConfigured>(await remaining);
        Assert.Equal(1, service.CheckCount);
    }

    [Fact]
    public async Task ShutdownCancelsPendingDiscoveryBeforeTheNetworkRequest()
    {
        var service = new StubUpdateService();
        var check = new DeferredUpdateCheck(service);
        var pending = check.WaitAsync(CancellationToken.None);

        check.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, service.CheckCount);
    }

    /// <summary>Сбой сервиса не роняет проверку: он приходит результатом «не удалось».</summary>
    [Fact]
    public async Task ServiceFailureBecomesAFailedResult()
    {
        using var check = new DeferredUpdateCheck(new ThrowingUpdateService(), TimeSpan.FromMilliseconds(1));

        var result = await check.WaitAsync(CancellationToken.None);

        Assert.IsType<UpdateCheckResult.Failed>(result);
    }

    private sealed class ThrowingUpdateService : MarkMello.Application.Abstractions.IUpdateService
    {
        public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
            => throw new HttpRequestException("offline");

        public Task<UpdateDownloadResult> DownloadUpdateAsync(
            AppUpdatePackage package,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UpdatePrepareResult> PrepareDownloadedUpdateAsync(
            AppUpdatePackage package,
            string downloadedFilePath,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
