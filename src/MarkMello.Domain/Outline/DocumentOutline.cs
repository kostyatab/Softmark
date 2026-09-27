using System.Text;

namespace MarkMello.Domain.Outline;

/// <summary>Пункт оглавления: заголовок верхнего уровня документа.</summary>
/// <param name="Level">Уровень заголовка, 1–<see cref="DocumentOutline.MaxLevel"/>.</param>
/// <param name="Text">Плоский текст заголовка в одну строку.</param>
/// <param name="BlockIndex">Индекс блока заголовка в <see cref="RenderedMarkdownDocument.Blocks"/>.</param>
public sealed record DocumentOutlineEntry(int Level, string Text, int BlockIndex);

/// <summary>
/// Оглавление документа для рельса у правого края: заголовки H1–H3 верхнего уровня.
/// Заголовки внутри цитат, плашек и списков не попадают — это часть их блока, а не
/// раздел документа; пустые заголовки пропускаются.
/// </summary>
public sealed class DocumentOutline
{
    public const int MaxLevel = 3;

    /// <summary>Меньше двух разделов — рельсу нечего показывать.</summary>
    public const int MinimumEntryCount = 2;

    /// <summary>
    /// Линия чтения — на столько ниже верха окна документа: раздел становится текущим,
    /// когда его заголовок поднялся к верху окна, а не когда он только показался.
    /// </summary>
    public const double ReadingLineOffset = 80;

    /// <summary>Прокрутка в пределах пикселя от конца считается концом: смещение дробное.</summary>
    private const double ScrollEndTolerance = 1;

    private DocumentOutline(IReadOnlyList<DocumentOutlineEntry> entries)
    {
        Entries = entries;
    }

    public static DocumentOutline Empty { get; } = new([]);

    public IReadOnlyList<DocumentOutlineEntry> Entries { get; }

    public bool HasEnoughEntries => Entries.Count >= MinimumEntryCount;

    public static DocumentOutline Create(RenderedMarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var entries = new List<DocumentOutlineEntry>();
        for (var index = 0; index < document.Blocks.Count; index++)
        {
            if (document.Blocks[index] is not MarkdownHeadingBlock { Level: >= 1 and <= MaxLevel } heading)
            {
                continue;
            }

            var text = CollapseWhitespace(MarkdownDocumentTextMap.ExtractPlainText(heading.Inlines));
            if (text.Length > 0)
            {
                entries.Add(new DocumentOutlineEntry(heading.Level, text, index));
            }
        }

        return entries.Count == 0 ? Empty : new DocumentOutline(entries);
    }

    /// <summary>
    /// Текущий раздел: последний заголовок, верх которого выше линии чтения в
    /// <see cref="ReadingLineOffset"/> от верха окна; пока первый заголовок до неё не
    /// дошёл — первый; в конце прокрутки — последний, иначе короткий последний раздел
    /// никогда не стал бы текущим.
    /// </summary>
    /// <param name="headingTops">Верх каждого заголовка в координатах документа, по порядку.</param>
    /// <param name="scrollOffset">Текущее смещение прокрутки.</param>
    /// <param name="scrollMaximum">Наибольшее смещение; 0 — документ целиком в окне.</param>
    /// <returns>Индекс пункта или -1, если заголовков нет.</returns>
    public static int FindCurrentEntry(
        IReadOnlyList<double> headingTops,
        double scrollOffset,
        double scrollMaximum)
    {
        ArgumentNullException.ThrowIfNull(headingTops);

        if (headingTops.Count == 0)
        {
            return -1;
        }

        if (scrollMaximum > 0 && scrollOffset >= scrollMaximum - ScrollEndTolerance)
        {
            return headingTops.Count - 1;
        }

        var readingLine = scrollOffset + ReadingLineOffset;
        var current = 0;
        for (var index = 0; index < headingTops.Count; index++)
        {
            if (headingTops[index] < readingLine)
            {
                current = index;
            }
            else
            {
                break;
            }
        }

        return current;
    }

    private static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
