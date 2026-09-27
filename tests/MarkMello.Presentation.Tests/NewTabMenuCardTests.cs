using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// «+» у вкладок открывает не черновик, а меню «Открыть файл… / Новый документ»
/// (ADR-0009 Rule 3) — карточку внутри окна, как «ещё N» и меню сайдбара (Rule 4):
/// под кнопкой по её левому краю, открытие и закрытие через <see cref="ShellOverlayKind"/>.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class NewTabMenuCardTests
{
    private const string Panel = "NewTabMenuPanel";

    private static readonly string Root = TestPaths.At("docs");
    private static readonly string Picked = TestPaths.At("docs", "picked.md");

    // Шестнадцать вкладок в окне 900: часть уходит в «ещё N», «+» стоит у правого края полосы.
    private static readonly string[] Documents = [.. Enumerable.Range(1, 16).Select(index => TestPaths.At("docs", $"chapter-{index}.md"))];

    private readonly AvaloniaHeadlessFixture _fixture;

    public NewTabMenuCardTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Клик по «+» документ не создаёт — открывает карточку из двух пунктов с подписями
    /// клавиш, как в ⋯. Тултипа у «+» нет. Карточка — та же поверхность, что у меню, в обеих темах.
    /// </summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task PlusOpensTheMenuInsteadOfADraft(string theme)
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, _) = await ShowAsync(documents: 1, theme: theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);
            var plus = PlusButton(window);

            Assert.Null(ToolTip.GetTip(plus));
            Assert.Null(window.GetControl<ContentControl>(Panel).Content);

            Click(window, plus);

            Assert.True(viewModel.IsNewTabMenuOpen);
            Assert.Single(viewModel.OpenDocuments.Tabs);
            Assert.Contains("open", plus.Classes);
            Assert.Equal(["NewTabMenuOpenFile", "NewTabMenuNewDocument"], Items(window).Select(static button => button.Name));
            Assert.Equal([viewModel.AppMenuOpenFile, viewModel.AppMenuNewDocument], Items(window).Select(Label));
            Assert.Equal([viewModel.OpenFileShortcutKeys, viewModel.NewDocumentShortcutKeys], Items(window).Select(Shortcut));

            var card = Card(window);
            Assert.Equal(Resource(window, "MmCardShadow"), card.BoxShadow);
            Assert.Same(Resource(window, "MmElevatedBackgroundBrush"), card.Background);
            Assert.Same(Resource(window, "MmBorderBrush"), card.BorderBrush);

            AssertCardUnderLeftEdgeOf(window, plus);

            // Пока карточка открыта, «+» держит заливку активной и без курсора над собой —
            // как «ещё N» и ⋯ (ADR-0009 Rule 2).
            window.MouseMove(new Point(window.Bounds.Width / 2, window.Bounds.Height - 40));
            Render(window);
            Assert.Same(Resource(window, "MmTabActiveBrush"), Presenter(plus).Background);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render(window);
            Assert.NotSame(Resource(window, "MmTabActiveBrush"), Presenter(plus).Background);

            window.Hide();
        });
    }

    /// <summary>«Новый документ» закрывает меню и создаёт черновик в новой вкладке — как ⌘N.</summary>
    [Fact]
    public Task NewDocumentItemCreatesADraft()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, _) = await ShowAsync(documents: 3);

            Click(window, PlusButton(window));
            Click(window, Item(window, "NewTabMenuNewDocument"));

            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.Null(window.GetControl<ContentControl>(Panel).Content);
            Assert.Equal(4, viewModel.OpenDocuments.Tabs.Count);
            Assert.True(viewModel.OpenDocuments.ActiveTab!.Path is null);

            window.Hide();
        });
    }

    /// <summary>
    /// «Открыть файл…» закрывает меню и спрашивает файл, как ⌘O: выбранный открывается
    /// в новой вкладке, отмена ничего не меняет, уже открытый становится активным.
    /// </summary>
    [Fact]
    public Task OpenFileItemPicksAFileLikeCommandO()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, picker) = await ShowAsync(documents: 3);

            picker.OpenPath = null;
            Click(window, PlusButton(window));
            Click(window, Item(window, "NewTabMenuOpenFile"));

            Assert.Equal(1, picker.PickMarkdownFileCallCount);
            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.Equal(3, viewModel.OpenDocuments.Tabs.Count);
            Assert.Equal(Documents[2], viewModel.OpenDocuments.ActiveTab!.Path);

            picker.OpenPath = Picked;
            Click(window, PlusButton(window));
            Click(window, Item(window, "NewTabMenuOpenFile"));

            Assert.Equal(2, picker.PickMarkdownFileCallCount);
            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.Equal(4, viewModel.OpenDocuments.Tabs.Count);
            Assert.Equal(Picked, viewModel.OpenDocuments.ActiveTab!.Path);

            picker.OpenPath = Documents[0];
            Click(window, PlusButton(window));
            Click(window, Item(window, "NewTabMenuOpenFile"));

            Assert.Equal(4, viewModel.OpenDocuments.Tabs.Count);
            Assert.Equal(Documents[0], viewModel.OpenDocuments.ActiveTab!.Path);

            window.Hide();
        });
    }

    /// <summary>
    /// Как у остальных карточек: повторный клик, Esc и клик мимо закрывают; другое меню
    /// сменяет «+», и «+» сменяет его — открыт всегда один оверлей.
    /// </summary>
    [Fact]
    public Task CardClosesOnSecondClickEscapeAndClickOutside()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, _) = await ShowAsync(documents: Documents.Length);
            var plus = PlusButton(window);

            Click(window, plus);
            Assert.True(viewModel.IsNewTabMenuOpen);
            Click(window, plus);
            Assert.False(viewModel.IsNewTabMenuOpen);

            Click(window, plus);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render(window);
            Assert.False(viewModel.IsNewTabMenuOpen);

            Click(window, plus);
            Click(window, new Point(window.Bounds.Width / 2, window.Bounds.Height - 40));
            Assert.False(viewModel.IsNewTabMenuOpen);

            // «ещё N» сменяет «+», и «+» сменяет «ещё N».
            Click(window, plus);
            Click(window, Trigger(window, "TabsOverflowButton"));
            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.True(viewModel.IsTabsOverflowMenuOpen);

            Click(window, plus);
            Assert.True(viewModel.IsNewTabMenuOpen);
            Assert.False(viewModel.IsTabsOverflowMenuOpen);

            // И ⋯ — тоже.
            Click(window, Trigger(window, "AppMenuTriggerButton"));
            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.True(viewModel.IsAppMenuOpen);

            Click(window, plus);
            Assert.True(viewModel.IsNewTabMenuOpen);
            Assert.False(viewModel.IsAppMenuOpen);

            window.Hide();
        });
    }

    /// <summary>
    /// «+» в фокусе открывает меню по Enter и по пробелу с фокусом на первом пункте;
    /// стрелки ходят по пунктам по кругу, Enter выполняет, после Esc фокус снова на «+».
    /// </summary>
    [Fact]
    public Task KeyboardOpensWalksAndRunsTheItems()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, _) = await ShowAsync(documents: 3);
            var plus = PlusButton(window);
            Assert.True(plus.Focus());

            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Render(window);

            Assert.True(viewModel.IsNewTabMenuOpen);
            Assert.True(Item(window, "NewTabMenuOpenFile").IsFocused);

            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            Render(window);
            Assert.True(Item(window, "NewTabMenuNewDocument").IsFocused);

            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            Render(window);
            Assert.True(Item(window, "NewTabMenuOpenFile").IsFocused);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render(window);

            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.True(plus.IsFocused);

            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            Render(window);

            Assert.True(viewModel.IsNewTabMenuOpen);
            Assert.True(Item(window, "NewTabMenuOpenFile").IsFocused);

            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
            Render(window);
            Assert.True(Item(window, "NewTabMenuNewDocument").IsFocused);

            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Render(window);

            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.Equal(4, viewModel.OpenDocuments.Tabs.Count);
            Assert.True(viewModel.OpenDocuments.ActiveTab!.Path is null);

            window.Hide();
        });
    }

    /// <summary>Мышь фокус «+» не отдаёт: он остаётся в документе, как у вкладок и «ещё N».</summary>
    [Fact]
    public Task ClickDoesNotTakeTheFocus()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, _, _) = await ShowAsync(documents: 3);
            var plus = PlusButton(window);

            Click(window, plus);
            Assert.False(plus.IsFocused);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render(window);
            Assert.False(plus.IsFocused);

            window.Hide();
        });
    }

    /// <summary>Под вопросом о правках меню не открывается: ни один его пункт там не сработает.</summary>
    [Fact]
    public Task MenuDoesNotOpenUnderAModalDialog()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, _) = await ShowAsync(documents: 1);

            await viewModel.CreateNewDocumentCommand.ExecuteAsync(null);
            viewModel.EditorSession!.SourceText = "# draft";
            await viewModel.CloseActiveTabCommand.ExecuteAsync(null);
            Render(window);
            Assert.True(viewModel.IsDirtyPromptOpen);

            viewModel.ToggleNewTabMenuCommand.Execute(null);
            Render(window);

            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.False(viewModel.HasOpenOverlay);
            Assert.Null(window.GetControl<ContentControl>(Panel).Content);

            window.Hide();
        });
    }

    /// <summary>
    /// ⌘W из карточки закрыл последнюю вкладку — строки с «+» больше нет, и меню
    /// закрывается само, а не висит без кнопки.
    /// </summary>
    [Fact]
    public Task MenuClosesWhenThePlusIsGone()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, _) = await ShowAsync(documents: 1);

            Click(window, PlusButton(window));
            Assert.True(viewModel.IsNewTabMenuOpen);

            await viewModel.CloseActiveTabCommand.ExecuteAsync(null);
            Render(window);

            Assert.False(viewModel.ShowsTabStrip);
            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.Null(window.GetControl<ContentControl>(Panel).Content);

            window.Hide();
        });
    }

    /// <summary>
    /// ⌘W из карточки поднял вопрос о правках — меню под ним закрывается: одновременно
    /// открыт не больше одного оверлея (ADR-0009 Rule 4).
    /// </summary>
    [Fact]
    public Task DirtyPromptClosesTheMenu()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, _) = await ShowAsync(documents: 1);
            await viewModel.CreateNewDocumentCommand.ExecuteAsync(null);
            viewModel.EditorSession!.SourceText = "# draft";
            Render(window);

            Click(window, PlusButton(window));
            Assert.True(viewModel.IsNewTabMenuOpen);

            await viewModel.CloseActiveTabCommand.ExecuteAsync(null);
            Render(window);

            Assert.True(viewModel.IsDirtyPromptOpen);
            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.Null(window.GetControl<ContentControl>(Panel).Content);

            window.Hide();
        });
    }

    /// <summary>Переход в правку меняет окно под карточками — меню «+» закрывается, как остальные.</summary>
    [Fact]
    public Task EnteringEditModeClosesTheMenu()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, _) = await ShowAsync(documents: 1);

            Click(window, PlusButton(window));
            Assert.True(viewModel.IsNewTabMenuOpen);

            await viewModel.ToggleEditModeCommand.ExecuteAsync(null);
            Render(window);

            Assert.True(viewModel.IsEditMode);
            Assert.False(viewModel.IsNewTabMenuOpen);

            window.Hide();
        });
    }

    /// <summary>
    /// Карточка встаёт под «+» левым краем, а у правого края окна сдвигается внутрь,
    /// не обрезаясь. Четыре вкладки в окне 900 сжимаются, но не уходят в «ещё N» —
    /// заполняют полосу целиком, и «+» стоит у кнопок строки справа. При переполнении
    /// «+» идёт сразу за видимыми вкладками, и зазор до края полосы зависел бы от
    /// отступов строки на каждой ОС.
    /// </summary>
    [FactSkippedOnWindows(
        "На Windows правее кнопок строки стоят кнопки окна: от «+» до края окна остаётся "
        + "больше ширины карточки, и сдвигать её внутрь не приходится.")]
    public Task CardShiftsInsideAtTheRightEdgeOfTheWindow()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, _) = await ShowAsync(documents: 4);
            Assert.False(viewModel.OpenDocuments.HasOverflow);
            Assert.True(viewModel.OpenDocuments.TabWidth < OpenDocumentsViewModel.PreferredTabWidth);
            var plus = PlusButton(window);
            var host = window.GetControl<Panel>("SidebarMenuHost");

            Click(window, plus);

            var card = window.GetControl<ContentControl>(Panel);
            var plusLeft = LayoutPoint(plus, default, host).X;
            var cardOrigin = card.TranslatePoint(default, host)!.Value;
            var limit = host.Bounds.Width - 8;

            Assert.True(plusLeft + card.Bounds.Width > limit, $"«+» at {plusLeft}, card {card.Bounds.Width}, limit {limit}");
            Assert.True(
                Math.Abs(cardOrigin.X + card.Bounds.Width - limit) < 0.5,
                $"card ends at {cardOrigin.X + card.Bounds.Width}, limit {limit}");
            Assert.True(
                Math.Abs(LayoutPoint(plus, new Point(0, plus.Bounds.Height), host).Y + 6 - cardOrigin.Y) < 0.5,
                $"card top {cardOrigin.Y}");

            window.Hide();
        });
    }

    /// <summary>
    /// В папке без вкладок в строке один «+» — и его меню работает так же: «Новый документ»
    /// создаёт черновик, карточка под кнопкой.
    /// </summary>
    [Fact]
    public Task MenuWorksInAFolderWithoutTabs()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel, _) = await ShowAsync(documents: 0, openFolder: true);
            Assert.False(viewModel.OpenDocuments.HasTabs);
            Assert.True(viewModel.ShowsTabStrip);
            var plus = PlusButton(window);

            Click(window, plus);

            Assert.True(viewModel.IsNewTabMenuOpen);
            AssertCardUnderLeftEdgeOf(window, plus);

            Click(window, Item(window, "NewTabMenuNewDocument"));

            Assert.False(viewModel.IsNewTabMenuOpen);
            Assert.True(viewModel.OpenDocuments.ActiveTab!.Path is null);

            window.Hide();
        });
    }

    /// <summary>Левый верхний угол карточки — в зазоре 6 под левым нижним углом кнопки.</summary>
    private static void AssertCardUnderLeftEdgeOf(Window window, Button button)
    {
        var buttonCorner = LayoutPoint(button, new Point(0, button.Bounds.Height), window);
        var card = window.GetControl<ContentControl>(Panel);
        Assert.True(card.Bounds.Width > 0);
        var cardCorner = card.TranslatePoint(default, window)!.Value;
        Assert.True(
            Math.Abs(buttonCorner.X - cardCorner.X) < 0.5 && Math.Abs(buttonCorner.Y + 6 - cardCorner.Y) < 0.5,
            $"button bottom-left {buttonCorner}, card top-left {cardCorner}");
    }

    /// <summary>
    /// Точка кнопки по её месту в раскладке, мимо RenderTransform: нажатая кнопка Fluent
    /// сжимается анимацией, и точка, снятая сквозь неё, зависела бы от кадра.
    /// </summary>
    private static Point LayoutPoint(Control control, Point point, Visual target)
        => control.GetVisualParent()!.TranslatePoint(
            new Point(control.Bounds.X + point.X, control.Bounds.Y + point.Y),
            target)!.Value;

    private static Button PlusButton(Window window) => Trigger(window, "NewTabButton");

    private static Button Trigger(Window window, string name)
        => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static List<Button> Items(Window window)
        => [.. window.GetControl<ContentControl>(Panel)
            .GetVisualDescendants()
            .OfType<Button>()
            .Where(static button => button.Classes.Contains("mm-menu-command"))];

    private static Button Item(Window window, string name)
        => Items(window).Single(button => button.Name == name);

    private static string? Label(Button button)
        => button.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(static block => block.Parent is not Border { Classes: var classes } || !classes.Contains("kbd"))?.Text;

    /// <summary>Клавиши сочетания — по плашке на клавишу.</summary>
    private static IReadOnlyList<string> Shortcut(Button button)
        => [.. button.GetVisualDescendants().OfType<Border>()
            .Where(static border => border.Classes.Contains("kbd"))
            .Select(static border => Assert.IsType<TextBlock>(border.Child).Text ?? string.Empty)];

    private static ContentPresenter Presenter(Button button)
        => button.GetVisualDescendants().OfType<ContentPresenter>().First();

    private static Border Card(Window window)
        => window.GetControl<ContentControl>(Panel)
            .GetVisualDescendants()
            .OfType<Border>()
            .Single(static border => border.Classes.Contains("mm-sidebar-menu-panel"));

    private static void Click(Window window, Control control)
        => Click(window, control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value);

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Render(window);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static object? Resource(Window window, string key)
    {
        Assert.True(window.TryFindResource(key, window.ActualThemeVariant, out var value), key);
        return value;
    }

    private static async Task<(MainWindow Window, ShellViewModel ViewModel, StubFilePicker Picker)> ShowAsync(
        int documents,
        bool openFolder = false,
        ThemeVariant? theme = null)
    {
        var picker = new StubFilePicker();
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root);
        var viewModel = CreateViewModel(picker, fileSystem);

        if (openFolder)
        {
            await viewModel.OpenFolderPathAsync(Root);
        }

        foreach (var path in Documents.Take(documents))
        {
            await viewModel.OpenPathAsync(path);
        }

        var window = new MainWindow(
            viewModel,
            StartupSmokeTestOptions.Disabled,
            new InMemorySettingsStore(),
            new RecordingStartupMetrics())
        {
            RequestedThemeVariant = theme ?? ThemeVariant.Light,
            Width = 900,
            Height = 600
        };
        window.Styles.Add(new FluentTheme());
        window.Styles.Add(Assert.IsAssignableFrom<IStyle>(LoadTheme("Controls.axaml")));
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(LoadTheme("Colors.axaml")));
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(LoadTheme("Typography.axaml")));
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(LoadTheme("Icons.axaml")));
        window.Show();
        Render(window);
        return (window, viewModel, picker);
    }

    private static object LoadTheme(string themeFile)
        => AvaloniaXamlLoader.Load(new Uri("avares://MarkMello.Presentation/Themes/" + themeFile));

    private static ShellViewModel CreateViewModel(StubFilePicker picker, FakeWorkspaceFileSystem fileSystem)
    {
        var loader = new StubDocumentLoader();
        foreach (var path in Documents.Append(Picked))
        {
            loader.Sources[path] = new MarkdownSource(path, System.IO.Path.GetFileName(path), "# doc");
        }

        var platform = new FakePlatformServices(fileSystem);

        return new ShellViewModel(
            new OpenDocumentUseCase(loader),
            new SaveDocumentUseCase(new RecordingDocumentSaver()),
            picker,
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
            new WorkspaceFileOperationsUseCase(fileSystem, platform),
            platform,
            static () => new FakeWorkspaceWatcher(),
            new RecordingWindowLauncher());
    }
}
