using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Окно с темой приложения, как в App.axaml: Fluent, стили контролов, палитра, шрифты,
/// иконки и полоса прокрутки. Тестовая сессия запускается без темы, а шаблоны Fluent и их
/// состояния нужны там, где тест проверяет вид и поведение контролов.
/// </summary>
internal static class ThemedTestWindow
{
    public static Window Create(ThemeVariant theme, object? content = null)
    {
        var window = new Window
        {
            RequestedThemeVariant = theme,
            Width = 400,
            Height = 600
        };

        window.Styles.Add(new FluentTheme());
        window.Styles.Add(Assert.IsAssignableFrom<IStyle>(Load("Controls.axaml")));
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(Load("Colors.axaml")));
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(Load("Typography.axaml")));
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(Load("Icons.axaml")));
        window.Resources.MergedDictionaries.Add(Assert.IsAssignableFrom<IResourceProvider>(Load("ScrollBar.axaml")));

        // Контент — только после темы: тема контрола выбирается, когда он попадает
        // в дерево, и добавленная позже Fluent его шаблон уже не подменит.
        window.Content = content;
        return window;
    }

    private static object Load(string themeFile)
        => AvaloniaXamlLoader.Load(new Uri("avares://MarkMello.Presentation/Themes/" + themeFile));
}
