using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownSelectionAutoScrollMathTests
{
    [Theory]
    [InlineData(-5, 100, -1)]
    [InlineData(105, 100, 1)]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 0)]
    [InlineData(100, 100, 0)]
    public void ScrollsOnlyPastTheEdgeWithoutInsets(double position, double viewport, int expected)
        => Assert.Equal(expected, MarkdownSelectionAutoScroll.GetDirection(position, viewport, 0, 0, offset: 50, maxOffset: 200));

    [Theory]
    [InlineData(30, -1)]
    [InlineData(70, 1)]
    [InlineData(50, 0)]
    public void InsetsStartTheScrollInsideTheEdge(double position, int expected)
        => Assert.Equal(expected, MarkdownSelectionAutoScroll.GetDirection(position, 100, 40, 40, offset: 50, maxOffset: 200));

    [Fact]
    public void DoesNotScrollPastTheStartOfTheContent()
        => Assert.Equal(0, MarkdownSelectionAutoScroll.GetDirection(-5, 100, 0, 0, offset: 0, maxOffset: 200));

    [Fact]
    public void DoesNotScrollPastTheEndOfTheContent()
        => Assert.Equal(0, MarkdownSelectionAutoScroll.GetDirection(105, 100, 0, 0, offset: 200, maxOffset: 200));

    [Fact]
    public void DoesNotScrollAnEmptyViewport()
        => Assert.Equal(0, MarkdownSelectionAutoScroll.GetDirection(-5, 0, 0, 0, offset: 50, maxOffset: 200));

    [Fact]
    public void StepMovesAtTheConstantSpeed()
    {
        var elapsed = TimeSpan.FromMilliseconds(20);
        var distance = MarkdownSelectionAutoScroll.Speed * elapsed.TotalSeconds;

        Assert.Equal(100 + distance, MarkdownSelectionAutoScroll.Step(100, 1000, 1, elapsed), 6);
        Assert.Equal(100 - distance, MarkdownSelectionAutoScroll.Step(100, 1000, -1, elapsed), 6);
    }

    [Theory]
    [InlineData(-1, 1000, 0)]
    [InlineData(1, 1000, 1000)]
    public void StepStopsAtTheEdgeOfTheContent(int direction, double maxOffset, double expected)
        => Assert.Equal(expected, MarkdownSelectionAutoScroll.Step(direction < 0 ? 3 : maxOffset - 3, maxOffset, direction, TimeSpan.FromMilliseconds(50)));

    [Fact]
    public void StepAfterALongPauseIsCapped()
    {
        var capped = MarkdownSelectionAutoScroll.Step(0, 10_000, 1, TimeSpan.FromSeconds(2));
        Assert.InRange(capped, 1, MarkdownSelectionAutoScroll.Speed * 0.1 + 0.001);
    }

    [Fact]
    public void StepWithoutDirectionKeepsTheOffset()
        => Assert.Equal(120, MarkdownSelectionAutoScroll.Step(120, 1000, 0, TimeSpan.FromMilliseconds(16)));

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(40, 40)]
    [InlineData(130, 100)]
    public void ClampKeepsThePointInTheVisiblePart(double position, double expected)
        => Assert.Equal(expected, MarkdownSelectionAutoScroll.ClampToViewport(position, 100));
}
