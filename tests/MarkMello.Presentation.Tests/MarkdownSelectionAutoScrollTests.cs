using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Domain;
using MarkMello.Presentation.Views;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Протягивание выделения за край страницы или широкого блока прокручивает их
/// по таймеру, пока курсор за краем, — как в браузерах.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MarkdownSelectionAutoScrollTests
{
    private const string LongLine = "let first = 1; padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding needle";

    private readonly AvaloniaHeadlessFixture _fixture;

    public MarkdownSelectionAutoScrollTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task DraggingPastTheRightEdgeOfACodeBlockScrollsItAndExtendsTheSelection()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, _, view) = ShowOnPage(new RenderedMarkdownDocument([new MarkdownCodeBlock(null, LongLine)]));
            var code = CodeScrollViewer(view);
            var fragment = FragmentIn(code);
            var start = PointOnCharacter(window, fragment, 1);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(Beside(window, code, start, pastRight: 20));
            var endBeforeScroll = view.SelectionEnd;

            Wait(TimeSpan.FromMilliseconds(300));

            Assert.True(code.Offset.X > 0);
            Assert.True(view.SelectionEnd > endBeforeScroll);

            window.MouseUp(start, MouseButton.Left);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task DraggingLeftOfAScrolledCodeBlockScrollsItBack()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, _, view) = ShowOnPage(new RenderedMarkdownDocument([new MarkdownCodeBlock(null, LongLine)]));
            var code = CodeScrollViewer(view);
            code.Offset = new Vector(code.ScrollBarMaximum.X, 0);
            Settle(window);
            var start = code.TranslatePoint(new Point(code.Viewport.Width / 2, 8), window)!.Value;
            var scrolledOffset = code.Offset.X;

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(Beside(window, code, start, pastRight: -code.Viewport.Width - 20));
            var startBeforeScroll = view.SelectionStart;
            Wait(TimeSpan.FromMilliseconds(300));

            Assert.True(code.Offset.X < scrolledOffset);
            Assert.True(view.SelectionStart < startBeforeScroll);

            window.MouseUp(start, MouseButton.Left);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task DraggingIntoTheFadeOfAWideTableScrollsIt()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, _, view) = ShowOnPage(WideTable());
            var host = view.GetVisualDescendants().OfType<MarkdownTableHost>().Single();
            Assert.True(host.RightFade > 0);
            var table = host.ScrollViewer;
            var start = PointOnCharacter(window, Cell(host, row: 1, column: 1), 0);

            window.MouseDown(start, MouseButton.Left);

            // Внутри полосы затухания, ещё не за краем.
            window.MouseMove(Beside(window, table, start, pastRight: -MarkdownTableHost.EdgeFadeWidth / 2));
            var endBeforeScroll = view.SelectionEnd;
            Wait(TimeSpan.FromMilliseconds(300));

            Assert.True(table.Offset.X > 0);
            Assert.True(view.SelectionEnd > endBeforeScroll);

            window.MouseUp(start, MouseButton.Left);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task DraggingBelowThePageScrollsItDownUntilRelease()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, page, view) = ShowOnPage(ManyParagraphs(), height: 400);
            var fragment = view.GetVisualDescendants().OfType<MarkdownSelectionTextFragment>().First();
            var start = PointOnCharacter(window, fragment, 1);
            var below = new Point(start.X, page.Bounds.Height + 30);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(below);
            var endBeforeScroll = view.SelectionEnd;
            Wait(TimeSpan.FromMilliseconds(300));

            Assert.True(page.Offset.Y > 0);
            Assert.True(view.SelectionEnd > endBeforeScroll);

            window.MouseUp(below, MouseButton.Left);
            var offsetAtRelease = page.Offset.Y;
            Wait(TimeSpan.FromMilliseconds(150));

            Assert.Equal(offsetAtRelease, page.Offset.Y);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task DraggingAboveAScrolledPageScrollsItUp()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, page, view) = ShowOnPage(ManyParagraphs(), height: 400);
            page.Offset = new Vector(0, page.ScrollBarMaximum.Y);
            Settle(window);
            var scrolledOffset = page.Offset.Y;
            var start = new Point(window.Bounds.Width / 2, page.Bounds.Height / 2);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point(start.X, -30));
            Wait(TimeSpan.FromMilliseconds(300));

            Assert.True(page.Offset.Y < scrolledOffset);
            Assert.True(view.HasSelection);

            window.MouseUp(start, MouseButton.Left);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task DraggingAfterDoubleClickScrollsThePage()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, page, view) = ShowOnPage(ManyParagraphs(), height: 400);
            var fragment = view.GetVisualDescendants().OfType<MarkdownSelectionTextFragment>().First();
            var start = PointOnCharacter(window, fragment, 1);

            window.MouseDown(start, MouseButton.Left);
            window.MouseUp(start, MouseButton.Left);
            window.MouseDown(start, MouseButton.Left);
            Assert.True(view.HasSelection);

            window.MouseMove(new Point(start.X, page.Bounds.Height + 30));
            Wait(TimeSpan.FromMilliseconds(300));

            Assert.True(page.Offset.Y > 0);

            window.MouseUp(start, MouseButton.Left);
            window.Close();
        }, CancellationToken.None);
    }

    /// <summary>
    /// Блок кода последний, курсор правее и ниже его строк — в нижнем поле
    /// документа. Ближайший к курсору текст всё ещё в блоке, но строки блока
    /// курсор покинул: блок стоит.
    /// </summary>
    [Fact]
    public Task CodeBlockStaysWhenThePointerLeavesItsRows()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, _, view) = ShowOnPage(
                new RenderedMarkdownDocument(
                [
                    Paragraph("A paragraph above the code block."),
                    new MarkdownCodeBlock(null, LongLine)
                ]),
                height: 800);
            var code = CodeScrollViewer(view);
            var start = PointOnCharacter(window, FragmentIn(code), 1);
            var besideAndBelow = code.TranslatePoint(new Point(code.Viewport.Width + 20, code.Bounds.Height + 10), window)!.Value;
            Assert.True(besideAndBelow.Y < view.TranslatePoint(new Point(0, view.Bounds.Height), window)!.Value.Y);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(besideAndBelow);
            Wait(TimeSpan.FromMilliseconds(300));

            Assert.Equal(0, code.Offset.X);
            Assert.True(view.HasSelection);

            window.MouseUp(besideAndBelow, MouseButton.Left);
            window.Close();
        }, CancellationToken.None);
    }

    /// <summary>
    /// Окно во весь экран: курсор не уходит ниже последней строки пикселей
    /// страницы, и прокрутку начинает полоса у края.
    /// </summary>
    [Fact]
    public Task DraggingToTheLastPixelRowOfThePageScrollsIt()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, page, view) = ShowOnPage(ManyParagraphs(), height: 400);
            var fragment = view.GetVisualDescendants().OfType<MarkdownSelectionTextFragment>().First();
            var start = PointOnCharacter(window, fragment, 1);
            var lastRow = new Point(start.X, page.Bounds.Height - 1);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(lastRow);
            Wait(TimeSpan.FromMilliseconds(300));

            Assert.True(page.Offset.Y > 0);

            window.MouseUp(lastRow, MouseButton.Left);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task PointerBackInsideStopsTheScroll()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, page, view) = ShowOnPage(ManyParagraphs(), height: 400);
            var fragment = view.GetVisualDescendants().OfType<MarkdownSelectionTextFragment>().First();
            var start = PointOnCharacter(window, fragment, 1);
            var inside = new Point(start.X, page.Bounds.Height / 2);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point(start.X, page.Bounds.Height + 30));
            Wait(TimeSpan.FromMilliseconds(150));
            Assert.True(page.Offset.Y > 0);

            window.MouseMove(inside);
            var offsetBackInside = page.Offset.Y;
            Wait(TimeSpan.FromMilliseconds(150));

            Assert.Equal(offsetBackInside, page.Offset.Y);
            Assert.True(view.HasSelection);

            window.MouseUp(inside, MouseButton.Left);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task LosingThePointerCaptureStopsTheScroll()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, page, view) = ShowOnPage(ManyParagraphs(), height: 400);
            IPointer? pointer = null;
            view.AddHandler(
                InputElement.PointerPressedEvent,
                (_, e) => pointer = e.Pointer,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            var fragment = view.GetVisualDescendants().OfType<MarkdownSelectionTextFragment>().First();
            var start = PointOnCharacter(window, fragment, 1);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point(start.X, page.Bounds.Height + 30));
            Wait(TimeSpan.FromMilliseconds(150));
            Assert.True(page.Offset.Y > 0);

            Assert.NotNull(pointer);
            pointer.Capture(null);
            var offsetAfterCaptureLoss = page.Offset.Y;
            Wait(TimeSpan.FromMilliseconds(150));

            Assert.Equal(offsetAfterCaptureLoss, page.Offset.Y);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task PointerPastTheWindowAndTheBlockScrollsBoth()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var blocks = new List<MarkdownBlock>();
            blocks.AddRange(ManyParagraphs().Blocks.Take(8));
            blocks.Add(new MarkdownCodeBlock(null, string.Join('\n', Enumerable.Repeat(LongLine, 30))));
            blocks.AddRange(ManyParagraphs().Blocks.Take(8));
            var (window, page, view) = ShowOnPage(new RenderedMarkdownDocument(blocks), height: 400);
            var code = CodeScrollViewer(view);

            // Блок кода уходит за нижний край окна.
            var codeTop = code.TranslatePoint(default, page)!.Value.Y;
            Assert.InRange(codeTop, 0, page.Bounds.Height - 60);
            Assert.True(codeTop + code.Bounds.Height > page.Bounds.Height);

            var start = PointOnCharacter(window, FragmentIn(code), 1);
            var pastBoth = new Point(
                code.TranslatePoint(new Point(code.Viewport.Width + 20, 0), window)!.Value.X,
                page.Bounds.Height + 30);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(pastBoth);
            Wait(TimeSpan.FromMilliseconds(300));

            Assert.True(page.Offset.Y > 0);
            Assert.True(code.Offset.X > 0);

            window.MouseUp(pastBoth, MouseButton.Left);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task RebuildStopsTheScroll()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, page, view) = ShowOnPage(ManyParagraphs(), height: 400);
            var fragment = view.GetVisualDescendants().OfType<MarkdownSelectionTextFragment>().First();
            var start = PointOnCharacter(window, fragment, 1);

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point(start.X, page.Bounds.Height + 30));
            Wait(TimeSpan.FromMilliseconds(100));
            Assert.True(page.Offset.Y > 0);

            view.Document = ManyParagraphs();
            Settle(window);
            var offsetAfterRebuild = page.Offset.Y;
            Wait(TimeSpan.FromMilliseconds(150));

            Assert.Equal(offsetAfterRebuild, page.Offset.Y);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public Task ClickInTheFadeOfAWideTableDoesNotScroll()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var (window, page, view) = ShowOnPage(WideTable());
            var host = view.GetVisualDescendants().OfType<MarkdownTableHost>().Single();
            var table = host.ScrollViewer;
            var inFade = Beside(window, table, PointOnCharacter(window, Cell(host, row: 1, column: 1), 0), pastRight: -MarkdownTableHost.EdgeFadeWidth / 2);

            window.MouseDown(inFade, MouseButton.Left);
            Wait(TimeSpan.FromMilliseconds(150));
            window.MouseUp(inFade, MouseButton.Left);
            Wait(TimeSpan.FromMilliseconds(150));

            Assert.Equal(0, table.Offset.X);
            Assert.Equal(0, page.Offset.Y);
            window.Close();
        }, CancellationToken.None);
    }

    /// <summary>
    /// Точка на высоте <paramref name="point"/> на <paramref name="pastRight"/> правее
    /// видимой части области <paramref name="scrollViewer"/> (отрицательное — левее её правого края).
    /// </summary>
    private static Point Beside(Window window, ScrollViewer scrollViewer, Point point, double pastRight)
    {
        var inScrollViewer = window.TranslatePoint(point, scrollViewer)!.Value;
        return scrollViewer.TranslatePoint(new Point(scrollViewer.Viewport.Width + pastRight, inScrollViewer.Y), window)!.Value;
    }

    /// <summary>Середина символа <paramref name="character"/> в координатах окна.</summary>
    private static Point PointOnCharacter(Window window, MarkdownSelectionTextFragment fragment, int character)
    {
        Assert.True(fragment.TryGetHorizontalExtentForLocalRange(character, character + 1, out var left, out var right));
        Assert.True(fragment.TryGetLineTopForLocalOffset(character, out var lineTop));
        return fragment.TranslatePoint(new Point((left + right) / 2, lineTop + 8), window)!.Value;
    }

    private static ScrollViewer CodeScrollViewer(MarkdownDocumentView view)
        => view.GetVisualDescendants().OfType<ScrollViewer>().Single();

    private static MarkdownSelectionTextFragment FragmentIn(ScrollViewer scrollViewer)
        => scrollViewer.GetVisualDescendants().OfType<MarkdownSelectionTextFragment>().First();

    private static MarkdownSelectionTextFragment Cell(MarkdownTableHost host, int row, int column)
    {
        var cell = Assert.IsType<Border>(host.Panel.Children[row * host.Panel.ColumnCount + column]);
        return Assert.IsType<MarkdownSelectionTextFragment>(cell.Child);
    }

    /// <summary>Документ на странице, как в ViewerView: прокручиваемая область во всё окно.</summary>
    private static (Window Window, ScrollViewer Page, MarkdownDocumentView View) ShowOnPage(
        RenderedMarkdownDocument document,
        double height = 600)
    {
        var view = new MarkdownDocumentView
        {
            ReadingPreferences = ReadingPreferences.Default,
            Document = document,
            DocumentPadding = new Thickness(72, 24, 72, 24)
        };
        var page = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Grid
            {
                Children =
                {
                    new Border
                    {
                        MaxWidth = 600,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Child = view
                    }
                }
            }
        };

        var window = ThemedTestWindow.Create(ThemeVariant.Light, page);
        window.Width = 1000;
        window.Height = height;
        window.Show();
        Settle(window);
        return (window, page, view);
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    /// <summary>
    /// Даёт пройти настоящему времени кадрами по 20 мс: таймер автопрокрутки тикает,
    /// а между тиками проходит раскладка, как в приложении.
    /// </summary>
    private static void Wait(TimeSpan duration)
    {
        var deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(20));
            Dispatcher.UIThread.PushFrame(frame);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static RenderedMarkdownDocument ManyParagraphs()
        => new(Enumerable.Range(1, 60)
            .Select(index => (MarkdownBlock)Paragraph($"Paragraph {index} with enough words to be a real line of text."))
            .ToArray());

    private static MarkdownParagraphBlock Paragraph(string text) => new([new MarkdownTextInline(text)]);

    private static RenderedMarkdownDocument WideTable()
    {
        var longText = string.Join(' ', Enumerable.Repeat("The rationale keeps going far past the reading column.", 6));
        return new RenderedMarkdownDocument(
        [
            new MarkdownTableBlock(
                Row("#", "CPU", "Rationale"),
                [Row("1", "2 vCPU", longText), Row("3", "8 vCPU", "Mirror of the first node.")])
        ]);
    }

    private static MarkdownTableCell[] Row(params string[] cells)
        => cells.Select(static text => new MarkdownTableCell([new MarkdownTextInline(text)])).ToArray();
}
