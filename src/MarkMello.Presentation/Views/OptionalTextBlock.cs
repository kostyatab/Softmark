using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MarkMello.Presentation.Views;

/// <summary>
/// Второстепенная подпись, которая либо видна целиком, либо не рисуется совсем:
/// обрубок вроде «Язык, обновления, верс…» хуже пустого места. Место под неё
/// раскладка по-прежнему отводит, а обрезку определяет свёрнутая строка
/// <see cref="TextBlock.TextLayout"/>, поэтому <see cref="TextBlock.TextTrimming"/>
/// нужен любой, кроме <see cref="TextTrimming.None"/>.
/// </summary>
/// <remarks>
/// Пример — нижняя строка карточки Aa: рядом плашки «⌘» «,» на macOS и «Ctrl» «,»
/// на Windows и Linux, и русская подсказка там не помещается, а с «Ctrl» — и английская. Ссылка и сочетание клавиш важнее подсказки, поэтому уступает она.
/// Текст остаётся в дереве доступности.
/// </remarks>
public sealed class OptionalTextBlock : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>Подпись не поместилась и поэтому не рисуется.</summary>
    public bool IsTrimmed => TextLayout.TextLines.Any(line => line.HasCollapsed);

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        if (!IsTrimmed)
        {
            base.RenderTextLayout(context, origin);
        }
    }
}
