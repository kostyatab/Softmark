using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Block-level visual for <see cref="MarkdownDiagramBlock"/>. Operates on the
/// already-materialized <see cref="DiagramRenderResult"/> attached to the
/// block by <c>RenderMarkdownDocumentUseCase</c> — this view does NOT call
/// the diagram service. Two visual states:
///
/// <list type="bullet">
///   <item>Success: SVG payload from the renderer is fed through
///   <see cref="AotSafeSvgImage"/> and displayed as a native picture inside
///   a horizontal scroller for oversize diagrams.</item>
///   <item>Failure: the same dashed "place for the image" as a broken
///   image, carrying the dialect name, the gist of the renderer message and
///   the original source in a code block, so the author keeps access to what
///   they wrote (ADR-0005 §6, §8).</item>
/// </list>
///
/// The control is intentionally not part of the document text map — diagram
/// content does not pollute continuous text selection (ADR-0005 §8). Final
/// selection/copy semantics are refined in M6.
/// </summary>
internal sealed class MarkdownDiagramBlockView : ContentControl
{
    private readonly MarkdownBlockTypography _typography;
    private readonly MarkdownDiagramStrings _strings;
    private readonly Func<string, string, Control> _buildSourceBlock;

    /// <param name="block">The diagram with its materialized render result.</param>
    /// <param name="typography">Document sizes and fonts for the error block.</param>
    /// <param name="strings">Localized titles of the error block.</param>
    /// <param name="buildSourceBlock">
    /// Builds the code block for the diagram source from its language and text —
    /// the same sheet as a code block in the document, owned by the document view.
    /// </param>
    public MarkdownDiagramBlockView(
        MarkdownDiagramBlock block,
        MarkdownBlockTypography typography,
        MarkdownDiagramStrings strings,
        Func<string, string, Control> buildSourceBlock)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(typography);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(buildSourceBlock);

        _typography = typography;
        _strings = strings;
        _buildSourceBlock = buildSourceBlock;

        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        UseLayoutRounding = true;

        Content = BuildContent(block);
    }

    private Control BuildContent(MarkdownDiagramBlock block)
        => block.RenderResult switch
        {
            DiagramRenderResult.Success success => BuildSuccess(success, block),
            DiagramRenderResult.Failure failure => BuildFailure(failure, block.Kind),
            _ => BuildPending(block),
        };

    private Control BuildSuccess(DiagramRenderResult.Success success, MarkdownDiagramBlock block)
    {
        var svgBytes = Encoding.UTF8.GetBytes(success.Svg);

        if (!AotSafeSvgImage.TryLoad(svgBytes, out var image))
        {
            // The SVG produced by the backend uses constructs the native
            // AOT-safe SVG path does not yet support (see ADR-0005 §7 and
            // M5). Surface that honestly without falling back to a code
            // block — the diagram source is still preserved in the
            // document model.
            return BuildSvgUnsupportedPlaceholder(block);
        }

        var imageControl = new Image
        {
            Source = image,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            UseLayoutRounding = true,
        };

        // The background comes from the theme style: Naiad draws only in the
        // light Mermaid palette, so the dark theme lays a light sheet under it.
        var metrics = _typography.Metrics;
        var padding = metrics.DiagramPadding;
        return new Border
        {
            Classes = { "mm-md-diagram", "mm-md-diagram-success" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(padding, padding, padding, 0),
            CornerRadius = new CornerRadius(metrics.DiagramCornerRadius),
            // The scroll bar sits on the sheet, which is light in both themes,
            // so it takes the light variant. The scope stays inside the border:
            // on the border itself the sheet brush would resolve to the light
            // theme's transparent one.
            Child = new ThemeVariantScope
            {
                RequestedThemeVariant = ThemeVariant.Light,
                Child = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Classes = { ScrollBarReveal.PersistentHorizontalClass },
                    // The bottom padding lives inside the scroll area, like a code
                    // block's: the scroll bar of a wide diagram lies in it rather
                    // than over the bottom of the picture.
                    Content = new Border
                    {
                        Padding = new Thickness(0, 0, 0, padding),
                        Child = imageControl,
                    },
                },
            },
        };
    }

    private Grid BuildFailure(DiagramRenderResult.Failure failure, MarkdownDiagramKind kind)
        => BuildErrorFrame(
            "mm-md-diagram-error",
            Format(_strings.RenderFailed, kind),
            failure.Reason == DiagramFailureReason.EmptyDiagram
                ? _strings.EmptyDiagram
                : SummarizeMessage(failure.Message),
            kind,
            failure.Source);

    private Grid BuildPending(MarkdownDiagramBlock block)
    {
        // If we ever reach the view with an un-materialized RenderResult,
        // surface it as an error rather than silently rendering as a code
        // block (success path must not be impersonated by source dump,
        // ADR-0005 §3).
        return BuildFailure(
            new DiagramRenderResult.Failure(
                "Diagram render was not materialized. This is an application composition error.",
                block.Source),
            block.Kind);
    }

    private Grid BuildSvgUnsupportedPlaceholder(MarkdownDiagramBlock block)
        => BuildErrorFrame(
            "mm-md-diagram-svg-unsupported",
            Format(_strings.SvgUnsupported, block.Kind),
            message: null,
            block.Kind,
            block.Source);

    /// <summary>
    /// The dashed frame of a broken image: the icon, the title and the message
    /// centred, the source below them across the whole frame.
    /// </summary>
    private Grid BuildErrorFrame(string stateClass, string title, string? message, MarkdownDiagramKind kind, string source)
    {
        var metrics = _typography.Metrics;
        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };

        var icon = MarkdownMissingContentFrame.CreateIcon(metrics, "mm-md-diagram-error-icon");
        icon.Margin = new Thickness(0, 0, 0, metrics.MissingIconGap);
        content.Children.Add(icon);

        content.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = _typography.BodyFontFamily,
            FontSize = metrics.FontSize,
            LineHeight = metrics.BodyLineHeight,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Classes = { "mm-md-diagram-error-title" },
        });

        if (!string.IsNullOrWhiteSpace(message))
        {
            content.Children.Add(new TextBlock
            {
                Text = message,
                FontFamily = _typography.BodyFontFamily,
                FontSize = metrics.DiagramErrorMessageFontSize,
                LineHeight = metrics.DiagramErrorMessageLineHeight,
                Margin = new Thickness(0, metrics.DiagramErrorMessageGap, 0, 0),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Classes = { "mm-md-diagram-error-message" },
            });
        }

        var sourceBlock = _buildSourceBlock(GetLanguage(kind), source);
        sourceBlock.Margin = new Thickness(0, metrics.DiagramErrorSourceGap, 0, 0);
        content.Children.Add(sourceBlock);

        var frame = MarkdownMissingContentFrame.Create(metrics, metrics.DiagramErrorPadding, minHeight: 0, content);
        frame.Classes.Add("mm-md-diagram");
        frame.Classes.Add(stateClass);
        return frame;
    }

    /// <summary>
    /// The gist of a renderer message: its first line, then where the parser
    /// stopped. Naiad reports a parse error over several lines — what it met,
    /// the long list of what it expected, and the position — and the list is
    /// noise for the reader. A message without that shape is shown as it is.
    /// </summary>
    internal static string SummarizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var lines = message
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0)
            .ToArray();
        if (lines.Length <= 1)
        {
            return lines.Length == 0 ? string.Empty : lines[0];
        }

        var unexpected = lines.FirstOrDefault(static line => line.StartsWith("unexpected ", StringComparison.Ordinal));
        var position = lines.LastOrDefault(static line => line.StartsWith("at line ", StringComparison.Ordinal));
        if (unexpected is null && position is null)
        {
            return string.Join('\n', lines);
        }

        var detail = unexpected is not null && position is not null
            ? $"{unexpected} {position}"
            : unexpected ?? position;
        return $"{lines[0]}\n{detail}";
    }

    private static string GetLanguage(MarkdownDiagramKind kind)
        => kind.ToString().ToLowerInvariant();

    private static string Format(string format, MarkdownDiagramKind kind)
        => string.Format(CultureInfo.CurrentCulture, format, kind);
}

/// <summary>
/// Localized texts of a diagram that could not be shown: titles, where <c>{0}</c>
/// is the dialect, and the explanation of an empty diagram.
/// </summary>
internal sealed record MarkdownDiagramStrings(string RenderFailed, string SvgUnsupported, string EmptyDiagram)
{
    public static MarkdownDiagramStrings English { get; } = new(
        "{0} diagram could not be rendered",
        "{0} diagram rendered, but its SVG is not yet supported by the built-in viewer",
        "The diagram came out empty. Check its syntax.");
}
