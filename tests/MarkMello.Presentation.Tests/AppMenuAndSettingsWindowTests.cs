using Avalonia;
using Avalonia.Controls;
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
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;
using System.Globalization;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Меню ⋯ (A-Menu, ADR-0009 Rule 4) и окно «Настройки» (A-AppSettings, Rule 7) в живом
/// окне: какие пункты видны, что делает клик по пункту, как окно открывается и закрывается.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class AppMenuAndSettingsWindowTests
{
    private static readonly string Root = TestPaths.At("menu-docs");
    private static readonly string Readme = TestPaths.At("menu-docs", "README.md");

    private readonly AvaloniaHeadlessFixture _fixture;

    public AppMenuAndSettingsWindowTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Без папки пунктов папки нет; сохранение, перечитывание и закрытие вкладки гаснут,
    /// когда их не к чему применить. «Закрыть файл» из меню ушёл — его заменила вкладка.
    /// </summary>
    [Fact]
    public Task MenuOnTheWelcomeScreenHidesFolderItemsAndDimsDocumentCommands()
    {
        return _fixture.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var window = Show(viewModel);

            viewModel.ToggleAppMenuCommand.Execute(null);
            Render(window);

            // «Проверить обновления…», «О MarkMello» и разделитель над ними — только вне macOS:
            // там оба пункта в системном меню.
            string[] expectedItems = OperatingSystem.IsMacOS()
                ? ["MenuNewDocument", "MenuOpenFile", "MenuOpenFolder", "MenuSave", "MenuSaveAs", "MenuReload", "MenuCloseTab", "MenuSettings"]
                : ["MenuNewDocument", "MenuOpenFile", "MenuOpenFolder", "MenuSave", "MenuSaveAs", "MenuReload", "MenuCloseTab", "MenuSettings", "MenuCheckForUpdates", "MenuAbout"];

            Assert.Equal(expectedItems, VisibleMenuItems(window).Select(static button => button.Name));
            Assert.Equal(
                ["MenuSave", "MenuSaveAs", "MenuReload", "MenuCloseTab"],
                VisibleMenuItems(window).Where(static button => !button.IsEffectivelyEnabled).Select(static button => button.Name));
            Assert.Equal(
                OperatingSystem.IsMacOS() ? 3 : 4,
                window.GetVisualDescendants().OfType<Border>().Count(static border => border.Classes.Contains("mm-menu-separator") && border.IsVisible));

            window.Hide();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// С папкой появляются «Панель файлов» и «Закрыть папку». Клик по пункту выполняет
    /// команду и закрывает меню — даже если команда сама меню не трогает.
    /// </summary>
    [Fact]
    public Task MenuItemRunsItsCommandAndClosesTheMenu()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = await CreateViewModelWithFolderAsync();
            var window = Show(viewModel);

            viewModel.ToggleAppMenuCommand.Execute(null);
            Render(window);

            Assert.Contains("MenuFilesPanel", VisibleMenuItems(window).Select(static button => button.Name));
            Assert.Contains("MenuCloseFolder", VisibleMenuItems(window).Select(static button => button.Name));
            Assert.False(viewModel.IsSidebarCollapsed);

            Click(window, MenuItem(window, "MenuFilesPanel"));

            Assert.True(viewModel.IsSidebarCollapsed);
            Assert.False(viewModel.IsAppMenuOpen);
            Assert.Null(window.GetControl<ContentControl>("AppMenuPanel").Content);

            window.Hide();
        });
    }

    /// <summary>
    /// «Настройки…» открывают окно на рамке диалогов: скрим на всё окно, карточка 480 px.
    /// Меню под ним закрыто, ✕ окно закрывает.
    /// </summary>
    [Fact]
    public Task SettingsItemOpensTheSettingsWindowAndCloseButtonClosesIt()
    {
        return _fixture.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var window = Show(viewModel);
            Assert.Empty(window.GetVisualDescendants().OfType<AppSettingsDialogView>());

            viewModel.ToggleAppMenuCommand.Execute(null);
            Render(window);
            Click(window, MenuItem(window, "MenuSettings"));

            Assert.True(viewModel.IsAppSettingsOpen);
            Assert.False(viewModel.IsAppMenuOpen);
            var frame = window.GetVisualDescendants().OfType<AppSettingsDialogView>().Single()
                .GetVisualDescendants().OfType<ModalDialogFrame>().Single();
            Assert.Equal(window.GetControl<Grid>("SidebarLayout").Bounds.Size, frame.Bounds.Size);
            var card = frame.GetVisualDescendants().OfType<Border>().Single(static border => border.Classes.Contains("mm-dialog-card"));
            Assert.Equal(480, card.Bounds.Width);

            // Клик по скриму окно не закрывает, как и у остальных диалогов.
            Click(window, new Point(20, window.Bounds.Height - 20));
            Assert.True(viewModel.IsAppSettingsOpen);

            Click(window, window.GetVisualDescendants().OfType<Button>().Single(static button => button.Name == "CloseSettingsButton"));

            Assert.False(viewModel.IsAppSettingsOpen);
            Assert.Empty(window.GetVisualDescendants().OfType<AppSettingsDialogView>());
            window.Hide();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// «Проверить обновления…» — одна просьба к <c>IWindowLauncher</c> показать окно обновления
    /// с новой проверкой; окно и единственный его экземпляр — забота запуска окон. Подпись
    /// идёт за языком приложения.
    /// </summary>
    [Fact]
    public void CheckForUpdatesAsksTheLauncherForTheUpdateWindowWithACheck()
    {
        var launcher = new RecordingWindowLauncher();
        var localization = new LocalizationService(AppLanguage.English);
        var viewModel = CreateViewModel(launcher: launcher, localization: localization);

        viewModel.CheckForUpdatesCommand.Execute(null);

        Assert.Equal([true], launcher.UpdateRequests);
        Assert.Equal("Check for Updates…", viewModel.AppMenuCheckForUpdates);
        localization.SetLanguage(AppLanguage.Russian);
        Assert.Equal("Проверить обновления…", viewModel.AppMenuCheckForUpdates);
    }

    /// <summary>Вне macOS пункт в меню ⋯ стоит над «О Softmark» и запускает ту же команду.</summary>
    [FactSkippedOnMacOs("На macOS пункт живёт в системном меню приложения, а не в меню ⋯.")]
    public Task CheckForUpdatesMenuItemSitsAboveAboutAndOpensTheUpdateWindow()
    {
        return _fixture.RunAsync(() =>
        {
            var launcher = new RecordingWindowLauncher();
            var viewModel = CreateViewModel(launcher: launcher);
            var window = Show(viewModel);

            viewModel.ToggleAppMenuCommand.Execute(null);
            Render(window);

            var names = VisibleMenuItems(window).Select(static button => button.Name).ToList();
            Assert.Equal(names.IndexOf("MenuAbout") - 1, names.IndexOf("MenuCheckForUpdates"));

            Click(window, MenuItem(window, "MenuCheckForUpdates"));

            Assert.Equal([true], launcher.UpdateRequests);
            Assert.False(viewModel.IsAppMenuOpen);
            window.Hide();
            return Task.CompletedTask;
        });
    }

    /// <summary>⌘, (Ctrl+,) открывает окно на стартовом экране, Esc закрывает.</summary>
    [Theory]
    [InlineData(RawInputModifiers.Meta)]
    [InlineData(RawInputModifiers.Control)]
    public Task SettingsShortcutOpensTheWindowAndEscapeClosesIt(RawInputModifiers modifier)
    {
        return _fixture.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var window = Show(viewModel);

            window.KeyPress(Key.OemComma, modifier, PhysicalKey.Comma, ",");
            Render(window);

            Assert.True(viewModel.IsAppSettingsOpen);
            Assert.Single(window.GetVisualDescendants().OfType<AppSettingsDialogView>());

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render(window);

            Assert.False(viewModel.IsAppSettingsOpen);
            Assert.Empty(window.GetVisualDescendants().OfType<AppSettingsDialogView>());
            window.Hide();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Одна страница по A-AppSettings: подсказка про Aa, язык, рамка окна и строка версии со
    /// ссылками — в обеих темах на токенах палитры. Раздела «Обновления» нет: проверка живёт
    /// в меню приложения и окне обновления (ADR-0004, «Update Model»).
    /// </summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task SettingsWindowShowsOnePageOnThePalette(string theme)
    {
        return _fixture.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var window = Show(viewModel, theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);

            viewModel.OpenAppSettingsCommand.Execute(null);
            Render(window);

            var dialog = window.GetVisualDescendants().OfType<AppSettingsDialogView>().Single();
            var texts = dialog.GetVisualDescendants().OfType<TextBlock>().Select(static block => block.Text).ToList();
            Assert.Contains("Settings", texts);
            Assert.Contains("Language", texts);
            Assert.Contains("Window border", texts);
            Assert.DoesNotContain("UPDATES", texts);
            Assert.Contains(texts, static text => text is not null && text.StartsWith("Softmark ", StringComparison.Ordinal) && text.EndsWith("· GPLv3", StringComparison.Ordinal));
            Assert.Null(dialog.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault());

            Assert.DoesNotContain(dialog.GetVisualDescendants().OfType<Button>(), static button => button.Name == "UpdateActionButton");

            Assert.Equal(
                ["https://github.com/kostyatab/Softmark"],
                dialog.GetVisualDescendants().OfType<Button>().Where(static button => button.Classes.Contains("mm-link")).Select(static button => button.Tag as string));

            window.Hide();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Между строками настроек — по линии, над футером — одна линия, его собственная: у
    /// последней строки нижней границы нет. От неё до линии 16 px, от линии до текста 14.
    /// </summary>
    [Fact]
    public Task SettingsWindowHasOneDividerAboveTheFooter()
    {
        return _fixture.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var window = Show(viewModel);

            viewModel.OpenAppSettingsCommand.Execute(null);
            Render(window);

            var dialog = window.GetVisualDescendants().OfType<AppSettingsDialogView>().Single();
            var rows = dialog.GetVisualDescendants().OfType<Border>()
                .Where(static border => border.Classes.Contains("mm-app-settings-row"))
                .ToList();
            Assert.True(rows.Count > 1);
            Assert.All(rows[..^1], static row => Assert.Equal(new Thickness(0, 0, 0, 1), row.BorderThickness));
            Assert.Equal(default, rows[^1].BorderThickness);

            var footer = dialog.GetVisualDescendants().OfType<Border>()
                .Single(static border => border.Classes.Contains("mm-app-settings-footer"));
            Assert.Equal(new Thickness(0, 1, 0, 0), footer.BorderThickness);
            Assert.Equal(new Thickness(0, 8, 0, 0), footer.Margin);
            Assert.Equal(new Thickness(0, 14, 0, 0), footer.Padding);
            Assert.Equal(8, rows[^1].Padding.Bottom);

            window.Hide();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Язык выбирают в ComboBox окна настроек. Все шесть переходов между «Системным»,
    /// English и «Русским» — и когда язык интерфейса от выбора меняется, и когда нет
    /// (системный совпадает с выбранным): выбор не откатывается, ComboBox не пустеет,
    /// подписи вариантов идут за языком интерфейса.
    /// </summary>
    [Theory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    public Task LanguageComboBoxKeepsEverySelection(string systemCulture)
    {
        return _fixture.RunAsync(() =>
        {
            var previousCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(systemCulture);
            try
            {
                var settings = new InMemorySettingsStore { Language = AppLanguage.System };
                var localization = new LocalizationService(AppLanguage.System);
                var viewModel = CreateViewModel(localization: localization, settings: settings);
                var window = Show(viewModel);
                viewModel.OpenAppSettingsCommand.Execute(null);
                Render(window);

                var combo = window.GetVisualDescendants().OfType<ComboBox>()
                    .Single(static box => box.Classes.Contains("mm-language-select"));
                AppLanguage[] order = [AppLanguage.System, AppLanguage.English, AppLanguage.Russian];

                // Обход задевает каждый из шести переходов ровно раз.
                AppLanguage[] path =
                [
                    AppLanguage.Russian, AppLanguage.English, AppLanguage.System,
                    AppLanguage.English, AppLanguage.Russian, AppLanguage.System
                ];
                var from = AppLanguage.System;
                foreach (var to in path)
                {
                    var step = $"{from} → {to}";
                    combo.SelectedIndex = Array.IndexOf(order, to);
                    Render(window);

                    Assert.True(to == viewModel.Language, step);
                    Assert.True(to == settings.Language, step);
                    Assert.True(Array.IndexOf(order, to) == combo.SelectedIndex, step);
                    var selected = Assert.IsType<LanguageSelectionItem>(combo.SelectedItem);
                    Assert.True(to == selected.Language, step);
                    Assert.Same(viewModel.SelectedLanguageOption, selected);

                    string[] expectedLabels =
                    [
                        localization["LanguageSystem"],
                        localization["LanguageEnglish"],
                        localization["LanguageRussian"]
                    ];
                    Assert.Equal(expectedLabels, viewModel.LanguageOptions.Select(static option => option.Label));
                    Assert.Contains(
                        expectedLabels[Array.IndexOf(order, to)],
                        combo.GetVisualDescendants().OfType<TextBlock>().Select(static block => block.Text));
                    from = to;
                }

                window.Hide();
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCulture;
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Раскрытый список языков при смене языка показывает те же пункты модели и
    /// перерисовывает их подписи. Окно тестов выше собирается без шаблона Fluent, и popup в нём не открыть,
    /// поэтому карточка настроек здесь — в окне с темой.
    /// </summary>
    [Fact]
    public Task OpenLanguageListRelabelsItsItemsWhenTheLanguageChanges()
    {
        return _fixture.RunAsync(() =>
        {
            var viewModel = CreateViewModel(localization: new LocalizationService(AppLanguage.English));
            var window = ThemedTestWindow.Create(ThemeVariant.Light, new AppSettingsDialogView { DataContext = viewModel });
            window.Show();
            Render(window);

            var combo = window.GetVisualDescendants().OfType<ComboBox>()
                .Single(static box => box.Classes.Contains("mm-language-select"));
            combo.IsDropDownOpen = true;
            Render(window);
            var items = combo.GetRealizedContainers().OfType<ComboBoxItem>().ToList();
            var options = items.Select(static item => item.Content).ToList();
            Assert.Equal(["System", "English", "Russian"], Labels(items));

            combo.SelectedIndex = 2;
            Render(window);

            Assert.Equal(AppLanguage.Russian, viewModel.Language);
            items = combo.GetRealizedContainers().OfType<ComboBoxItem>().ToList();
            Assert.True(options.SequenceEqual(items.Select(static item => item.Content), ReferenceEqualityComparer.Instance));
            Assert.Equal(["Системный", "Английский", "Русский"], Labels(items));

            window.Close();
            return Task.CompletedTask;
        });

        static IEnumerable<string?> Labels(IEnumerable<ComboBoxItem> items)
            => items.Select(static item => item.GetVisualDescendants().OfType<TextBlock>().Single().Text);
    }

    private static IEnumerable<Button> VisibleMenuItems(Window window)
        => window.GetControl<ContentControl>("AppMenuPanel")
            .GetVisualDescendants()
            .OfType<Button>()
            .Where(static button => button.Classes.Contains("mm-menu-command") && button.IsVisible);

    private static Button MenuItem(Window window, string name)
        => VisibleMenuItems(window).Single(button => button.Name == name);

    private static void Click(Window window, Control control)
        => Click(window, control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value);

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Render(window);
    }

    /// <summary>
    /// Попадание мыши считается по отрисованной сцене, а меню закрывается отложенно —
    /// после команды пункта.
    /// </summary>
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

    /// <summary>
    /// Окно из полного конструктора: только он подключает ⌘, и классы, которыми карточки
    /// под строкой проявляются. Тема — как в <see cref="ModalDialogTests"/>: без неё нет
    /// шаблонов Fluent и стилей карточек; меню и диалог создаются уже после неё.
    /// </summary>
    private static MainWindow Show(ShellViewModel viewModel, ThemeVariant? theme = null)
    {
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
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(LoadTheme("Icons.axaml")));
        window.Show();
        Render(window);
        return window;
    }

    private static object LoadTheme(string themeFile)
        => AvaloniaXamlLoader.Load(new Uri("avares://MarkMello.Presentation/Themes/" + themeFile));

    private static async Task<ShellViewModel> CreateViewModelWithFolderAsync()
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        fileSystem.AddDirectory(Root, WorkspaceEntry.ForFile(Readme, "README.md"));
        var viewModel = CreateViewModel(fileSystem);
        await viewModel.OpenFolderPathAsync(Root);
        return viewModel;
    }

    private static ShellViewModel CreateViewModel(
        FakeWorkspaceFileSystem? fileSystem = null,
        RecordingWindowLauncher? launcher = null,
        LocalizationService? localization = null,
        InMemorySettingsStore? settings = null)
    {
        var loader = new StubDocumentLoader();
        loader.Sources[Readme] = new MarkdownSource(Readme, "README.md", "# readme");
        fileSystem ??= new FakeWorkspaceFileSystem();
        var platform = new FakePlatformServices(fileSystem);

        return new ShellViewModel(
            new OpenDocumentUseCase(loader),
            new SaveDocumentUseCase(new RecordingDocumentSaver()),
            new StubFilePicker(),
            new StubCommandLineActivation(),
            localization ?? new LocalizationService(AppLanguage.English),
            settings ?? new InMemorySettingsStore(),
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
            launcher ?? new RecordingWindowLauncher());
    }
}
