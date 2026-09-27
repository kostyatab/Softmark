using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.Abstractions;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Domain.Outline;
using MarkMello.Infrastructure.Markdown;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;
using MarkMello.Presentation.Views.Markdown;
using MarkMello.Presentation.Views.Markdown.Outline;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Оглавление у правого края документа: рельс по заголовкам H1–H3 верхнего уровня
/// и карточка по наведению, переход тем же путём, что ссылки <c>#якорь</c>.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class DocumentOutlineRailTests
{
    private static readonly string DocumentPath = TestPaths.At("docs", "README.md");

    private const double ScrollTopInset = 24;

    private readonly AvaloniaHeadlessFixture _fixture;

    public DocumentOutlineRailTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task RailShowsOneDashPerTopLevelHeadingFromTwoHeadings()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two", "Three")));

            var layer = Layer(window);
            Assert.True(layer.IsVisible);
            Assert.Equal(3, layer.Rail!.EntryCount);

            window.Close();
        });
    }

    [Fact]
    public Task RailIsHiddenWithASingleHeading()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("Only")));

            Assert.False(Layer(window).IsVisible);
            Assert.Equal(0, MarkdownTableHost.GetPageEndReserve(DocScroll(window)));

            window.Close();
        });
    }

    [Fact]
    public Task HeadingsInsideQuotesDoNotGetADash()
    {
        return _fixture.RunAsync(async () =>
        {
            var markdown = "## First\n\n> ## Quoted\n\n> [!NOTE]\n> ## Alert\n\n- ## Listed\n\n## Second\n";
            var window = Show(await CreateViewerAsync(markdown));

            Assert.Equal(2, Layer(window).Rail!.EntryCount);

            window.Close();
        });
    }

    [Fact]
    public Task CurrentEntryFollowsScrolling()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two", "Three")));
            var layer = Layer(window);
            Assert.Equal(0, layer.Rail!.CurrentIndex);

            var scroll = DocScroll(window);
            scroll.Offset = new Vector(0, scroll.ScrollBarMaximum.Y);
            Settle(window);

            Assert.Equal(2, layer.Rail.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task ClickingACardItemBringsTheHeadingIntoViewAndClosesTheCard()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two", "Three")));
            var layer = Layer(window);
            Assert.Null(layer.Card);

            layer.OpenCard();
            Settle(window);
            var card = Assert.IsType<DocumentOutlineCard>(layer.Card);
            Assert.True(card.IsVisible);
            Assert.Contains("mm-current", card.Items[0].Classes);

            card.Items[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle(window);

            Assert.False(layer.IsCardOpen);
            var heading = HeadingControl(window, "Two");
            var top = heading.TranslatePoint(default, DocScroll(window))!.Value.Y;
            Assert.Equal(ScrollTopInset, top, 1);
            Assert.Equal(1, layer.Rail!.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task FirstEntryIsCurrentAtTheTopWhileTheSecondHeadingIsInView()
    {
        return _fixture.RunAsync(async () =>
        {
            // Как samples/sample.md: короткое вступление, второй заголовок виден сразу.
            var markdown = "# Title\n\nA short introduction.\n\n" + Sections("Second", "Third");
            var window = Show(await CreateViewerAsync(markdown));

            // Старое правило — треть окна документа: «Second» выше неё, и он подсветился бы.
            var oldReadingLine = DocScroll(window).Viewport.Height / 3;
            Assert.True(HeadingTop(window, "Second") < oldReadingLine, $"Second at {HeadingTop(window, "Second")}, line {oldReadingLine}");
            Assert.Equal(0, Layer(window).Rail!.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task ClickingTheEntryOfAShortSectionMakesItCurrent()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One") + "## Two\n\n### Three\n\n" + Filler));
            var layer = Layer(window);

            ClickCardItem(window, 1);

            // Следующий заголовок встаёт на линию чтения: без удержания подсветку решало бы округление.
            Assert.True(
                HeadingTop(window, "Three") <= DocumentOutline.ReadingLineOffset,
                $"Three at {HeadingTop(window, "Three")}");
            Assert.Equal(1, layer.Rail!.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task ClickingAnEntryNearTheEndMakesItCurrent()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two") + "## Three\n\n## Four\n\nThe end.\n"));
            var layer = Layer(window);
            var scroll = DocScroll(window);

            ClickCardItem(window, 2);

            Assert.Equal(scroll.ScrollBarMaximum.Y, scroll.Offset.Y, 1);
            Assert.Equal(2, layer.Rail!.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task NavigationThatDoesNotMoveTheDocumentStillMakesTheEntryCurrent()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two") + "## Three\n\n## Four\n\nThe end.\n"));
            var layer = Layer(window);
            var scroll = DocScroll(window);

            ClickCardItem(window, 3);
            var offset = scroll.Offset.Y;
            Assert.Equal(3, layer.Rail!.CurrentIndex);

            // «Three» у самого конца — переход к нему упирается в тот же максимум.
            ClickCardItem(window, 2);

            Assert.Equal(offset, scroll.Offset.Y);
            Assert.Equal(2, layer.Rail.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task AnchorLinkToAnOutlineHeadingMakesItCurrent()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two") + "## Three\n\n## Four\n\nThe end.\n"));
            var layer = Layer(window);

            Assert.True(DocumentView(window).TryScrollToHeadingAnchor("#three"));
            Settle(window);

            Assert.Equal(2, layer.Rail!.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task AnchorLinkToAHeadingOutsideTheOutlineUsesTheReadingLine()
    {
        return _fixture.RunAsync(async () =>
        {
            var markdown = Sections("One") + "#### Deep\n\n" + Filler + "\n\n" + Sections("Two", "Three");
            var window = Show(await CreateViewerAsync(markdown));
            var layer = Layer(window);
            ClickCardItem(window, 2);

            // H4 в рельс не входит: текущий — раздел, в котором он лежит.
            Assert.True(DocumentView(window).TryScrollToHeadingAnchor("#deep"));
            Settle(window);

            Assert.Equal(0, layer.Rail!.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task AnchorLinkToAQuotedHeadingReleasesTheNavigatedEntryEvenWithoutMoving()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two") + "## Three\n\n> ## Quoted\n\n## Four\n\nThe end.\n"));
            var layer = Layer(window);
            var scroll = DocScroll(window);
            ClickCardItem(window, 2);
            var offset = scroll.Offset.Y;
            Assert.Equal(2, layer.Rail!.CurrentIndex);

            // Цитата у самого конца — переход к ней упирается в тот же максимум.
            Assert.True(DocumentView(window).TryScrollToHeadingAnchor("#quoted"));
            Settle(window);

            Assert.Equal(offset, scroll.Offset.Y);
            Assert.Equal(3, layer.Rail.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task CodeHighlightingKeepsTheNavigatedEntry()
    {
        return _fixture.RunAsync(async () =>
        {
            using var highlighter = new GatedHighlighter();
            var markdown = "```cs\nvar x = 1;\n```\n\n" + Sections("One", "Two") + "## Three\n\n## Four\n\nThe end.\n";
            var viewModel = await CreateViewerAsync(markdown, highlightCodeBlocks: new HighlightCodeBlocksUseCase(highlighter));
            var window = Show(viewModel);
            var layer = Layer(window);
            Assert.NotNull(viewModel.CodeHighlighting);
            ClickCardItem(window, 2);

            highlighter.Open();
            await viewModel.CodeHighlighting!;
            Settle(window);

            Assert.Equal(2, layer.Rail!.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task ScrollingAfterNavigationReturnsToTheReadingLine()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two") + "## Three\n\n## Four\n\nThe end.\n"));
            var layer = Layer(window);
            var scroll = DocScroll(window);
            ClickCardItem(window, 2);
            Assert.Equal(2, layer.Rail!.CurrentIndex);

            scroll.Offset = new Vector(0, 0);
            Settle(window);
            Assert.Equal(0, layer.Rail.CurrentIndex);

            // Вернулись к тому же смещению прокруткой, а не переходом — действует «в конце — последний».
            scroll.Offset = new Vector(0, scroll.ScrollBarMaximum.Y);
            Settle(window);
            Assert.Equal(3, layer.Rail.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task RerenderingReleasesTheNavigatedEntry()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = await CreateViewerAsync(Sections("One", "Two") + "## Three\n\n## Four\n\nThe end.\n");
            var window = Show(viewModel);
            var layer = Layer(window);
            ClickCardItem(window, 2);

            window.Height = 700;
            Settle(window);

            // Окно ниже — прокрутка больше не в конце, и по линии чтения текущим был бы не «Three».
            var scroll = DocScroll(window);
            Assert.True(scroll.Offset.Y < scroll.ScrollBarMaximum.Y - 1);
            Assert.True(HeadingTop(window, "Three") > DocumentOutline.ReadingLineOffset, $"Three at {HeadingTop(window, "Three")}");
            Assert.Equal(1, layer.Rail!.CurrentIndex);

            window.Close();
        });
    }

    [Fact]
    public Task CardUsesTheMenuCardSurface()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two")));
            var layer = Layer(window);
            layer.OpenCard();
            Settle(window);
            var card = layer.Card!;

            Assert.Same(Resource(window, "MmElevatedBackgroundBrush"), card.Background);
            Assert.Same(Resource(window, "MmBorderBrush"), card.BorderBrush);
            Assert.Equal(new Thickness(1), card.BorderThickness);
            Assert.Equal(new CornerRadius(12), card.CornerRadius);
            Assert.Equal(new Thickness(6), card.Padding);
            Assert.Equal(Assert.IsType<BoxShadows>(Resource(window, "MmCardShadow")), card.BoxShadow);

            window.Close();
        });
    }

    [Fact]
    public Task CardDoesNotOpenWhileAnotherOverlayIsOpen()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = await CreateViewerAsync(Sections("One", "Two"));
            var window = Show(viewModel);
            var layer = Layer(window);

            viewModel.IsFindBarOpen = true;
            Settle(window);
            layer.OpenCard();

            Assert.False(layer.IsCardOpen);

            viewModel.IsFindBarOpen = false;
            Settle(window);
            layer.OpenCard();
            Assert.True(layer.IsCardOpen);

            viewModel.IsFindBarOpen = true;
            Settle(window);
            Assert.False(layer.IsCardOpen);

            window.Close();
        });
    }

    [Fact]
    public Task TurningTheOutlineOffHidesTheRailAtOnce()
    {
        return _fixture.RunAsync(async () =>
        {
            var settings = new InMemorySettingsStore();
            var viewModel = await CreateViewerAsync(Sections("One", "Two"), settings);
            var window = Show(viewModel);
            var layer = Layer(window);
            Assert.True(layer.IsVisible);

            viewModel.IsDocumentOutlineOffSelected = true;
            Settle(window);

            Assert.False(layer.IsVisible);
            Assert.False(settings.DocumentOutlineEnabled);
            Assert.Equal(0, MarkdownTableHost.GetPageEndReserve(DocScroll(window)));

            viewModel.IsDocumentOutlineOnSelected = true;
            Settle(window);

            Assert.True(layer.IsVisible);

            window.Close();
        });
    }

    [Fact]
    public Task SavedOffFlagKeepsTheRailHiddenOnOpen()
    {
        return _fixture.RunAsync(async () =>
        {
            var settings = new InMemorySettingsStore { DocumentOutlineEnabled = false };
            var window = Show(await CreateViewerAsync(Sections("One", "Two"), settings));

            Assert.False(Layer(window).IsVisible);

            window.Close();
        });
    }

    [Theory]
    [InlineData(640)]
    [InlineData(1280)]
    public Task RailStaysClearOfTheTextColumn(double windowWidth)
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two")), windowWidth);
            var layer = Layer(window);
            Assert.True(layer.IsVisible);

            var documentView = window.GetVisualDescendants().OfType<MarkdownDocumentView>().Single();
            var textRight = documentView.TranslatePoint(
                new Point(documentView.Bounds.Width - documentView.DocumentPadding.Right, 0),
                window)!.Value.X;
            var railLeft = layer.Rail!.TranslatePoint(default, window)!.Value.X;

            Assert.True(railLeft - textRight >= 16, $"rail {railLeft}, text {textRight}");

            window.Close();
        });
    }

    [Fact]
    public Task WideTablesReserveRoomForTheRail()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two")));

            Assert.Equal(
                DocumentOutlineLayer.RailFootprint,
                MarkdownTableHost.GetPageEndReserve(DocScroll(window)));

            window.Close();
        });
    }

    [Fact]
    public Task RailIsRebuiltWhenTheDocumentChanges()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = await CreateViewerAsync(Sections("One", "Two"));
            var window = Show(viewModel);
            var layer = Layer(window);

            viewModel.RenderedDocument = new MarkdigMarkdownDocumentRenderer().Render(Sections("A", "B", "C", "D"));
            Settle(window);

            Assert.Equal(4, layer.Rail!.EntryCount);

            window.Close();
        });
    }

    [Fact]
    public Task WheelOverTheRailScrollsTheDocument()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two", "Three")));
            var rail = Layer(window).Rail!;
            var railCentre = rail.TranslatePoint(new Point(rail.Bounds.Width / 2, rail.Bounds.Height / 2), window)!.Value;

            window.MouseWheel(railCentre, new Vector(0, -1));
            Settle(window);

            Assert.True(DocScroll(window).Offset.Y > 0);

            window.Close();
        });
    }

    [Fact]
    public Task WheelOverAShortCardScrollsTheDocument()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two", "Three")));
            var layer = Layer(window);
            layer.OpenCard();
            Settle(window);
            var card = layer.Card!;
            var cardCentre = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;

            window.MouseWheel(cardCentre, new Vector(0, -1));
            Settle(window);

            Assert.True(DocScroll(window).Offset.Y > 0);

            window.Close();
        });
    }

    [Fact]
    public Task WheelOverALongCardScrollsOnlyTheCard()
    {
        return _fixture.RunAsync(async () =>
        {
            var titles = Enumerable.Range(1, 40).Select(static index => $"Section {index}").ToArray();
            var window = Show(await CreateViewerAsync(Sections(titles)));
            var layer = Layer(window);
            layer.OpenCard();
            Settle(window);
            var card = layer.Card!;
            var list = card.GetVisualDescendants().OfType<ScrollViewer>().First();
            Assert.True(list.ScrollBarMaximum.Y > 0);
            var cardCentre = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;
            var documentOffset = DocScroll(window).Offset.Y;

            window.MouseWheel(cardCentre, new Vector(0, -1));
            Settle(window);

            // Штатный шаг Avalonia, как у документа: 50 px на единицу дельты.
            Assert.Equal(ViewerWheelScrollTests.PixelsPerWheelDelta, list.Offset.Y, 1);
            Assert.Equal(documentOffset, DocScroll(window).Offset.Y);

            window.Close();
        });
    }

    [Fact]
    public Task OnlyATruncatedItemShowsItsFullTextInATooltip()
    {
        return _fixture.RunAsync(async () =>
        {
            var longTitle = "A very long section title that does not fit into the outline card at all";
            var window = Show(await CreateViewerAsync(Sections("Short", longTitle)));
            var layer = Layer(window);
            layer.OpenCard();
            Settle(window);
            var items = layer.Card!.Items;

            Assert.True(RaiseToolTipOpening(items[0]).Cancel);
            Assert.False(RaiseToolTipOpening(items[1]).Cancel);
            Assert.Equal(longTitle, ToolTip.GetTip(items[1]));

            window.Close();
        });
    }

    private static CancelRoutedEventArgs RaiseToolTipOpening(Control item)
    {
        var args = new CancelRoutedEventArgs(ToolTip.ToolTipOpeningEvent, item);
        item.RaiseEvent(args);
        return args;
    }

    [Fact]
    public Task RerenderingKeepsTheRailAndTheTableReserveInPlace()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = await CreateViewerAsync(Sections("One", "Two"));
            var window = Show(viewModel);
            var layer = Layer(window);

            // Внешнее изменение файла: новый документ того же вида, до его DocumentRendered.
            viewModel.RenderedDocument = new MarkdigMarkdownDocumentRenderer().Render(Sections("One", "Two"));

            Assert.True(layer.IsVisible);
            Assert.Equal(DocumentOutlineLayer.RailFootprint, MarkdownTableHost.GetPageEndReserve(DocScroll(window)));

            Settle(window);
            Assert.True(layer.IsVisible);

            window.Close();
        });
    }

    [Fact]
    public Task EmptyingTheDocumentHidesTheRail()
    {
        return _fixture.RunAsync(async () =>
        {
            var viewModel = await CreateViewerAsync(Sections("One", "Two"));
            var window = Show(viewModel);

            viewModel.RenderedDocument = RenderedMarkdownDocument.Empty;
            Settle(window);

            Assert.False(Layer(window).IsVisible);
            Assert.Equal(0, MarkdownTableHost.GetPageEndReserve(DocScroll(window)));

            window.Close();
        });
    }

    [Fact]
    public Task RelayoutKeepsTheItemsOfAnOpenCard()
    {
        return _fixture.RunAsync(async () =>
        {
            var window = Show(await CreateViewerAsync(Sections("One", "Two", "Three")));
            var layer = Layer(window);
            layer.OpenCard();
            Settle(window);
            var item = layer.Card!.Items[1];

            window.Height = 700;
            Settle(window);

            Assert.True(layer.IsCardOpen);
            Assert.Same(item, layer.Card.Items[1]);

            window.Close();
        });
    }

    private static string Sections(params string[] titles)
        => string.Concat(titles.Select(static title => $"## {title}\n\n{Filler}\n\n"));

    private static string Filler { get; } = string.Join(
        "\n\n",
        Enumerable.Range(0, 30).Select(static index => $"Paragraph {index} keeps the section long enough to scroll."));

    private static object? Resource(Window window, string key)
    {
        Assert.True(window.TryFindResource(key, window.ActualThemeVariant, out var value));
        return value;
    }

    private static void ClickCardItem(Window window, int index)
    {
        var layer = Layer(window);
        layer.OpenCard();
        Settle(window);
        layer.Card!.Items[index].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
    }

    private static double HeadingTop(Window window, string text)
        => HeadingControl(window, text).TranslatePoint(default, DocScroll(window))!.Value.Y;

    private static MarkdownDocumentView DocumentView(Window window)
        => window.GetVisualDescendants().OfType<MarkdownDocumentView>().Single();

    private static DocumentOutlineLayer Layer(Window window)
        => window.GetVisualDescendants().OfType<DocumentOutlineLayer>().Single();

    internal static ScrollViewer DocScroll(Window window)
        => window.GetVisualDescendants().OfType<ScrollViewer>().Single(scroll => scroll.Name == "DocScroll");

    private static Control HeadingControl(Window window, string text)
    {
        var documentView = window.GetVisualDescendants().OfType<MarkdownDocumentView>().Single();
        var document = documentView.Document!;
        var index = document.Blocks
            .Select((block, blockIndex) => (block, blockIndex))
            .Single(pair => pair.block is MarkdownHeadingBlock heading
                && MarkdownDocumentTextMap.ExtractPlainText(heading.Inlines) == text)
            .blockIndex;
        return documentView.GetTopLevelHeadingControl(index)!;
    }

    /// <summary>Оглавление строится с фоновым приоритетом после отрисовки — даём ему пройти.</summary>
    internal static void Settle(Window window)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Вьюер в окне с темой приложения: без шаблона Fluent у ScrollViewer нет прокрутки.</summary>
    internal static Window Show(ShellViewModel viewModel, double width = 1280)
    {
        var window = ThemedTestWindow.Create(ThemeVariant.Light, new ViewerView { DataContext = viewModel });
        window.Width = width;
        window.Height = 800;
        window.Show();
        Settle(window);
        return window;
    }

    internal static async Task<ShellViewModel> CreateViewerAsync(
        string markdown,
        InMemorySettingsStore? settings = null,
        HighlightCodeBlocksUseCase? highlightCodeBlocks = null)
    {
        var loader = new StubDocumentLoader();
        loader.Sources[DocumentPath] = new MarkdownSource(DocumentPath, "README.md", markdown);
        var fileSystem = new FakeWorkspaceFileSystem();

        var viewModel = new ShellViewModel(
            new OpenDocumentUseCase(loader),
            new SaveDocumentUseCase(new RecordingDocumentSaver()),
            new StubFilePicker(),
            new StubCommandLineActivation(),
            new LocalizationService(AppLanguage.English),
            settings ?? new InMemorySettingsStore(),
            new RecordingThemeService(),
            new RecordingStartupMetrics(),
            new RenderMarkdownDocumentUseCase(new MarkdigMarkdownDocumentRenderer(), new FakeDiagramRenderService()),
            TestUpdates.CreateViewModel(),
            new OpenFolderUseCase(fileSystem),
            new ExpandFolderNodeUseCase(fileSystem),
            new SearchWorkspaceFilesUseCase(fileSystem),
            new WorkspaceFileOperationsUseCase(fileSystem, new FakePlatformServices()),
            new FakePlatformServices(),
            static () => new FakeWorkspaceWatcher(),
            new RecordingWindowLauncher(),
            highlightCodeBlocks: highlightCodeBlocks);

        await viewModel.InitializeAsync();
        await viewModel.OpenPathAsync(DocumentPath);
        Assert.True(viewModel.IsViewer);
        return viewModel;
    }

    /// <summary>Докраска, которая ждёт разрешения теста: успевает случиться после перехода.</summary>
    private sealed class GatedHighlighter : ICodeHighlighter, IDisposable
    {
        private readonly ManualResetEventSlim _gate = new();

        public void Open() => _gate.Set();

        public IReadOnlyList<MarkdownCodeToken>? Highlight(
            string language,
            string code,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            _gate.Wait(cancellationToken);
            return [new MarkdownCodeToken(0, code.Length, MarkdownCodeTokenKind.Keyword)];
        }

        public void Dispose() => _gate.Dispose();
    }
}
