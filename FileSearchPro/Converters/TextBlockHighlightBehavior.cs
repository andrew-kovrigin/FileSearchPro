using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileSearchPro.Converters;

public static class TextBlockHighlightBehavior
{
    public static readonly DependencyProperty HighlightTextProperty =
        DependencyProperty.RegisterAttached(
            "HighlightText",
            typeof(string),
            typeof(TextBlockHighlightBehavior),
            new PropertyMetadata(string.Empty, OnHighlightChanged));

    public static readonly DependencyProperty HighlightWordsProperty =
        DependencyProperty.RegisterAttached(
            "HighlightWords",
            typeof(string),
            typeof(TextBlockHighlightBehavior),
            new PropertyMetadata(string.Empty, OnHighlightChanged));

    public static string GetHighlightText(DependencyObject obj) => (string)obj.GetValue(HighlightTextProperty);
    public static void SetHighlightText(DependencyObject obj, string value) => obj.SetValue(HighlightTextProperty, value);

    public static string GetHighlightWords(DependencyObject obj) => (string)obj.GetValue(HighlightWordsProperty);
    public static void SetHighlightWords(DependencyObject obj, string value) => obj.SetValue(HighlightWordsProperty, value);

    private static void OnHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;

        var text = GetHighlightText(textBlock);
        var wordsRaw = GetHighlightWords(textBlock);

        textBlock.Inlines.Clear();

        if (string.IsNullOrEmpty(text))
            return;

        var words = wordsRaw?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 0)
            .ToArray();

        if (words == null || words.Length == 0)
        {
            textBlock.Inlines.Add(new System.Windows.Documents.Run(text));
            return;
        }

        var pattern = string.Join("|", words.Select(Regex.Escape));
        var lines = text.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            if (line.Length > 0)
            {
                var matches = Regex.Matches(line, pattern, RegexOptions.IgnoreCase);

                if (matches.Count > 0)
                {
                    int lastIdx = 0;
                    foreach (Match m in matches)
                    {
                        if (m.Index > lastIdx)
                            textBlock.Inlines.Add(new System.Windows.Documents.Run(line[lastIdx..m.Index]));

                        var run = new System.Windows.Documents.Run(m.Value);
                        run.Background = Brushes.Gold;
                        run.Foreground = Brushes.Black;
                        run.FontWeight = FontWeights.Bold;
                        textBlock.Inlines.Add(run);

                        lastIdx = m.Index + m.Length;
                    }
                    if (lastIdx < line.Length)
                        textBlock.Inlines.Add(new System.Windows.Documents.Run(line[lastIdx..]));
                }
                else
                {
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(line));
                }
            }

            textBlock.Inlines.Add(new System.Windows.Documents.LineBreak());
        }
    }
}
