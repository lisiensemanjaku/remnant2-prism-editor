using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace PrismEditor;

public partial class MainWindow : Window
{
    private readonly SaveEditor _editor = new();
    private PrismViewModel? _selected;
    private readonly List<EnhancementOption> _enhancementOptions;
    private readonly List<EnhancementOption> _bonusOptions;
    private bool _suppressEvents;

    // Remnant-inspired palette: blood red for Fusions, worn bone/tan for Segments, antique gold for the Legendary bonus.
    private static readonly Color FusionColor = (Color)ColorConverter.ConvertFromString("#b23a30");
    private static readonly Color SegmentColor = (Color)ColorConverter.ConvertFromString("#8f7a5e");
    private static readonly Color BonusColor = (Color)ColorConverter.ConvertFromString("#cfa44a");
    private static readonly Color EmptyColor = (Color)ColorConverter.ConvertFromString("#2a2222");

    public MainWindow()
    {
        InitializeComponent();

        _enhancementOptions = PrismCatalog.Fusions
            .OrderBy(f => f.Name)
            .Select(f => new EnhancementOption(f.RowName, $"[Fusion] {f.Name} — {PrismCatalog.DescribeFusion(f.RowName)}", BuildFusionLines(f.RowName)))
            .Concat(PrismCatalog.Segments.OrderBy(s => PrismCatalog.DescribeSegment(s))
                .Select(s => new EnhancementOption(s.RowName, $"[Segment] {PrismCatalog.DescribeSegment(s)}", BuildSegmentLines(s))))
            .ToList();

        _bonusOptions = PrismCatalog.Bonuses
            .OrderBy(b => b.Name != "?" ? b.Name : PrismCatalog.Humanize(b.RowName))
            .Select(b =>
            {
                var name = b.Name != "?" ? b.Name : PrismCatalog.Humanize(b.RowName);
                return new EnhancementOption(b.RowName, $"{name} — {PrismCatalog.DescribeBonus(b)}", BuildBonusLines(b, name));
            })
            .ToList();

        var optionTemplate = (DataTemplate)FindResource("RichOptionItemTemplate");
        foreach (var combo in new[] { Slot0, Slot1, Slot2, Slot3, Slot4 })
        {
            combo.ItemsSource = _enhancementOptions;
            combo.ItemTemplate = optionTemplate;
        }
        BonusSlot.ItemsSource = _bonusOptions;
        BonusSlot.ItemTemplate = optionTemplate;

        BuildCommunityBuildsPanel();
        DrawDiagram();
    }

    // ---------------- File open / save ----------------

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open your Remnant 2 profile.sav",
            Filter = "Remnant 2 profile (profile.sav)|profile.sav|All files (*.*)|*.*",
            InitialDirectory = @"C:\Users\Lisien\Saved Games\Remnant2\Epic"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _editor.Open(dlg.FileName);
            PathLabel.Text = dlg.FileName;
            PrismList.ItemsSource = _editor.Prisms;
            SaveAsBtn.IsEnabled = true;
            RevertBtn.IsEnabled = _editor.HasOriginalBackup;
            DetailsPanel.IsEnabled = false;
            SetStatus($"Loaded {_editor.Prisms.Count} prisms. A pristine copy of this file was saved as a safety net -- use Revert to original any time.", good: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not read that save file:\n\n" + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save a copy as...",
            Filter = "Remnant 2 profile (*.sav)|*.sav|All files (*.*)|*.*",
            FileName = "profile_edited.sav"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _editor.SaveAs(dlg.FileName);
            SetStatus("Saved a copy to " + dlg.FileName, good: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Save failed:\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RevertButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(this,
            "This restores profile.sav exactly as it was the very first time you opened it in this app -- " +
            "discarding every change made since, including ones you've already saved.\n\nThis cannot be undone. Continue?",
            "Revert to original", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _editor.RevertToOriginal();
            PrismList.ItemsSource = _editor.Prisms;
            DetailsPanel.IsEnabled = false;
            _selected = null;
            SetStatus("Reverted to the original file. All edits since the first time you opened it are gone.", good: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Revert failed:\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---------------- Prism selection ----------------

    private void PrismList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = PrismList.SelectedItem as PrismViewModel;
        if (_selected == null) { DetailsPanel.IsEnabled = false; return; }

        _suppressEvents = true;
        DetailsPanel.IsEnabled = true;
        PrismTitle.Text = _selected.DisplayName;
        PrismStatus.Text = _selected.IsBuilt
            ? $"Raw Level {_selected.RawLevel}  |  Fed: {_selected.HasBeenFed}  |  Seed: {_selected.CurrentSeed}"
            : "Empty / not yet leveled -- pick a configuration below and click Apply to build it.";

        var combos = new[] { Slot0, Slot1, Slot2, Slot3, Slot4 };
        for (int i = 0; i < 5; i++)
        {
            var row = _selected.Slots[i]?.RowName ?? "";
            combos[i].SelectedItem = _enhancementOptions.FirstOrDefault(o => o.RowName == row);
        }
        var bonusRow = _selected.Slots[5]?.RowName ?? "";
        BonusSlot.SelectedItem = _bonusOptions.FirstOrDefault(o => o.RowName == bonusRow);

        _suppressEvents = false;
        RefreshAll();
    }

    private void SlotChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        RefreshAll();
    }

    private void RefreshAll()
    {
        UpdateConflictWarning();
        DrawDiagram();
    }

    private List<string> GetChosenEnhancementRows()
    {
        var combos = new[] { Slot0, Slot1, Slot2, Slot3, Slot4 };
        return combos.Select(c => (c.SelectedItem as EnhancementOption)?.RowName ?? "").ToList();
    }

    private void UpdateConflictWarning()
    {
        var rows = GetChosenEnhancementRows();
        if (rows.Any(string.IsNullOrEmpty)) { ConflictWarning.Text = ""; ApplyBtn.IsEnabled = true; return; }

        var conflicts = SaveEditor.FindOverlapConflicts(rows);
        var tooMany = SaveEditor.TooManyFusions(rows, out var fusionCount);
        var overridden = AllowOverlapCheck.IsChecked == true;

        if (conflicts.Count == 0 && !tooMany)
        {
            ConflictWarning.Text = "";
            ApplyBtn.IsEnabled = true;
            return;
        }

        var problems = new List<string>();
        if (conflicts.Count > 0)
            problems.Add($"{string.Join(", ", conflicts.Select(PrismCatalog.Humanize))} would be granted more than once");
        if (tooMany) problems.Add($"{fusionCount} Fusions picked, but the game caps a prism at {SaveEditor.MaxFusions} (at least 1 slot must be a Segment)");

        if (overridden)
        {
            ConflictWarning.Text = "Rule bypassed (experimental): " + string.Join("; ", problems) + ".";
            ConflictWarning.Foreground = Brushes.Orange;
            ApplyBtn.IsEnabled = true;
        }
        else
        {
            ConflictWarning.Text = "Not allowed by the game: " + string.Join("; ", problems) + ". " +
                "Fix the slots, or check \"Allow the same stat multiple times\" below to override.";
            ConflictWarning.Foreground = Brushes.Red;
            ApplyBtn.IsEnabled = false;
        }
    }

    // ---------------- Community builds panel ----------------

    private void BuildCommunityBuildsPanel()
    {
        BuildsPanel.Children.Clear();
        foreach (var build in CommunityBuilds.All)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x13, 0x13)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3a, 0x22, 0x22)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = build.Name.ToUpperInvariant(), FontFamily = new FontFamily("Georgia"), FontWeight = FontWeights.Bold, FontSize = 14, Foreground = (Brush)new SolidColorBrush(BonusColor) });
            stack.Children.Add(new TextBlock { Text = build.Playstyle, FontSize = 11, Foreground = (Brush)new SolidColorBrush(SegmentColor), Margin = new Thickness(0, 0, 0, 6) });
            stack.Children.Add(new TextBlock { Text = string.Join(" + ", build.EnhancementRowNames.Select(PrismCatalog.Humanize)) + "  |  " + PrismCatalog.Humanize(build.BonusRowName),
                TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
            stack.Children.Add(new TextBlock { Text = build.Notes, TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brushes.LightGray, Margin = new Thickness(0, 0, 0, 6) });
            stack.Children.Add(new TextBlock { Text = "Source: " + build.Source, TextWrapping = TextWrapping.Wrap, FontSize = 10, Foreground = Brushes.DimGray, FontStyle = FontStyles.Italic, Margin = new Thickness(0, 0, 0, 8) });

            var applyBtn = new Button { Content = "Apply to selected prism", Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x16, 0x16)), BorderBrush = new SolidColorBrush(FusionColor), Foreground = new SolidColorBrush(BonusColor) };
            applyBtn.Click += (s, e) => ApplyCommunityBuild(build);
            stack.Children.Add(applyBtn);

            border.Child = stack;
            BuildsPanel.Children.Add(border);
        }
    }

    private void ApplyCommunityBuild(CommunityBuild build)
    {
        if (_selected == null)
        {
            MessageBox.Show(this, "Select a prism on the left first.", "No prism selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _suppressEvents = true;
        var combos = new[] { Slot0, Slot1, Slot2, Slot3, Slot4 };
        for (int i = 0; i < 5; i++)
            combos[i].SelectedItem = _enhancementOptions.FirstOrDefault(o => o.RowName == build.EnhancementRowNames[i]);
        BonusSlot.SelectedItem = _bonusOptions.FirstOrDefault(o => o.RowName == build.BonusRowName);
        _suppressEvents = false;

        RefreshAll();
        SetStatus($"Filled in \"{build.Name}\" for {_selected.DisplayName}. Click Apply to commit it.", good: true);
    }

    // ---------------- Apply / write ----------------

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var rows = GetChosenEnhancementRows();
        if (rows.Any(string.IsNullOrEmpty) || BonusSlot.SelectedItem == null)
        {
            MessageBox.Show(this, "Pick all 5 enhancement slots and the legendary bonus first.", "Incomplete",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        const int level = 99; // reliably produces max Display Level 51 in-game; not something the user needs to tune.

        var bonusRow = ((EnhancementOption)BonusSlot.SelectedItem).RowName;
        var allowOverlap = AllowOverlapCheck.IsChecked == true;

        var confirm = MessageBox.Show(this,
            $"This writes directly into your real profile.sav for {_selected.DisplayName}.\n\n" +
            "A timestamped backup is made automatically, and you can always use \"Revert to original\" to undo everything back to the very first time you opened this file.\n\nContinue?",
            "Write to profile.sav", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _editor.ApplyConfiguration(_selected, rows.ToArray(), bonusRow, level, allowOverlap);
            _editor.Save(backup: true);
            PrismList.Items.Refresh();
            PrismList_SelectionChanged(this, null!);
            SetStatus($"Applied and saved {_selected.DisplayName} to profile.sav. A backup of the previous file was created alongside it.", good: true);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "Can't apply that configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Something went wrong applying/saving the configuration:\n\n" + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---------------- Prism visual: a faceted gem + level badge, and a game-styled build list ----------------
    // Modeled on an actual in-game screenshot: the prism is a crystal with its level in a circular
    // badge near the top, and the equipped build is a plain left-aligned list (icon, name, value;
    // a Fusion's two stats indented under its own header) -- not a ring/spiral of icons.

    private void DrawDiagram()
    {
        DiagramCanvas.Children.Clear();
        const double cx = 90, topY = 10, bottomY = 190;

        // The exact same gem shape used for each prism's small list icon, just "zoomed in" here
        // for the selected one -- not a separately hand-built shape that could drift out of sync.
        var gem = new System.Windows.Shapes.Path
        {
            Data = (Geometry)FindResource("GemGeometry"),
            Fill = (Brush)FindResource("GemFillBrush"),
            Stroke = new SolidColorBrush(Color.FromRgb(0x0b, 0x0a, 0x0a)),
            StrokeThickness = 1.5,
            Width = 108, Height = 190, Stretch = Stretch.Uniform,
        };
        gem.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = BonusColor, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.35 };
        Canvas.SetLeft(gem, cx - 54);
        Canvas.SetTop(gem, topY);
        DiagramCanvas.Children.Add(gem);

        // Level badge, overlapping the top of the gem.
        var badge = new Ellipse { Width = 40, Height = 40, Fill = new SolidColorBrush(Color.FromRgb(0x14, 0x10, 0x10)), Stroke = new SolidColorBrush(BonusColor), StrokeThickness = 1.5 };
        Canvas.SetLeft(badge, cx - 20);
        Canvas.SetTop(badge, topY - 6);
        DiagramCanvas.Children.Add(badge);

        var levelText = new TextBlock
        {
            Text = (_selected?.IsBuilt == true) ? "51" : "0",
            FontWeight = FontWeights.Bold, FontSize = 15, Foreground = new SolidColorBrush(BonusColor), TextAlignment = TextAlignment.Center, Width = 40
        };
        Canvas.SetLeft(levelText, cx - 20);
        Canvas.SetTop(levelText, topY + 4);
        DiagramCanvas.Children.Add(levelText);

        BuildPrismStyleList();
    }

    private void BuildPrismStyleList()
    {
        BuildListPanel.Children.Clear();
        var rows = GetChosenEnhancementRows();
        var bonusOpt = BonusSlot.SelectedItem as EnhancementOption;

        for (int i = 0; i < 5; i++)
        {
            var row = rows[i];
            if (string.IsNullOrEmpty(row)) continue;

            if (PrismCatalog.Fusions.Any(f => f.RowName == row))
            {
                var entry = PrismCatalog.Fusions.First(f => f.RowName == row);
                var (c1, c2) = PrismCatalog.GetFusionColorNames(row);
                BuildListPanel.Children.Add(MakeListRow(ColorNameToBrush(c1), $"{entry.Name} +10", null, bold: true));
                foreach (var half in PrismCatalog.DescribeFusion(row).Split(", "))
                {
                    var (name, val) = SplitNameValue(half);
                    BuildListPanel.Children.Add(MakeListRow(ColorNameToBrush(c2), name, val, indent: true));
                }
            }
            else
            {
                var entry = PrismCatalog.Segments.First(s => s.RowName == row);
                var (name, val) = SplitNameValue(PrismCatalog.DescribeSegment(entry));
                BuildListPanel.Children.Add(MakeListRow(ColorNameToBrush(PrismCatalog.GetSegmentColorName(row)), name, val));
            }
        }

        if (bonusOpt != null)
        {
            var entry = PrismCatalog.Bonuses.First(b => b.RowName == bonusOpt.RowName);
            BuildListPanel.Children.Add(MakeListRow(new SolidColorBrush(BonusColor), entry.Name, null, bold: true, topMargin: true));
            BuildListPanel.Children.Add(new TextBlock
            {
                Text = PrismCatalog.DescribeBonus(entry), TextWrapping = TextWrapping.Wrap, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xb0, 0xa6, 0x98)), Margin = new Thickness(20, 1, 0, 6)
            });
        }

        if (BuildListPanel.Children.Count == 0)
            BuildListPanel.Children.Add(new TextBlock { Text = "Pick slots below to build this prism.", Foreground = Brushes.Gray, FontSize = 12 });
    }

    private static (string name, string? value) SplitNameValue(string text)
    {
        var i = text.LastIndexOf('+');
        if (i <= 0) { i = text.LastIndexOf(" -"); }
        return i > 0 ? (text[..i].Trim(), text[i..].Trim()) : (text, null);
    }

    private UIElement MakeListRow(Brush? iconColor, string name, string? value, bool bold = false, bool indent = false, bool topMargin = false)
    {
        var grid = new Grid { Margin = new Thickness(indent ? 20 : 0, topMargin ? 10 : 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (iconColor != null)
        {
            var icon = new Polygon
            {
                Points = new PointCollection { new Point(0, 8), new Point(5, 0), new Point(10, 8), new Point(5, 12) },
                Fill = iconColor, Width = 10, Height = 12, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);
        }

        var nameBlock = new TextBlock { Text = name, FontSize = 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(nameBlock, 1);
        grid.Children.Add(nameBlock);

        if (value != null)
        {
            var valueBlock = new TextBlock { Text = value, FontSize = 12, Foreground = new SolidColorBrush(BonusColor), HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(valueBlock, 2);
            grid.Children.Add(valueBlock);
        }
        return grid;
    }

    private static Brush ColorNameToBrush(string? colorName) => colorName switch
    {
        "Red" => new SolidColorBrush(Color.FromRgb(0xc6, 0x3a, 0x30)),
        "Blue" => new SolidColorBrush(Color.FromRgb(0x4a, 0x7f, 0xa8)),
        "Yellow" => new SolidColorBrush(Color.FromRgb(0xc9, 0xa2, 0x3f)),
        "Green" => new SolidColorBrush(Color.FromRgb(0x5f, 0x9e, 0x5a)),
        _ => new SolidColorBrush(Color.FromRgb(0x8a, 0x7e, 0x74)),
    };

    // ---------------- Dropdown row content: same visual language as the build-list preview ----------------

    private static List<OptionLine> BuildFusionLines(string rowName)
    {
        var entry = PrismCatalog.Fusions.First(f => f.RowName == rowName);
        var (c1, c2) = PrismCatalog.GetFusionColorNames(rowName);
        var lines = new List<OptionLine>
        {
            new() { Text = $"{entry.Name} +10", Weight = FontWeights.Bold, Icon = ColorNameToBrush(c1) }
        };
        foreach (var half in PrismCatalog.DescribeFusion(rowName).Split(", "))
        {
            var (name, val) = SplitNameValue(half);
            lines.Add(new OptionLine { Text = name, Value = val, Icon = ColorNameToBrush(c2), Margin = new Thickness(18, 1, 0, 1) });
        }
        return lines;
    }

    private static List<OptionLine> BuildSegmentLines(CatalogEntry segment)
    {
        var (name, val) = SplitNameValue(PrismCatalog.DescribeSegment(segment));
        return new List<OptionLine> { new() { Text = name, Value = val, Icon = ColorNameToBrush(PrismCatalog.GetSegmentColorName(segment.RowName)) } };
    }

    private static List<OptionLine> BuildBonusLines(CatalogEntry bonus, string name)
    {
        return new List<OptionLine>
        {
            new() { Text = name, Weight = FontWeights.Bold, Icon = new SolidColorBrush(BonusColor) },
            new() { Text = PrismCatalog.DescribeBonus(bonus), FontSize = 10, Margin = new Thickness(18, 0, 0, 2) }
        };
    }

    private void SetStatus(string text, bool good)
    {
        StatusLabel.Text = text;
        StatusLabel.Foreground = good ? Brushes.LightGreen : Brushes.OrangeRed;
    }
}
