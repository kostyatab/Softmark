using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Domain;
using MarkMello.Infrastructure.Markdown;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Своя полоса прокрутки (Themes/ScrollBar.axaml): без стрелок и дорожки, в покое не
/// видна, появляется при прокрутке; у блоков документа, которые листаются вбок, —
/// видна всегда, пока содержимое не помещается, и ничего не закрывает.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class ScrollBarThemeTests
{
    private const double ThinThumb = 7;
    private const double WideThumb = 11;
    private const double ScrollBarThickness = 12;

    private const string WideSvg =
        """<svg xmlns="http://www.w3.org/2000/svg" width="2000" height="120" viewBox="0 0 2000 120"><line x1="0" y1="60" x2="2000" y2="60" stroke="#333"/></svg>""";

    private const string NarrowSvg =
        """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="60" viewBox="0 0 100 60"><line x1="0" y1="30" x2="100" y2="30" stroke="#333"/></svg>""";

    private readonly AvaloniaHeadlessFixture _fixture;

    public ScrollBarThemeTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task ScrollBarHasNoArrowsNorTrackAndIsHiddenAtRest(string theme)
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, scroll) = ShowTallContent(theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);
            var bar = VerticalBar(scroll);

            Assert.True(bar.IsVisible);
            Assert.Equal(ScrollBarThickness, bar.Bounds.Width);
            Assert.DoesNotContain(bar.GetVisualDescendants(), visual => visual is Control
            {
                Name: "PART_LineUpButton" or "PART_LineDownButton" or "TrackRect"
            });

            var thumb = Thumb(bar);
            Assert.Equal(ThinThumb, thumb.Bounds.Width);
            Assert.Equal(0, thumb.Opacity);

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task ScrollingRevealsTheScrollBarUntilAPause()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, scroll) = ShowTallContent(ThemeVariant.Light);
            var bar = VerticalBar(scroll);
            bar.HideDelay = TimeSpan.FromMilliseconds(400);
            Assert.DoesNotContain(ScrollBarReveal.ScrollingClass, bar.Classes);

            scroll.Offset = new Vector(0, 200);
            Wait(TimeSpan.FromMilliseconds(250));

            Assert.Contains(ScrollBarReveal.ScrollingClass, bar.Classes);
            Assert.Equal(1, Thumb(bar).Opacity);

            Wait(TimeSpan.FromMilliseconds(500));

            Assert.DoesNotContain(ScrollBarReveal.ScrollingClass, bar.Classes);
            Assert.Equal(0, Thumb(bar).Opacity);

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task HoveringTheScrollBarWidensTheThumb()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, scroll) = ShowTallContent(ThemeVariant.Light);
            var bar = VerticalBar(scroll);
            bar.ShowDelay = TimeSpan.FromMilliseconds(10);
            var thumb = Thumb(bar);

            window.MouseMove(bar.TranslatePoint(new Point(bar.Bounds.Width / 2, 40), window)!.Value);
            Wait(TimeSpan.FromMilliseconds(250));

            Assert.True(bar.IsExpanded);
            Assert.Equal(1, thumb.Opacity);
            Assert.Equal(WideThumb, thumb.Bounds.Width);

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task WideDocumentBlocksKeepTheirHorizontalScrollBarVisibleAtRest()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                .. Render(
                    $"""
                    | {string.Join(" | ", Enumerable.Range(1, 12).Select(static index => $"Column number {index}"))} |
                    |{string.Concat(Enumerable.Repeat("---|", 12))}
                    | {string.Join(" | ", Enumerable.Range(1, 12).Select(static index => $"value {index}"))} |

                    ```
                    {new string('x', 400)}
                    ```
                    """).Blocks,
                Diagram(WideSvg)
            ]));
            var window = Show(view);

            var blocks = PersistentScrollViewers(view);
            Assert.Equal(3, blocks.Count);
            foreach (var block in blocks)
            {
                Assert.True(block.Extent.Width > block.Viewport.Width + 1);

                var bar = HorizontalBar(block);
                Assert.True(bar.IsVisible);
                Assert.Equal(1, Thumb(bar).Opacity);
                Assert.Equal(ThinThumb, Thumb(bar).Bounds.Height);

                // Полоса лежит в поле под содержимым и не закрывает его.
                var content = LowestContent(block);
                var contentBottom = content.TranslatePoint(new Point(0, content.Bounds.Height), window)!.Value.Y;
                var barTop = bar.TranslatePoint(default, window)!.Value.Y;
                Assert.True(
                    barTop >= contentBottom - 0.5,
                    $"{block.Content?.GetType().Name}: полоса {barTop} выше низа содержимого {contentBottom}");
            }

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task DocumentBlocksThatFitHaveNoHorizontalScrollBar()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var view = CreateView(new RenderedMarkdownDocument(
            [
                .. Render(
                    """
                    | A | B |
                    |---|---|
                    | 1 | 2 |

                    ```
                    short
                    ```
                    """).Blocks,
                Diagram(NarrowSvg)
            ]));
            var window = Show(view);

            var blocks = PersistentScrollViewers(view);
            Assert.Equal(3, blocks.Count);
            Assert.All(blocks, block => Assert.False(HorizontalBar(block).IsVisible));

            window.Close();
        }, CancellationToken.None);
    }

    /// <summary>
    /// Даёт пройти настоящему времени: таймеры показа и скрытия полосы и переходы
    /// прозрачности и ширины ползунка.
    /// </summary>
    private static void Wait(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, duration);
        Dispatcher.UIThread.PushFrame(frame);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static RenderedMarkdownDocument Render(string markdown)
        => new MarkdigMarkdownDocumentRenderer().Render(markdown);

    private static MarkdownDiagramBlock Diagram(string svg)
        => new(MarkdownDiagramKind.Mermaid, "flowchart LR\nA --> B")
        {
            RenderResult = new DiagramRenderResult.Success(svg),
        };

    private static MarkdownDocumentView CreateView(RenderedMarkdownDocument document)
        => new()
        {
            ReadingPreferences = ReadingPreferences.Default,
            Document = document
        };

    private static Window Show(Control content)
    {
        var window = ThemedTestWindow.Create(ThemeVariant.Light, content);
        window.Width = 600;
        window.Height = 1200;
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static (Window Window, ScrollViewer Scroll) ShowTallContent(ThemeVariant theme)
    {
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Height = 5000 }
        };
        var window = ThemedTestWindow.Create(theme, scroll);
        window.Show();
        window.UpdateLayout();
        return (window, scroll);
    }

    private static List<ScrollViewer> PersistentScrollViewers(Control root)
        => root.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Where(static scroll => scroll.Classes.Contains(ScrollBarReveal.PersistentHorizontalClass))
            .ToList();

    private static ScrollBar VerticalBar(ScrollViewer scroll) => Bar(scroll, Orientation.Vertical);

    private static ScrollBar HorizontalBar(ScrollViewer scroll) => Bar(scroll, Orientation.Horizontal);

    private static ScrollBar Bar(ScrollViewer scroll, Orientation orientation)
        => scroll.GetVisualDescendants()
            .OfType<ScrollBar>()
            .Single(bar => bar.Orientation == orientation && ReferenceEquals(bar.TemplatedParent, scroll));

    private static Thumb Thumb(ScrollBar bar) => bar.GetVisualDescendants().OfType<Thumb>().Single();

    /// <summary>
    /// Самое нижнее содержимое блока: у таблицы — панель ячеек без её отступа под
    /// полосу, у кода — текст, у диаграммы — картинка.
    /// </summary>
    private static Control LowestContent(ScrollViewer block)
    {
        if (block.Content is Views.Markdown.MarkdownTablePanel panel)
        {
            return panel.Children.OfType<Control>().MaxBy(static cell => cell.Bounds.Bottom)!;
        }

        var inner = Assert.IsType<Border>(block.Content);
        return inner.Child!;
    }
}
