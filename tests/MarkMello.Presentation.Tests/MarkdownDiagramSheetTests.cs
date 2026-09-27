using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MarkMello.Domain;
using MarkMello.Presentation.Views;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Naiad рисует Mermaid только в светлой палитре: рёбра, подписи и текст без
/// fill — тёмные. В тёмной теме диаграмма лежит на светлом листе, в светлой —
/// прямо на странице. Регрессия: в тёмной теме линии и подписи диаграмм были
/// тёмными на тёмном фоне и почти не читались.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MarkdownDiagramSheetTests
{
    private const string Svg =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><line x1="0" y1="5" x2="10" y2="5" stroke="#333"/></svg>""";

    private readonly AvaloniaHeadlessFixture _fixture;

    public MarkdownDiagramSheetTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task DarkThemeLaysALightSheetUnderTheDiagram()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, sheet) = Show(ThemeVariant.Dark);

            var background = Color(sheet);
            Assert.Equal(255, background.A);
            // Тёмные штрихи Mermaid (#333) должны читаться на листе: контраст не ниже 7:1.
            Assert.True(Contrast(background, Avalonia.Media.Color.Parse("#333333")) >= 7);
            Assert.Same(Resource(window, "MmDiagramBackgroundBrush", ThemeVariant.Dark), sheet.Background);

            // Полоса прокрутки лежит на светлом листе — ей светлая тема.
            var scroll = Assert.Single(sheet.GetVisualDescendants().OfType<ScrollViewer>());
            Assert.Equal(ThemeVariant.Light, scroll.ActualThemeVariant);

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task LightThemeKeepsTheDiagramOnThePage()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, sheet) = Show(ThemeVariant.Light);

            Assert.Equal(0, Color(sheet).A);

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task SheetFollowsThemeChangeWithoutRebuildingTheDiagram()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, sheet) = Show(ThemeVariant.Light);
            var bounds = sheet.Bounds;

            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();

            // Тот же блок, без повторного рендера SVG, и документ не сдвинулся.
            Assert.Same(sheet, Sheet(window));
            Assert.Equal(255, Color(sheet).A);
            Assert.Equal(bounds, sheet.Bounds);

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task SheetHasPaddingAndCornersInEm()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, sheet) = Show(ThemeVariant.Dark);
            var em = ReadingPreferences.Default.FontSize;

            // Нижнее поле — внутри прокрутки: полоса широкой диаграммы ложится в него,
            // а не на картинку.
            var scrolledPadding = sheet.GetVisualDescendants().OfType<ScrollViewer>().Single()
                .GetVisualDescendants().OfType<Border>().First(border => border.Child is Image).Padding;
            Assert.Equal(new Thickness(em, em, em, 0), sheet.Padding);
            Assert.Equal(new Thickness(0, 0, 0, em), scrolledPadding);
            Assert.Equal(new CornerRadius(em * 0.625), sheet.CornerRadius);

            window.Close();
        }, CancellationToken.None);
    }

    private static (Window Window, Border Sheet) Show(ThemeVariant theme)
    {
        var diagram = new MarkdownDiagramBlock(MarkdownDiagramKind.Mermaid, "flowchart LR\nA --> B")
        {
            RenderResult = new DiagramRenderResult.Success(Svg),
        };
        var view = new MarkdownDocumentView
        {
            ReadingPreferences = ReadingPreferences.Default,
            Document = new RenderedMarkdownDocument([diagram]),
        };
        var window = ThemedTestWindow.Create(theme, view);
        window.Width = 700;
        window.Height = 500;
        window.Show();
        window.UpdateLayout();
        return (window, Sheet(window));
    }

    private static Border Sheet(Window window)
    {
        var diagram = Assert.Single(window.GetVisualDescendants().OfType<MarkdownDiagramBlockView>());
        var sheet = Assert.IsType<Border>(diagram.Content);
        Assert.Contains("mm-md-diagram-success", sheet.Classes);
        return sheet;
    }

    private static Color Color(Border sheet)
        => Assert.IsAssignableFrom<ISolidColorBrush>(sheet.Background).Color;

    private static object Resource(Window window, string key, ThemeVariant theme)
    {
        Assert.True(window.TryFindResource(key, theme, out var value), $"{key} is not defined in the theme");
        return value!;
    }

    private static double Contrast(Color a, Color b)
    {
        var (lighter, darker) = (Luminance(a), Luminance(b)) is var (x, y) && x > y ? (x, y) : (y, x);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }
}
