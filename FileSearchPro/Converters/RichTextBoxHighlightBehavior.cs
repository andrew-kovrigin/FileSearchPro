using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace FileSearchPro.Converters;

public static class RichTextBoxHighlightBehavior
{
    public static readonly DependencyProperty HighlightTextProperty =
        DependencyProperty.RegisterAttached(
            "HighlightText",
            typeof(string),
            typeof(RichTextBoxHighlightBehavior),
            new PropertyMetadata(string.Empty, OnHighlightChanged));

    public static readonly DependencyProperty HighlightWordsProperty =
        DependencyProperty.RegisterAttached(
            "HighlightWords",
            typeof(string),
            typeof(RichTextBoxHighlightBehavior),
            new PropertyMetadata(string.Empty, OnHighlightChanged));

    public static string GetHighlightText(DependencyObject obj) => (string)obj.GetValue(HighlightTextProperty);
    public static void SetHighlightText(DependencyObject obj, string value) => obj.SetValue(HighlightTextProperty, value);

    public static string GetHighlightWords(DependencyObject obj) => (string)obj.GetValue(HighlightWordsProperty);
    public static void SetHighlightWords(DependencyObject obj, string value) => obj.SetValue(HighlightWordsProperty, value);

    private static void OnHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox richTextBox) return;

        var text = GetHighlightText(richTextBox);
        var wordsRaw = GetHighlightWords(richTextBox);

        var doc = new FlowDocument();
        doc.Background = Brushes.Transparent;

        if (string.IsNullOrEmpty(text))
        {
            richTextBox.Document = doc;
            return;
        }

        var words = wordsRaw?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 0)
            .ToArray();

        var lines = text.Split('\n');
        var para = new Paragraph();
        para.Margin = new Thickness(0);
        para.Padding = new Thickness(0);

        var pattern = words is { Length: > 0 }
            ? string.Join("|", words.Select(Regex.Escape))
            : null;

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                para.Inlines.Add(new LineBreak());

            var line = lines[i].EndsWith('\r') ? lines[i][..^1] : lines[i];

            if (line.Length > 0 && pattern != null)
            {
                var matches = Regex.Matches(line, pattern, RegexOptions.IgnoreCase);

                if (matches.Count > 0)
                {
                    int lastIdx = 0;
                    foreach (Match m in matches)
                    {
                        if (m.Index > lastIdx)
                            para.Inlines.Add(new Run(line[lastIdx..m.Index]));

                        var run = new Run(m.Value);
                        run.Background = Brushes.Gold;
                        run.Foreground = Brushes.Black;
                        run.FontWeight = FontWeights.Bold;
                        para.Inlines.Add(run);

                        lastIdx = m.Index + m.Length;
                    }
                    if (lastIdx < line.Length)
                        para.Inlines.Add(new Run(line[lastIdx..]));
                }
                else
                {
                    para.Inlines.Add(new Run(line));
                }
            }
            else if (line.Length > 0)
            {
                para.Inlines.Add(new Run(line));
            }
        }

        doc.Blocks.Add(para);

        richTextBox.Document = doc;
    }
}
