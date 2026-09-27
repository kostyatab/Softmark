using Avalonia.Controls;
using Avalonia.VisualTree;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Пустой экран папки без документов (A-EmptyFolder): разметка собирается, кнопки ведут
/// в команды сайдбара и меню, а сам экран не строится, пока папки нет.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class EmptyFolderSurfaceViewTests
{
    private static readonly string Root = TestPaths.At("drafts");

    private readonly AvaloniaHeadlessFixture _fixture;

    public EmptyFolderSurfaceViewTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task EmptyFolderOffersToCreateTheFirstFile()
    {
        return _fixture.RunAsync(async () =>
        {
            var (viewModel, fileSystem) = CreateViewModel();
            var window = Show(viewModel);

            // Без папки пустого экрана нет вовсе — он не висит скрытым с запуска.
            Assert.Empty(window.GetVisualDescendants().OfType<EmptyDocumentSurfaceView>());

            await viewModel.OpenFolderPathAsync(Root);
            window.UpdateLayout();

            var surface = Assert.Single(window.GetVisualDescendants().OfType<EmptyDocumentSurfaceView>());
            Assert.True(Find<StackPanel>(surface, "NoDocumentsPanel").IsVisible);
            Assert.False(Find<StackPanel>(surface, "NoSelectionPanel").IsVisible);
            Assert.Contains(
                surface.GetVisualDescendants().OfType<TextBlock>(),
                static block => block.Text == "No Markdown files in this folder yet");

            Assert.Same(viewModel.Workspace!.StartNewFileCommand, Find<Button>(surface, "EmptyFolderNewFileButton").Command);
            Assert.Same(viewModel.OpenFolderCommand, Find<Button>(surface, "EmptyFolderOpenFolderButton").Command);

            var sidebar = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceSidebarView>());
            var note = Find<TextBlock>(sidebar, "NoDocumentsNote");
            Assert.True(note.IsVisible);
            Assert.Equal("No .md files here", note.Text);
            Assert.False(SearchShell(sidebar).IsVisible);

            // Файл появился снаружи — экран и сайдбар возвращаются к обычному виду.
            var notes = TestPaths.At("drafts", "notes.md");
            fileSystem.AddDirectory(Root, WorkspaceEntry.ForFile(notes, "notes.md"));
            await viewModel.ApplyWorkspaceChangesAsync([new WorkspaceChange(WorkspaceChangeKind.Created, notes)]);
            window.UpdateLayout();

            Assert.False(Find<StackPanel>(surface, "NoDocumentsPanel").IsVisible);
            Assert.True(Find<StackPanel>(surface, "NoSelectionPanel").IsVisible);
            Assert.False(note.IsVisible);
            Assert.True(SearchShell(sidebar).IsVisible);

            window.Hide();
        });
    }

    private static T Find<T>(Control root, string name)
        where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static Border SearchShell(WorkspaceSidebarView sidebar)
        => sidebar.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("mm-search-shell"));

    /// <summary>
    /// Окно без composition root: закрыть его нельзя — отписка в OnClosed ждёт VM
    /// из полного конструктора, — поэтому тест его прячет.
    /// </summary>
    private static MainWindow Show(ShellViewModel viewModel)
    {
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static (ShellViewModel ViewModel, FakeWorkspaceFileSystem FileSystem) CreateViewModel()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root);

        var viewModel = new ShellViewModel(
            new OpenDocumentUseCase(new StubDocumentLoader()),
            new SaveDocumentUseCase(new RecordingDocumentSaver()),
            new StubFilePicker(),
            new StubCommandLineActivation(),
            new LocalizationService(AppLanguage.English),
            new InMemorySettingsStore(),
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

        return (viewModel, fileSystem);
    }
}
