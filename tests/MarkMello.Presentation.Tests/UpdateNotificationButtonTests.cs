using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.Services;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Кнопка обновления в строке окна (ADR-0004, «Update Model»): когда она видна, что в ней
/// нарисовано, как выезжает надпись и что делает нажатие.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class UpdateNotificationButtonTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public UpdateNotificationButtonTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Пока новой версии нет, кнопки нет и в визуальном дереве: она создаётся, только когда
    /// есть что показать. Ошибка загрузки кнопку оставляет — версия всё ещё известна; убирает
    /// её только ручной ответ «последняя версия».
    /// </summary>
    [Fact]
    public Task ButtonExistsOnlyWhileAnUpdateIsPending()
    {
        return _fixture.RunAsync(async () =>
        {
            var (updates, service, coordinator) = await UpdateWindowTests.CreateInStateAsync("up-to-date");
            var window = Show(CreateShell(updates));

            Assert.False(window.GetControl<ContentControl>("UpdateButtonHost").IsVisible);
            Assert.Empty(window.GetVisualDescendants().OfType<UpdateNotificationButton>());

            service.NextCheckResult = UpdateCoordinatorTests.Available();
            await coordinator.CheckAsync();
            Render(window);
            Assert.Single(window.GetVisualDescendants().OfType<UpdateNotificationButton>());

            service.NextDownloadResult = new MarkMello.Application.Updates.UpdateDownloadResult.Failed("reset");
            await coordinator.DownloadAsync();
            Render(window);
            Assert.Single(window.GetVisualDescendants().OfType<UpdateNotificationButton>());

            service.NextCheckResult = UpdateCoordinatorTests.UpToDate();
            await coordinator.CheckAsync();
            Render(window);
            Assert.Empty(window.GetVisualDescendants().OfType<UpdateNotificationButton>());

            window.Close();
        });
    }

    /// <summary>
    /// Стрелка — есть новая версия, кольцо — идёт загрузка, галочка — скачано. У каждого
    /// состояния своё имя для экранного диктора, с версией.
    /// </summary>
    [Theory]
    [InlineData("available", "UpdateArrowIcon", "Update to 1.2.3")]
    [InlineData("downloading", "UpdateProgressRing", "Downloading…")]
    [InlineData("downloaded", "UpdateCheckIcon", "Install 1.2.3")]
    public Task ButtonShowsTheStateIcon(string state, string icon, string automationName)
    {
        return _fixture.RunAsync(async () =>
        {
            var (updates, _, coordinator) = await UpdateWindowTests.CreateInStateAsync(state);
            var window = Show(CreateShell(updates));

            var button = window.GetVisualDescendants().OfType<UpdateNotificationButton>().Single();
            string[] icons = ["UpdateArrowIcon", "UpdateProgressRing", "UpdateCheckIcon"];
            Assert.Equal([icon], icons.Where(name => button.GetControl<Control>(name).IsVisible));
            Assert.Equal(automationName, AutomationProperties.GetName(button.GetControl<Button>("UpdateButton")));
            Assert.Equal(state == "downloading", button.GetControl<Button>("UpdateButton").Classes.Contains("mm-downloading"));

            coordinator.CancelDownload();
            window.Close();
        });
    }

    /// <summary>
    /// В раскладке кнопка всегда 30 px: при наведении надпись выезжает поверх вкладок, а
    /// кнопки строки не сдвигаются. Уходит курсор — надпись прячется.
    /// </summary>
    [Fact]
    public Task HoverRevealsTheLabelWithoutMovingTheRow()
    {
        return _fixture.RunAsync(async () =>
        {
            var (updates, _, _) = await UpdateWindowTests.CreateInStateAsync("available");
            var window = Show(CreateShell(updates));
            var host = window.GetControl<ContentControl>("UpdateButtonHost");
            var button = window.GetVisualDescendants().OfType<UpdateNotificationButton>().Single();
            var labelHost = button.GetControl<Border>("UpdateLabelHost");
            var menu = window.GetControl<ToggleButton>("AppMenuTriggerButton");
            var menuX = menu.TranslatePoint(default, window)!.Value.X;

            Assert.Equal(30, host.Bounds.Width);
            Assert.Equal(0, labelHost.Width);
            Assert.False(button.IsExpanded);

            window.MouseMove(Center(button.GetControl<Button>("UpdateButton"), window));
            Render(window);

            Assert.True(button.IsExpanded);
            Assert.True(labelHost.GetBaseValue(Layoutable.WidthProperty).Value > 30);
            Assert.Equal("Update", button.GetControl<TextBlock>("UpdateLabel").Text);
            Assert.Contains("mm-expanded", button.GetControl<Border>("UpdateHalo").Classes);
            Assert.Equal(30, host.Bounds.Width);
            Assert.Equal(menuX, menu.TranslatePoint(default, window)!.Value.X);

            window.MouseMove(new Point(4, window.Bounds.Height - 4));
            Render(window);

            Assert.False(button.IsExpanded);
            Assert.Equal(0d, labelHost.GetBaseValue(Layoutable.WidthProperty).Value);
            window.Close();
        });
    }

    /// <summary>
    /// Раскрытая плашка шире кнопки в раскладке и рисуется за её границами: ни один предок до
    /// строки окна не обрезает её по своим. Иначе видна только полоса в 30 px, ровно по кнопке.
    /// </summary>
    [Theory]
    [InlineData("available")]
    [InlineData("downloading")]
    [InlineData("downloaded")]
    public Task ExpandedLabelIsNotClippedByTheRow(string state)
    {
        return _fixture.RunAsync(async () =>
        {
            var (updates, _, coordinator) = await UpdateWindowTests.CreateInStateAsync(state);
            var window = Show(CreateShell(updates));
            var button = window.GetVisualDescendants().OfType<UpdateNotificationButton>().Single();
            var halo = button.GetControl<Border>("UpdateHalo");
            var row = window.GetControl<Grid>("WindowRowContent");

            // В покое плашка без фона: левее кнопки мышь достаётся вкладкам, а не ей.
            var besideButton = Center(button.GetControl<Button>("UpdateButton"), window) - new Point(40, 0);
            var hit = window.InputHitTest(besideButton) as Visual;
            Assert.False(hit is not null && (ReferenceEquals(hit, button) || hit.GetVisualAncestors().Contains(button)));

            window.MouseMove(Center(button.GetControl<Button>("UpdateButton"), window));
            Render(window);

            Assert.True(button.IsExpanded);
            Assert.True(button.GetControl<Border>("UpdateLabelHost").GetBaseValue(Layoutable.WidthProperty).Value > 30);
            var clipping = halo.GetVisualAncestors()
                .TakeWhile(ancestor => !ReferenceEquals(ancestor, row))
                .Where(ancestor => ancestor.ClipToBounds)
                .Select(ancestor => ancestor.GetType().Name + (ancestor is Control { Name: { } name } ? " " + name : ""));
            Assert.Empty(clipping);

            coordinator.CancelDownload();
            window.Close();
        });
    }

    /// <summary>
    /// Раскрытая подложка облегает плашку — по 4 px с каждой стороны, какой бы ни была
    /// надпись. Левее неё мышь достаётся вкладкам, и надпись там сворачивается.
    /// </summary>
    [Theory]
    [InlineData("available", AppLanguage.English)]
    [InlineData("downloading", AppLanguage.English)]
    [InlineData("downloaded", AppLanguage.English)]
    [InlineData("available", AppLanguage.Russian)]
    [InlineData("downloading", AppLanguage.Russian)]
    [InlineData("downloaded", AppLanguage.Russian)]
    public Task ExpandedHaloHugsTheLabel(string state, AppLanguage language)
    {
        return _fixture.RunAsync(async () =>
        {
            var (updates, _, coordinator) = await UpdateWindowTests.CreateInStateAsync(state, localization: new LocalizationService(language));
            var window = Show(CreateShell(updates));
            var button = window.GetVisualDescendants().OfType<UpdateNotificationButton>().Single();
            var halo = button.GetControl<Border>("UpdateHalo");
            var updateButton = button.GetControl<Button>("UpdateButton");
            var labelHost = button.GetControl<Border>("UpdateLabelHost");

            // Без перехода надпись выезжает сразу: подложка меряется по полной ширине плашки.
            labelHost.Transitions = null;
            window.MouseMove(Center(updateButton, window));
            Render(window);

            Assert.True(button.IsExpanded);
            Assert.True(labelHost.Bounds.Width > 30);
            Assert.Equal(labelHost.Width, labelHost.Bounds.Width);
            Assert.Equal(updateButton.Bounds.Width + 8, halo.Bounds.Width);
            Assert.Equal(updateButton.Bounds.Height + 8, halo.Bounds.Height);

            var besideHalo = updateButton.TranslatePoint(new Point(-4 - 10, updateButton.Bounds.Height / 2), window)!.Value;
            var hit = window.InputHitTest(besideHalo) as Visual;
            Assert.False(hit is not null && (ReferenceEquals(hit, button) || hit.GetVisualAncestors().Contains(button)));

            window.MouseMove(besideHalo);
            Render(window);

            Assert.False(button.IsExpanded);
            coordinator.CancelDownload();
            window.Close();
        });
    }

    /// <summary>Во время загрузки надпись — проценты; кольцо показывает ту же долю.</summary>
    [Fact]
    public Task DownloadingLabelShowsPercent()
    {
        return _fixture.RunAsync(async () =>
        {
            var (updates, service, coordinator) = await UpdateWindowTests.CreateInStateAsync("downloading");
            var window = Show(CreateShell(updates));

            service.LastProgress!.Report(new MarkMello.Application.Updates.UpdateDownloadProgress(62, 100));
            Render(window);

            var button = window.GetVisualDescendants().OfType<UpdateNotificationButton>().Single();
            Assert.Equal("Downloading · 62%", button.GetControl<TextBlock>("UpdateLabel").Text);
            Assert.Equal(0.62, button.GetControl<ProgressRing>("UpdateProgressRing").Value);

            coordinator.CancelDownload();
            window.Close();
        });
    }

    /// <summary>
    /// Кольцо без доли крутится, только пока его видно: спрятанное бесконечной анимацией
    /// держало бы перерисовку окна.
    /// </summary>
    [Fact]
    public Task RingSpinsOnlyWhileVisibleAndWithoutAFraction()
    {
        return _fixture.RunAsync(() =>
        {
            var ring = new ProgressRing();
            Assert.Contains(":indeterminate", ring.Classes);

            ring.Value = 0.4;
            Assert.DoesNotContain(":indeterminate", ring.Classes);

            ring.Value = null;
            ring.IsVisible = false;
            Assert.DoesNotContain(":indeterminate", ring.Classes);

            // Свой поворот у каждого кольца: общий крутили бы все кольца разом.
            Assert.IsType<Avalonia.Media.RotateTransform>(ring.RenderTransform);
            Assert.NotSame(ring.RenderTransform, new ProgressRing().RenderTransform);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Нажатие: скачанное сразу уходит установке ОС, в остальных состояниях открывается окно
    /// обновления — без новой проверки.
    /// </summary>
    [Theory]
    [InlineData("available")]
    [InlineData("downloading")]
    [InlineData("downloaded")]
    public Task ClickOpensTheUpdateWindowOrInstalls(string state)
    {
        return _fixture.RunAsync(async () =>
        {
            var launcher = new RecordingWindowLauncher();
            var (updates, service, coordinator) = await UpdateWindowTests.CreateInStateAsync(state, launcher: launcher);
            service.NextPrepareResult = new MarkMello.Application.Updates.UpdatePrepareResult.Success("opened");
            var window = Show(CreateShell(updates));

            Click(window, window.GetVisualDescendants().OfType<UpdateNotificationButton>().Single().GetControl<Button>("UpdateButton"));

            if (state == "downloaded")
            {
                Assert.Empty(launcher.UpdateRequests);
                Assert.Equal(1, service.PrepareCount);
            }
            else
            {
                Assert.Equal([false], launcher.UpdateRequests);
                Assert.Equal(0, service.PrepareCount);
            }

            coordinator.CancelDownload();
            window.Close();
        });
    }

    /// <summary>
    /// «Установить» не молчит, если установка не началась: файл не открылся — открывается окно
    /// с объяснением; файла больше нет — окно предлагает скачать заново.
    /// </summary>
    [Theory]
    [InlineData(false, UpdateState.Downloaded)]
    [InlineData(true, UpdateState.Available)]
    public Task FailedInstallOpensTheUpdateWindow(bool fileMissing, UpdateState expected)
    {
        return _fixture.RunAsync(async () =>
        {
            var launcher = new RecordingWindowLauncher();
            var (updates, service, coordinator) = await UpdateWindowTests.CreateInStateAsync("downloaded", launcher: launcher);
            service.NextPrepareResult = fileMissing
                ? new MarkMello.Application.Updates.UpdatePrepareResult.FileNotFound("/tmp/Softmark.dmg")
                : new MarkMello.Application.Updates.UpdatePrepareResult.Failed("busy");
            var window = Show(CreateShell(updates));

            Click(window, window.GetVisualDescendants().OfType<UpdateNotificationButton>().Single().GetControl<Button>("UpdateButton"));

            Assert.Equal([false], launcher.UpdateRequests);
            Assert.Equal(expected, coordinator.State);
            Assert.Equal(!fileMissing, coordinator.OpenDownloadedFailed);
            window.Close();
        });
    }

    /// <summary>
    /// Кнопка с находкой фоновой проверки выводит эту версию в окно: оно открывается уже на
    /// «Доступна версия», без новой проверки.
    /// </summary>
    [Fact]
    public Task ClickOnABackgroundFindingShowsItInTheWindow()
    {
        return _fixture.RunAsync(async () =>
        {
            var launcher = new RecordingWindowLauncher();
            var service = new StubUpdateService { NextCheckResult = UpdateCoordinatorTests.Available() };
            var coordinator = new UpdateCoordinator(service, new FakePlatformServices(), new DeferredUpdateCheck(service, TimeSpan.FromMilliseconds(1)));
            var updates = new UpdateViewModel(coordinator, new LocalizationService(AppLanguage.English), launcher);
            coordinator.StartBackgroundCheck();
            await coordinator.BackgroundCheck!;
            var window = Show(CreateShell(updates));

            var button = window.GetVisualDescendants().OfType<UpdateNotificationButton>().Single();
            Assert.Equal("Update to 1.2.3", AutomationProperties.GetName(button.GetControl<Button>("UpdateButton")));
            Click(window, button.GetControl<Button>("UpdateButton"));

            Assert.Equal([false], launcher.UpdateRequests);
            Assert.Equal(UpdateState.Available, coordinator.State);
            Assert.Equal(1, service.CheckCount);
            coordinator.Dispose();
            window.Close();
        });
    }

    /// <summary>Состояние одно на приложение: кнопка появляется во всех окнах сразу.</summary>
    [Fact]
    public Task EveryWindowShowsTheSameButton()
    {
        return _fixture.RunAsync(async () =>
        {
            var (updates, service, coordinator) = await UpdateWindowTests.CreateInStateAsync("up-to-date");
            var first = Show(CreateShell(updates));
            var second = Show(CreateShell(updates));

            service.NextCheckResult = UpdateCoordinatorTests.Available();
            await coordinator.CheckAsync();
            Render(first);
            Render(second);

            Assert.Single(first.GetVisualDescendants().OfType<UpdateNotificationButton>());
            Assert.Single(second.GetVisualDescendants().OfType<UpdateNotificationButton>());
            first.Close();
            second.Close();
        });
    }

    /// <summary>
    /// Открытие главного окна запускает фоновую проверку; второе окно новой не добавляет.
    /// До открытия окна проверки нет.
    /// </summary>
    [Fact]
    public Task OpeningAWindowStartsTheSingleBackgroundCheck()
    {
        return _fixture.RunAsync(() =>
        {
            var service = new StubUpdateService();
            var coordinator = new UpdateCoordinator(service, new FakePlatformServices());
            var updates = new UpdateViewModel(coordinator, new LocalizationService(AppLanguage.English), new RecordingWindowLauncher());
            var shell = CreateShell(updates);
            Assert.Null(coordinator.BackgroundCheck);

            var first = Show(shell);
            var check = coordinator.BackgroundCheck;
            Assert.NotNull(check);
            var second = Show(CreateShell(updates));

            Assert.Same(check, coordinator.BackgroundCheck);
            Assert.Equal(0, service.CheckCount);

            coordinator.Dispose();
            first.Close();
            second.Close();
            return Task.CompletedTask;
        });
    }

    private static Point Center(Control control, Window window)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static void Click(Window window, Control control)
    {
        var point = Center(control, window);
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

    /// <summary>Окно из полного конструктора, как в <see cref="AppMenuAndSettingsWindowTests"/>: оно запускает фоновую проверку.</summary>
    private static MainWindow Show(ShellViewModel viewModel)
    {
        var window = new MainWindow(
            viewModel,
            StartupSmokeTestOptions.Disabled,
            new InMemorySettingsStore(),
            new RecordingStartupMetrics())
        {
            RequestedThemeVariant = ThemeVariant.Light
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

    private static ShellViewModel CreateShell(UpdateViewModel updates)
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        var platform = new FakePlatformServices(fileSystem);

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
            updates,
            new OpenFolderUseCase(fileSystem),
            new ExpandFolderNodeUseCase(fileSystem),
            new SearchWorkspaceFilesUseCase(fileSystem),
            new WorkspaceFileOperationsUseCase(fileSystem, platform),
            platform,
            static () => new FakeWorkspaceWatcher(),
            new RecordingWindowLauncher());
    }
}
