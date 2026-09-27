using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Domain;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Вкладки открытых документов. Активная вкладка — это то, что видно в document surface:
/// её содержимое живёт в полях shell, а остальные вкладки держат снимок своего документа
/// и позицию прокрутки, чтобы возврат не перечитывал файл (ADR-0007 Rule 4).
/// </summary>
public partial class ShellViewModel
{
    private bool _isRestoringTab;
    private double? _pendingScrollOffset;

    public OpenDocumentsViewModel OpenDocuments { get; private set; } = default!;

    /// <summary>
    /// Вкладки и «+» после них (ADR-0009 Rule 3): есть, когда есть документ — и в папке,
    /// и без неё. В папке без открытых вкладок остаётся одна «+», на стартовом экране
    /// нет ни того, ни другого.
    /// </summary>
    public bool ShowsTabStrip => OpenDocuments.HasTabs || Workspace is not null;

    /// <summary>Полоса вкладок создаётся с первой вкладкой или папкой, а не висит скрытой с запуска.</summary>
    public object? TabStripContent => ShowsTabStrip ? this : null;

    /// <summary>
    /// Папка открыта, но документ не выбран. Отдельно от welcome: там предлагают открыть файл,
    /// здесь — выбрать его в дереве слева (макет 07).
    /// </summary>
    public bool IsEmptyDocumentSurface => State == ViewState.NoDocument && ShowsSidebar;

    /// <summary>
    /// Пустой экран папки строится только в папке: с кнопками A-EmptyFolder он уже не две
    /// строки текста, и держать его скрытым с запуска незачем (ADR-0009 Rule 12).
    /// </summary>
    public object? EmptyDocumentSurfaceContent => IsEmptyDocumentSurface ? this : null;

    private void InitializeOpenDocuments()
    {
        OpenDocuments = new OpenDocumentsViewModel(ActivateTabAsync, CloseTabAsync);

        // Подпись «ещё N» собирается здесь, а число вкладок в переполнении считает
        // OpenDocuments: без подписки кнопка появлялась с текстом «0 more».
        OpenDocuments.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(OpenDocumentsViewModel.OverflowCountLabel))
            {
                OnPropertyChanged(nameof(TabsOverflowLabel));
            }

            // Последнюю скрытую вкладку закрыли ✕ или окно стало шире — список пуст,
            // а кнопка, под которой он висел, пропала: меню закрывается само.
            if (e.PropertyName is nameof(OpenDocumentsViewModel.HasOverflow)
                && !OpenDocuments.HasOverflow
                && IsTabsOverflowMenuOpen)
            {
                ShellOverlay = ShellOverlayKind.None;
            }
        };
    }

    /// <summary>
    /// Позиция прокрутки приходит из вьюера на каждое изменение: держать её в вкладке
    /// дешевле, чем спрашивать вьюер в момент переключения — его может уже не быть.
    /// </summary>
    public void ReportScrollOffset(double offset)
    {
        if (_isRestoringTab || OpenDocuments.ActiveTab is not { } tab)
        {
            return;
        }

        tab.ScrollOffset = offset;
    }

    /// <summary>Возвращает отложенную позицию ровно один раз — после того, как документ отрисован.</summary>
    public double? TakePendingScrollOffset()
    {
        var offset = _pendingScrollOffset;
        _pendingScrollOffset = null;
        return offset;
    }

    [RelayCommand]
    private Task ActivateNextTabAsync()
    {
        CloseAppSettingsWindow();
        return ActivateNeighbourAsync(1);
    }

    [RelayCommand]
    private Task ActivatePreviousTabAsync()
    {
        CloseAppSettingsWindow();
        return ActivateNeighbourAsync(-1);
    }

    [RelayCommand(CanExecute = nameof(CanCloseActiveTab))]
    private Task CloseActiveTabAsync()
    {
        CloseAppSettingsWindow();
        return OpenDocuments.ActiveTab is { } tab ? CloseTabAsync(tab) : Task.CompletedTask;
    }

    private bool CanCloseActiveTab() => OpenDocuments.ActiveTab is not null;

    private Task ActivateNeighbourAsync(int direction)
    {
        var neighbour = OpenDocuments.GetNeighbour(direction);
        return neighbour is null || ReferenceEquals(neighbour, OpenDocuments.ActiveTab)
            ? Task.CompletedTask
            : ActivateTabAsync(neighbour);
    }

    /// <summary>
    /// Переключение на другую вкладку. Документ не перечитывается с диска — берётся снимок,
    /// поэтому переключение стоит столько же, сколько перерисовка. Сюда сходятся клик,
    /// «ещё N» и Ctrl+Tab; под диалогом о правках вкладка не меняется — он спрашивает
    /// о текущей.
    /// </summary>
    private async Task ActivateTabAsync(DocumentTabViewModel tab)
    {
        if (IsModalDialogOpen || ReferenceEquals(OpenDocuments.ActiveTab, tab))
        {
            return;
        }

        // Диалог тут не нужен: правки остаются в своей вкладке и никуда не деваются,
        // спросим о них при закрытии вкладки или окна.
        await RestoreTabAsync(tab).ConfigureAwait(true);
    }

    private async Task RestoreTabAsync(DocumentTabViewModel tab)
    {
        if (tab.LoadError is { } error)
        {
            ShowLoadErrorTab(tab, error);
            return;
        }

        _isRestoringTab = true;
        try
        {
            OpenDocuments.Activate(tab);

            // Сессия и режим правки принадлежат вкладке: возвращаемся ровно в то состояние,
            // в котором её оставили, вместе с несохранённым текстом.
            EditorSession = tab.EditorSession;
            IsEditMode = tab.IsEditMode && tab.EditorSession is not null;
            Document = tab.Document;
            RenderedDocument = tab.RenderedDocument;
            _currentPath = tab.Path;
            State = ViewState.Viewing;
            ClearLoadError();

            _pendingScrollOffset = tab.ScrollOffset;
            ReadingProgress = 0;

            SyncWorkspaceActiveDocument();
            RefreshWindowTitle();
            UpdateCommandStates();
            UpdateTabCommandStates();
            SyncExternalChangeBanner();
        }
        finally
        {
            _isRestoringTab = false;
        }

        // Файл поменялся, пока вкладка была в фоне: показываем актуальное содержимое, а не снимок.
        // Перечитываем после восстановления: shell уже отпустил сессию прошлой вкладки, и если
        // файл не прочтётся, ошибкой станет эта вкладка, а не чужая правка.
        if (tab is { NeedsReload: true, Path: { } stalePath, EditorSession: null })
        {
            tab.NeedsReload = false;
            var offset = tab.ScrollOffset;
            await LoadDocumentAsync(stalePath, preserveEditModeAfterLoad: false).ConfigureAwait(true);
            _pendingScrollOffset = offset;
        }
    }

    /// <summary>
    /// Показывает уже открытую вкладку, ничего в ней не перечитывая. Активная остаётся как
    /// есть — только уходит экран ошибки, если его поверх неё оставило неудачное открытие.
    /// </summary>
    private async Task ShowTabAsync(DocumentTabViewModel tab)
    {
        if (!ReferenceEquals(OpenDocuments.ActiveTab, tab))
        {
            await RestoreTabAsync(tab).ConfigureAwait(true);
            return;
        }

        if (State == ViewState.LoadError)
        {
            State = ViewState.Viewing;
            ClearLoadError();
            RefreshWindowTitle();
        }
    }

    /// <summary>
    /// «Повторить» на экране ошибки: вкладка ошибки перечитывает свой путь и при успехе
    /// показывает документ в себе же; ошибка поверх вкладки с правками — как F5.
    /// </summary>
    [RelayCommand]
    private async Task RetryLoadAsync()
    {
        if (OpenDocuments.ActiveTab is { IsLoadError: true, Path: { } path })
        {
            await LoadDocumentAsync(path, preserveEditModeAfterLoad: false).ConfigureAwait(true);
            return;
        }

        await ReloadAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Закрытие вкладки. Грязная вкладка проходит через существующий диалог
    /// «Сохранить / Не сохранять / Отмена», причём «Отмена» оставляет вкладку на месте.
    /// Под уже открытым диалогом не закрывается ни одна вкладка (×, Ctrl+W): грязную
    /// пришлось бы показать вместо спрошенной, а чистая пропала бы у пользователя из-под рук.
    /// </summary>
    private async Task CloseTabAsync(DocumentTabViewModel tab)
    {
        if (IsModalDialogOpen)
        {
            return;
        }

        if (tab.EditorSession?.IsDirty == true)
        {
            // Диалог работает с активной сессией, поэтому сначала показываем пользователю
            // ту вкладку, о правках которой спрашиваем.
            if (!ReferenceEquals(OpenDocuments.ActiveTab, tab))
            {
                await RestoreTabAsync(tab).ConfigureAwait(true);
            }

            await RunWithDirtyCheckAsync(PendingDirtyActionKind.CloseFile, () => RemoveTabAsync(tab))
                .ConfigureAwait(true);
            return;
        }

        await RemoveTabAsync(tab).ConfigureAwait(true);
    }

    private async Task RemoveTabAsync(DocumentTabViewModel tab)
    {
        var wasActive = ReferenceEquals(OpenDocuments.ActiveTab, tab);
        var session = tab.EditorSession;

        // Вкладка ошибки закрывается туда, откуда пользователь пытался открыть файл.
        var returnTab = tab.ReturnTab is { } candidate && OpenDocuments.Tabs.Contains(candidate)
            ? candidate
            : null;

        if (wasActive && ReferenceEquals(EditorSession, session))
        {
            // Снимаем сессию с shell до удаления вкладки, иначе она останется подписанной.
            // После Remove активной станет соседняя вкладка, и синхронизация записала бы в неё.
            IsEditMode = false;
            EditorSession = null;
        }

        OpenDocuments.Remove(tab);

        // Shell не выбросил сессию — вкладка ещё была в списке, — а синхронизация отвязала её
        // от активной вкладки, так что tab.Dispose() её уже не увидит. Фоновая вкладка держит
        // сессию до конца и гасит её сама: в обоих случаях сессия гасится ровно один раз.
        if (session is not null && !ReferenceEquals(tab.EditorSession, session))
        {
            session.Dispose();
        }

        tab.Dispose();

        if (!wasActive)
        {
            RefreshTabState();
            return;
        }

        if ((returnTab ?? OpenDocuments.ActiveTab) is { } next)
        {
            // Remove уже перевёл активность на соседа — восстанавливаем его содержимое.
            OpenDocuments.Activate(null);
            await RestoreTabAsync(next).ConfigureAwait(true);
            RefreshTabState();
            return;
        }

        ClearDocumentSurface();
        RefreshTabState();
    }

    /// <summary>Последняя вкладка закрыта: в folder mode остаётся пустое состояние, иначе welcome.</summary>
    private void ClearDocumentSurface()
    {
        IsEditMode = false;
        EditorSession = null;
        Document = null;
        RenderedDocument = RenderedMarkdownDocument.Empty;
        _currentPath = null;
        State = ViewState.NoDocument;
        ReadingProgress = 0;
        ClearLoadError();
        SyncWorkspaceActiveDocument();
        RefreshWindowTitle();
        UpdateCommandStates();
    }

    /// <summary>Заводит вкладку под загруженный документ или обновляет уже открытую.</summary>
    private void TrackLoadedDocumentTab(MarkdownSource source, RenderedMarkdownDocument rendered)
    {
        var tab = OpenDocuments.FindByPath(source.Path);
        if (tab is null)
        {
            tab = OpenDocuments.Add(new DocumentTabViewModel(source.Path, source.FileName));
        }

        tab.ApplyDocument(source, rendered);
        ApplyWorkspaceMembership(tab);
        tab.IsDirty = false;

        OpenDocuments.Activate(tab);
        OpenDocuments.Refresh();
        RefreshTabState();
    }

    /// <summary>Новый несохранённый документ тоже занимает вкладку — просто без пути.</summary>
    private void TrackNewDocumentTab()
    {
        var tab = OpenDocuments.Add(new DocumentTabViewModel(null, _localization["UntitledFileName"]));
        tab.ApplyDocument(null, RenderedMarkdownDocument.Empty);
        tab.Tooltip = _localization["UntitledFileName"];

        OpenDocuments.Activate(tab);
        OpenDocuments.Refresh();
        RefreshTabState();
    }

    /// <summary>«Сохранить как» уводит вкладку на новый путь, вместе с заголовком и тултипом.</summary>
    private void RetargetActiveTab(MarkdownSource source)
    {
        if (OpenDocuments.ActiveTab is not { } tab)
        {
            return;
        }

        tab.Retarget(source.Path, source.FileName);
        tab.ApplyDocument(source, RenderedDocument);
        ApplyWorkspaceMembership(tab);
        OpenDocuments.Refresh();
        RefreshTabState();
    }

    /// <summary>
    /// Папку открыли поверх уже открытых вкладок: те, что лежат внутри неё, становятся
    /// её вкладками так же, как открытые из дерева, — с точкой несохранённого в дереве
    /// и местом в счётчике подвала.
    /// </summary>
    private void AdoptOpenTabsIntoWorkspace()
    {
        foreach (var tab in OpenDocuments.Tabs.Where(static tab => tab.Path is not null))
        {
            ApplyWorkspaceMembership(tab);
        }

        SyncWorkspaceDirtyMarks();
    }

    /// <summary>Принадлежность папке и тултип считаются по пути вкладки, а не по тому, откуда её открыли.</summary>
    private void ApplyWorkspaceMembership(DocumentTabViewModel tab)
    {
        tab.Tooltip = BuildTabTooltip(tab.Path);
        tab.BelongsToWorkspace = Workspace is { } workspace && IsInsideWorkspace(tab.Path, workspace.Folder);
    }

    private void SyncActiveTabDirtyState()
    {
        if (OpenDocuments.ActiveTab is { } tab)
        {
            tab.IsDirty = IsDirty;
        }

        SyncWorkspaceDirtyMarks();
    }

    /// <summary>Точки несохранённого в дереве повторяют точки на вкладках.</summary>
    private void SyncWorkspaceDirtyMarks()
    {
        if (Workspace is not { } workspace)
        {
            return;
        }

        var dirtyPaths = OpenDocuments.Tabs
            .Where(static tab => tab.IsDirty)
            .Select(static tab => tab.Path)
            .OfType<string>()
            .ToList();

        workspace.ApplyDirtyPaths(dirtyPaths);
    }

    /// <summary>
    /// Сессия и режим правки, включённые в shell, приписываются активной вкладке.
    /// Во время восстановления вкладки зеркалирование выключено: там поток данных обратный.
    /// </summary>
    private void SyncActiveTabEditorState()
    {
        if (_isRestoringTab || OpenDocuments is null || OpenDocuments.ActiveTab is not { } tab)
        {
            return;
        }

        tab.EditorSession = EditorSession;
        tab.IsEditMode = IsEditMode;
        tab.IsDirty = EditorSession?.IsDirty == true;
    }

    /// <summary>Первая вкладка с несохранёнными правками — с неё начинается закрытие окна.</summary>
    private DocumentTabViewModel? FindFirstDirtyTab()
        => FindFirstDirtyTab(static _ => true);

    private DocumentTabViewModel? FindFirstDirtyTab(Func<DocumentTabViewModel, bool> scope)
        => OpenDocuments.Tabs.FirstOrDefault(tab => tab.EditorSession?.IsDirty == true && scope(tab));

    /// <summary>
    /// Операция над несколькими вкладками сразу (закрытие окна, папки) спрашивает о каждой
    /// грязной по очереди: вкладка показывается, диалог решает её судьбу, и обход повторяется,
    /// пока грязных не останется. Только тогда выполняется сама операция; «Отмена» на любой
    /// вкладке сбрасывает отложенное действие, а с ним и всю операцию (ADR-0007 Rule 11).
    /// </summary>
    private async Task ResolveDirtyTabsThenAsync(
        PendingDirtyActionKind kind,
        Func<DocumentTabViewModel, bool> scope,
        Func<Task> action)
    {
        if (IsModalDialogOpen)
        {
            return;
        }

        if (FindFirstDirtyTab(scope) is not { } dirtyTab)
        {
            await action().ConfigureAwait(true);
            return;
        }

        // Диалог работает с активной сессией, поэтому сначала показываем ту вкладку,
        // о правках которой спрашиваем.
        if (!ReferenceEquals(OpenDocuments.ActiveTab, dirtyTab))
        {
            await RestoreTabAsync(dirtyTab).ConfigureAwait(true);
        }

        QueueDirtyAction(kind, () => ResolveDirtyTabsThenAsync(kind, scope, action));
    }

    private void RefreshTabState()
    {
        OnPropertyChanged(nameof(ShowsTabStrip));
        OnPropertyChanged(nameof(TabStripContent));

        // Последнюю вкладку закрыли ⌘W или закрылась папка — строка с «+» ушла,
        // а его меню висело бы без кнопки: закрывается само, как «ещё N».
        if (!ShowsTabStrip && IsNewTabMenuOpen)
        {
            ShellOverlay = ShellOverlayKind.None;
        }

        OnPropertyChanged(nameof(IsEmptyDocumentSurface));
        UpdateTabCommandStates();

        // Состав вкладок изменился — снимок сессии устарел.
        _ = PersistSessionAsync();
    }

    private void UpdateTabCommandStates()
    {
        CloseActiveTabCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Тултип — полный путь к файлу, домашняя папка сокращается до <c>~</c> (ADR-0009 Rule 3):
    /// имя без пути не отвечает на вопрос «какой из двух README», а путь от корня папки
    /// не отвечает на вопрос «какая это папка».
    /// </summary>
    private string BuildTabTooltip(string? path)
        => string.IsNullOrEmpty(path) ? string.Empty : AbbreviateHomePath(path);

    /// <summary>Домашняя папка в начале пути сокращается до <c>~</c> — в тултипах и «Недавних».</summary>
    private string AbbreviateHomePath(string path)
    {
        var home = Path.TrimEndingDirectorySeparator(_platform.HomeDirectory);
        if (home.Length == 0)
        {
            return path;
        }

        var prefix = home + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, PathComparison)
            ? "~" + path[home.Length..]
            : path;
    }
}
