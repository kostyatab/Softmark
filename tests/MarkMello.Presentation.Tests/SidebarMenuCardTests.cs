using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Меню сайдбара и контекстное меню строки дерева (ADR-0009 Rule 4) — карточки внутри
/// окна, как меню ⋯: состав пунктов, открытие и закрытие, позиция у края окна.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class SidebarMenuCardTests
{
    private static readonly string Root = TestPaths.At("docs");
    private static readonly string Readme = TestPaths.At("docs", "README.md");
    private static readonly string Notes = TestPaths.At("docs", "notes.md");

    private readonly AvaloniaHeadlessFixture _fixture;

    public SidebarMenuCardTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    /// <summary>
    /// «имя папки ▾» — те же три действия над папкой, что и в попапе, но карточкой
    /// на общей поверхности и с тенью: в обеих темах она берёт их из палитры.
    /// </summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task FolderMenuIsACardOnTheSharedSurface(string theme)
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync(theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);

            Click(window, Trigger(window, "FolderMenuButton"));

            Assert.True(viewModel.IsFolderMenuOpen);
            Assert.Equal(
                ["FolderMenuOpenAnotherFolder", "FolderMenuReveal", "FolderMenuCloseFolder"],
                Items(window, "FolderMenuPanel").Select(static button => button.Name));
            Assert.Equal(
                ["Open Another Folder…", "Show in Explorer", "Close Folder"],
                Items(window, "FolderMenuPanel").Select(Label));

            var card = Card(window, "FolderMenuPanel");
            Assert.Equal(Resource(window, "MmCardShadow"), card.BoxShadow);
            Assert.Same(Resource(window, "MmElevatedBackgroundBrush"), card.Background);
            Assert.Same(Resource(window, "MmBorderBrush"), card.BorderBrush);
            Assert.False(card.ClipToBounds);

            // Карточка строки дерева — та же поверхность и та же тень, «Удалить» акцентом.
            viewModel.OpenTreeContextMenuCommand.Execute(Row(window, "notes.md").DataContext);
            Render(window);

            var rowCard = Card(window, "TreeContextMenuPanel");
            Assert.Equal(Resource(window, "MmCardShadow"), rowCard.BoxShadow);
            Assert.Same(Resource(window, "MmElevatedBackgroundBrush"), rowCard.Background);
            Assert.Same(
                Resource(window, "MmAccentBrush"),
                Presenter(Item(window, "TreeContextMenuPanel", "TreeMenuDelete")).Foreground);
            Assert.Same(
                Resource(window, "MmTextBrush"),
                Presenter(Item(window, "TreeContextMenuPanel", "TreeMenuRename")).Foreground);

            window.Hide();
        });
    }

    /// <summary>«+» — новый файл и новая папка; пункт выполняет команду и закрывает меню.</summary>
    [Fact]
    public Task CreateMenuItemRunsItsCommandAndClosesTheCard()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync();

            Click(window, Trigger(window, "CreateMenuButton"));

            Assert.True(viewModel.IsCreateMenuOpen);
            Assert.Equal(["New File", "New Folder"], Items(window, "CreateMenuPanel").Select(Label));

            Click(window, Item(window, "CreateMenuPanel", "CreateMenuNewFile"));

            Assert.True(viewModel.Workspace!.IsEditingName);
            Assert.False(viewModel.IsCreateMenuOpen);
            Assert.Null(window.GetControl<ContentControl>("CreateMenuPanel").Content);

            window.Hide();
        });
    }

    /// <summary>
    /// Открытие и закрытие — как у ⋯: повторный клик по кнопке, Esc и клик мимо
    /// закрывают карточку, а переключение на другое меню оставляет открытым одно.
    /// </summary>
    [Fact]
    public Task CardClosesOnSecondClickEscapeAndClickOutside()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync();

            Click(window, Trigger(window, "FolderMenuButton"));
            Assert.True(viewModel.IsFolderMenuOpen);
            Click(window, Trigger(window, "FolderMenuButton"));
            Assert.False(viewModel.IsFolderMenuOpen);

            Click(window, Trigger(window, "FolderMenuButton"));
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render(window);
            Assert.False(viewModel.IsFolderMenuOpen);
            Assert.Null(window.GetControl<ContentControl>("FolderMenuPanel").Content);

            Click(window, Trigger(window, "FolderMenuButton"));
            Click(window, new Point(window.Bounds.Width - 40, window.Bounds.Height - 40));
            Assert.False(viewModel.IsFolderMenuOpen);

            // Второе меню сменяет первое: одновременно открыт не больше одного оверлея.
            Click(window, Trigger(window, "FolderMenuButton"));
            Click(window, Trigger(window, "CreateMenuButton"));
            Assert.False(viewModel.IsFolderMenuOpen);
            Assert.True(viewModel.IsCreateMenuOpen);
            Assert.Null(window.GetControl<ContentControl>("FolderMenuPanel").Content);

            Click(window, Trigger(window, "AppMenuTriggerButton"));
            Assert.True(viewModel.IsAppMenuOpen);
            Assert.False(viewModel.IsCreateMenuOpen);

            window.Hide();
        });
    }

    /// <summary>
    /// Правый клик по строке: карточка встаёт у курсора, строка остаётся выделенной,
    /// пункты — прежние, «Удалить» акцентом. Команда работает со строкой меню,
    /// а не с выделением.
    /// </summary>
    [Fact]
    public Task TreeContextMenuOpensAtThePointerAndKeepsTheRow()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync();
            var row = Row(window, "notes.md");
            var point = row.TranslatePoint(new Point(20, row.Bounds.Height / 2), window)!.Value;

            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            Render(window);

            Assert.True(viewModel.IsTreeContextMenuOpen);
            Assert.Same(row.DataContext, viewModel.TreeContextNode);
            Assert.Same(row.DataContext, viewModel.Workspace!.SelectedNode);
            Assert.Equal(
                ["Open in New Tab", "Rename", "Duplicate", "Show in Explorer", "Delete"],
                Items(window, "TreeContextMenuPanel").Select(Label));
            Assert.Contains(
                "mm-menu-destructive",
                Item(window, "TreeContextMenuPanel", "TreeMenuDelete").Classes);

            AssertCardStartsAt(window, "TreeContextMenuPanel", point);

            Click(window, Item(window, "TreeContextMenuPanel", "TreeMenuRename"));

            Assert.True(viewModel.Workspace.IsEditingName);
            Assert.False(viewModel.IsTreeContextMenuOpen);
            Assert.Null(viewModel.TreeContextNode);

            window.Hide();
        });
    }

    /// <summary>
    /// Правый клик по другой строке при открытом меню переносит карточку к новому курсору.
    /// Состав пунктов у строк одинаковый, размер карточки не меняется — позиция обязана
    /// пересчитываться в момент открытия, а не по изменению размера.
    /// </summary>
    [Fact]
    public Task TreeContextMenuMovesToTheRowClickedNext()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync();

            // Вторая строка — выше первой: карточка раскрывается вниз и накрыла бы её.
            var first = RightClick(window, Row(window, "notes.md"));
            Assert.True(viewModel.IsTreeContextMenuOpen);
            AssertCardStartsAt(window, "TreeContextMenuPanel", first);

            var second = RightClick(window, Row(window, "README.md"));

            Assert.True(viewModel.IsTreeContextMenuOpen);
            Assert.Same(Row(window, "README.md").DataContext, viewModel.TreeContextNode);
            AssertCardStartsAt(window, "TreeContextMenuPanel", second);
            Assert.NotEqual(first.Y, second.Y);

            window.Hide();
        });
    }

    /// <summary>
    /// Карточка забирает фокус, чтобы работали стрелки и Enter, но по закрытию отдаёт его
    /// обратно: иначе дерево перестаёт слышать F2 и Delete.
    /// </summary>
    [Fact]
    public Task ClosedCardGivesTheFocusBack()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync();
            var row = Row(window, "README.md");
            Assert.True(row.Focus());

            RightClick(window, row);
            Assert.Contains(Items(window, "TreeContextMenuPanel"), static button => button.IsFocused);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render(window);

            Assert.False(viewModel.IsTreeContextMenuOpen);
            Assert.True(row.IsFocused);

            // Клавиатура дерева снова жива: F2 доходит до строки.
            window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, null);
            Render(window);
            Assert.True(viewModel.Workspace!.IsEditingName);

            window.Hide();
        });
    }

    /// <summary>
    /// Открытое меню первый пункт не подсвечивает, хотя фокус в нём; подсветка приходит
    /// с обходом стрелками (ADR-0009 Rule 4).
    /// </summary>
    [Fact]
    public Task ArrowKeysHighlightTheItemButOpeningDoesNot()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, _) = await ShowWithFolderAsync();

            Click(window, Trigger(window, "FolderMenuButton"));

            var items = Items(window, "FolderMenuPanel").ToList();
            Assert.True(items[0].IsFocused);
            Assert.NotSame(Resource(window, "MmTabHoverBrush"), Presenter(items[0]).Background);

            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            Render(window);

            Assert.True(items[1].IsFocused);
            Assert.Same(Resource(window, "MmTabHoverBrush"), Presenter(items[1]).Background);

            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
            Render(window);
            Assert.True(items[0].IsFocused);

            window.Hide();
        });
    }

    /// <summary>
    /// В поле инлайн-переименования правый клик принадлежит полю: карточка строки его
    /// не перехватывает и не отменяет ввод.
    /// </summary>
    [Fact]
    public Task RightClickInsideTheRenameEditorKeepsTheEdit()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync();
            var row = Row(window, "README.md");
            viewModel.Workspace!.StartRenameCommand.Execute(row.DataContext);
            Render(window);

            var editor = row.GetVisualDescendants().OfType<TextBox>()
                .Single(static box => box.Classes.Contains("mm-tree-edit-input"));

            // Собственное меню правки TextBox — попап платформы, в headless его не создать;
            // проверяем ровно то, что наше: карточка строки клик не перехватывает.
            editor.ContextFlyout = null;
            RightClick(window, editor);

            Assert.False(viewModel.IsTreeContextMenuOpen);
            Assert.True(viewModel.Workspace.IsEditingName);

            window.Hide();
        });
    }

    /// <summary>
    /// Обе карточки встают под своей кнопкой, выровненные по её левому краю: размер
    /// карточки в момент открытия ещё неизвестен, поэтому проверяем нарисованные границы.
    /// </summary>
    [Fact]
    public Task CardsLineUpWithTheirButtons()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, _) = await ShowWithFolderAsync();

            foreach (var name in (string[])["FolderMenuButton", "CreateMenuButton"])
            {
                var button = Trigger(window, name);
                var origin = button.TranslatePoint(default, window)!.Value;
                Click(window, button);

                var panel = name == "FolderMenuButton" ? "FolderMenuPanel" : "CreateMenuPanel";
                Assert.True(window.GetControl<ContentControl>(panel).Bounds.Width > 0, name);
                AssertCardStartsAt(
                    window,
                    panel,
                    new Point(origin.X, origin.Y + button.Bounds.Height + 6),
                    name);
            }

            window.Hide();
        });
    }

    /// <summary>
    /// Рамка окна сдвигает слой карточек на свою толщину внутрь окна — на Windows она
    /// есть по умолчанию (<see cref="MainWindow.ShouldDrawWindowBorder"/>). Карточка
    /// всё равно обязана встать под кнопкой: якорь переводится в координаты слоя.
    /// Рамка включается явно, чтобы геометрия Windows проверялась на любой платформе.
    /// </summary>
    [Fact]
    public Task CardsLineUpWithTheirButtonsInsideTheWindowBorder()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync();
            viewModel.WindowBorderMode = WindowBorderMode.On;
            Render(window);

            var host = window.GetControl<Panel>("SidebarMenuHost");
            var inset = host.TranslatePoint(default, window)!.Value;
            Assert.Equal(1, inset.X, 0);

            var button = Trigger(window, "FolderMenuButton");
            var origin = button.TranslatePoint(default, window)!.Value;
            Click(window, button);

            AssertCardStartsAt(
                window,
                "FolderMenuPanel",
                new Point(origin.X, origin.Y + button.Bounds.Height + 6));

            window.Hide();
        });
    }

    /// <summary>
    /// Нажатая кнопка Fluent сжимается и отпускается анимацией, поэтому в момент
    /// открытия меню её RenderTransform — случайный кадр. Карточка встаёт по месту
    /// кнопки в раскладке: сжатие задано явно и без анимации, чтобы кадр был один.
    /// </summary>
    [Fact]
    public Task CardIgnoresTheButtonPressAnimation()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, _) = await ShowWithFolderAsync();
            var button = Trigger(window, "FolderMenuButton");
            var origin = button.TranslatePoint(default, window)!.Value;

            button.Transitions = null;
            button.RenderTransform = TransformOperations.Parse("scale(0.9)");
            Click(window, button);

            AssertCardStartsAt(
                window,
                "FolderMenuPanel",
                new Point(origin.X, origin.Y + button.Bounds.Height + 6));

            window.Hide();
        });
    }

    /// <summary>Меню строки открывается и с клавиатуры: ⇧F10 — как было у попапа.</summary>
    [Fact]
    public Task ShiftF10OpensTheTreeContextMenu()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync();
            var row = Row(window, "README.md");
            var node = Assert.IsType<FileTreeNodeViewModel>(row.DataContext);

            viewModel.Workspace!.SelectedNode = node;
            Render(window);
            Assert.True(row.Focus());
            window.KeyPress(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10, null);
            Render(window);

            Assert.True(viewModel.IsTreeContextMenuOpen);
            Assert.Same(node, viewModel.TreeContextNode);
            Assert.NotEmpty(Items(window, "TreeContextMenuPanel"));

            window.Hide();
        });
    }

    /// <summary>
    /// У края окна карточка сдвигается внутрь, а не обрезается: попапу это давала ОС.
    /// </summary>
    [Fact]
    public void CardStaysInsideTheWindow()
    {
        var limits = new Rect(8, 8, 384, 584);
        var card = new Size(200, 160);

        // Под кнопкой: левый край карточки — левый край кнопки, зазор 6 снизу.
        Assert.Equal(
            new Thickness(11, 46, 0, 0),
            MainWindow.CalculateSidebarMenuMargin(new Rect(11, 12, 120, 28), card, limits));

        // Кнопка у правого края сайдбара: карточка сдвигается внутрь окна.
        Assert.Equal(
            new Thickness(192, 46, 0, 0),
            MainWindow.CalculateSidebarMenuMargin(new Rect(300, 12, 30, 28), card, limits));

        // У курсора: карточка не вылезает ни за правый край, ни за низ.
        Assert.Equal(
            new Thickness(192, 432, 0, 0),
            MainWindow.CalculateSidebarMenuMargin(new Rect(new Point(380, 560), default(Size)), card, limits));

        // Пока карточка не измерена, края окна её не двигают — поправит раскладка.
        Assert.Equal(
            new Thickness(380, 560, 0, 0),
            MainWindow.CalculateSidebarMenuMargin(new Rect(new Point(380, 560), default(Size)), default, limits));

        // В окне ниже карточки верх важнее низа: меню не уезжает за строку окна.
        Assert.Equal(
            new Thickness(8, 8, 0, 0),
            MainWindow.CalculateSidebarMenuMargin(new Rect(new Point(0, 0), default(Size)), card, new Rect(8, 8, 100, 100)));
    }

    private static ContentPresenter Presenter(Button button)
        => button.GetVisualDescendants().OfType<ContentPresenter>().First();

    private static string? Label(Button button)
        => button.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(static block => block.Parent is not Border { Classes: var classes } || !classes.Contains("kbd"))?.Text;

    private static IEnumerable<Button> Items(Window window, string panel)
        => window.GetControl<ContentControl>(panel)
            .GetVisualDescendants()
            .OfType<Button>()
            .Where(static button => button.Classes.Contains("mm-menu-command") && button.IsVisible);

    private static Button Item(Window window, string panel, string name)
        => Items(window, panel).Single(button => button.Name == name);

    private static Border Card(Window window, string panel)
        => window.GetControl<ContentControl>(panel)
            .GetVisualDescendants()
            .OfType<Border>()
            .Single(static border => border.Classes.Contains("mm-sidebar-menu-panel"));

    /// <summary>
    /// Карточка сравнивается с якорем в координатах окна: сам слой карточек лежит
    /// внутри рамки окна, поэтому там, где рамку рисуют, его Bounds смещены на её
    /// толщину — сравнение Bounds карточки с точкой в координатах окна ловило бы
    /// толщину рамки, а не расхождение карточки с кнопкой.
    /// </summary>
    private static void AssertCardStartsAt(Window window, string panelName, Point expected, string? what = null)
    {
        var card = window.GetControl<ContentControl>(panelName);
        var actual = card.TranslatePoint(default, window)!.Value;
        Assert.True(
            Math.Abs(expected.X - actual.X) < 0.5 && Math.Abs(expected.Y - actual.Y) < 0.5,
            $"{panelName}{(what is null ? string.Empty : $" ({what})")}: expected {expected}, actual {actual}");
    }

    private static Button Trigger(Window window, string name)
        => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static TreeViewItem Row(Window window, string name)
        => window.GetVisualDescendants()
            .OfType<TreeViewItem>()
            .Single(item => item.DataContext is FileTreeNodeViewModel node && node.Name == name);

    private static void Click(Window window, Control control)
        => Click(window, control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value);

    /// <summary>Правый клик по центру контрола; возвращает точку, где он случился.</summary>
    private static Point RightClick(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(20, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);
        Render(window);
        return point;
    }

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

    private static async Task<(MainWindow Window, ShellViewModel ViewModel)> ShowWithFolderAsync(ThemeVariant? theme = null)
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(
            Root,
            WorkspaceEntry.ForFile(Readme, "README.md"),
            WorkspaceEntry.ForFile(Notes, "notes.md"));

        var viewModel = CreateViewModel(fileSystem);
        await viewModel.OpenFolderPathAsync(Root);

        var window = new MainWindow(
            viewModel,
            StartupSmokeTestOptions.Disabled,
            new InMemorySettingsStore(),
            new RecordingStartupMetrics())
        {
            RequestedThemeVariant = theme ?? ThemeVariant.Light
        };
        window.Styles.Add(new FluentTheme());
        window.Styles.Add(Assert.IsAssignableFrom<IStyle>(LoadTheme("Controls.axaml")));
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(LoadTheme("Colors.axaml")));
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(LoadTheme("Typography.axaml")));
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(LoadTheme("Icons.axaml")));
        window.Show();
        Render(window);
        return (window, viewModel);
    }

    private static object LoadTheme(string themeFile)
        => AvaloniaXamlLoader.Load(new Uri("avares://MarkMello.Presentation/Themes/" + themeFile));

    private static ShellViewModel CreateViewModel(FakeWorkspaceFileSystem fileSystem)
    {
        var loader = new StubDocumentLoader();
        loader.Sources[Readme] = new MarkdownSource(Readme, "README.md", "# readme");
        loader.Sources[Notes] = new MarkdownSource(Notes, "notes.md", "# notes");
        var platform = new FakePlatformServices(fileSystem);

        return new ShellViewModel(
            new OpenDocumentUseCase(loader),
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
            new WorkspaceFileOperationsUseCase(fileSystem, platform),
            platform,
            static () => new FakeWorkspaceWatcher(),
            new RecordingWindowLauncher());
    }
}
