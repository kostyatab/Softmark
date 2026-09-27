using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Колесо во вьюере крутит штатно, как везде в Avalonia: 50 px на единицу дельты,
/// поэтому трекпад на macOS (он отдаёт пиксели, делённые на 50) идёт 1:1 с пальцем.
/// Своего шага у вьюера нет — прежний множитель гнал трекпад почти вдвое быстрее
/// пальца. Горизонтальные жесты и Shift+колесо достаются вложенным блокам.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class ViewerWheelScrollTests
{
    /// <summary>Шаг штатного <c>ScrollContentPresenter</c> на единицу дельты.</summary>
    internal const double PixelsPerWheelDelta = 50;

    private readonly AvaloniaHeadlessFixture _fixture;

    public ViewerWheelScrollTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(-0.3, 15)]
    [InlineData(-0.02, 1)]
    [InlineData(-1, 50)]
    public Task WheelScrollsTheDocumentByTheStandardStep(double deltaY, double expectedOffset)
    {
        return _fixture.RunAsync(async () =>
        {
            var window = DocumentOutlineRailTests.Show(await DocumentOutlineRailTests.CreateViewerAsync(LongDocument()));
            var scroll = DocumentOutlineRailTests.DocScroll(window);

            window.MouseWheel(Centre(scroll, window), new Vector(0, deltaY));
            DocumentOutlineRailTests.Settle(window);

            Assert.Equal(expectedOffset, scroll.Offset.Y, 6);

            window.Close();
        });
    }

    [Theory]
    [InlineData(-1, 0, RawInputModifiers.None)]
    [InlineData(0, -1, RawInputModifiers.Shift)]
    public Task SidewaysWheelOverAWideCodeBlockScrollsTheBlock(double deltaX, double deltaY, RawInputModifiers modifiers)
    {
        return _fixture.RunAsync(async () =>
        {
            var window = DocumentOutlineRailTests.Show(await DocumentOutlineRailTests.CreateViewerAsync(
                $"```\n{new string('x', 600)}\n```\n\n" + LongDocument()));
            var page = DocumentOutlineRailTests.DocScroll(window);
            var block = page.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .Single(scroll => scroll.Classes.Contains(ScrollBarReveal.PersistentHorizontalClass));
            Assert.True(block.ScrollBarMaximum.X > PixelsPerWheelDelta);

            window.MouseWheel(Centre(block, window), new Vector(deltaX, deltaY), modifiers);
            DocumentOutlineRailTests.Settle(window);

            Assert.Equal(PixelsPerWheelDelta, block.Offset.X, 6);
            Assert.Equal(0, page.Offset.Y);

            window.Close();
        });
    }

    private static string LongDocument()
        => string.Join("\n\n", Enumerable.Range(1, 80).Select(static index => $"Paragraph {index} of a long document."));

    private static Point Centre(Visual visual, Window window)
        => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;
}
