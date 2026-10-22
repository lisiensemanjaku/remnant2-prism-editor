using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PrismEditor
{
    /// <summary>One visual row within a dropdown entry -- a Fusion renders as 3 of these
    /// (bold header + 2 indented sub-stats), a Segment or Bonus as 1 or 2.</summary>
    public class OptionLine
    {
        public string Text { get; set; } = "";
        public string? Value { get; set; }
        public Brush? Icon { get; set; }
        public FontWeight Weight { get; set; } = FontWeights.Normal;
        public double FontSize { get; set; } = 12;
        public Thickness Margin { get; set; } = new Thickness(0, 1, 0, 1);
        public Visibility ValueVisibility => Value != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IconVisibility => Icon != null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>One enhancement/bonus dropdown entry: a stable RowName to select by, a plain
    /// Display string for the closed combo box, and the rich Lines shown in the open dropdown
    /// (styled the same way the "current build" preview renders a slot).</summary>
    public class EnhancementOption
    {
        public string RowName { get; set; } = "";
        public string Display { get; set; } = "";
        public List<OptionLine> Lines { get; set; } = new();

        public EnhancementOption(string rowName, string display, List<OptionLine> lines)
        {
            RowName = rowName;
            Display = display;
            Lines = lines;
        }
    }
}
