using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Сочетание клавиш рисуется рядом плашек, по клавише на плашку (<c>ItemsControl.mm-shortcut-keys</c>),
/// а не одной строкой. На macOS все модификаторы однознаковые, и плашки ряда — квадраты одной
/// ширины; на Windows и Linux слова растягивают плашку в прямоугольник. Платформа приходит
/// параметром, как в <see cref="ShortcutLabelTests"/>, поэтому обе проверяются на любой машине.
/// Ряд — единственный способ показать сочетание: меню, стартовый экран и экран ошибки
/// рисуют плашки, а не строку.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class ShortcutKeysRowTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public ShortcutKeysRowTests(AvaloniaHeadlessFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("macOS", "Light")]
    [InlineData("macOS", "Dark")]
    [InlineData("Windows", "Light")]
    [InlineData("Windows", "Dark")]
    public Task EachKeyOfAThreeKeyShortcutGetsItsOwnCap(string platformName, string theme)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var keys = ShortcutLabel.Keys(ShortcutLabel.Command(Key.O, platformName, KeyModifiers.Shift), platformName);
            var caps = RenderCaps(keys, theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light, out var window);

            Assert.Equal(3, keys.Count);
            Assert.Equal(keys, caps.Select(static cap => Assert.IsType<TextBlock>(cap.Child).Text));
            Assert.All(caps, static cap => Assert.Equal(20, cap.Bounds.Height, 0.5));

            window.Close();
        }, CancellationToken.None);
    }

    [Theory]
    [InlineData(Key.O, KeyModifiers.Shift)]
    [InlineData(Key.S, KeyModifiers.Shift)]
    [InlineData(Key.OemComma, KeyModifiers.None)]
    [InlineData(Key.W, KeyModifiers.None)]
    public Task SingleCharacterCapsOnMacOSAreSquaresOfOneWidth(Key key, KeyModifiers extra)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var keys = ShortcutLabel.Keys(ShortcutLabel.Command(key, "macOS", extra), "macOS");
            var caps = RenderCaps(keys, ThemeVariant.Light, out var window);

            Assert.Equal(keys.Count, caps.Count);
            Assert.All(caps, static cap => Assert.Equal(cap.Bounds.Height, cap.Bounds.Width, 0.5));

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task WordCapsOnWindowsStretchIntoRectangles()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var keys = ShortcutLabel.Keys(ShortcutLabel.Command(Key.O, "Windows", KeyModifiers.Shift), "Windows");
            var caps = RenderCaps(keys, ThemeVariant.Light, out var window);

            Assert.True(caps[0].Bounds.Width > caps[0].Bounds.Height, "Ctrl should be wider than tall.");
            Assert.True(caps[1].Bounds.Width > caps[1].Bounds.Height, "Shift should be wider than tall.");
            Assert.Equal(caps[2].Bounds.Height, caps[2].Bounds.Width, 0.5);

            window.Close();
        }, CancellationToken.None);
    }

    /// <summary>
    /// Все пункты ⋯ с сочетанием показывают его рядом плашек. Скрытый без папки пункт
    /// «Панель файлов» не раскладывается, поэтому у него проверяется только ряд.
    /// </summary>
    [Theory]
    [InlineData("macOS")]
    [InlineData("Windows")]
    public Task AppMenuShowsEveryShortcutAsCaps(string platformName)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var viewModel = CreateViewModel(platformName);
            var view = new AppMenuPanelView { DataContext = viewModel };
            var window = Show(view);

            var expected = new Dictionary<string, IReadOnlyList<string>>
            {
                ["MenuNewDocument"] = viewModel.NewDocumentShortcutKeys,
                ["MenuOpenFile"] = viewModel.OpenFileShortcutKeys,
                ["MenuOpenFolder"] = viewModel.OpenFolderShortcutKeys,
                ["MenuSave"] = viewModel.SaveShortcutKeys,
                ["MenuSaveAs"] = viewModel.SaveAsShortcutKeys,
                ["MenuReload"] = viewModel.ReloadShortcutKeys,
                ["MenuFilesPanel"] = viewModel.ToggleSidebarShortcutKeys,
                ["MenuCloseTab"] = viewModel.CloseTabShortcutKeys,
                ["MenuSettings"] = viewModel.SettingsShortcutKeys
            };

            foreach (var (name, keys) in expected)
            {
                var item = view.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == name);
                Assert.Equal(keys, KeysRow(item).ItemsSource as IEnumerable<string>);
                if (item.IsVisible)
                {
                    Assert.Equal(keys, CapTexts(item));
                }
            }

            window.Close();
        }, CancellationToken.None);
    }

    [Theory]
    [InlineData("macOS")]
    [InlineData("Windows")]
    public Task TreeMenuShowsRenameAndDeleteAsCaps(string platformName)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var viewModel = CreateViewModel(platformName);
            var view = new TreeContextMenuView { DataContext = viewModel };
            var window = Show(view);

            Assert.Equal(viewModel.TreeRenameShortcutKeys, CapTexts(Item(view, "TreeMenuRename")));
            Assert.Equal(viewModel.TreeDeleteShortcutKeys, CapTexts(Item(view, "TreeMenuDelete")));

            window.Close();
        }, CancellationToken.None);
    }

    [Theory]
    [InlineData("macOS")]
    [InlineData("Windows")]
    public Task WelcomeAndLoadErrorShowTheirShortcutAsCaps(string platformName)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var viewModel = CreateViewModel(platformName);

            var welcome = new WelcomeView { DataContext = viewModel };
            var window = Show(welcome);
            Assert.Equal(viewModel.OpenFileShortcutKeys, CapTexts(welcome));
            window.Close();

            var loadError = new LoadErrorView { DataContext = viewModel };
            window = Show(loadError);
            Assert.Equal(viewModel.DialogCancelShortcutKeys, CapTexts(loadError));
            window.Close();
        }, CancellationToken.None);
    }

    /// <summary>
    /// Неприменимый пункт гасит плашки вместе с подписью: буква бледнеет, крышка сливается
    /// с карточкой, рамка становится мягкой. Без документа «Сохранить» выключено, «Новый» — нет.
    /// </summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task DisabledMenuItemDimsItsCaps(string theme)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var view = new AppMenuPanelView { DataContext = CreateViewModel("macOS") };
            var window = Show(view, theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);

            var enabled = Item(view, "MenuNewDocument");
            var disabled = Item(view, "MenuSave");
            Assert.True(enabled.IsEffectivelyEnabled);
            Assert.False(disabled.IsEffectivelyEnabled);

            Assert.All(Caps(enabled), cap =>
            {
                Assert.Same(Resource(window, "MmKeyboardBackgroundBrush"), cap.Background);
                Assert.Same(Resource(window, "MmKeyboardBorderBrush"), cap.BorderBrush);
                Assert.Same(Resource(window, "MmTextSoftBrush"), Assert.IsType<TextBlock>(cap.Child).Foreground);
            });
            Assert.All(Caps(disabled), cap =>
            {
                Assert.Equal(Avalonia.Media.Colors.Transparent, Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(cap.Background).Color);
                Assert.Same(Resource(window, "MmBorderSoftBrush"), cap.BorderBrush);
                Assert.Same(Resource(window, "MmTextFaintBrush"), Assert.IsType<TextBlock>(cap.Child).Foreground);
            });

            window.Close();
        }, CancellationToken.None);
    }

    private static Window Show(Control view, ThemeVariant? theme = null)
    {
        var window = ThemedTestWindow.Create(theme ?? ThemeVariant.Light, view);
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Button Item(Control view, string name)
        => view.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static ItemsControl KeysRow(Button item)
        => item.GetLogicalDescendants().OfType<ItemsControl>().Single(static row => row.Classes.Contains("mm-shortcut-keys"));

    private static List<Border> Caps(Visual root)
        => [.. root.GetVisualDescendants().OfType<Border>().Where(static border => border.Classes.Contains("kbd"))];

    private static List<string> CapTexts(Visual root)
        => [.. Caps(root).Select(static cap => Assert.IsType<TextBlock>(cap.Child).Text ?? string.Empty)];

    private static object? Resource(Window window, string key)
        => window.TryFindResource(key, window.ActualThemeVariant, out var value) ? value : null;

    private static ShellViewModel CreateViewModel(string platformName)
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        var platform = new FakePlatformServices(fileSystem) { PlatformName = platformName };

        return new ShellViewModel(
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
            new WorkspaceFileOperationsUseCase(fileSystem, platform),
            platform,
            static () => new FakeWorkspaceWatcher(),
            new RecordingWindowLauncher());
    }

    private static List<Border> RenderCaps(IReadOnlyList<string> keys, ThemeVariant theme, out Window window)
    {
        var row = new ItemsControl { Classes = { "mm-shortcut-keys" }, ItemsSource = keys };
        window = ThemedTestWindow.Create(theme, new StackPanel { Children = { row } });
        window.Show();
        window.UpdateLayout();

        return [.. row.GetVisualDescendants().OfType<Border>().Where(static border => border.Classes.Contains("kbd"))];
    }
}
