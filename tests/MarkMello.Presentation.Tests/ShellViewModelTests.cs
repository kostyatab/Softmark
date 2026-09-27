using MarkMello.Application.UseCases;
using MarkMello.Application.Updates;
using MarkMello.Domain;
using MarkMello.Domain.Diagnostics;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using System.Globalization;

namespace MarkMello.Presentation.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task ToggleEditModeCommandLazilyCreatesEditorSession()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "alpha beta");

        await harness.ViewModel.OpenPathAsync(path);

        Assert.False(harness.ViewModel.IsEditMode);
        Assert.Null(harness.ViewModel.EditorSession);
        Assert.Same(harness.ViewModel, harness.ViewModel.ActiveDocumentContent);
        Assert.Contains(StartupStage.DocumentModelReady, harness.StartupMetrics.Marks);
        Assert.DoesNotContain(StartupStage.ReadableDocument, harness.StartupMetrics.Marks);

        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsEditMode);
        Assert.NotNull(harness.ViewModel.EditorSession);
        Assert.Same(harness.ViewModel.EditorSession, harness.ViewModel.ActiveDocumentContent);
        Assert.Equal(1, harness.StartupMetrics.Marks.Count(stage => stage == StartupStage.EditorActivation));
    }

    [Fact]
    public async Task ToggleEditModeCommandWhenDirtyShowsPromptAndDiscardLeavesEditMode()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "alpha beta");

        await harness.ViewModel.OpenPathAsync(path);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "changed";

        Assert.True(harness.ViewModel.IsDirty);
        Assert.Equal("one.md •", harness.ViewModel.TitleFileDisplayName);

        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
        Assert.True(harness.ViewModel.IsEditMode);
        Assert.Equal("Save changes to \"one.md\"?", harness.ViewModel.DirtyPromptTitle);
        Assert.Equal("Otherwise your changes will be lost when you leave editing.", harness.ViewModel.DirtyPromptMessage);

        await harness.ViewModel.ConfirmDirtyDiscardCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.False(harness.ViewModel.IsEditMode);
        Assert.False(harness.ViewModel.IsDirty);
        Assert.Equal("alpha beta", harness.ViewModel.Document!.Content);
    }

    /// <summary>
    /// Перетащенный файл встаёт в свою вкладку, а правки активной остаются в её сессии —
    /// спрашивать о них нечего (ADR-0009 Rule 3). Раньше тут поднимался диалог, и
    /// «Не сохранять» стирало правки зря.
    /// </summary>
    [Fact]
    public async Task OpenDroppedFileAsyncWhenEditorIsDirtyOpensItInANewTabWithoutAsking()
    {
        var harness = CreateHarness();
        var firstPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        var secondPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "two.md");
        harness.Loader.Sources[firstPath] = CreateSource(firstPath, "first");
        harness.Loader.Sources[secondPath] = CreateSource(secondPath, "second");

        await harness.ViewModel.OpenPathAsync(firstPath);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first changed";
        var first = harness.ViewModel.OpenDocuments.ActiveTab!;

        await harness.ViewModel.OpenDroppedFileAsync(secondPath);

        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.False(harness.ViewModel.IsEditMode);
        Assert.Equal("two.md", harness.ViewModel.FileName);
        Assert.Equal("second", harness.ViewModel.Document!.Content);
        Assert.Equal(2, harness.ViewModel.OpenDocuments.Tabs.Count);
        Assert.True(first.IsDirty);
        Assert.True(first.IsEditMode);
        Assert.Equal("first changed", first.EditorSession!.SourceText);
    }

    [Fact]
    public void ToggleAppMenuCommandOpensMenuAndClearErrorClosesOverlay()
    {
        var harness = CreateHarness();

        harness.ViewModel.ToggleAppMenuCommand.Execute(null);

        Assert.True(harness.ViewModel.IsAppMenuOpen);
        Assert.True(harness.ViewModel.HasOpenOverlay);

        harness.ViewModel.ClearErrorCommand.Execute(null);

        Assert.False(harness.ViewModel.IsAppMenuOpen);
        Assert.False(harness.ViewModel.HasOpenOverlay);
    }

    [Fact]
    public async Task ToggleFindBarCommandOpensAndClosesFindBar()
    {
        var harness = CreateHarness();
        await OpenSampleAsync(harness);
        Assert.Null(harness.ViewModel.FindOverlayContent);

        harness.ViewModel.ToggleFindBarCommand.Execute(null);

        Assert.True(harness.ViewModel.IsFindBarOpen);
        Assert.Same(harness.ViewModel, harness.ViewModel.FindOverlayContent);

        harness.ViewModel.ToggleFindBarCommand.Execute(null);

        Assert.False(harness.ViewModel.IsFindBarOpen);
        Assert.Null(harness.ViewModel.FindOverlayContent);
    }

    /// <summary>
    /// Карточка поиска раскрывается под кнопкой поиска, а без документа кнопки нет:
    /// ⌘F на стартовом экране ничего не открывает.
    /// </summary>
    [Fact]
    public void ToggleFindBarCommandDoesNothingWithoutADocument()
    {
        var harness = CreateHarness();

        harness.ViewModel.ToggleFindBarCommand.Execute(null);

        Assert.False(harness.ViewModel.IsFindBarOpen);
        Assert.Null(harness.ViewModel.FindOverlayContent);
    }

    [Fact]
    public async Task OpeningAppMenuClosesFindBar()
    {
        var harness = CreateHarness();
        await OpenSampleAsync(harness);
        harness.ViewModel.ToggleFindBarCommand.Execute(null);
        Assert.True(harness.ViewModel.IsFindBarOpen);

        harness.ViewModel.ToggleAppMenuCommand.Execute(null);

        Assert.False(harness.ViewModel.IsFindBarOpen);
        Assert.True(harness.ViewModel.IsAppMenuOpen);
    }

    [Fact]
    public async Task ClearErrorCommandClosesFindBarFirst()
    {
        var harness = CreateHarness();
        await OpenSampleAsync(harness);
        harness.ViewModel.ToggleFindBarCommand.Execute(null);
        Assert.True(harness.ViewModel.IsFindBarOpen);

        harness.ViewModel.ClearErrorCommand.Execute(null);

        Assert.False(harness.ViewModel.IsFindBarOpen);
    }

    [Fact]
    public void FindResultLabelFormatsCountersAndNoResults()
    {
        var harness = CreateHarness();

        harness.ViewModel.FindQuery = "alpha";
        harness.ViewModel.FindMatchCount = 3;
        harness.ViewModel.FindMatchIndex = 1;

        Assert.Equal("2 of 3", harness.ViewModel.FindResultLabel);

        harness.ViewModel.FindMatchCount = 0;

        Assert.Equal("No results", harness.ViewModel.FindResultLabel);

        harness.ViewModel.FindQuery = string.Empty;

        Assert.Equal("0 of 0", harness.ViewModel.FindResultLabel);
    }

    [Fact]
    public async Task EnteringEditModeClosesFindBar()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "alpha beta");

        await harness.ViewModel.OpenPathAsync(path);
        harness.ViewModel.ToggleFindBarCommand.Execute(null);
        Assert.True(harness.ViewModel.IsFindBarOpen);

        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsFindBarOpen);
        Assert.True(harness.ViewModel.IsEditMode);
    }

    /// <summary>
    /// Меню ⋯ стоит в строке окна и в правке (ADR-0009 Rule 2): вход в правку закрывает
    /// открытую карточку, но меню открывается снова, как и настройки по ⌘,.
    /// </summary>
    [Fact]
    public async Task EnteringEditModeClosesTheAppMenuButKeepsItAvailable()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "alpha beta");

        await harness.ViewModel.OpenPathAsync(path);
        harness.ViewModel.ToggleAppMenuCommand.Execute(null);

        Assert.True(harness.ViewModel.IsAppMenuOpen);
        Assert.NotNull(harness.ViewModel.AppMenuOverlayContent);

        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsEditMode);
        Assert.False(harness.ViewModel.IsAppMenuOpen);
        Assert.Null(harness.ViewModel.AppMenuOverlayContent);

        harness.ViewModel.ToggleAppMenuCommand.Execute(null);

        Assert.True(harness.ViewModel.IsAppMenuOpen);
        Assert.NotNull(harness.ViewModel.AppMenuOverlayContent);

        harness.ViewModel.ToggleAppSettingsCommand.Execute(null);

        Assert.True(harness.ViewModel.IsAppSettingsOpen);
        Assert.True(harness.ViewModel.IsEditMode);
    }

    [Fact]
    public async Task ReadableDocumentMetricIsMarkedOnlyAfterViewReportsRender()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "alpha beta");

        await harness.ViewModel.OpenPathAsync(path);

        Assert.Contains(StartupStage.DocumentModelReady, harness.StartupMetrics.Marks);
        Assert.DoesNotContain(StartupStage.ReadableDocument, harness.StartupMetrics.Marks);

        harness.ViewModel.MarkReadableDocumentRendered();
        harness.ViewModel.MarkReadableDocumentRendered();

        Assert.Equal(1, harness.StartupMetrics.Marks.Count(stage => stage == StartupStage.ReadableDocument));
    }

    [Fact]
    public void ToggleSettingsCommandReplacesAppMenuWithReadingSettings()
    {
        var harness = CreateHarness();

        harness.ViewModel.ToggleAppMenuCommand.Execute(null);
        harness.ViewModel.ToggleSettingsCommand.Execute(null);

        Assert.False(harness.ViewModel.IsAppMenuOpen);
        Assert.True(harness.ViewModel.IsSettingsOpen);
    }

    [Fact]
    public async Task RuntimeFileActivationOpensDocumentInRunningApp()
    {
        // Reproduces the macOS bug: while MarkMello is already running,
        // double-clicking another .md file in Finder fires an Apple Event
        // that Avalonia surfaces as FileActivated. The view-model must
        // route it through OpenPathAsync just like a command-line argument.
        var harness = CreateHarness();
        var firstPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "first.md");
        var secondPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "second.md");
        harness.Loader.Sources[firstPath] = CreateSource(firstPath, "first");
        harness.Loader.Sources[secondPath] = CreateSource(secondPath, "second");

        await harness.ViewModel.OpenPathAsync(firstPath);
        Assert.Equal("first.md", harness.ViewModel.FileName);

        var pending = WaitForDocumentChangeAsync(harness.ViewModel, "second.md");
        harness.CommandLine.RaiseFileActivated(secondPath);
        await pending;

        Assert.Equal("second.md", harness.ViewModel.FileName);
        Assert.Equal("second", harness.ViewModel.Document!.Content);
    }

    private static Task WaitForDocumentChangeAsync(ShellViewModel viewModel, string expectedFileName)
    {
        if (viewModel.FileName == expectedFileName)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ShellViewModel.FileName) && viewModel.FileName == expectedFileName)
            {
                viewModel.PropertyChanged -= Handler;
                tcs.TrySetResult();
            }
        }

        viewModel.PropertyChanged += Handler;
        if (viewModel.FileName == expectedFileName)
        {
            viewModel.PropertyChanged -= Handler;
            tcs.TrySetResult();
        }
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// «Настройки…» из меню ⋯ и строка карточки Aa ведут в одно окно; меню под ним
    /// закрывается, а кнопка ⋯ не остаётся нажатой — окно модальное, не карточка меню.
    /// </summary>
    [Fact]
    public void OpenAppSettingsCommandReplacesTheMenuWithTheSettingsWindow()
    {
        var harness = CreateHarness();

        harness.ViewModel.ToggleAppMenuCommand.Execute(null);
        harness.ViewModel.OpenAppSettingsCommand.Execute(null);

        Assert.False(harness.ViewModel.IsAppMenuOpen);
        Assert.True(harness.ViewModel.IsAppSettingsOpen);
        Assert.Equal(ShellOverlayKind.Settings, harness.ViewModel.ShellOverlay);
        Assert.NotNull(harness.ViewModel.AppSettingsContent);
        Assert.Null(harness.ViewModel.AppMenuOverlayContent);

        harness.ViewModel.ClearErrorCommand.Execute(null);

        Assert.False(harness.ViewModel.IsAppSettingsOpen);
        Assert.Null(harness.ViewModel.AppSettingsContent);
    }

    /// <summary>
    /// Под вопросом о правках ⌘, окно «Настройки» не открывает: оно встало бы под скрим
    /// вопроса и увело бы с него фокус.
    /// </summary>
    [Fact]
    public async Task SettingsShortcutDoesNothingWhileADialogAsks()
    {
        var harness = CreateHarness();
        await harness.ViewModel.CreateNewDocumentCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "# Draft";
        await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);
        Assert.True(harness.ViewModel.IsDirtyPromptOpen);

        harness.ViewModel.ToggleAppSettingsCommand.Execute(null);
        harness.ViewModel.OpenAppSettingsCommand.Execute(null);

        Assert.False(harness.ViewModel.IsAppSettingsOpen);
        Assert.Null(harness.ViewModel.AppSettingsContent);
        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
    }

    /// <summary>
    /// Сочетания, которые меняют документ за окном «Настройки», сначала закрывают его:
    /// модальная карточка не остаётся поверх другой вкладки или режима.
    /// </summary>
    [Theory]
    [InlineData("close-tab")]
    [InlineData("edit")]
    [InlineData("reload")]
    [InlineData("next-tab")]
    [InlineData("previous-tab")]
    public async Task DocumentShortcutsCloseTheSettingsWindow(string shortcut)
    {
        var harness = CreateHarness();
        var first = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        var second = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "two.md");
        harness.Loader.Sources[first] = CreateSource(first, "alpha");
        harness.Loader.Sources[second] = CreateSource(second, "beta");
        await harness.ViewModel.OpenPathAsync(first);
        await harness.ViewModel.OpenPathAsync(second);

        harness.ViewModel.OpenAppSettingsCommand.Execute(null);
        Assert.True(harness.ViewModel.IsAppSettingsOpen);

        switch (shortcut)
        {
            case "close-tab":
                await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);
                break;
            case "edit":
                await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
                break;
            case "reload":
                await harness.ViewModel.ReloadCommand.ExecuteAsync(null);
                break;
            case "next-tab":
                await harness.ViewModel.ActivateNextTabCommand.ExecuteAsync(null);
                break;
            case "previous-tab":
                await harness.ViewModel.ActivatePreviousTabCommand.ExecuteAsync(null);
                break;
        }

        Assert.False(harness.ViewModel.IsAppSettingsOpen);
        Assert.Null(harness.ViewModel.AppSettingsContent);
    }

    /// <summary>Быстрый путь: окна «Настройки» нет, пока его не открыли (ADR-0009 Rule 12).</summary>
    [Fact]
    public void SettingsWindowIsNotBuiltAtStartup()
    {
        var harness = CreateHarness();

        Assert.Null(harness.ViewModel.AppSettingsContent);
        Assert.Null(harness.ViewModel.AppMenuOverlayContent);
    }

    /// <summary>
    /// Состав меню ⋯ (ADR-0009 Rule 4): «Панель файлов» и «Закрыть папку» есть только
    /// при открытой папке; сохранение — только в правке; перечитать и закрыть вкладку —
    /// когда есть вкладка с файлом. Открыть, создать и настройки доступны всегда.
    /// </summary>
    [Fact]
    public async Task AppMenuItemsFollowTheFolderTheTabAndEditMode()
    {
        var root = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "notes");
        var readmePath = Path.Combine(root, "README.md");
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(root, WorkspaceEntry.ForFile(readmePath, "README.md"));
        var harness = CreateHarness(fileSystem);
        harness.Loader.Sources[readmePath] = CreateSource(readmePath, "# readme");
        var viewModel = harness.ViewModel;

        // Стартовый экран: ни папки, ни документа.
        Assert.False(viewModel.CanToggleSidebar);
        Assert.False(viewModel.CanCloseFolder);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.SaveAsCommand.CanExecute(null));
        Assert.False(viewModel.ReloadCommand.CanExecute(null));
        Assert.False(viewModel.CloseActiveTabCommand.CanExecute(null));
        AssertAlwaysAvailable(viewModel);

        // Папка с открытым README: пункты папки появились, файл можно перечитать и закрыть.
        await viewModel.OpenFolderPathAsync(root);
        Assert.True(viewModel.CanToggleSidebar);
        Assert.True(viewModel.CanCloseFolder);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.SaveAsCommand.CanExecute(null));
        Assert.True(viewModel.ReloadCommand.CanExecute(null));
        Assert.True(viewModel.CloseActiveTabCommand.CanExecute(null));
        AssertAlwaysAvailable(viewModel);

        // Правка: сохранить и сохранить как — доступны.
        await viewModel.ToggleEditModeCommand.ExecuteAsync(null);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.True(viewModel.SaveAsCommand.CanExecute(null));
        Assert.True(viewModel.ReloadCommand.CanExecute(null));
        Assert.True(viewModel.CloseActiveTabCommand.CanExecute(null));
        AssertAlwaysAvailable(viewModel);

        static void AssertAlwaysAvailable(ShellViewModel viewModel)
        {
            Assert.True(viewModel.CreateNewDocumentCommand.CanExecute(null));
            Assert.True(viewModel.OpenFileCommand.CanExecute(null));
            Assert.True(viewModel.OpenFolderCommand.CanExecute(null));
            Assert.True(viewModel.OpenAppSettingsCommand.CanExecute(null));
        }
    }

    /// <summary>
    /// Клавиши сочетаний меню — по платформе (MM-25): ⌘ и порядок ⇧⌘ на macOS. Каждая
    /// клавиша — своя плашка, поэтому в данных они через пробел.
    /// </summary>
    [Theory]
    [InlineData("macOS", "⌘ N", "⇧ ⌘ S", "⌘ R", "⌘ W", "⌘ ,")]
    [InlineData("Windows", "Ctrl N", "Ctrl Shift S", "Ctrl R", "Ctrl W", "Ctrl ,")]
    public void AppMenuShortcutsAreLabelledForThePlatform(
        string platformName,
        string newDocument,
        string saveAs,
        string reload,
        string closeTab,
        string settings)
    {
        var harness = CreateHarness(platformName: platformName);

        Assert.Equal(newDocument.Split(' '), harness.ViewModel.NewDocumentShortcutKeys);
        Assert.Equal(saveAs.Split(' '), harness.ViewModel.SaveAsShortcutKeys);
        Assert.Equal(reload.Split(' '), harness.ViewModel.ReloadShortcutKeys);
        Assert.Equal(closeTab.Split(' '), harness.ViewModel.CloseTabShortcutKeys);
        Assert.Equal(settings.Split(' '), harness.ViewModel.SettingsShortcutKeys);
    }

    [Fact]
    public async Task CreateNewDocumentCommandStartsInEditModeWithUnsavedDraft()
    {
        var harness = CreateHarness();

        await harness.ViewModel.CreateNewDocumentCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsViewer);
        Assert.True(harness.ViewModel.IsEditMode);
        Assert.Null(harness.ViewModel.Document);
        Assert.NotNull(harness.ViewModel.EditorSession);
        Assert.Null(harness.ViewModel.EditorSession.CurrentPath);
        Assert.Equal("Untitled.md", harness.ViewModel.FileName);
        Assert.Equal("Untitled.md — Softmark", harness.ViewModel.WindowTitle);
        Assert.Contains(StartupStage.EditorActivation, harness.StartupMetrics.Marks);
        Assert.DoesNotContain(StartupStage.ReadableDocument, harness.StartupMetrics.Marks);
    }

    /// <summary>
    /// Справа в строке окна при чтении — карандаш; в правке на его месте «Готово», а слева
    /// от кнопок «Не сохранено ⌘S», пока есть несохранённые правки (ADR-0009 Rule 2).
    /// </summary>
    [Fact]
    public async Task EditActionsFollowModeAndUnsavedChanges()
    {
        var harness = CreateHarness();
        var viewModel = harness.ViewModel;
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "first");

        await viewModel.OpenPathAsync(path);

        Assert.True(viewModel.ShowsEditToggle);
        Assert.False(viewModel.ShowsDoneButton);
        Assert.False(viewModel.ShowsUnsavedIndicator);

        await viewModel.ToggleEditModeCommand.ExecuteAsync(null);

        Assert.False(viewModel.ShowsEditToggle);
        Assert.True(viewModel.ShowsDoneButton);
        Assert.False(viewModel.ShowsUnsavedIndicator);

        var names = new List<string?>();
        viewModel.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        viewModel.EditorSession!.SourceText = "first updated";

        Assert.True(viewModel.ShowsUnsavedIndicator);
        Assert.True(viewModel.ShowsDoneButton);
        Assert.Contains(nameof(ShellViewModel.ShowsUnsavedIndicator), names);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.ShowsUnsavedIndicator);
        Assert.True(viewModel.ShowsDoneButton);

        names.Clear();
        await viewModel.ToggleEditModeCommand.ExecuteAsync(null);

        Assert.True(viewModel.ShowsEditToggle);
        Assert.False(viewModel.ShowsDoneButton);
        Assert.Contains(nameof(ShellViewModel.ShowsEditToggle), names);
        Assert.Contains(nameof(ShellViewModel.ShowsDoneButton), names);
    }

    /// <summary>
    /// «Готово» при грязной правке не бросает правки молча: выход в чтение идёт через
    /// диалог несохранённых правок, как у ⌘E.
    /// </summary>
    [Fact]
    public async Task DoneWithUnsavedChangesAsksBeforeLeavingEditMode()
    {
        var harness = CreateHarness();
        var viewModel = harness.ViewModel;
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "first");

        await viewModel.OpenPathAsync(path);
        await viewModel.ToggleEditModeCommand.ExecuteAsync(null);
        viewModel.EditorSession!.SourceText = "first updated";

        await viewModel.ToggleEditModeCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsDirtyPromptOpen);
        Assert.True(viewModel.IsEditMode);
        Assert.True(viewModel.ShowsDoneButton);
        Assert.True(viewModel.ShowsUnsavedIndicator);
    }

    /// <summary>
    /// Черновик ⌘N читать нельзя, пока у него нет пути: «Готово» нет, а «Не сохранено»
    /// есть с самого начала, даже пока черновик пуст. После «Сохранить как» черновик
    /// становится обычным документом и «Готово» появляется.
    /// </summary>
    [Fact]
    public async Task DraftShowsUnsavedWithoutDoneUntilSavedAs()
    {
        var harness = CreateHarness();
        var viewModel = harness.ViewModel;
        harness.FilePicker.SavePath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "draft.md");

        await viewModel.CreateNewDocumentCommand.ExecuteAsync(null);

        Assert.False(viewModel.ShowsEditToggle);
        Assert.False(viewModel.ShowsDoneButton);
        Assert.True(viewModel.ShowsUnsavedIndicator);

        viewModel.EditorSession!.SourceText = "# Draft";

        Assert.False(viewModel.ShowsDoneButton);
        Assert.True(viewModel.ShowsUnsavedIndicator);

        await viewModel.SaveAsCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsEditMode);
        Assert.True(viewModel.ShowsDoneButton);
        Assert.False(viewModel.ShowsUnsavedIndicator);
        Assert.False(viewModel.ShowsEditToggle);
    }

    [Fact]
    public async Task CloseActiveTabCommandReturnsTheOnlyDocumentToWelcome()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "alpha beta");

        await harness.ViewModel.OpenPathAsync(path);

        await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsWelcome);
        Assert.False(harness.ViewModel.IsViewer);
        Assert.Null(harness.ViewModel.Document);
        Assert.Null(harness.ViewModel.EditorSession);
        Assert.Equal("Softmark", harness.ViewModel.WindowTitle);
        Assert.False(harness.ViewModel.CloseActiveTabCommand.CanExecute(null));
    }

    [Fact]
    public async Task CloseActiveTabCommandWhenDirtyDraftPromptsAndDiscardReturnsToWelcome()
    {
        var harness = CreateHarness();

        await harness.ViewModel.CreateNewDocumentCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "# Draft";

        await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Equal("Save changes to \"Untitled.md\"?", harness.ViewModel.DirtyPromptTitle);
        Assert.Equal("Otherwise your changes will be lost when the tab closes.", harness.ViewModel.DirtyPromptMessage);
        Assert.True(harness.ViewModel.IsEditMode);

        await harness.ViewModel.ConfirmDirtyDiscardCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsWelcome);
        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Null(harness.ViewModel.Document);
        Assert.Null(harness.ViewModel.EditorSession);
        Assert.Equal("Softmark", harness.ViewModel.WindowTitle);
    }

    /// <summary>
    /// Заголовок называет файл, а текст — когда пропадут правки, если их не сохранить: у каждого
    /// действия, которое закрывает или перечитывает вкладку, своя формулировка. Раньше заголовок
    /// был один на всё — «Есть несохранённые изменения», — и чьи это правки, не говорилось.
    /// </summary>
    [Theory]
    [InlineData("tab", "Otherwise your changes will be lost when the tab closes.")]
    [InlineData("folder", "Otherwise your changes will be lost when the folder closes.")]
    [InlineData("reload", "Otherwise your changes will be lost on reload.")]
    [InlineData("edit", "Otherwise your changes will be lost when you leave editing.")]
    [InlineData("window", "Otherwise your changes will be lost when you quit Softmark.")]
    public async Task DirtyPromptNamesTheFileAndSaysWhenTheChangesAreLost(string action, string expectedMessage)
    {
        var root = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "notes");
        var readmePath = Path.Combine(root, "README.md");
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(root, WorkspaceEntry.ForFile(readmePath, "README.md"));
        var harness = CreateHarness(fileSystem);
        harness.Loader.Sources[readmePath] = CreateSource(readmePath, "# readme");

        await harness.ViewModel.OpenFolderPathAsync(root);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "# readme edited";
        Assert.Null(harness.ViewModel.DirtyPromptContent);

        switch (action)
        {
            case "tab":
                await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);
                break;
            case "folder":
                await harness.ViewModel.CloseFolderCommand.ExecuteAsync(null);
                break;
            case "reload":
                await harness.ViewModel.ReloadCommand.ExecuteAsync(null);
                break;
            case "edit":
                await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
                break;
            case "window":
                Assert.True(harness.ViewModel.TryQueueCloseRequest());
                break;
        }

        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Same(harness.ViewModel, harness.ViewModel.DirtyPromptContent);
        Assert.Equal("Save changes to \"README.md\"?", harness.ViewModel.DirtyPromptTitle);
        Assert.Equal(expectedMessage, harness.ViewModel.DirtyPromptMessage);

        harness.ViewModel.ClearErrorCommand.Execute(null);

        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Null(harness.ViewModel.DirtyPromptContent);
        Assert.Equal("# readme edited", harness.ViewModel.EditorSession!.SourceText);
    }

    /// <summary>
    /// Порядок кнопок — как у платформы (ADR-0009 Rule 10). Колонка 0 — у левого края,
    /// колонка 1 — распорка, дальше кнопки у правого края по порядку.
    /// </summary>
    [Theory]
    [InlineData("macOS", new[] { "Discard", "Cancel", "Save" }, "Discard")]
    [InlineData("Windows", new[] { "Save", "Discard", "Cancel" }, null)]
    [InlineData("Linux", new[] { "Save", "Discard", "Cancel" }, null)]
    public void DirtyPromptButtonsFollowThePlatformOrder(string platformName, string[] expectedOrder, string? leftButton)
    {
        var viewModel = CreateHarness(platformName: platformName).ViewModel;

        var buttons = new[]
        {
            (Column: viewModel.DirtyPromptSaveColumn, Label: viewModel.DirtyPromptSave),
            (Column: viewModel.DirtyPromptDiscardColumn, Label: viewModel.DirtyPromptDiscard),
            (Column: viewModel.DirtyPromptCancelColumn, Label: viewModel.DirtyPromptCancel)
        };

        Assert.Equal(expectedOrder, buttons.OrderBy(static button => button.Column).Select(static button => button.Label));

        // У левого края (колонка 0) на macOS стоит только «Не сохранять», остальные — за распоркой.
        Assert.Equal(leftButton, buttons.Where(static button => button.Column == 0).Select(static button => button.Label).SingleOrDefault());
        Assert.All(
            buttons.Where(static button => button.Column != 0),
            static button => Assert.True(button.Column > 1));
    }

    [Theory]
    [InlineData("macOS", new[] { "Cancel", "Delete" })]
    [InlineData("Windows", new[] { "Delete", "Cancel" })]
    [InlineData("Linux", new[] { "Delete", "Cancel" })]
    public void DeletePromptButtonsFollowThePlatformOrder(string platformName, string[] expectedOrder)
    {
        var viewModel = CreateHarness(platformName: platformName).ViewModel;

        var buttons = new[]
        {
            (Column: viewModel.DeleteCancelColumn, Label: viewModel.DeleteCancelLabel),
            (Column: viewModel.DeleteConfirmColumn, Label: viewModel.DeleteConfirmLabel)
        };

        Assert.Equal(expectedOrder, buttons.OrderBy(static button => button.Column).Select(static button => button.Label));
        Assert.All(buttons, static button => Assert.True(button.Column > 1));
    }

    /// <summary>Подсказки клавиш в тултипах кнопок: Enter, Esc и ⌘⌫ / Ctrl+Backspace.</summary>
    [Theory]
    [InlineData("macOS", "↵", "⎋", "⌘⌫")]
    [InlineData("Windows", "Enter", "Esc", "Ctrl+Backspace")]
    [InlineData("Linux", "Enter", "Esc", "Ctrl+Backspace")]
    public void DialogButtonTooltipsNameTheirKeys(string platformName, string confirm, string cancel, string discard)
    {
        var viewModel = CreateHarness(platformName: platformName).ViewModel;

        Assert.Equal(confirm, viewModel.DialogConfirmShortcut);
        Assert.Equal(cancel, viewModel.DialogCancelShortcut);
        Assert.Equal(discard, viewModel.DirtyPromptDiscardShortcut);
    }

    /// <summary>
    /// Ошибка сохранения показывается внутри карточки, а диалог остаётся открытым: вкладку
    /// с несохранёнными правками нельзя закрыть, пока их не удалось записать.
    /// </summary>
    [Fact]
    public async Task FailedSaveFromTheDirtyPromptKeepsItOpenWithTheError()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "first");

        await harness.ViewModel.OpenPathAsync(path);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first changed";
        var tab = harness.ViewModel.OpenDocuments.ActiveTab!;
        await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);
        harness.DocumentSaver.NextException = new UnauthorizedAccessException("blocked");

        await harness.ViewModel.ConfirmDirtySaveCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsDirtyPromptOpen);
        Assert.True(harness.ViewModel.HasDirtyPromptError);
        Assert.Equal($"Access denied: {path}", harness.ViewModel.DirtyPromptErrorMessage);
        Assert.Equal("Save changes to \"one.md\"?", harness.ViewModel.DirtyPromptTitle);
        Assert.Same(tab, Assert.Single(harness.ViewModel.OpenDocuments.Tabs));

        await harness.ViewModel.ConfirmDirtySaveCommand.ExecuteAsync(null);

        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.False(harness.ViewModel.HasDirtyPromptError);
        Assert.Empty(harness.ViewModel.OpenDocuments.Tabs);
        Assert.Equal("first changed", Assert.Single(harness.DocumentSaver.Saves).Content);
    }

    /// <summary>
    /// Esc отвечает диалогу, а не карточке поиска под ним: раньше первый Esc закрывал поиск,
    /// а вопрос о правках оставался висеть.
    /// </summary>
    [Fact]
    public async Task EscapeCancelsTheDirtyPromptBeforeClosingTheFindCard()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "first");

        await harness.ViewModel.OpenPathAsync(path);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first changed";
        harness.ViewModel.ToggleFindBarCommand.Execute(null);
        await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);
        Assert.True(harness.ViewModel.IsFindBarOpen);
        Assert.True(harness.ViewModel.IsDirtyPromptOpen);

        harness.ViewModel.ClearErrorCommand.Execute(null);

        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.True(harness.ViewModel.IsFindBarOpen);

        harness.ViewModel.ClearErrorCommand.Execute(null);

        Assert.False(harness.ViewModel.IsFindBarOpen);
    }

    [Fact]
    public async Task CloseActiveTabCommandWhenDirtyAndSavedPersistsThenReturnsToWelcome()
    {
        var harness = CreateHarness();
        var savedPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "close-after-save.md");
        harness.FilePicker.SavePath = savedPath;

        await harness.ViewModel.CreateNewDocumentCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first draft";

        await harness.ViewModel.CloseActiveTabCommand.ExecuteAsync(null);
        await harness.ViewModel.ConfirmDirtySaveCommand.ExecuteAsync(null);

        Assert.Equal(["Untitled.md"], harness.FilePicker.SuggestedSaveFileNames);

        var save = Assert.Single(harness.DocumentSaver.Saves);
        Assert.Equal(savedPath, save.Path);
        Assert.Equal("first draft", save.Content);
        Assert.True(harness.ViewModel.IsWelcome);
        Assert.False(harness.ViewModel.IsDirtyPromptOpen);
        Assert.Null(harness.ViewModel.Document);
        Assert.Null(harness.ViewModel.EditorSession);
        Assert.Equal("Softmark", harness.ViewModel.WindowTitle);
    }

    [Fact]
    public async Task SaveCommandPersistsEditorBufferAndClearsDirtyState()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "first");

        await harness.ViewModel.OpenPathAsync(path);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first updated";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        var save = Assert.Single(harness.DocumentSaver.Saves);
        Assert.Equal(path, save.Path);
        Assert.Equal("first updated", save.Content);
        Assert.False(harness.ViewModel.IsDirty);
        Assert.Equal("first updated", harness.ViewModel.Document!.Content);
        Assert.Equal("one.md", harness.ViewModel.TitleFileDisplayName);
    }

    [Fact]
    public async Task SaveCommandForNewDocumentUsesSaveAsPickerAndCreatesDocumentIdentity()
    {
        var harness = CreateHarness();
        var savedPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "draft.md");
        harness.FilePicker.SavePath = savedPath;

        await harness.ViewModel.CreateNewDocumentCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first draft";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["Untitled.md"], harness.FilePicker.SuggestedSaveFileNames);

        var save = Assert.Single(harness.DocumentSaver.Saves);
        Assert.Equal(savedPath, save.Path);
        Assert.Equal("first draft", save.Content);
        Assert.Equal(savedPath, harness.ViewModel.Document!.Path);
        Assert.Equal("draft.md", harness.ViewModel.FileName);
        Assert.False(harness.ViewModel.IsDirty);
    }

    [Fact]
    public async Task SaveCommandWhenSavingFailsKeepsDirtyStateAndShowsStatusMessage()
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "first");
        harness.DocumentSaver.NextException = new UnauthorizedAccessException("blocked");

        await harness.ViewModel.OpenPathAsync(path);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first updated";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.IsEditMode);
        Assert.True(harness.ViewModel.IsDirty);
        Assert.Equal("first", harness.ViewModel.Document!.Content);
        Assert.Equal($"Access denied: {path}", harness.ViewModel.EditorSession.StatusMessage);
    }

    [Fact]
    public async Task SaveAsCommandUsesPickerPathAndUpdatesDocumentIdentity()
    {
        var harness = CreateHarness();
        var originalPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        var savedAsPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "renamed.md");
        harness.Loader.Sources[originalPath] = CreateSource(originalPath, "first");
        harness.FilePicker.SavePath = savedAsPath;

        await harness.ViewModel.OpenPathAsync(originalPath);
        await harness.ViewModel.ToggleEditModeCommand.ExecuteAsync(null);
        harness.ViewModel.EditorSession!.SourceText = "first updated";

        await harness.ViewModel.SaveAsCommand.ExecuteAsync(null);

        Assert.Equal(["one.md"], harness.FilePicker.SuggestedSaveFileNames);

        var save = Assert.Single(harness.DocumentSaver.Saves);
        Assert.Equal(savedAsPath, save.Path);
        Assert.Equal("first updated", harness.ViewModel.Document!.Content);
        Assert.Equal(savedAsPath, harness.ViewModel.Document.Path);
        Assert.Equal("renamed.md", harness.ViewModel.FileName);
        Assert.False(harness.ViewModel.IsDirty);
    }

    [Fact]
    public async Task InitializeAsyncLoadsSavedLanguageAndLocalizesShellLabels()
    {
        var harness = CreateHarness();
        harness.Settings.Language = AppLanguage.Russian;

        await harness.ViewModel.InitializeAsync();

        Assert.True(harness.ViewModel.IsRussianLanguageSelected);
        Assert.Equal("Переключить режим редактирования (Ctrl+E)", harness.ViewModel.EditToggleTooltip);
        Assert.Equal("Проверить обновления…", harness.ViewModel.AppMenuCheckForUpdates);
    }

    [Fact]
    public void SelectRussianLanguageCommandPersistsLanguageAndRefreshesComputedLabels()
    {
        var harness = CreateHarness();

        harness.ViewModel.SelectRussianLanguageCommand.Execute(null);

        Assert.Equal(AppLanguage.Russian, harness.Settings.Language);
        Assert.True(harness.ViewModel.IsRussianLanguageSelected);
        Assert.Equal("Проверить обновления…", harness.ViewModel.AppMenuCheckForUpdates);
        Assert.Equal("0 слов · 1 мин", harness.ViewModel.ReadingStatusLabel);
    }

    [Fact]
    public void SelectedLanguageOptionPersistsLanguageAndRefreshesDropdownLabels()
    {
        var harness = CreateHarness();
        var options = harness.ViewModel.LanguageOptions;
        var englishOption = options.Single(option => option.Language == AppLanguage.English);
        var russianOption = options.Single(option => option.Language == AppLanguage.Russian);
        var relabelled = new List<AppLanguage>();
        foreach (var option in options)
        {
            option.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LanguageSelectionItem.Label))
                {
                    relabelled.Add(option.Language);
                }
            };
        }

        harness.ViewModel.SelectedLanguageOption = russianOption;

        Assert.Equal(AppLanguage.Russian, harness.Settings.Language);
        Assert.Same(options, harness.ViewModel.LanguageOptions);
        Assert.Same(russianOption, harness.ViewModel.SelectedLanguageOption);
        Assert.Equal("Английский", englishOption.Label);
        Assert.Contains(AppLanguage.English, relabelled);
        Assert.Equal("0 слов · 1 мин", harness.ViewModel.ReadingStatusLabel);
    }


    [Fact]
    public void SelectedLanguageOptionRaisesTypedNotificationsForVisibleShellBindings()
    {
        var harness = CreateHarness();
        var names = new List<string?>();
        harness.ViewModel.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        var russianOption = harness.ViewModel.LanguageOptions.Single(option => option.Language == AppLanguage.Russian);

        harness.ViewModel.SelectedLanguageOption = russianOption;

        Assert.Contains(nameof(ShellViewModel.WelcomeTagline), names);
        Assert.Contains(nameof(ShellViewModel.AppMenuSettings), names);
        Assert.Contains(nameof(ShellViewModel.AppMenuCheckForUpdates), names);
        Assert.Contains(nameof(ShellViewModel.SelectedLanguageOption), names);
        // Список языков не подменяется: ComboBox не должен терять выбор посреди записи.
        Assert.DoesNotContain(nameof(ShellViewModel.LanguageOptions), names);
        Assert.DoesNotContain("Item", names);
        Assert.DoesNotContain("Item[]", names);
        Assert.Equal("Тихое место для чтения Markdown.", harness.ViewModel.WelcomeTagline);
        Assert.Equal("Настройки…", harness.ViewModel.AppMenuSettings);
        Assert.Equal("Проверить обновления…", harness.ViewModel.AppMenuCheckForUpdates);
    }

    [Fact]
    public void LanguageOptionsKeepsStableItemReferencesBetweenLocalizationChanges()
    {
        var harness = CreateHarness();
        var options = harness.ViewModel.LanguageOptions;
        var systemOption = options.Single(option => option.Language == AppLanguage.System);

        Assert.Same(systemOption, harness.ViewModel.SelectedLanguageOption);

        harness.ViewModel.SelectRussianLanguageCommand.Execute(null);
        harness.ViewModel.SelectSystemLanguageCommand.Execute(null);

        Assert.Same(options, harness.ViewModel.LanguageOptions);
        Assert.Same(systemOption, harness.ViewModel.SelectedLanguageOption);
        // Равенство по ссылке: пункт с тем же языком и подписью — другой пункт.
        Assert.NotEqual(new LanguageSelectionItem(systemOption.Language, systemOption.Label), systemOption);
    }

    [Theory]
    [InlineData("macOS", "Toggle edit mode (⌘E)", "Find in document (⌘F)", "⌘O", "⇧⌘O", "⌘B")]
    [InlineData("Windows", "Toggle edit mode (Ctrl+E)", "Find in document (Ctrl+F)", "Ctrl+O", "Ctrl+Shift+O", "Ctrl+B")]
    [InlineData("Linux", "Toggle edit mode (Ctrl+E)", "Find in document (Ctrl+F)", "Ctrl+O", "Ctrl+Shift+O", "Ctrl+B")]
    public void ShortcutLabelsFollowPlatform(
        string platformName,
        string editTooltip,
        string findTooltip,
        string openFile,
        string openFolder,
        string toggleSidebar)
    {
        var viewModel = CreateHarness(platformName: platformName).ViewModel;

        Assert.Equal(editTooltip, viewModel.EditToggleTooltip);
        Assert.Equal(findTooltip, viewModel.FindToggleTooltip);
        Assert.Equal(openFile, viewModel.OpenFileShortcut);
        Assert.Equal(openFolder, viewModel.OpenFolderShortcut);
        Assert.Equal(toggleSidebar, viewModel.ToggleSidebarShortcut);
    }

    [Theory]
    [InlineData("macOS", "Previous match (⇧↵)", "Next match (↵)", "Close search (⎋)")]
    [InlineData("Windows", "Previous match (Shift+Enter)", "Next match (Enter)", "Close search (Esc)")]
    [InlineData("Linux", "Previous match (Shift+Enter)", "Next match (Enter)", "Close search (Esc)")]
    public void FindCardShortcutLabelsFollowPlatform(
        string platformName,
        string previousTooltip,
        string nextTooltip,
        string closeTooltip)
    {
        var viewModel = CreateHarness(platformName: platformName).ViewModel;

        Assert.Equal(previousTooltip, viewModel.FindPreviousTooltip);
        Assert.Equal(nextTooltip, viewModel.FindNextTooltip);
        Assert.Equal(closeTooltip, viewModel.FindCloseTooltip);
    }

    [Fact]
    public void ShortcutTooltipsKeepShortcutAfterLanguageChange()
    {
        var viewModel = CreateHarness(platformName: "macOS").ViewModel;

        viewModel.SelectRussianLanguageCommand.Execute(null);

        Assert.Equal("Переключить режим редактирования (⌘E)", viewModel.EditToggleTooltip);
        Assert.Equal("Найти в документе (⌘F)", viewModel.FindToggleTooltip);
        Assert.Equal("Предыдущее совпадение (⇧↵)", viewModel.FindPreviousTooltip);
        Assert.Equal("Закрыть поиск (⎋)", viewModel.FindCloseTooltip);
        Assert.Equal("Вид: тема, шрифт, размер", viewModel.ReadingSettingsTooltip);
        Assert.Equal("Меньше (⌘-)", viewModel.ReadingSizeDecreaseTooltip);
        Assert.Equal("Больше (⌘+)", viewModel.ReadingSizeIncreaseTooltip);
        Assert.Equal("Готово", viewModel.EditDoneLabel);
        Assert.Equal("Закончить правку (⌘E)", viewModel.EditDoneTooltip);
        Assert.Equal("Не сохранено", viewModel.EditUnsavedLabel);
    }

    [Theory]
    [InlineData("macOS", "Finish editing (⌘E)", "⌘ S")]
    [InlineData("Windows", "Finish editing (Ctrl+E)", "Ctrl S")]
    [InlineData("Linux", "Finish editing (Ctrl+E)", "Ctrl S")]
    public void EditActionShortcutLabelsFollowPlatform(string platformName, string doneTooltip, string saveShortcut)
    {
        var viewModel = CreateHarness(platformName: platformName).ViewModel;

        Assert.Equal("Done", viewModel.EditDoneLabel);
        Assert.Equal(doneTooltip, viewModel.EditDoneTooltip);
        Assert.Equal("Unsaved", viewModel.EditUnsavedLabel);
        Assert.Equal(saveShortcut.Split(' '), viewModel.SaveShortcutKeys);
    }

    [Theory]
    [InlineData("macOS", "Smaller (⌘-)", "Larger (⌘+)", "⌘ ,")]
    [InlineData("Windows", "Smaller (Ctrl+-)", "Larger (Ctrl++)", "Ctrl ,")]
    [InlineData("Linux", "Smaller (Ctrl+-)", "Larger (Ctrl++)", "Ctrl ,")]
    public void ReadingCardShortcutLabelsFollowPlatform(
        string platformName,
        string decreaseTooltip,
        string increaseTooltip,
        string settingsShortcut)
    {
        var viewModel = CreateHarness(platformName: platformName).ViewModel;

        // ⌘, открывает настройки приложения, а не карточку: у кнопки Aa сочетания нет.
        Assert.Equal("View: theme, font, size", viewModel.ReadingSettingsTooltip);

        Assert.Equal(decreaseTooltip, viewModel.ReadingSizeDecreaseTooltip);
        Assert.Equal(increaseTooltip, viewModel.ReadingSizeIncreaseTooltip);
        Assert.Equal(settingsShortcut.Split(' '), viewModel.SettingsShortcutKeys);
    }

    [Fact]
    public async Task TextSizeCommandsStepByOnePixelAndPersistLikeTheSlider()
    {
        var harness = CreateHarness();
        await OpenSampleAsync(harness);
        var viewModel = harness.ViewModel;

        Assert.Equal(14, viewModel.ReadingPreferences.FontSize);
        Assert.Equal("14 px", viewModel.FontSizeLabel);

        viewModel.IncreaseTextSizeCommand.Execute(null);
        Assert.Equal(15, viewModel.ReadingPreferences.FontSize);
        Assert.Equal(15, viewModel.FontSizeSetting);
        Assert.Equal("15 px", viewModel.FontSizeLabel);
        await WaitUntilAsync(() => harness.Settings.Preferences.FontSize == 15);

        // Каждое сохранение идёт в фоне отдельной задачей: ждём его перед следующим
        // шагом, иначе запись «14» могла бы лечь после «13».
        viewModel.DecreaseTextSizeCommand.Execute(null);
        Assert.Equal(14, viewModel.ReadingPreferences.FontSize);
        await WaitUntilAsync(() => harness.Settings.Preferences.FontSize == 14);

        viewModel.DecreaseTextSizeCommand.Execute(null);
        Assert.Equal(13, viewModel.ReadingPreferences.FontSize);
        await WaitUntilAsync(() => harness.Settings.Preferences.FontSize == 13);

        viewModel.ResetTextSizeCommand.Execute(null);
        Assert.Equal(ReadingPreferences.Default.FontSize, viewModel.ReadingPreferences.FontSize);
        await WaitUntilAsync(() => harness.Settings.Preferences.FontSize == 14);
    }

    [Fact]
    public async Task TextSizeCommandsDoNothingAtTheBoundsOfTheRange()
    {
        var harness = CreateHarness();
        await OpenSampleAsync(harness);
        var viewModel = harness.ViewModel;

        viewModel.FontSizeSetting = ReadingPreferences.MaxFontSize;
        Assert.False(viewModel.IncreaseTextSizeCommand.CanExecute(null));
        Assert.True(viewModel.DecreaseTextSizeCommand.CanExecute(null));
        viewModel.IncreaseTextSizeCommand.Execute(null);
        Assert.Equal(24, viewModel.ReadingPreferences.FontSize);

        viewModel.FontSizeSetting = ReadingPreferences.MinFontSize;
        Assert.False(viewModel.DecreaseTextSizeCommand.CanExecute(null));
        Assert.True(viewModel.IncreaseTextSizeCommand.CanExecute(null));
        viewModel.DecreaseTextSizeCommand.Execute(null);
        Assert.Equal(12, viewModel.ReadingPreferences.FontSize);

        viewModel.ResetTextSizeCommand.Execute(null);
        Assert.Equal(14, viewModel.ReadingPreferences.FontSize);
        Assert.False(viewModel.ResetTextSizeCommand.CanExecute(null));
    }

    [Fact]
    public async Task TextSizeCommandsWorkInEditModeButNotWithoutADocument()
    {
        var harness = CreateHarness();
        var viewModel = harness.ViewModel;

        Assert.False(viewModel.IncreaseTextSizeCommand.CanExecute(null));
        Assert.False(viewModel.DecreaseTextSizeCommand.CanExecute(null));

        await OpenSampleAsync(harness);
        await viewModel.ToggleEditModeCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsEditMode);

        viewModel.IncreaseTextSizeCommand.Execute(null);

        Assert.Equal(15, viewModel.ReadingPreferences.FontSize);
        Assert.Equal(15, viewModel.EditorSession!.ReadingPreferences.FontSize);
    }

    [Fact]
    public async Task ReadingSettingsToggleIsHiddenInEditModeAndTheCardCloses()
    {
        var harness = CreateHarness();
        await OpenSampleAsync(harness);
        var viewModel = harness.ViewModel;

        Assert.True(viewModel.ShowsReadingSettingsToggle);
        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.Same(viewModel, viewModel.ReadingSettingsOverlayContent);

        await viewModel.ToggleEditModeCommand.ExecuteAsync(null);

        Assert.False(viewModel.ShowsReadingSettingsToggle);
        Assert.False(viewModel.IsSettingsOpen);
        Assert.Null(viewModel.ReadingSettingsOverlayContent);

        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.False(viewModel.IsSettingsOpen);
    }

    [Fact]
    public async Task SettingsShortcutTogglesTheSettingsWindowEverywhere()
    {
        var harness = CreateHarness();
        var viewModel = harness.ViewModel;

        // Стартовый экран: документа нет, настройки всё равно открываются.
        viewModel.ToggleAppSettingsCommand.Execute(null);
        Assert.True(viewModel.IsAppSettingsOpen);
        viewModel.ToggleAppSettingsCommand.Execute(null);
        Assert.False(viewModel.HasOpenOverlay);

        // Из открытой карточки Aa — туда же, куда ведёт её нижняя строка.
        await OpenSampleAsync(harness);
        viewModel.ToggleSettingsCommand.Execute(null);
        viewModel.ToggleAppSettingsCommand.Execute(null);
        Assert.False(viewModel.IsSettingsOpen);
        Assert.True(viewModel.IsAppSettingsOpen);

        // В правке тоже: меню ⋯ стоит в строке окна и там (ADR-0009 Rules 2, 7).
        viewModel.CloseOverlayCommand.Execute(null);
        await viewModel.ToggleEditModeCommand.ExecuteAsync(null);
        viewModel.ToggleAppSettingsCommand.Execute(null);
        Assert.True(viewModel.IsAppSettingsOpen);
        Assert.True(viewModel.IsEditMode);
    }

    [Theory]
    [InlineData(ThemeMode.System)]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public async Task ThemeSelectionAppliesAndPersistsEachOfTheThreeModes(ThemeMode mode)
    {
        var harness = CreateHarness();
        harness.Settings.Theme = mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        var viewModel = harness.ViewModel;
        await viewModel.InitializeAsync();

        switch (mode)
        {
            case ThemeMode.System:
                viewModel.IsSystemThemeSelected = true;
                break;
            case ThemeMode.Light:
                viewModel.IsLightThemeSelected = true;
                break;
            default:
                viewModel.IsDarkThemeSelected = true;
                break;
        }

        Assert.Equal(mode, viewModel.Theme);
        Assert.Equal(mode, harness.ThemeService.AppliedTheme);
        Assert.Equal(mode == ThemeMode.System, viewModel.IsSystemThemeSelected);
        Assert.Equal(mode == ThemeMode.Light, viewModel.IsLightThemeSelected);
        Assert.Equal(mode == ThemeMode.Dark, viewModel.IsDarkThemeSelected);
        await WaitUntilAsync(() => harness.Settings.Theme == mode);
    }

    [Fact]
    public async Task UncheckingTheSelectedThemeKeepsIt()
    {
        var harness = CreateHarness();
        harness.Settings.Theme = ThemeMode.Dark;
        var viewModel = harness.ViewModel;
        await viewModel.InitializeAsync();

        viewModel.IsDarkThemeSelected = false;

        Assert.Equal(ThemeMode.Dark, viewModel.Theme);
        Assert.True(viewModel.IsDarkThemeSelected);
        Assert.Equal(ThemeMode.Dark, harness.ThemeService.AppliedTheme);
    }

    [Theory]
    [InlineData("""{"theme":"Dark","language":"English"}""", ThemeMode.Dark)]
    [InlineData("""{"theme":"Light"}""", ThemeMode.Light)]
    [InlineData("""{"theme":"System"}""", ThemeMode.System)]
    [InlineData("""{"language":"English"}""", ThemeMode.System)]
    public async Task ThemeFromAnExistingSettingsFileIsSelectedOnStart(string settingsJson, ThemeMode expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "theme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), settingsJson);
            var themeService = new RecordingThemeService();
            var viewModel = CreateHarness(
                settingsStore: new MarkMello.Infrastructure.Settings.JsonSettingsStore(directory),
                themeService: themeService).ViewModel;

            await viewModel.InitializeAsync();

            Assert.Equal(expected, viewModel.Theme);
            Assert.Equal(expected, themeService.AppliedTheme);
            Assert.Equal(expected == ThemeMode.System, viewModel.IsSystemThemeSelected);
            Assert.Equal(expected == ThemeMode.Light, viewModel.IsLightThemeSelected);
            Assert.Equal(expected == ThemeMode.Dark, viewModel.IsDarkThemeSelected);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task OpenSampleAsync(TestHarness harness)
    {
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "one.md");
        harness.Loader.Sources[path] = CreateSource(path, "alpha beta");
        await harness.ViewModel.OpenPathAsync(path);
        Assert.True(harness.ViewModel.IsViewer);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition was not met in time.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task CodeIsHighlightedAfterTheDocumentIsShown()
    {
        var highlighter = new GatedCodeHighlighter();
        var harness = CreateHarness(
            markdownRenderer: new MarkMello.Infrastructure.Markdown.MarkdigMarkdownDocumentRenderer(),
            highlightCodeBlocks: new HighlightCodeBlocksUseCase(highlighter));
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "code.md");
        harness.Loader.Sources[path] = CreateSource(path, "# Code\n\n```cs\nvar x = 1;\n```");
        var highlighted = WaitForHighlightedDocument(harness.ViewModel);

        await harness.ViewModel.OpenPathAsync(path);

        // Документ показан сразу, без цветов; разбор ждёт, пока view его нарисует.
        Assert.Null(SingleCodeBlock(harness.ViewModel.RenderedDocument).Tokens);
        Assert.Null(harness.ViewModel.CodeHighlighting);
        harness.ViewModel.StartPendingCodeHighlighting();
        Assert.NotNull(harness.ViewModel.CodeHighlighting);
        highlighter.Release();

        var document = await highlighted.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(SingleCodeBlock(document).Tokens);
        Assert.Same(document, harness.ViewModel.OpenDocuments.ActiveTab!.RenderedDocument);
    }

    [Fact]
    public async Task OpeningAnotherDocumentCancelsHighlighting()
    {
        var highlighter = new GatedCodeHighlighter();
        var harness = CreateHarness(
            markdownRenderer: new MarkMello.Infrastructure.Markdown.MarkdigMarkdownDocumentRenderer(),
            highlightCodeBlocks: new HighlightCodeBlocksUseCase(highlighter));
        var codePath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "code.md");
        var textPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "text.md");
        harness.Loader.Sources[codePath] = CreateSource(codePath, "```cs\nvar x = 1;\n```");
        harness.Loader.Sources[textPath] = CreateSource(textPath, "Just text.");

        await harness.ViewModel.OpenPathAsync(codePath);
        harness.ViewModel.StartPendingCodeHighlighting();
        var cancellation = await highlighter.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        var codeTab = harness.ViewModel.OpenDocuments.ActiveTab!;
        var codeDocument = codeTab.RenderedDocument;
        await harness.ViewModel.OpenPathAsync(textPath);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.IsType<MarkdownParagraphBlock>(Assert.Single(harness.ViewModel.RenderedDocument.Blocks));
        Assert.Same(codeDocument, codeTab.RenderedDocument);
    }

    [Fact]
    public async Task DocumentWithoutLabelledCodeDoesNotStartHighlighting()
    {
        var highlighter = new GatedCodeHighlighter();
        var harness = CreateHarness(
            markdownRenderer: new MarkMello.Infrastructure.Markdown.MarkdigMarkdownDocumentRenderer(),
            highlightCodeBlocks: new HighlightCodeBlocksUseCase(highlighter));
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "plain.md");
        harness.Loader.Sources[path] = CreateSource(path, "# Title\n\n```\nno label\n```");

        await harness.ViewModel.OpenPathAsync(path);
        harness.ViewModel.StartPendingCodeHighlighting();

        Assert.Null(harness.ViewModel.CodeHighlighting);
        Assert.False(highlighter.Entered.IsCompleted);
    }

    [Fact]
    public async Task DocumentReplacedBeforeItWasDrawnIsNotHighlighted()
    {
        var highlighter = new GatedCodeHighlighter();
        var harness = CreateHarness(
            markdownRenderer: new MarkMello.Infrastructure.Markdown.MarkdigMarkdownDocumentRenderer(),
            highlightCodeBlocks: new HighlightCodeBlocksUseCase(highlighter));
        var codePath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "code.md");
        var textPath = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "text.md");
        harness.Loader.Sources[codePath] = CreateSource(codePath, "```cs\nvar x = 1;\n```");
        harness.Loader.Sources[textPath] = CreateSource(textPath, "Just text.");

        await harness.ViewModel.OpenPathAsync(codePath);
        await harness.ViewModel.OpenPathAsync(textPath);
        harness.ViewModel.StartPendingCodeHighlighting();

        Assert.Null(harness.ViewModel.CodeHighlighting);
        Assert.False(highlighter.Entered.IsCompleted);
    }

    [Fact]
    public async Task RecolorIsReportedOnceWhileTheHighlightedDocumentIsApplied()
    {
        var highlighter = new GatedCodeHighlighter();
        highlighter.Release();
        var harness = CreateHarness(
            markdownRenderer: new MarkMello.Infrastructure.Markdown.MarkdigMarkdownDocumentRenderer(),
            highlightCodeBlocks: new HighlightCodeBlocksUseCase(highlighter));
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "code.md");
        harness.Loader.Sources[path] = CreateSource(path, "```cs\nvar x = 1;\n```");
        await harness.ViewModel.OpenPathAsync(path);
        var plain = harness.ViewModel.RenderedDocument;
        var answers = new List<bool>();
        harness.ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ShellViewModel.RenderedDocument))
            {
                // Так спрашивает view в начале пересборки — дважды, как при повторе.
                answers.Add(harness.ViewModel.ConsumeRecolor(harness.ViewModel.RenderedDocument));
                answers.Add(harness.ViewModel.ConsumeRecolor(harness.ViewModel.RenderedDocument));
            }
        };

        Assert.False(harness.ViewModel.ConsumeRecolor(plain));
        harness.ViewModel.StartPendingCodeHighlighting();
        await harness.ViewModel.CodeHighlighting!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([true, false], answers);
        Assert.False(harness.ViewModel.ConsumeRecolor(harness.ViewModel.RenderedDocument));
    }

    private static Task<RenderedMarkdownDocument> WaitForHighlightedDocument(ShellViewModel viewModel)
    {
        var completion = new TaskCompletionSource<RenderedMarkdownDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ShellViewModel.RenderedDocument)
                && viewModel.RenderedDocument.Blocks.OfType<MarkdownCodeBlock>().Any(block => block.Tokens is not null))
            {
                completion.TrySetResult(viewModel.RenderedDocument);
            }
        };
        return completion.Task;
    }

    private static MarkdownCodeBlock SingleCodeBlock(RenderedMarkdownDocument document)
        => document.Blocks.OfType<MarkdownCodeBlock>().Single();

    /// <summary>
    /// Движок, который держит разбор, пока тест его не отпустит, и отдаёт
    /// тесту токен отмены первого вызова.
    /// </summary>
    private sealed class GatedCodeHighlighter : MarkMello.Application.Abstractions.ICodeHighlighter
    {
        private readonly TaskCompletionSource<CancellationToken> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CancellationToken> Entered => _entered.Task;

        public void Release() => _gate.TrySetResult();

        public IReadOnlyList<MarkdownCodeToken>? Highlight(
            string language,
            string code,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult(cancellationToken);
            _gate.Task.Wait(cancellationToken);
            return [new MarkdownCodeToken(0, code.Length, MarkdownCodeTokenKind.Keyword)];
        }
    }

    private static MarkdownSource CreateSource(string path, string content)
        => new(path, Path.GetFileName(path), content);

    private static TestHarness CreateHarness(
        FakeWorkspaceFileSystem? workspaceFileSystem = null,
        string platformName = "Windows",
        MarkMello.Application.Abstractions.ISettingsStore? settingsStore = null,
        RecordingThemeService? themeService = null,
        MarkMello.Application.Abstractions.IMarkdownDocumentRenderer? markdownRenderer = null,
        HighlightCodeBlocksUseCase? highlightCodeBlocks = null)
    {
        var loader = new StubDocumentLoader();
        var saver = new RecordingDocumentSaver();
        var picker = new StubFilePicker();
        var settings = new InMemorySettingsStore();
        var localization = new LocalizationService(AppLanguage.English);
        themeService ??= new RecordingThemeService();
        var startupMetrics = new RecordingStartupMetrics();
        var updateService = new StubUpdateService();
        var commandLine = new StubCommandLineActivation();
        var fileSystem = workspaceFileSystem ?? new FakeWorkspaceFileSystem();
        var viewModel = new ShellViewModel(
            new OpenDocumentUseCase(loader),
            new SaveDocumentUseCase(saver),
            picker,
            commandLine,
            localization,
            settingsStore ?? settings,
            themeService,
            startupMetrics,
            new RenderMarkdownDocumentUseCase(markdownRenderer ?? new TestMarkdownRenderer(), new FakeDiagramRenderService()),
            TestUpdates.CreateViewModel(updateService),
            new OpenFolderUseCase(fileSystem),
            new ExpandFolderNodeUseCase(fileSystem),
            new SearchWorkspaceFilesUseCase(fileSystem),
            new WorkspaceFileOperationsUseCase(fileSystem, new FakePlatformServices()),
            new FakePlatformServices { PlatformName = platformName },
            static () => new FakeWorkspaceWatcher(),
            new RecordingWindowLauncher(),
            highlightCodeBlocks: highlightCodeBlocks);

        return new TestHarness(
            loader,
            saver,
            picker,
            settings,
            themeService,
            startupMetrics,
            updateService,
            commandLine,
            fileSystem,
            viewModel);
    }

    private sealed record TestHarness(
        StubDocumentLoader Loader,
        RecordingDocumentSaver DocumentSaver,
        StubFilePicker FilePicker,
        InMemorySettingsStore Settings,
        RecordingThemeService ThemeService,
        RecordingStartupMetrics StartupMetrics,
        StubUpdateService UpdateService,
        StubCommandLineActivation CommandLine,
        FakeWorkspaceFileSystem WorkspaceFileSystem,
        ShellViewModel ViewModel);
}
