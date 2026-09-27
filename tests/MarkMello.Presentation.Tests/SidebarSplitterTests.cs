using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
/// Граница сайдбара и документа — одна линия: разделитель лежит поверх правой границы
/// сайдбара в колонке шириной 0 и сам в покое ничего не рисует. Раньше он занимал
/// колонку в 1 px со своей линией, и рядом с границей сайдбара выходила вторая.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class SidebarSplitterTests
{
    private static readonly string Root = TestPaths.At("docs");
    private static readonly string Readme = TestPaths.At("docs", "README.md");

    private readonly AvaloniaHeadlessFixture _fixture;

    public SidebarSplitterTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task SplitterAddsNoLineOfItsOwnNextToTheSidebarBorder(string theme)
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, _) = await ShowWithFolderAsync(theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);

            var layout = window.GetControl<Grid>("SidebarLayout");
            Assert.Equal(new GridLength(0), layout.ColumnDefinitions[1].Width);

            var splitter = Splitter(window);
            Assert.Contains("mm-sidebar-splitter", splitter.Classes);
            Assert.DoesNotContain("mm-editor-splitter", splitter.Classes);

            // Граница сайдбара — его последний пиксель; линия разделителя ровно на нём.
            // Координаты — от раскладки: на Windows её сдвигает рамка окна в 1 px.
            var sidebar = SidebarBorder(window);
            var sidebarRight = sidebar.TranslatePoint(new Point(sidebar.Bounds.Width, 0), layout)!.Value.X;
            Assert.Equal(WorkspaceSidebarWidth.Default, sidebarRight);
            Assert.Same(Resource(window, "MmBorderSoftBrush"), sidebar.BorderBrush);
            Assert.Equal(new Thickness(0, 0, 1, 0), sidebar.BorderThickness);

            var line = Line(splitter);
            Assert.Equal(1, line.Bounds.Width);
            Assert.Equal(sidebarRight - 1, line.TranslatePoint(default, layout)!.Value.X);
            Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(line.Background).Color);

            // Документ начинается сразу за сайдбаром, без полосы под разделитель.
            var document = Assert.IsAssignableFrom<Control>(layout.Children.Single(child => Grid.GetColumn(child) == 2));
            Assert.Equal(sidebarRight, document.TranslatePoint(default, layout)!.Value.X);

            // Зона захвата — 9 px с центром на пикселе границы.
            var hitArea = splitter.TranslatePoint(default, layout)!.Value.X;
            Assert.Equal(9, splitter.Bounds.Width);
            Assert.Equal(sidebarRight - 5, hitArea);
            Assert.Equal(nameof(StandardCursorType.SizeWestEast), splitter.Cursor?.ToString());
            window.Hide();
        });
    }

    /// <summary>
    /// Наведение попадает в разделитель и с обеих сторон границы — он поверх документа
    /// (ZIndex), — и зажигает акцент на месте границы.
    /// </summary>
    [Fact]
    public Task HoverOnEitherSideOfTheBorderLightsTheAccentOnIt()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, _) = await ShowWithFolderAsync();
            var splitter = Splitter(window);
            var border = WorkspaceSidebarWidth.Default - 0.5;

            foreach (var x in new[] { border - 4, border, border + 4 })
            {
                window.MouseMove(InWindow(window, new Point(WorkspaceSidebarWidth.Default + 200, 300)));
                Render(window);
                Assert.Equal(Colors.Transparent, LineColor(splitter));

                window.MouseMove(InWindow(window, new Point(x, 300)));
                Render(window);
                Assert.Equal(AccentColor(window), LineColor(splitter));
            }

            window.Hide();
        });
    }

    /// <summary>
    /// Перетаскивание тянет ширину сайдбара в пределах 220–340, держит акцент, даже когда
    /// курсор ушёл за край диапазона, и сохраняет ширину после отпускания.
    /// </summary>
    [Fact]
    public Task DraggingResizesTheSidebarWithinItsRangeAndKeepsTheAccent()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync();
            var layout = window.GetControl<Grid>("SidebarLayout");
            var splitter = Splitter(window);
            var y = 300d;
            var start = InWindow(window, new Point(WorkspaceSidebarWidth.Default - 0.5, y));

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(40, 0), RawInputModifiers.LeftMouseButton);
            Render(window);
            Assert.Equal(WorkspaceSidebarWidth.Default + 40, layout.ColumnDefinitions[0].ActualWidth);
            Assert.Contains("dragging", splitter.Classes);

            window.MouseMove(start + new Point(400, 0), RawInputModifiers.LeftMouseButton);
            Render(window);
            Assert.Equal(WorkspaceSidebarWidth.Maximum, layout.ColumnDefinitions[0].ActualWidth);
            Assert.Equal(AccentColor(window), LineColor(splitter));

            window.MouseMove(start + new Point(-200, 0), RawInputModifiers.LeftMouseButton);
            Render(window);
            Assert.Equal(WorkspaceSidebarWidth.Minimum, layout.ColumnDefinitions[0].ActualWidth);

            window.MouseMove(start + new Point(30, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Point(30, 0), MouseButton.Left);
            Render(window);

            Assert.DoesNotContain("dragging", splitter.Classes);
            Assert.Equal(WorkspaceSidebarWidth.Default + 30, viewModel.SidebarWidth);
            Assert.Equal(WorkspaceSidebarWidth.Default + 30, SidebarBorder(window).Bounds.Width);
            window.Hide();
        });
    }

    /// <summary>Свёрнутый сайдбар уносит и разделитель: документ от левого края окна.</summary>
    [Fact]
    public Task HiddenSidebarLeavesNoSplitterAndNoOffset()
    {
        return _fixture.RunAsync(async () =>
        {
            var (window, viewModel) = await ShowWithFolderAsync();

            viewModel.ToggleSidebarCommand.Execute(null);
            Render(window);

            Assert.False(Splitter(window).IsVisible);
            var layout = window.GetControl<Grid>("SidebarLayout");
            var document = Assert.IsAssignableFrom<Control>(layout.Children.Single(child => Grid.GetColumn(child) == 2));
            Assert.Equal(0, document.TranslatePoint(default, layout)!.Value.X);
            window.Hide();
        });
    }

    /// <summary>
    /// Точка раскладки в координатах окна: на Windows раскладку сдвигает рамка окна
    /// в 1 px, и мышь, наведённая по координатам раскладки, промахивалась бы на пиксель.
    /// </summary>
    private static Point InWindow(Window window, Point point)
        => window.GetControl<Grid>("SidebarLayout").TranslatePoint(point, window)!.Value;

    private static GridSplitter Splitter(Window window)
        => window.GetControl<GridSplitter>("SidebarSplitter");

    private static Border SidebarBorder(Window window)
        => window.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("mm-sidebar"));

    private static Border Line(GridSplitter splitter)
        => splitter.GetVisualDescendants().OfType<Border>().Single();

    private static Color LineColor(GridSplitter splitter)
        => Assert.IsAssignableFrom<ISolidColorBrush>(Line(splitter).Background).Color;

    private static Color AccentColor(Window window)
        => Assert.IsAssignableFrom<ISolidColorBrush>(Resource(window, "MmAccentBrush")).Color;

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
        fileSystem.AddDirectory(Root, WorkspaceEntry.ForFile(Readme, "README.md"));

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
