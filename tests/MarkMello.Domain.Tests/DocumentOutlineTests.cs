using MarkMello.Domain;
using MarkMello.Domain.Outline;

namespace MarkMello.Domain.Tests;

public sealed class DocumentOutlineTests
{
    [Fact]
    public void CreateTakesTopLevelHeadingsUpToLevelThreeInDocumentOrder()
    {
        var document = new RenderedMarkdownDocument(
        [
            Heading(1, "Title"),
            Paragraph("Intro"),
            Heading(2, "Install"),
            Heading(3, "Windows"),
            Heading(4, "Details"),
            Heading(6, "Deep"),
            Heading(2, "License")
        ]);

        var outline = DocumentOutline.Create(document);

        Assert.Equal(
            [
                new DocumentOutlineEntry(1, "Title", 0),
                new DocumentOutlineEntry(2, "Install", 2),
                new DocumentOutlineEntry(3, "Windows", 3),
                new DocumentOutlineEntry(2, "License", 6)
            ],
            outline.Entries);
    }

    [Fact]
    public void CreateSkipsHeadingsNestedInQuotesAlertsAndLists()
    {
        var document = new RenderedMarkdownDocument(
        [
            Heading(2, "First"),
            new MarkdownQuoteBlock([Heading(2, "In quote")]),
            new MarkdownQuoteBlock([Heading(2, "In alert")], MarkdownAlertKind.Note),
            new MarkdownListBlock(false, [new MarkdownListItem([Heading(2, "In list")])]),
            Heading(2, "Second")
        ]);

        var outline = DocumentOutline.Create(document);

        Assert.Equal(["First", "Second"], outline.Entries.Select(static entry => entry.Text));
    }

    [Fact]
    public void CreateSkipsEmptyHeadingsAndFlattensInlinesToOneLine()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownHeadingBlock(2, []),
            Heading(2, "   "),
            new MarkdownHeadingBlock(2,
            [
                new MarkdownTextInline("Use "),
                new MarkdownCodeInline("dotnet"),
                new MarkdownLineBreakInline(),
                new MarkdownEmphasisInline([new MarkdownTextInline("  wisely ")])
            ])
        ]);

        var outline = DocumentOutline.Create(document);

        var entry = Assert.Single(outline.Entries);
        Assert.Equal("Use dotnet wisely", entry.Text);
        Assert.Equal(2, entry.BlockIndex);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void HasEnoughEntriesRequiresAtLeastTwoHeadings(int count, bool expected)
    {
        var document = new RenderedMarkdownDocument(
            Enumerable.Range(0, count).Select(static index => (MarkdownBlock)Heading(2, $"Section {index}")).ToList());

        Assert.Equal(expected, DocumentOutline.Create(document).HasEnoughEntries);
    }

    [Fact]
    public void FindCurrentEntryIsFirstBeforeTheFirstHeadingReachesTheReadingLine()
    {
        // Линия чтения — 80 от верха окна.
        Assert.Equal(0, DocumentOutline.FindCurrentEntry([500, 1500, 2500], 0, 3000));
    }

    [Fact]
    public void FindCurrentEntryIsFirstAtTheTopWhileTheSecondHeadingIsStillBelowTheReadingLine()
    {
        // samples/sample.md в самом верху: второй заголовок уже в окне, но ниже линии.
        Assert.Equal(0, DocumentOutline.FindCurrentEntry([85, 252, 900], 0, 3000));
    }

    [Fact]
    public void FindCurrentEntryIsLastHeadingAboveTheReadingLine()
    {
        double[] tops = [100, 1500, 2500];

        Assert.Equal(0, DocumentOutline.FindCurrentEntry(tops, 1419, 3000));
        Assert.Equal(1, DocumentOutline.FindCurrentEntry(tops, 1421, 3000));
        Assert.Equal(1, DocumentOutline.FindCurrentEntry(tops, 2419, 3000));
        Assert.Equal(2, DocumentOutline.FindCurrentEntry(tops, 2421, 3000));
    }

    [Fact]
    public void FindCurrentEntryKeepsAHeadingPlacedAtTheTopByNavigation()
    {
        // Переход ставит заголовок на 24 от верха; следующий, в 60 ниже, до линии не дошёл.
        Assert.Equal(1, DocumentOutline.FindCurrentEntry([100, 1500, 1560], 1476, 3000));
    }

    [Fact]
    public void FindCurrentEntryIsLastAtTheEndOfScrolling()
    {
        // Короткий последний раздел не дотягивается до линии чтения — в конце он всё равно текущий.
        double[] tops = [100, 1500, 2950];

        Assert.Equal(1, DocumentOutline.FindCurrentEntry(tops, 2000, 2400));
        Assert.Equal(2, DocumentOutline.FindCurrentEntry(tops, 2400, 2400));
        Assert.Equal(2, DocumentOutline.FindCurrentEntry(tops, 2399.5, 2400));
    }

    [Fact]
    public void FindCurrentEntryUsesTheReadingLineWhenTheDocumentDoesNotScroll()
    {
        Assert.Equal(0, DocumentOutline.FindCurrentEntry([10, 400], 0, 0));
        Assert.Equal(1, DocumentOutline.FindCurrentEntry([10, 60, 400], 0, 0));
    }

    [Fact]
    public void FindCurrentEntryIsMinusOneWithoutHeadings()
    {
        Assert.Equal(-1, DocumentOutline.FindCurrentEntry([], 0, 100));
    }

    [Fact]
    public void RailKeepsPreferredPitchWhenEverythingFits()
    {
        var layout = DocumentOutlineRailLayout.Compute(16, 5, 400, 10, 6);

        Assert.Equal(new DocumentOutlineRailLayout(10, 0, 16), layout);
        Assert.False(layout.IsWindowed(16));
    }

    [Fact]
    public void RailCompressesPitchBeforeWindowing()
    {
        var layout = DocumentOutlineRailLayout.Compute(49, 23, 416, 10, 6);

        Assert.Equal(416d / 48, layout.Pitch, 6);
        Assert.Equal(0, layout.FirstVisibleIndex);
        Assert.Equal(49, layout.VisibleCount);
    }

    [Fact]
    public void RailShowsWindowAroundCurrentWhenMinimumPitchDoesNotFit()
    {
        var layout = DocumentOutlineRailLayout.Compute(111, 47, 416, 10, 6);

        Assert.Equal(6, layout.Pitch);
        Assert.Equal(70, layout.VisibleCount);
        Assert.Equal(12, layout.FirstVisibleIndex);
        Assert.True(layout.IsWindowed(111));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(105, 41)]
    [InlineData(110, 41)]
    public void RailWindowStaysInsideTheList(int current, int expectedFirst)
    {
        var layout = DocumentOutlineRailLayout.Compute(111, current, 416, 10, 6);

        Assert.Equal(expectedFirst, layout.FirstVisibleIndex);
        Assert.InRange(current, layout.FirstVisibleIndex, layout.FirstVisibleIndex + layout.VisibleCount - 1);
    }

    private static MarkdownHeadingBlock Heading(int level, string text)
        => new(level, [new MarkdownTextInline(text)]);

    private static MarkdownParagraphBlock Paragraph(string text)
        => new([new MarkdownTextInline(text)]);
}
