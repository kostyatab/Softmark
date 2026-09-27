using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Строка окна 44 px с постоянным блоком кнопок справа (ADR-0009 Rules 1–2): что в ней
/// видно в каждом состоянии оболочки, когда под ней появляется линия и как выглядят
/// её кнопки.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class WindowRowTests
{
    private static readonly string DocumentPath = TestPaths.At("docs", "README.md");

    private readonly AvaloniaHeadlessFixture _fixture;

    public WindowRowTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task WelcomeScreenShowsOnlyTheMenu()
    {
        return _fixture.RunAsync(() =>
        {
            var window = Show(CreateViewModel());

            Assert.Equal(["AppMenuTriggerButton"], VisibleRowButtons(window));

            window.Hide();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task OpenDocumentShowsFindReadingCardEditAndMenu()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = CreateViewModel();
            await viewModel.OpenPathAsync(DocumentPath);
            var window = Show(viewModel);

            Assert.Equal(
                ["FindTriggerButton", "SettingsTriggerButton", "EditTriggerButton", "AppMenuTriggerButton"],
                VisibleRowButtons(window));

            window.Hide();
        });
    }

    /// <summary>
    /// В правке Aa нет, карандаш на том же месте становится «Готово», а «Не сохранено ⌘S»
    /// появляется слева от кнопок с первой несохранённой правкой.
    /// </summary>
    [Fact]
    public Task EditModeSwapsThePencilForDone()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = CreateViewModel();
            await viewModel.OpenPathAsync(DocumentPath);
            await viewModel.ToggleEditModeCommand.ExecuteAsync(null);
            var window = Show(viewModel);

            Assert.Equal(["FindTriggerButton", "DoneEditButton", "AppMenuTriggerButton"], VisibleRowButtons(window));

            viewModel.EditorSession!.SourceText = "# changed";

            Assert.Equal(
                ["UnsavedIndicator", "FindTriggerButton", "DoneEditButton", "AppMenuTriggerButton"],
                VisibleRowButtons(window));
            Assert.Equal(["Ctrl", "S"], UnsavedShortcut(window));

            window.Hide();
        });
    }

    /// <summary>
    /// Черновик ⌘N: «Готово» нет — читать его без пути нельзя, — а «Не сохранено ⌘S» есть
    /// сразу. После «Сохранить как» черновик становится документом и «Готово» появляется.
    /// </summary>
    [Fact]
    public Task DraftShowsUnsavedButNoDoneUntilSavedAs()
    {
        return _fixture.RunAsync(async () =>
        {
            var filePicker = new StubFilePicker { SavePath = TestPaths.At("docs", "draft.md") };
            var viewModel = CreateViewModel(filePicker);
            await viewModel.CreateNewDocumentCommand.ExecuteAsync(null);
            var window = Show(viewModel);

            Assert.Equal(["UnsavedIndicator", "FindTriggerButton", "AppMenuTriggerButton"], VisibleRowButtons(window));

            await viewModel.SaveAsCommand.ExecuteAsync(null);

            Assert.Equal(["FindTriggerButton", "DoneEditButton", "AppMenuTriggerButton"], VisibleRowButtons(window));

            window.Hide();
        });
    }

    /// <summary>
    /// «Готово» — единственная залитая кнопка строки: фон цвета текста, подпись и галочка
    /// цвета фона, радиус 8, высота 30. Заливка держится и под курсором, в обеих темах.
    /// </summary>
    [Theory]
    [InlineData("Light", "")]
    [InlineData("Light", ":pointerover")]
    [InlineData("Light", ":pressed")]
    [InlineData("Dark", "")]
    [InlineData("Dark", ":pointerover")]
    [InlineData("Dark", ":pressed")]
    public Task DoneButtonIsFilledWithTheTextColour(string theme, string state)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var icon = new LucideIcon { Width = 14, Height = 14 };
            var label = new TextBlock { Text = "Done" };
            var button = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Children = { icon, label }
                }
            };
            button.Classes.Add("mm-row-primary");
            var window = ThemedTestWindow.Create(theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light, button);
            window.Show();
            window.UpdateLayout();

            if (state.Length > 0)
            {
                ((IPseudoClasses)button.Classes).Add(state);
            }

            Assert.True(window.TryFindResource("MmTextBrush", window.ActualThemeVariant, out var text));
            Assert.True(window.TryFindResource("MmBackgroundBrush", window.ActualThemeVariant, out var background));

            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();
            Assert.Same(text, presenter.Background);
            Assert.Same(background, icon.Foreground);
            Assert.Same(background, label.Foreground);
            Assert.Equal(new Avalonia.CornerRadius(8), presenter.CornerRadius);
            Assert.Equal(30, button.Bounds.Height);

            window.Close();
        }, CancellationToken.None);
    }

    /// <summary>
    /// Одна строка вместо тайтлбара и строки вкладок: высота 44 (на macOS 52), текста заголовка
    /// в ней нет (он остаётся в <see cref="Window.Title"/> для Dock и панели задач),
    /// плавающих групп кнопок над документом тоже нет.
    /// </summary>
    [Fact]
    public Task RowIsOneLineWithoutTitleTextOrFloatingButtons()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = CreateViewModel();
            await viewModel.OpenPathAsync(DocumentPath);
            var window = Show(viewModel);
            window.UpdateLayout();

            var row = window.GetControl<Border>("WindowRow");
            Assert.Equal(MainWindow.CalculateWindowRowHeight(OperatingSystem.IsMacOS()), row.Bounds.Height);
            Assert.Equal("README.md — Softmark", window.Title);
            Assert.DoesNotContain(
                window.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == window.Title);
            Assert.DoesNotContain(
                window.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("mm-top-chrome"));

            // Вкладки стоят в самой строке, а не отдельной полосой под ней.
            Assert.NotNull(row.GetVisualDescendants().OfType<TabStripView>().SingleOrDefault());

            window.Hide();
        });
    }

    /// <summary>Меню ⋯ и карточка Aa раскрываются у правого края, под своими кнопками.</summary>
    [Theory]
    [InlineData("AppMenuPanel")]
    [InlineData("SettingsPanel")]
    public Task CardsOpenUnderTheRowAtTheRightEdge(string panelName)
    {
        return _fixture.RunAsync(() =>
        {
            var window = Show(CreateViewModel());
            var card = window.GetControl<ContentControl>(panelName);

            Assert.Equal(Avalonia.Layout.HorizontalAlignment.Right, card.HorizontalAlignment);

            // Правый отступ держит хост: на Windows он уступает место кнопкам окна.
            Assert.Same(window.GetControl<Panel>("OverlayCardHost"), card.Parent);

            window.Hide();
            return Task.CompletedTask;
        });
    }

    /// <summary>Линия под строкой видна всегда, как под тулбарами macOS, — не только при прокрутке.</summary>
    [Fact]
    public Task DividerUnderTheRowIsAlwaysShown()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = CreateViewModel();
            var window = Show(viewModel);
            var divider = window.GetControl<Border>("WindowRowDivider");

            Assert.True(divider.IsVisible);

            await viewModel.OpenPathAsync(DocumentPath);
            viewModel.ReportScrollOffset(0);
            Assert.True(divider.IsVisible);

            viewModel.ReportScrollOffset(120);
            Assert.True(divider.IsVisible);

            window.Hide();
        });
    }

    [Fact]
    public async Task FindButtonIsShownOnlyWithADocument()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.ShowsFindToggle);

        await viewModel.OpenPathAsync(DocumentPath);

        Assert.True(viewModel.ShowsFindToggle);
    }

    /// <summary>
    /// Кнопки строки без рамки и фона: наведение — MmTabHoverBrush, нажатая кнопка
    /// или открытая карточка — MmTabActiveBrush, в обеих темах.
    /// </summary>
    [Theory]
    [InlineData("Light", "", null)]
    [InlineData("Light", ":pointerover", "MmTabHoverBrush")]
    [InlineData("Light", ":pressed", "MmTabActiveBrush")]
    [InlineData("Light", ":checked", "MmTabActiveBrush")]
    [InlineData("Light", ":checked :pointerover", "MmTabActiveBrush")]
    [InlineData("Dark", "", null)]
    [InlineData("Dark", ":pointerover", "MmTabHoverBrush")]
    [InlineData("Dark", ":pressed", "MmTabActiveBrush")]
    [InlineData("Dark", ":checked", "MmTabActiveBrush")]
    [InlineData("Dark", ":checked :pointerover", "MmTabActiveBrush")]
    public Task RowButtonFillFollowsItsState(string theme, string states, string? expectedBrushKey)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var button = new ToggleButton { Content = new LucideIcon { Width = 14, Height = 14 } };
            button.Classes.Add("mm-icon-button");
            button.Classes.Add("mm-row-button");
            var window = ThemedTestWindow.Create(theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light, button);
            window.Show();
            window.UpdateLayout();

            foreach (var state in states.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (state == ":checked")
                {
                    button.IsChecked = true;
                }
                else
                {
                    ((IPseudoClasses)button.Classes).Add(state);
                }
            }

            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();
            if (expectedBrushKey is null)
            {
                Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);
            }
            else
            {
                Assert.True(window.TryFindResource(expectedBrushKey, window.ActualThemeVariant, out var expected));
                Assert.Same(expected, presenter.Background);
            }

            Assert.Equal(30, button.Bounds.Width);
            Assert.Equal(30, button.Bounds.Height);

            window.Close();
        }, CancellationToken.None);
    }

    /// <summary>
    /// Крестик окна Windows под курсором — белый глиф на красном, как у системных
    /// кнопок; остальные кнопки окна под курсором темнеют до цвета текста.
    /// </summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task WindowsCloseGlyphTurnsWhiteUnderThePointer(string theme)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var closeGlyph = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M0,0 L10,10 M10,0 L0,10") };
            var close = new Button { Content = closeGlyph };
            close.Classes.Add("titlebar");
            close.Classes.Add("close");
            var minimizeGlyph = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M0,5 L10,5") };
            var minimize = new Button { Content = minimizeGlyph };
            minimize.Classes.Add("titlebar");

            var window = ThemedTestWindow.Create(
                theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
                new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { minimize, close } });
            window.Show();
            window.UpdateLayout();

            Assert.True(window.TryFindResource("MmTextSoftBrush", window.ActualThemeVariant, out var soft));
            Assert.True(window.TryFindResource("MmTextBrush", window.ActualThemeVariant, out var text));
            Assert.Same(soft, closeGlyph.Stroke);
            Assert.Equal(44, close.Bounds.Height);
            Assert.Equal(46, close.Bounds.Width);

            ((IPseudoClasses)close.Classes).Add(":pointerover");
            ((IPseudoClasses)minimize.Classes).Add(":pointerover");

            Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(closeGlyph.Stroke).Color);
            Assert.Same(text, minimizeGlyph.Stroke);

            window.Close();
        }, CancellationToken.None);
    }

    /// <summary>
    /// Окно тянется за пустое место строки и шапки сайдбара — всё, что лежит внутри
    /// области с классом <c>mm-window-drag</c>; документ под строкой окно не тянет.
    /// </summary>
    [Fact]
    public void OnlyTheRowAndTheSidebarHeaderDragTheWindow()
    {
        var label = new TextBlock();
        var row = new Border { Child = label };
        row.Classes.Add(MainWindow.WindowDragClass);

        Assert.True(MainWindow.IsWindowDragSource(row));
        Assert.True(MainWindow.IsWindowDragSource(label));
        Assert.False(MainWindow.IsWindowDragSource(new TextBlock()));
    }

    /// <summary>Блок справа в строке окна слева направо.</summary>
    private static readonly string[] RowButtonNames =
    [
        "UnsavedIndicator",
        "FindTriggerButton",
        "SettingsTriggerButton",
        "EditTriggerButton",
        "DoneEditButton",
        "AppMenuTriggerButton"
    ];

    private static string[] VisibleRowButtons(MainWindow window)
        => RowButtonNames
            .Where(name => window.GetControl<Control>(name).IsVisible)
            .ToArray();

    /// <summary>
    /// Клавиши рядом с «Не сохранено» — ряд плашек. Окно здесь без тем приложения,
    /// поэтому берём клавиши ряда, а как они рисуются, проверяет <see cref="ShortcutKeysRowTests"/>.
    /// </summary>
    private static IEnumerable<string>? UnsavedShortcut(MainWindow window)
        => window.GetControl<StackPanel>("UnsavedIndicator")
            .GetVisualDescendants()
            .OfType<ItemsControl>()
            .Single(static keys => keys.Classes.Contains("mm-shortcut-keys"))
            .ItemsSource as IEnumerable<string>;

    /// <summary>
    /// Окно без composition root, как в <see cref="TextSizeShortcutTests"/>: закрыть его
    /// нельзя — отписка в OnClosed ждёт VM из полного конструктора, — поэтому тесты его прячут.
    /// </summary>
    private static MainWindow Show(ShellViewModel viewModel)
    {
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        return window;
    }

    private static ShellViewModel CreateViewModel(StubFilePicker? filePicker = null)
    {
        var loader = new StubDocumentLoader();
        loader.Sources[DocumentPath] = new MarkdownSource(DocumentPath, "README.md", "# readme");
        var fileSystem = new FakeWorkspaceFileSystem();

        return new ShellViewModel(
            new OpenDocumentUseCase(loader),
            new SaveDocumentUseCase(new RecordingDocumentSaver()),
            filePicker ?? new StubFilePicker(),
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
    }
}
