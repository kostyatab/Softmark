using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.UseCases;
using MarkMello.Domain.Recent;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// «Недавние» на стартовом экране (ADR-0009 Rule 8). Пишутся только явные открытия:
/// диалог «Открыть», файл от ОС и из командной строки, перетаскивание, «Сохранить как»,
/// сами «Недавние» и любая папка. Файлы из дерева, README папки, неудачные открытия и
/// черновики без пути не пишутся.
///
/// Список и проверка записей на диске поднимаются, только когда стартовый экран
/// действительно показан после инициализации окна: запуск с файлом их не трогает.
/// </summary>
public partial class ShellViewModel
{
    private bool _isStartupActivationComplete;
    private bool _isRecentListShown;
    private CancellationTokenSource? _recentProbeCancellation;
    private RecentItemViewModel? _recentRemoveTarget;
    private IReadOnlyList<string> _missingRecentPaths = [];
    private bool _isRecentProbeStarted;

    public ObservableCollection<RecentItemViewModel> RecentItems { get; } = [];

    [ObservableProperty]
    private bool _hasRecentItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecentRemovePromptContent))]
    private bool _isRecentRemovePromptOpen;

    /// <summary>Карточка «Убрать из „Недавних“» строится в момент вопроса, а не живёт скрытой.</summary>
    public object? RecentRemovePromptContent => IsRecentRemovePromptOpen ? this : null;

    [ObservableProperty]
    private string _recentRemovePromptTitle = string.Empty;

    [ObservableProperty]
    private string _recentRemovePromptMessage = string.Empty;

    /// <summary>Порядок кнопок — как у платформы, в тех же колонках, что и у удаления.</summary>
    public int RecentRemoveCancelColumn => UsesMacOSDialogOrder ? 3 : 4;

    public int RecentRemoveConfirmColumn => UsesMacOSDialogOrder ? 4 : 3;

    /// <summary>
    /// Стартовый экран в окне. До конца стартовой активации его нет: у окна, которое
    /// сейчас откроет файл из Finder или командной строки, <see cref="IsWelcome"/> пока
    /// истинно, но строить ради одного кадра экран с «Недавними» незачем.
    /// </summary>
    public object? WelcomeContent => _isStartupActivationComplete && IsWelcome ? this : null;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsWelcome))
        {
            OnPropertyChanged(nameof(WelcomeContent));
            SyncRecentListWithWelcome();
        }
        else if (e.PropertyName == nameof(IsEmptyDocumentSurface))
        {
            OnPropertyChanged(nameof(EmptyDocumentSurfaceContent));
        }
    }

    /// <summary>
    /// Окно закончило стартовую активацию. Только теперь видно, остаётся ли оно на стартовом
    /// экране: до этого <see cref="IsWelcome"/> истинно и у окна, которое сейчас откроет файл.
    /// </summary>
    private void CompleteStartupActivation()
    {
        _isStartupActivationComplete = true;
        OnPropertyChanged(nameof(WelcomeContent));
        SyncRecentListWithWelcome();
    }

    private void SyncRecentListWithWelcome()
    {
        if (!_isStartupActivationComplete || _recentItems is null)
        {
            return;
        }

        if (IsWelcome == _isRecentListShown)
        {
            return;
        }

        _isRecentListShown = IsWelcome;
        if (_isRecentListShown)
        {
            // Экран показан заново — прежний ответ диска устарел, проверка пойдёт снова.
            _missingRecentPaths = [];
            _isRecentProbeStarted = false;
            _ = RebuildRecentRowsAsync();
        }
        else
        {
            CancelRecentProbe();
        }
    }

    /// <summary>
    /// Строит строки из списка в памяти. Диск здесь не трогается: известные пропавшие
    /// записи приглушаются по прошлому ответу проверки.
    /// </summary>
    private async Task RebuildRecentRowsAsync()
    {
        if (_recentItems is not { } recentItems)
        {
            return;
        }

        var entries = await recentItems.GetEntriesAsync().ConfigureAwait(true);
        if (!_isRecentListShown)
        {
            return;
        }

        var now = recentItems.LocalNow;
        RecentItems.Clear();
        foreach (var entry in entries)
        {
            var item = CreateRecentItem(entry, now);
            item.IsMissing = _missingRecentPaths.Any(path => PathsMatch(path, item.Path));
            RecentItems.Add(item);
        }

        HasRecentItems = RecentItems.Count > 0;
    }

    /// <summary>
    /// Стартовый экран отрисован: теперь можно спросить диск, на месте ли записи. Вызывает
    /// вид после первого кадра, чтобы проверка не делила старт с отрисовкой (ADR-0009 Rule 8).
    /// Проверка идёт вне UI-потока; пропавшие записи приглушаются, когда она ответит.
    /// </summary>
    public void ProbeRecentItems()
    {
        if (!_isRecentListShown || _isRecentProbeStarted || _recentItems is null)
        {
            return;
        }

        _isRecentProbeStarted = true;
        _ = ProbeRecentItemsAsync(_recentItems);
    }

    private async Task ProbeRecentItemsAsync(RecentItemsUseCase recentItems)
    {
        CancelRecentProbe();
        var cancellation = new CancellationTokenSource();
        _recentProbeCancellation = cancellation;

        try
        {
            var entries = await recentItems.GetEntriesAsync(cancellation.Token).ConfigureAwait(true);
            if (entries.Count == 0)
            {
                return;
            }

            var missing = await recentItems.FindMissingAsync(entries, cancellation.Token).ConfigureAwait(true);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            _missingRecentPaths = missing;
            foreach (var item in RecentItems)
            {
                item.IsMissing = missing.Any(path => PathsMatch(path, item.Path));
            }
        }
        catch (OperationCanceledException)
        {
            // Стартовый экран скрылся раньше, чем ответил диск.
        }
        finally
        {
            if (ReferenceEquals(_recentProbeCancellation, cancellation))
            {
                _recentProbeCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelRecentProbe()
    {
        _recentProbeCancellation?.Cancel();
        _recentProbeCancellation = null;
    }

    private RecentItemViewModel CreateRecentItem(RecentEntry entry, DateTimeOffset now)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(entry.Path);
        var name = Path.GetFileName(trimmed);
        var directory = Path.GetDirectoryName(trimmed);

        return new RecentItemViewModel(
            entry,
            string.IsNullOrEmpty(name) ? entry.Path : name,
            string.IsNullOrEmpty(directory) ? string.Empty : AbbreviateHomePath(directory),
            FormatRecentOpened(entry.OpenedAt, now),
            OpenRecentItemAsync);
    }

    /// <summary>«сегодня», «вчера», иначе дата по языку интерфейса; год — только не текущий.</summary>
    private string FormatRecentOpened(DateTimeOffset openedAt, DateTimeOffset now)
    {
        var opened = openedAt.ToOffset(now.Offset).Date;
        var today = now.Date;

        if (opened == today)
        {
            return _localization["RecentToday"];
        }

        if (opened == today.AddDays(-1))
        {
            return _localization["RecentYesterday"];
        }

        var format = opened.Year == today.Year ? "RecentDateFormat" : "RecentDateWithYearFormat";
        return opened.ToString(_localization[format], _localization.Culture);
    }

    /// <summary>Клик или Enter по строке: файл открывается, как «Открыть файл», папка — в этом окне.</summary>
    internal async Task OpenRecentItemAsync(RecentItemViewModel? item)
    {
        if (item is null || IsModalDialogOpen)
        {
            return;
        }

        CloseOverlayCore();

        if (item.IsMissing)
        {
            ShowRecentRemovePrompt(item);
            return;
        }

        if (item.IsFolder)
        {
            await OpenFolderPathAsync(item.Path).ConfigureAwait(true);
        }
        else
        {
            await OpenAndRememberFileAsync(item.Path).ConfigureAwait(true);
        }
    }

    /// <summary>«Очистить» — без подтверждения: список восстанавливается сам по мере открытий.</summary>
    [RelayCommand]
    private async Task ClearRecentItemsAsync()
    {
        if (_recentItems is not { } recentItems)
        {
            return;
        }

        CancelRecentProbe();
        await recentItems.ClearAsync().ConfigureAwait(true);
        RecentItems.Clear();
        HasRecentItems = false;
    }

    private void ShowRecentRemovePrompt(RecentItemViewModel item)
    {
        _recentRemoveTarget = item;
        RecentRemovePromptTitle = _localization[item.IsFolder ? "RecentFolderNotFoundTitle" : "RecentFileNotFoundTitle"];
        RecentRemovePromptMessage = _localization.Format("RecentRemoveBody", item.Name);
        IsRecentRemovePromptOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmRecentRemoveAsync()
    {
        var target = _recentRemoveTarget;
        CloseRecentRemovePrompt();

        if (target is null || _recentItems is not { } recentItems)
        {
            return;
        }

        await recentItems.RemoveAsync(target.Path).ConfigureAwait(true);
        RecentItems.Remove(target);
        HasRecentItems = RecentItems.Count > 0;
    }

    [RelayCommand]
    private void CancelRecentRemove() => CloseRecentRemovePrompt();

    private void CloseRecentRemovePrompt()
    {
        IsRecentRemovePromptOpen = false;
        _recentRemoveTarget = null;

        // Файлы, которые ОС прислала, пока шёл вопрос, открываются после ответа.
        _ = OpenDeferredActivationsAsync();
    }

    /// <summary>Явное открытие файла: удачное попадает в «Недавние», неудачное — нет.</summary>
    private async Task OpenAndRememberFileAsync(string path)
    {
        if (await OpenDocumentInTabAsync(path).ConfigureAwait(true))
        {
            RememberRecentFile(path);
        }
    }

    private void RememberRecentFile(string? path)
    {
        if (_recentItems is { } recentItems && !string.IsNullOrEmpty(path))
        {
            _ = recentItems.RecordFileAsync(path);
        }
    }

    private void RememberRecentFolder(string path)
    {
        if (_recentItems is { } recentItems)
        {
            _ = recentItems.RecordFolderAsync(path);
        }
    }
}
