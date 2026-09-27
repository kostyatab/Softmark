using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// M1: открытие папки, ленивое дерево и то, ради чего всё это ограничено —
/// single-file режим не должен инициализировать workspace-подсистему.
/// </summary>
public sealed class WorkspaceSidebarTests
{
    private static readonly string Root = TestPaths.At("docs");

    [Fact]
    public async Task StartingWithASingleFileDoesNotCreateWorkspace()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        var harness = CreateHarness(fileSystem);
        harness.CommandLine.ActivationPath = TestPaths.At("docs", "notes.md");
        harness.Loader.Sources[TestPaths.At("docs", "notes.md")] = new MarkdownSource(TestPaths.At("docs", "notes.md"), "notes.md", "# notes");

        await harness.ViewModel.InitializeAsync();

        Assert.Null(harness.ViewModel.Workspace);
        Assert.False(harness.ViewModel.ShowsSidebar);
        Assert.Null(harness.ViewModel.SidebarContent);
        Assert.Empty(fileSystem.EnumeratedPaths);
    }

    [Fact]
    public async Task OpeningFolderBuildsRootLevelOnly()
    {
        var fileSystem = CreateFileSystem();
        var harness = CreateHarness(fileSystem);

        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = Assert.IsType<WorkspaceViewModel>(harness.ViewModel.Workspace);
        Assert.True(harness.ViewModel.ShowsSidebar);
        Assert.False(harness.ViewModel.ShowsExpandSidebarButton);
        Assert.Equal("docs", workspace.RootDisplayName);
        Assert.Equal(["adr", "README.md", "pack.bat"], workspace.Roots.Select(node => node.Name));
        Assert.Equal([Root], fileSystem.EnumeratedPaths);
    }

    [Fact]
    public async Task OpeningFolderOpensRootReadme()
    {
        var harness = CreateHarness(CreateFileSystem());

        await harness.ViewModel.OpenFolderPathAsync(Root);

        Assert.Equal(TestPaths.At("docs", "README.md"), harness.ViewModel.CurrentDocumentPath);
        Assert.Equal("README.md — docs — Softmark", harness.ViewModel.WindowTitle);

        var readmeNode = harness.ViewModel.Workspace!.Roots.Single(node => node.Name == "README.md");
        Assert.True(readmeNode.IsActiveDocument);
    }

    [Fact]
    public async Task OpeningFolderWithoutReadmeLeavesDocumentSurfaceAlone()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root, WorkspaceEntry.ForFile(TestPaths.At("docs", "notes.md"), "notes.md"));
        var harness = CreateHarness(fileSystem);

        await harness.ViewModel.OpenFolderPathAsync(Root);

        Assert.True(harness.ViewModel.ShowsSidebar);
        Assert.Null(harness.ViewModel.CurrentDocumentPath);
        Assert.Equal("docs — Softmark", harness.ViewModel.WindowTitle);
    }

    /// <summary>
    /// Файл открыт раньше своей папки. Раньше вкладка оставалась активной, а строка
    /// в дереве не подсвечивалась до первого переключения вкладок.
    /// </summary>
    [Fact]
    public async Task OpeningFolderOfOpenDocumentHighlightsItInTheTree()
    {
        var harness = CreateHarness(CreateFileSystemWithTwoDocuments());
        harness.Loader.Sources[TestPaths.At("docs", "notes.md")] = new MarkdownSource(TestPaths.At("docs", "notes.md"), "notes.md", "# notes");
        await harness.ViewModel.OpenPathAsync(TestPaths.At("docs", "notes.md"));

        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        Assert.Equal(TestPaths.At("docs", "notes.md"), harness.ViewModel.CurrentDocumentPath);
        Assert.Equal(TestPaths.At("docs", "notes.md"), workspace.ActiveDocumentPath);
        Assert.True(workspace.Roots.Single(node => node.Name == "notes.md").IsActiveDocument);
        Assert.False(workspace.Roots.Single(node => node.Name == "README.md").IsActiveDocument);
        Assert.Single(harness.ViewModel.OpenDocuments.Tabs);
        Assert.Equal("notes.md — docs — Softmark", harness.ViewModel.WindowTitle);
    }

    [Fact]
    public async Task ExpandingDirectoryReadsChildrenOnce()
    {
        var fileSystem = CreateFileSystem();
        var harness = CreateHarness(fileSystem);
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var adr = harness.ViewModel.Workspace!.Roots.Single(node => node.Name == "adr");
        Assert.Single(adr.Children); // placeholder, каталог ещё не читался
        Assert.False(adr.HasLoadedChildren);

        adr.IsExpanded = true;
        await WaitForChildrenAsync(adr);

        Assert.Equal(["adr_0001.md"], adr.Children.Select(node => node.Name));
        Assert.Equal([Root, TestPaths.At("docs", "adr")], fileSystem.EnumeratedPaths);

        adr.IsExpanded = false;
        adr.IsExpanded = true;

        Assert.Equal([Root, TestPaths.At("docs", "adr")], fileSystem.EnumeratedPaths);
    }

    [Fact]
    public async Task DirectoryFailureStaysInsideItsNode()
    {
        var fileSystem = CreateFileSystem();
        fileSystem.FailWith(TestPaths.At("docs", "adr"), new UnauthorizedAccessException());
        var harness = CreateHarness(fileSystem);
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var adr = harness.ViewModel.Workspace!.Roots.Single(node => node.Name == "adr");
        adr.IsExpanded = true;
        await WaitForLoadErrorAsync(adr);

        Assert.True(adr.HasLoadError);
        Assert.Equal("Access denied", adr.LoadError);
        Assert.True(harness.ViewModel.ShowsSidebar);
        Assert.Equal(ViewState.Viewing, harness.ViewModel.State);
    }

    [Fact]
    public async Task ActivatingSupportedDocumentOpensIt()
    {
        var harness = CreateHarness(CreateFileSystemWithTwoDocuments());
        harness.Loader.Sources[TestPaths.At("docs", "notes.md")] = new MarkdownSource(TestPaths.At("docs", "notes.md"), "notes.md", "# notes");
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        await workspace.OpenNodeCommand.ExecuteAsync(
            workspace.Roots.Single(node => node.Name == "notes.md"));

        Assert.Equal(TestPaths.At("docs", "notes.md"), harness.ViewModel.CurrentDocumentPath);
    }

    /// <summary>
    /// Выделение само по себе ничего не открывает: иначе документ открывал бы и правый клик,
    /// и каждое нажатие стрелки при навигации по дереву.
    /// </summary>
    [Fact]
    public async Task SelectionAloneDoesNotOpenTheDocument()
    {
        var harness = CreateHarness(CreateFileSystemWithTwoDocuments());
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        workspace.SelectedNode = workspace.Roots.Single(node => node.Name == "notes.md");

        Assert.Equal(TestPaths.At("docs", "README.md"), harness.ViewModel.CurrentDocumentPath);
    }

    /// <summary>
    /// Папка без документов: до фикса пустой экран отправлял выбирать файл в пустое дерево.
    /// Не-документ в корне не считается — открыть его всё равно нельзя.
    /// </summary>
    [Fact]
    public async Task FolderWithoutDocumentsIsRecognisedFromTheRootLevel()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root, WorkspaceEntry.ForFile(TestPaths.At("docs", "pack.bat"), "pack.bat"));
        var harness = CreateHarness(fileSystem);

        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        Assert.True(harness.ViewModel.IsEmptyDocumentSurface);
        Assert.True(workspace.HasNoDocuments);
        Assert.True(workspace.ShowsNoDocumentsNote);
        Assert.Equal([Root], fileSystem.EnumeratedPaths);
    }

    [Fact]
    public async Task FolderWithADocumentIsNotEmpty()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root, WorkspaceEntry.ForFile(TestPaths.At("docs", "notes.md"), "notes.md"));
        var harness = CreateHarness(fileSystem);

        await harness.ViewModel.OpenFolderPathAsync(Root);

        Assert.True(harness.ViewModel.IsEmptyDocumentSurface);
        Assert.False(harness.ViewModel.Workspace!.HasNoDocuments);
        Assert.False(harness.ViewModel.Workspace.ShowsNoDocumentsNote);
    }

    /// <summary>
    /// Что лежит в нераскрытом каталоге, неизвестно, а читать его ради пустого экрана нельзя
    /// (ADR-0007 Rule 5): остаётся обычный экран. Раскрыли и увидели, что пусто, — флаг ставится.
    /// </summary>
    [Fact]
    public async Task UnreadDirectoryKeepsTheUsualEmptySurfaceUntilExpanded()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root, WorkspaceEntry.ForDirectory(TestPaths.At("docs", "images"), "images"));
        fileSystem.AddDirectory(
            TestPaths.At("docs", "images"),
            WorkspaceEntry.ForFile(TestPaths.At("docs", "images", "logo.png"), "logo.png"));
        var harness = CreateHarness(fileSystem);

        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        Assert.False(workspace.HasNoDocuments);
        Assert.Equal([Root], fileSystem.EnumeratedPaths);

        await workspace.ExpandNodeAsync(workspace.Roots.Single());

        Assert.True(workspace.HasNoDocuments);
    }

    /// <summary>Каталог, который не удалось прочитать, мог хранить документы: папка не считается пустой.</summary>
    [Fact]
    public async Task UnreadableDirectoryDoesNotMakeTheFolderEmpty()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root, WorkspaceEntry.ForDirectory(TestPaths.At("docs", "private"), "private"));
        fileSystem.FailWith(TestPaths.At("docs", "private"), new UnauthorizedAccessException());
        var harness = CreateHarness(fileSystem);
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        var folder = workspace.Roots.Single();
        await workspace.ExpandNodeAsync(folder);

        Assert.True(folder.HasLoadError);
        Assert.False(workspace.HasNoDocuments);
    }

    /// <summary>
    /// Раскрытые папки возвращаются по одной: пока первая уже прочитана, а вторая ещё нет,
    /// до фикса экран мигал «выберите файл» посреди перечитывания корня.
    /// </summary>
    [Fact]
    public async Task EmptyFolderStateHoldsWhileTheTreeIsRebuilt()
    {
        var fileSystem = CreateFileSystemWithOnlyImages("images", "scans");
        var harness = CreateHarness(fileSystem);
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        foreach (var folder in workspace.Roots.ToList())
        {
            await workspace.ExpandNodeAsync(folder);
        }

        Assert.True(workspace.HasNoDocuments);

        var flips = RecordHasNoDocumentsChanges(workspace);
        await AddFileFromOutsideAsync(harness, fileSystem, "LICENSE", "images", "scans");

        Assert.True(workspace.HasNoDocuments);
        Assert.Empty(flips);
    }

    /// <summary>
    /// Свёрнутая, но уже прочитанная папка переживает перечитывание соседей: до фикса она
    /// снова становилась непрочитанной, и экран пустой папки уходил насовсем.
    /// </summary>
    [Fact]
    public async Task CollapsedFolderStaysReadAfterARefreshNextToIt()
    {
        var fileSystem = CreateFileSystemWithOnlyImages("images");
        var harness = CreateHarness(fileSystem);
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        var images = workspace.Roots.Single();
        await workspace.ExpandNodeAsync(images);
        images.IsExpanded = false;

        await AddFileFromOutsideAsync(harness, fileSystem, "LICENSE", "images");

        Assert.True(workspace.HasNoDocuments);
        var rebuiltImages = workspace.Roots.Single(static node => node.Name == "images");
        Assert.False(rebuiltImages.IsExpanded);
        Assert.Equal(["logo.png"], rebuiltImages.Children.Select(static node => node.Name));
        Assert.Single(fileSystem.EnumeratedPaths, TestPaths.At("docs", "images"));
    }

    /// <summary>Новая папка раскрывается показом уже после перечитывания — экран не мигает и здесь.</summary>
    [Fact]
    public async Task CreatingAFolderInAnEmptyFolderKeepsTheEmptyState()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root);
        var harness = CreateHarness(fileSystem);
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        var flips = RecordHasNoDocumentsChanges(workspace);

        workspace.StartNewFolderCommand.Execute(null);
        workspace.EditName = "images";
        await workspace.CommitEditCommand.ExecuteAsync(null);

        Assert.Contains(workspace.Roots, static node => node.Name == "images");
        Assert.True(workspace.HasNoDocuments);
        Assert.Empty(flips);
    }

    /// <summary>Файл положили в папку снаружи — экран меняется без перезапуска.</summary>
    [Fact]
    public async Task DocumentAddedFromOutsideLeavesTheEmptyFolderState()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root);
        var harness = CreateHarness(fileSystem);
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        Assert.True(workspace.HasNoDocuments);

        var notified = new List<string>();
        workspace.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? string.Empty);

        var notes = TestPaths.At("docs", "notes.md");
        fileSystem.AddDirectory(Root, WorkspaceEntry.ForFile(notes, "notes.md"));
        await harness.ViewModel.ApplyWorkspaceChangesAsync([new WorkspaceChange(WorkspaceChangeKind.Created, notes)]);

        Assert.False(workspace.HasNoDocuments);
        Assert.False(workspace.ShowsNoDocumentsNote);
        Assert.Contains(nameof(WorkspaceViewModel.HasNoDocuments), notified);
        Assert.Contains(nameof(WorkspaceViewModel.ShowsNoDocumentsNote), notified);
    }

    /// <summary>«Новый файл» с пустого экрана — та же команда, что в шапке сайдбара: файл создаётся и открывается.</summary>
    [Fact]
    public async Task CreatingTheFirstFileLeavesTheEmptyFolderState()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root);
        var harness = CreateHarness(fileSystem);
        await harness.ViewModel.OpenFolderPathAsync(Root);
        harness.Loader.Sources[TestPaths.At("docs", "first.md")] =
            new MarkdownSource(TestPaths.At("docs", "first.md"), "first.md", "");

        var workspace = harness.ViewModel.Workspace!;
        workspace.StartNewFileCommand.Execute(null);
        workspace.EditName = "first";
        await workspace.CommitEditCommand.ExecuteAsync(null);

        Assert.False(workspace.HasNoDocuments);
        Assert.Equal(TestPaths.At("docs", "first.md"), harness.ViewModel.CurrentDocumentPath);
        Assert.False(harness.ViewModel.IsEmptyDocumentSurface);
    }

    [Fact]
    public async Task ActivatingNonDocumentIsInert()
    {
        var harness = CreateHarness(CreateFileSystem());
        await harness.ViewModel.OpenFolderPathAsync(Root);

        var workspace = harness.ViewModel.Workspace!;
        var packBat = workspace.Roots.Single(node => node.Name == "pack.bat");

        Assert.True(packBat.IsInert);

        await workspace.OpenNodeCommand.ExecuteAsync(packBat);

        Assert.Equal(TestPaths.At("docs", "README.md"), harness.ViewModel.CurrentDocumentPath);
        Assert.Equal(ViewState.Viewing, harness.ViewModel.State);
    }

    [Fact]
    public async Task MissingFolderFailsWithoutBreakingTheOpenDocument()
    {
        var harness = CreateHarness(new FakeWorkspaceFileSystem());
        harness.Loader.Sources[TestPaths.At("notes.md")] = new MarkdownSource(TestPaths.At("notes.md"), "notes.md", "# notes");
        await harness.ViewModel.OpenPathAsync(TestPaths.At("notes.md"));

        await harness.ViewModel.OpenFolderPathAsync(TestPaths.At("missing"));

        Assert.Null(harness.ViewModel.Workspace);
        Assert.Equal(ViewState.LoadError, harness.ViewModel.State);
        Assert.Equal(TestPaths.At("notes.md"), harness.ViewModel.CurrentDocumentPath);
    }

    [Fact]
    public async Task ClosingFolderClosesDocumentThatBelongsToIt()
    {
        var harness = CreateHarness(CreateFileSystem());
        await harness.ViewModel.OpenFolderPathAsync(Root);

        await harness.ViewModel.CloseFolderCommand.ExecuteAsync(null);

        Assert.Null(harness.ViewModel.Workspace);
        Assert.False(harness.ViewModel.ShowsSidebar);
        Assert.Null(harness.ViewModel.CurrentDocumentPath);
        Assert.Equal(ViewState.NoDocument, harness.ViewModel.State);
    }

    [Fact]
    public async Task ClosingFolderKeepsDocumentOpenedOverIt()
    {
        var harness = CreateHarness(CreateFileSystem());
        await harness.ViewModel.OpenFolderPathAsync(Root);
        harness.Loader.Sources[TestPaths.At("outside", "notes.md")] =
            new MarkdownSource(TestPaths.At("outside", "notes.md"), "notes.md", "# notes");
        await harness.ViewModel.OpenPathAsync(TestPaths.At("outside", "notes.md"));

        await harness.ViewModel.CloseFolderCommand.ExecuteAsync(null);

        Assert.Null(harness.ViewModel.Workspace);
        Assert.Equal(TestPaths.At("outside", "notes.md"), harness.ViewModel.CurrentDocumentPath);
        Assert.Equal(ViewState.Viewing, harness.ViewModel.State);
    }

    [Fact]
    public async Task SidebarWidthIsRestoredAndPersistedClamped()
    {
        var harness = CreateHarness(CreateFileSystem());
        harness.Settings.SidebarWidth = 300;

        await harness.ViewModel.OpenFolderPathAsync(Root);
        Assert.Equal(300, harness.ViewModel.SidebarWidth);

        harness.ViewModel.SidebarWidth = 900;
        await harness.ViewModel.PersistSidebarWidthAsync();

        Assert.Equal(WorkspaceSidebarWidth.Maximum, harness.ViewModel.SidebarWidth);
        Assert.Equal(WorkspaceSidebarWidth.Maximum, harness.Settings.SidebarWidth);
    }

    private static FakeWorkspaceFileSystem CreateFileSystem()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            Root,
            WorkspaceEntry.ForDirectory(TestPaths.At("docs", "adr"), "adr"),
            WorkspaceEntry.ForFile(TestPaths.At("docs", "README.md"), "README.md"),
            WorkspaceEntry.ForFile(TestPaths.At("docs", "pack.bat"), "pack.bat"));
        fileSystem.AddDirectory(
            TestPaths.At("docs", "adr"),
            WorkspaceEntry.ForFile(TestPaths.At("docs", "adr", "adr_0001.md"), "adr_0001.md"));
        return fileSystem;
    }

    private static FakeWorkspaceFileSystem CreateFileSystemWithTwoDocuments()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            Root,
            WorkspaceEntry.ForFile(TestPaths.At("docs", "README.md"), "README.md"),
            WorkspaceEntry.ForFile(TestPaths.At("docs", "notes.md"), "notes.md"));
        return fileSystem;
    }

    private static async Task WaitForChildrenAsync(FileTreeNodeViewModel node)
    {
        for (var attempt = 0; attempt < 50 && !node.HasLoadedChildren; attempt++)
        {
            await Task.Yield();
        }
    }

    /// <summary>В корне только папки, и в каждой — по картинке: документов нет нигде.</summary>
    private static FakeWorkspaceFileSystem CreateFileSystemWithOnlyImages(params string[] folders)
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root, [.. RootFolders(folders)]);
        foreach (var folder in folders)
        {
            fileSystem.AddDirectory(
                TestPaths.At("docs", folder),
                WorkspaceEntry.ForFile(TestPaths.At("docs", folder, "logo.png"), "logo.png"));
        }

        return fileSystem;
    }

    private static IEnumerable<WorkspaceEntry> RootFolders(string[] folders)
        => folders.Select(static folder => WorkspaceEntry.ForDirectory(TestPaths.At("docs", folder), folder));

    /// <summary>Файл-не-документ появился в корне: watcher перечитывает корень целиком.</summary>
    private static async Task AddFileFromOutsideAsync(
        WorkspaceTestHarness harness,
        FakeWorkspaceFileSystem fileSystem,
        string name,
        params string[] folders)
    {
        var path = TestPaths.At("docs", name);
        fileSystem.AddDirectory(Root, [.. RootFolders(folders), WorkspaceEntry.ForFile(path, name)]);
        await harness.ViewModel.ApplyWorkspaceChangesAsync([new WorkspaceChange(WorkspaceChangeKind.Created, path)]);
    }

    private static List<bool> RecordHasNoDocumentsChanges(WorkspaceViewModel workspace)
    {
        var values = new List<bool>();
        workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceViewModel.HasNoDocuments))
            {
                values.Add(workspace.HasNoDocuments);
            }
        };

        return values;
    }

    private static async Task WaitForLoadErrorAsync(FileTreeNodeViewModel node)
    {
        for (var attempt = 0; attempt < 50 && !node.HasLoadError; attempt++)
        {
            await Task.Yield();
        }
    }

    private static WorkspaceTestHarness CreateHarness(FakeWorkspaceFileSystem fileSystem)
    {
        var loader = new StubDocumentLoader();
        loader.Sources[TestPaths.At("docs", "README.md")] =
            new MarkdownSource(TestPaths.At("docs", "README.md"), "README.md", "# readme");
        var settings = new InMemorySettingsStore();
        var commandLine = new StubCommandLineActivation();
        var viewModel = new ShellViewModel(
            new OpenDocumentUseCase(loader),
            new SaveDocumentUseCase(new RecordingDocumentSaver()),
            new StubFilePicker(),
            commandLine,
            new LocalizationService(AppLanguage.English),
            settings,
            new RecordingThemeService(),
            new RecordingStartupMetrics(),
            new RenderMarkdownDocumentUseCase(new TestMarkdownRenderer(), new FakeDiagramRenderService()),
            TestUpdates.CreateViewModel(),
            new OpenFolderUseCase(fileSystem),
            new ExpandFolderNodeUseCase(fileSystem),
            new SearchWorkspaceFilesUseCase(fileSystem),
            new WorkspaceFileOperationsUseCase(fileSystem, new FakePlatformServices()),
            new FakePlatformServices(),
            static () => new FakeWorkspaceWatcher(),
            new RecordingWindowLauncher());

        return new WorkspaceTestHarness(loader, settings, commandLine, viewModel);
    }

    private sealed record WorkspaceTestHarness(
        StubDocumentLoader Loader,
        InMemorySettingsStore Settings,
        StubCommandLineActivation CommandLine,
        ShellViewModel ViewModel);
}
