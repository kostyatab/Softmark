using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Нижняя строка карточки Aa: подсказка «Язык, обновления, версия», ссылка
/// «Настройки…» и плашки сочетания. Ссылка и плашки видны всегда и целиком;
/// подсказка — целиком или никак, без обрубка на полуслове. С плашками «⌘» «,» на
/// macOS она помещается только по-английски, с «Ctrl» «,» на Windows и Linux — нет.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class ReadingCardFooterTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public ReadingCardFooterTests(AvaloniaHeadlessFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("macOS", AppLanguage.Russian, false)]
    [InlineData("macOS", AppLanguage.English, true)]
    [InlineData("Windows", AppLanguage.Russian, false)]
    [InlineData("Windows", AppLanguage.English, false)]
    [InlineData("Linux", AppLanguage.Russian, false)]
    public Task HintIsWholeOrHiddenAndLinkWithShortcutAlwaysFit(string platformName, AppLanguage language, bool hintShown)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var view = new ReadingSettingsPanelView { DataContext = CreateViewModel(platformName, language) };
            var window = ThemedTestWindow.Create(ThemeVariant.Light, view);
            window.Show();
            window.UpdateLayout();

            var footer = view.GetVisualDescendants().OfType<Button>().Single(static button => button.Classes.Contains("mm-card-footer"));
            var hint = footer.GetVisualDescendants().OfType<OptionalTextBlock>().Single();
            var link = footer.GetVisualDescendants().OfType<TextBlock>().Single(static text => text.Classes.Contains("mm-card-footer-link"));
            var shortcut = footer.GetVisualDescendants().OfType<ItemsControl>().Single(static keys => keys.Classes.Contains("mm-shortcut-keys"));

            Assert.Equal(!hintShown, hint.IsTrimmed);
            Assert.DoesNotContain(link.TextLayout.TextLines, static line => line.HasCollapsed);

            var shortcutRight = shortcut.TranslatePoint(new Point(shortcut.Bounds.Width, 0), footer)!.Value.X;
            var contentRight = footer.Bounds.Width - footer.Padding.Right;
            Assert.True(
                shortcutRight <= contentRight + 0.5,
                $"Shortcut ends at {shortcutRight:0.0}, footer content at {contentRight:0.0}.");

            window.Close();
        }, CancellationToken.None);
    }

    private static ShellViewModel CreateViewModel(string platformName, AppLanguage language)
    {
        var fileSystem = new FakeWorkspaceFileSystem();
        var platform = new FakePlatformServices(fileSystem) { PlatformName = platformName };

        return new ShellViewModel(
            new OpenDocumentUseCase(new StubDocumentLoader()),
            new SaveDocumentUseCase(new RecordingDocumentSaver()),
            new StubFilePicker(),
            new StubCommandLineActivation(),
            new LocalizationService(language),
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
