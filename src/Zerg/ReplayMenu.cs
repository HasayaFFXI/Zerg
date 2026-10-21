using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;

namespace Zerg;

/// <summary>
/// The Replay menu, under the command bar's Replay button: the newest event
/// files in the folder being followed, a line each (whose, when, how long,
/// how big), then Browse for any other. Made each time it opens, so it
/// lists what is in the folder now.
/// </summary>
static class ReplayMenu
{
    /// <summary>The columns of a file's line, so the lines below one another line up.</summary>
    static readonly double[] Columns = [84, 176, 64, 64];

    public static void Open(Button under, MainViewModel model)
    {
        var menu = new ContextMenu { PlacementTarget = under, Placement = PlacementMode.Bottom };
        var files = model.RecentEventFiles();
        if (files.Count == 0)
            menu.Items.Add(new MenuItem { Header = "No event files in " + model.EventsFolder, IsEnabled = false });
        foreach (var f in files)
        {
            var item = new MenuItem
            {
                Header = Line(f), Command = model.ReplayFileCommand, CommandParameter = f.Path, ToolTip = f.Path,
            };
            // What UI Automation calls it: the file, not the line's pieces.
            AutomationProperties.SetName(item, "Replay " + Path.GetFileName(f.Path));
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = "Browse…", Command = model.BrowseReplayCommand, ToolTip = "Replay any event file (.jsonl)",
        });
        menu.IsOpen = true;
    }

    static Grid Line(EventFileEntry f)
    {
        var line = new Grid();
        foreach (var w in Columns) line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        void Cell(string text, int column, bool right = false, bool quiet = false, bool strong = false)
        {
            var t = new TextBlock
            {
                Text = text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal,
            };
            Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
            if (quiet) t.SetResourceReference(TextBlock.ForegroundProperty, "Text3Brush");
            Grid.SetColumn(t, column);
            line.Children.Add(t);
        }
        Cell(f.Owner, 0, strong: true);
        // The file being followed is still being written: what it holds now.
        Cell(f.Followed ? (f.When.Length > 0 ? f.When + " · followed" : "being followed") : f.When, 1, quiet: f.Followed);
        Cell(f.Length, 2, right: true, quiet: true);
        Cell(f.Size, 3, right: true, quiet: true);
        return line;
    }
}
