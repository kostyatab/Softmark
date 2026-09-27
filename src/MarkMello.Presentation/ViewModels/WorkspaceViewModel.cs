using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Application.UseCases;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Открытая папка: корень, дерево и выбор файла. Создаётся только по команде
/// «Открыть папку» — при старте с одним файлом ничего из этого не инстанцируется
/// (ADR-0007 Rule 1). Поиск и файловые операции появятся в M3.
/// </summary>
public sealed partial class WorkspaceViewModel : ObservableObject
{
    private readonly ExpandFolderNodeUseCase _expandFolderNode;
    private readonly SearchWorkspaceFilesUseCase _searchWorkspaceFiles;
    private readonly WorkspaceFileOperationsUseCase _fileOperations;
    private readonly ILocalizationService _localization;
    private readonly Func<string, Task> _openDocumentAsync;
    private readonly Func<FileTreeNodeViewModel, Task> _deleteRequested;
    private readonly Action<string, string> _pathChanged;
    private readonly Func<bool> _areFileOperationsBlocked;

    private WorkspaceViewModel(
        WorkspaceFolder folder,
        IReadOnlyList<WorkspaceEntry> rootEntries,
        WorkspaceDependencies dependencies)
    {
        Folder = folder;
        _expandFolderNode = dependencies.ExpandFolderNode;
        _searchWorkspaceFiles = dependencies.SearchWorkspaceFiles;
        _fileOperations = dependencies.FileOperations;
        _localization = dependencies.Localization;
        _openDocumentAsync = dependencies.OpenDocumentAsync;
        _deleteRequested = dependencies.DeleteRequested;
        _pathChanged = dependencies.PathChanged;
        _areFileOperationsBlocked = dependencies.AreFileOperationsBlocked;

        foreach (var node in CreateNodes(rootEntries, depth: 0))
        {
            Roots.Add(node);
        }

        UpdateHasNoDocuments();
    }

    /// <summary>
    /// Собирает workspace из уже прочитанного корневого уровня. Чтение делает use case,
    /// view-model только раскладывает результат — так дерево остаётся тестируемым без диска.
    /// </summary>
    public static WorkspaceViewModel FromOpenedFolder(
        OpenFolderResult.Success result,
        WorkspaceDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(dependencies);

        return new WorkspaceViewModel(result.Folder, result.Children, dependencies);
    }

    /// <summary>
    /// Всё, что нужно открытой папке: use cases и обратные вызовы shell.
    /// Свёрнуто в один тип, чтобы конструктор не превращался в список из восьми аргументов.
    /// <see cref="AreFileOperationsBlocked"/> — shell ждёт ответа в модальном диалоге:
    /// создание, переименование и дублирование в это время не начинаются.
    /// </summary>
    public sealed record WorkspaceDependencies(
        ExpandFolderNodeUseCase ExpandFolderNode,
        SearchWorkspaceFilesUseCase SearchWorkspaceFiles,
        WorkspaceFileOperationsUseCase FileOperations,
        ILocalizationService Localization,
        Func<string, Task> OpenDocumentAsync,
        Func<FileTreeNodeViewModel, Task> DeleteRequested,
        Action<string, string> PathChanged,
        Func<bool> AreFileOperationsBlocked);

    public WorkspaceFolder Folder { get; }

    public string RootDisplayName => Folder.DisplayName;

    public ObservableCollection<FileTreeNodeViewModel> Roots { get; } = [];

    [ObservableProperty]
    private FileTreeNodeViewModel? _selectedNode;

    /// <summary>Путь документа, открытого в окне. Подсвечивает строку дерева акцентной планкой.</summary>
    [ObservableProperty]
    private string? _activeDocumentPath;

    /// <summary>
    /// В папке точно нет ни одного документа — пустой экран предлагает создать первый,
    /// а не выбрать файл в дереве (A-EmptyFolder). «Точно» — по уже прочитанному дереву:
    /// нераскрытый каталог может что-то хранить, а обходить его ради этого флага нельзя
    /// (ADR-0007 Rule 5), поэтому с ним флаг остаётся снятым.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsNoDocumentsNote))]
    private bool _hasNoDocuments;

    /// <summary>
    /// Строка «Здесь нет файлов .md» встаёт на место поиска: искать в такой папке нечего.
    /// Начатый поиск она не прячет — иначе запрос остался бы без поля, где его сбросить.
    /// </summary>
    public bool ShowsNoDocumentsNote => HasNoDocuments && !HasSearchQuery;


    /// <summary>Первый `README.md` в корне: с него открывается папка, если он есть.</summary>
    public string? TryGetRootReadmePath()
        => Roots
            .FirstOrDefault(node =>
                !node.IsDirectory
                && node.IsSupportedDocument
                && string.Equals(node.Name, "README.md", StringComparison.OrdinalIgnoreCase))
            ?.Path;

    partial void OnActiveDocumentPathChanged(string? value)
    {
        foreach (var node in EnumerateLoadedNodes())
        {
            node.IsActiveDocument = !node.IsDirectory
                && !string.IsNullOrEmpty(value)
                && PathsEqual(node.Path, value);
        }
    }

    /// <summary>
    /// Читает детей узла. Публичен, потому что раскрытие бывает нужно дождаться:
    /// из биндинга <c>IsExpanded</c> вызов идёт fire-and-forget, а тестам
    /// и диагностическому замеру нужен именно завершённый Task.
    /// </summary>
    public async Task ExpandNodeAsync(FileTreeNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.HasLoadedChildren || node.IsLoadingChildren)
        {
            return;
        }

        node.IsLoadingChildren = true;
        try
        {
            // Присваивание поднимет IsExpanded в UI; вложенный вызов из сеттера
            // упрётся в IsLoadingChildren и каталог не будет прочитан дважды.
            node.IsExpanded = true;

            var result = await _expandFolderNode.ExecuteAsync(node.Path).ConfigureAwait(true);

            switch (result)
            {
                case ExpandFolderNodeResult.Success success:
                    node.ReplaceChildren(CreateNodes(success.Children, node.Depth + 1));
                    ApplyActiveDocumentHighlight(node);
                    break;

                case ExpandFolderNodeResult.NotFound:
                    node.FailChildrenLoad(_localization["TreeNodeMissing"]);
                    break;

                case ExpandFolderNodeResult.AccessDenied:
                    node.FailChildrenLoad(_localization["TreeNodeAccessDenied"]);
                    break;

                case ExpandFolderNodeResult.ReadError:
                    node.FailChildrenLoad(_localization["TreeNodeReadError"]);
                    break;
            }
        }
        finally
        {
            node.IsLoadingChildren = false;
            UpdateHasNoDocuments();
        }
    }

    /// <summary>
    /// Сколько составных операций сейчас перестраивают дерево. Пока идёт хотя бы одна,
    /// флаг не пересчитывается: перечитанный каталог на время остаётся с непрочитанными
    /// подпапками, и экран мигал бы между «выберите файл» и «создайте первый».
    /// </summary>
    private int _documentPresenceHolds;

    /// <summary>
    /// Пересчитывается после каждого чтения каталога: создание файла, правка снаружи
    /// и раскрытие узла сходятся на нём, так что экран меняется без перезапуска.
    /// Каталог с ошибкой чтения тоже считается непрочитанным: что в нём, неизвестно.
    /// </summary>
    private void UpdateHasNoDocuments()
    {
        if (_documentPresenceHolds > 0)
        {
            return;
        }

        HasNoDocuments = !EnumerateLoadedNodes().Any(static node =>
            node.IsSupportedDocument
            || (node.IsDirectory && (!node.HasLoadedChildren || node.HasLoadError)));
    }

    /// <summary>
    /// Выполняет перестройку дерева целиком — перечитать, вернуть раскрытые папки,
    /// показать созданный элемент — и пересчитывает флаг один раз, по итогу.
    /// </summary>
    private async Task HoldingDocumentPresenceAsync(Func<Task> rebuild)
    {
        _documentPresenceHolds++;
        try
        {
            await rebuild().ConfigureAwait(true);
        }
        finally
        {
            _documentPresenceHolds--;
            UpdateHasNoDocuments();
        }
    }

    private IEnumerable<FileTreeNodeViewModel> CreateNodes(IReadOnlyList<WorkspaceEntry> entries, int depth)
        => entries.Select(entry => CreateNode(entry, depth));

    private FileTreeNodeViewModel CreateNode(WorkspaceEntry entry, int depth)
    {
        var node = new FileTreeNodeViewModel(entry, depth, ExpandNodeAsync);
        node.ExpansionChanged += OnNodeExpansionChanged;
        return node;
    }

    /// <summary>
    /// Счётчик раскрытий. Сам список раскрытых узлов живёт в дереве, а shell нужен
    /// повод переписать сессию: без него состав раскрытых папок сохранялся только
    /// при следующей смене вкладок.
    /// </summary>
    [ObservableProperty]
    private int _expansionRevision;

    private void OnNodeExpansionChanged(object? sender, EventArgs e) => ExpansionRevision++;

    private void ApplyActiveDocumentHighlight(FileTreeNodeViewModel parent)
    {
        if (string.IsNullOrEmpty(ActiveDocumentPath))
        {
            return;
        }

        foreach (var node in parent.Children)
        {
            node.IsActiveDocument = !node.IsDirectory && PathsEqual(node.Path, ActiveDocumentPath);
        }
    }

    /// <summary>
    /// Отмечает строки, у которых есть несохранённые правки. Пути приходят из shell:
    /// про вкладки знает он, про строки дерева — эта view-model.
    /// </summary>
    public void ApplyDirtyPaths(IReadOnlyCollection<string> dirtyPaths)
    {
        ArgumentNullException.ThrowIfNull(dirtyPaths);

        foreach (var node in EnumerateLoadedNodes())
        {
            node.IsDirty = !node.IsDirectory
                && dirtyPaths.Any(path => PathsEqual(node.Path, path));
        }
    }

    /// <summary>Раскрытые каталоги — часть состояния сессии окна.</summary>
    public IReadOnlyList<string> GetExpandedDirectories()
        => EnumerateLoadedNodes()
            .Where(static node => node is { IsDirectory: true, IsExpanded: true })
            .Select(static node => node.Path)
            .ToList();

    private IEnumerable<FileTreeNodeViewModel> EnumerateLoadedNodes()
        => Roots.SelectMany(static root => root.EnumerateLoadedNodes());

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
