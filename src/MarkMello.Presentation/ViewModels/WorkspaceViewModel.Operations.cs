using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.UseCases;
using MarkMello.Domain.Workspace;

namespace MarkMello.Presentation.ViewModels;

/// <summary>Что именно вводит пользователь в строке дерева.</summary>
public enum TreeEditKind
{
    None,
    NewFile,
    NewFolder,
    Rename
}

/// <summary>
/// Файловые операции дерева. Ввод имени — инлайн в строке дерева, а не диалог:
/// пользователь видит, куда попадёт элемент после сортировки (макет 09).
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>Куда встанет новый элемент и что переименовывается.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingName))]
    private TreeEditKind _editKind = TreeEditKind.None;

    [ObservableProperty]
    private string _editName = string.Empty;

    /// <summary>Ошибка под полем ввода: занятое имя или запрещённые символы.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditError))]
    private string? _editError;

    [ObservableProperty]
    private FileTreeNodeViewModel? _editingNode;

    /// <summary>Каталог, в котором создаётся элемент.</summary>
    private string _editDirectory = string.Empty;

    /// <summary>Черновая строка и коллекция, из которой её надо убрать по завершении ввода.</summary>
    private FileTreeNodeViewModel? _draftNode;
    private ObservableCollection<FileTreeNodeViewModel>? _draftOwner;

    public bool IsEditingName => EditKind != TreeEditKind.None;

    public bool HasEditError => !string.IsNullOrEmpty(EditError);

    /// <summary>Ошибка последней операции — показывается той же карточкой, что и подтверждение.</summary>
    [ObservableProperty]
    private string? _operationError;

    [RelayCommand]
    private void StartNewFile() => StartCreate(TreeEditKind.NewFile);

    [RelayCommand]
    private void StartNewFolder() => StartCreate(TreeEditKind.NewFolder);

    /// <summary>
    /// Куда вставляется новый элемент: в выделенную папку, рядом с выделенным файлом,
    /// иначе в корень (макет 09).
    /// </summary>
    private void StartCreate(TreeEditKind kind)
    {
        if (_areFileOperationsBlocked())
        {
            return;
        }

        CancelEdit();

        var parent = SelectedNode switch
        {
            { IsDirectory: true } folder => folder,
            { } file => FindLoadedNode(Path.GetDirectoryName(file.Path) ?? Folder.RootPath),
            _ => null
        };

        _editDirectory = parent?.Path ?? Folder.RootPath;
        EditingNode = null;
        EditName = kind == TreeEditKind.NewFile ? ".md" : string.Empty;
        EditError = null;
        EditKind = kind;

        InsertDraftRow(parent, kind);
    }

    /// <summary>
    /// Строка ввода встаёт в дерево на место будущего элемента (макет 09): папки идут
    /// перед файлами, поэтому черновик встаёт в начало своей группы.
    /// </summary>
    private void InsertDraftRow(FileTreeNodeViewModel? parent, TreeEditKind kind)
    {
        if (parent is { IsDirectory: true })
        {
            // В нераскрытую папку класть черновик некуда: сначала читаются её дети.
            parent.IsExpanded = true;
            if (!parent.HasLoadedChildren)
            {
                return;
            }
        }

        var owner = parent is null ? Roots : parent.Children;
        var depth = parent is null ? 0 : parent.Depth + 1;
        var draft = FileTreeNodeViewModel.CreateDraft(_editDirectory, depth);

        var index = kind == TreeEditKind.NewFolder
            ? 0
            : owner.Count(static node => node.IsDirectory);

        owner.Insert(index, draft);
        _draftNode = draft;
        _draftOwner = owner;
    }

    private void RemoveDraftRow()
    {
        if (_draftNode is not null)
        {
            _draftOwner?.Remove(_draftNode);
        }

        _draftNode = null;
        _draftOwner = null;
    }

    [RelayCommand]
    private void StartRename(FileTreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target is null || _areFileOperationsBlocked())
        {
            return;
        }

        CancelEdit();

        _editDirectory = Path.GetDirectoryName(target.Path) ?? Folder.RootPath;
        EditingNode = target;
        EditName = target.Name;
        EditError = null;
        EditKind = TreeEditKind.Rename;
        target.IsEditing = true;
    }

    /// <summary>Esc и потеря фокуса — отмена; ничего не создаётся и не переименовывается.</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        if (EditingNode is { } editing)
        {
            editing.IsEditing = false;
        }

        RemoveDraftRow();

        EditKind = TreeEditKind.None;
        EditingNode = null;
        EditName = string.Empty;
        EditError = null;
    }

    /// <summary>
    /// Ввод, начатый до вопроса shell о правках, под ним не подтверждается и остаётся открытым:
    /// переименование увело бы файл открытой вкладки, а новый файл открылся бы вместо спрошенной.
    /// </summary>
    [RelayCommand]
    private async Task CommitEditAsync()
    {
        if (EditKind == TreeEditKind.None || _areFileOperationsBlocked())
        {
            return;
        }

        // Пустое имя просто ничего не делает: ошибку показывать не за что.
        if (string.IsNullOrWhiteSpace(EditName) || EditName == ".md")
        {
            return;
        }

        var kind = EditKind;
        var node = EditingNode;

        var result = kind switch
        {
            TreeEditKind.NewFile => await _fileOperations.CreateFileAsync(_editDirectory, EditName).ConfigureAwait(true),
            TreeEditKind.NewFolder => await _fileOperations.CreateDirectoryAsync(_editDirectory, EditName).ConfigureAwait(true),
            _ when node is not null => await _fileOperations.RenameAsync(node.Path, EditName).ConfigureAwait(true),
            _ => null
        };

        if (result is null)
        {
            CancelEdit();
            return;
        }

        if (result is WorkspaceMutationResult.Success success)
        {
            var previousPath = node?.Path;
            var directory = _editDirectory;
            CancelEdit();

            // Новая папка после перечитывания ещё не прочитана и раскрывается только показом:
            // флаг пустой папки пересчитывается по итогу, а не между этими шагами.
            await HoldingDocumentPresenceAsync(async () =>
            {
                await RefreshDirectoryAsync(directory).ConfigureAwait(true);

                if (kind == TreeEditKind.Rename && previousPath is not null)
                {
                    // Вкладку переводим до показа узла: выделение открывает документ,
                    // и без этого на тот же файл завелась бы вторая вкладка.
                    _pathChanged(previousPath, success.Entry.Path);
                }

                await RevealAsync(success.Entry.Path).ConfigureAwait(true);
            }).ConfigureAwait(true);

            if (kind == TreeEditKind.NewFile)
            {
                // Созданный файл сразу открывается вкладкой — иначе создание выглядит как «ничего не произошло».
                await _openDocumentAsync(success.Entry.Path).ConfigureAwait(true);
            }

            return;
        }

        // Строка ввода остаётся открытой: пользователь правит имя, а не начинает заново.
        EditError = DescribeFailure(result);
    }

    [RelayCommand]
    private async Task DuplicateAsync(FileTreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target is null || _areFileOperationsBlocked())
        {
            return;
        }

        var result = await _fileOperations.DuplicateAsync(target.Path).ConfigureAwait(true);
        var directory = Path.GetDirectoryName(target.Path) ?? Folder.RootPath;

        if (result is WorkspaceMutationResult.Success success)
        {
            // Копия выделяется в дереве, но вкладку не открывает. Копия папки раскрывается
            // показом — флаг пустой папки пересчитывается уже после этого.
            await HoldingDocumentPresenceAsync(async () =>
            {
                await RefreshDirectoryAsync(directory).ConfigureAwait(true);
                await RevealAsync(success.Entry.Path).ConfigureAwait(true);
            }).ConfigureAwait(true);
            return;
        }

        OperationError = DescribeFailure(result);
    }

    /// <summary>
    /// Открытие строки: левый клик, Enter и пункт «Открыть в новой вкладке».
    /// Каталоги и не-документы инертны: строка выделяется, но ничего не открывается
    /// и сообщения нет (ADR-0007 Rule 6). Повторный клик по уже открытому документу
    /// не перечитывает его с диска.
    /// </summary>
    [RelayCommand]
    private async Task OpenNodeAsync(FileTreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target is null || target.IsDirectory || !target.IsSupportedDocument)
        {
            return;
        }

        if (!string.IsNullOrEmpty(ActiveDocumentPath) && PathsEqual(target.Path, ActiveDocumentPath))
        {
            return;
        }

        await _openDocumentAsync(target.Path).ConfigureAwait(true);
    }

    [RelayCommand]
    private Task RevealAsync(FileTreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        return target is null ? Task.CompletedTask : _fileOperations.RevealAsync(target.Path).AsTask();
    }

    /// <summary>Сама папка в файловом менеджере — пункт меню «имя папки ▾» в сайдбаре.</summary>
    [RelayCommand]
    private Task RevealRootAsync() => _fileOperations.RevealAsync(Folder.RootPath).AsTask();

    [RelayCommand]
    private Task RequestDeleteAsync(FileTreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        return target is null ? Task.CompletedTask : _deleteRequested(target);
    }

    /// <summary>Вызывается shell после подтверждения: сама операция и обновление дерева.</summary>
    public async Task<WorkspaceMutationResult> DeleteConfirmedAsync(FileTreeNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var directory = Path.GetDirectoryName(node.Path) ?? Folder.RootPath;
        var result = await _fileOperations.DeleteAsync(node.Path).ConfigureAwait(true);

        if (result is WorkspaceMutationResult.Deleted)
        {
            await RefreshDirectoryAsync(directory).ConfigureAwait(true);
        }
        else
        {
            OperationError = DescribeFailure(result);
        }

        return result;
    }

    /// <summary>Безвозвратное удаление — только после отдельного подтверждения в shell.</summary>
    public async Task<WorkspaceMutationResult> DeletePermanentlyConfirmedAsync(FileTreeNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var directory = Path.GetDirectoryName(node.Path) ?? Folder.RootPath;
        var result = await _fileOperations.DeletePermanentlyAsync(node.Path).ConfigureAwait(true);

        if (result is WorkspaceMutationResult.Deleted)
        {
            await RefreshDirectoryAsync(directory).ConfigureAwait(true);
        }
        else
        {
            OperationError = DescribeFailure(result);
        }

        return result;
    }

    /// <summary>Перечитывает один каталог после операции — обход остального дерева не нужен.</summary>
    public Task RefreshDirectoryAsync(string directoryPath)
        => HoldingDocumentPresenceAsync(() => RefreshDirectoryCoreAsync(directoryPath));

    private async Task RefreshDirectoryCoreAsync(string directoryPath)
    {
        // Перечитанный каталог строит новые узлы, поэтому раскрытые папки надо запомнить
        // и вернуть: иначе любая правка снаружи сворачивала бы дерево пользователю.
        var expanded = GetExpandedDirectories();

        if (PathsEqual(directoryPath, Folder.RootPath))
        {
            var result = await _expandFolderNode.ExecuteAsync(Folder.RootPath).ConfigureAwait(true);
            if (result is ExpandFolderNodeResult.Success success)
            {
                ReplaceRoots(success.Children);
                await RestoreExpansionAsync(expanded).ConfigureAwait(true);
            }

            return;
        }

        var node = FindLoadedNode(directoryPath);
        if (node is null)
        {
            return;
        }

        var children = await _expandFolderNode.ExecuteAsync(node.Path).ConfigureAwait(true);
        if (children is ExpandFolderNodeResult.Success loaded)
        {
            node.ReplaceChildren(RebuildNodes(loaded.Children, node.Children, node.Depth + 1));
            ApplyActiveDocumentHighlight(node);
            await RestoreExpansionAsync(expanded).ConfigureAwait(true);
        }
    }

    private async Task RestoreExpansionAsync(IReadOnlyList<string> directories)
    {
        foreach (var directory in directories)
        {
            await ExpandPathAsync(directory).ConfigureAwait(true);
        }
    }

    private void ReplaceRoots(IReadOnlyList<WorkspaceEntry> entries)
    {
        var rebuilt = RebuildNodes(entries, Roots, depth: 0);
        Roots.Clear();
        foreach (var node in rebuilt)
        {
            Roots.Add(node);
        }

        if (!string.IsNullOrEmpty(ActiveDocumentPath))
        {
            foreach (var node in Roots)
            {
                node.IsActiveDocument = !node.IsDirectory && PathsEqual(node.Path, ActiveDocumentPath);
            }
        }
    }

    /// <summary>
    /// Узлы перечитанного каталога. Свёрнутая, но уже прочитанная папка переходит в новое
    /// дерево как есть, вместе с детьми: это тот же in-memory cache, что и при повторном
    /// раскрытии (ADR-0007 Rule 5). Без этого любая правка рядом забывала бы, что в папке
    /// уже смотрели, и экран пустой папки сменялся бы на «выберите файл». Раскрытые папки
    /// строятся заново и перечитываются через <see cref="RestoreExpansionAsync"/>, как раньше.
    /// </summary>
    private List<FileTreeNodeViewModel> RebuildNodes(
        IReadOnlyList<WorkspaceEntry> entries,
        IEnumerable<FileTreeNodeViewModel> previous,
        int depth)
    {
        var cached = previous
            .Where(static node => node is
            {
                IsDirectory: true,
                HasLoadedChildren: true,
                HasLoadError: false,
                IsExpanded: false,
                IsLoadingChildren: false
            })
            .ToList();

        return entries
            .Select(entry => cached.FirstOrDefault(node => node.Entry == entry) ?? CreateNode(entry, depth))
            .ToList();
    }

    private FileTreeNodeViewModel? FindLoadedNode(string path)
        => Roots
            .SelectMany(static root => root.EnumerateLoadedNodes())
            .FirstOrDefault(node => PathsEqual(node.Path, path));

    private string DescribeFailure(WorkspaceMutationResult result)
        => result switch
        {
            WorkspaceMutationResult.NameTaken { IsDirectory: true } => _localization["TreeFolderNameTaken"],
            WorkspaceMutationResult.NameTaken => _localization["TreeNameTaken"],
            WorkspaceMutationResult.InvalidName { Problem: WorkspaceNameProblem.Reserved } =>
                _localization["TreeReservedName"],
            WorkspaceMutationResult.InvalidName => _localization["TreeInvalidChars"],
            WorkspaceMutationResult.NotFound => _localization["TreeNodeMissing"],
            WorkspaceMutationResult.AccessDenied => _localization["TreeNodeAccessDenied"],
            _ => _localization["TreeOperationFailed"]
        };
}
