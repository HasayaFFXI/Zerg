using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Zerg.Core;

namespace Zerg.Views;

/// <summary>
/// One compared figure in a Compare table: B's figure, then the change in
/// its tone (better, worse, or grey), on one line; and, while the table
/// shows them (<see cref="ShowAProperty"/>, inherited), A's figure under it
/// in A's text colour. A line in run A only has A's figure in that colour
/// and no change. Its hover card is numbers only: each run's figure by the
/// run's name (<see cref="NameAProperty"/>, inherited), and the change.
///
/// <para>A panel of its own, not a control: there is nothing to style, and a
/// table holds a few hundred of them. Its text is set when its cell changes,
/// not bound.</para>
/// </summary>
public sealed class DeltaCell : Grid
{
    public static readonly DependencyProperty CellProperty = DependencyProperty.Register(nameof(Cell), typeof(Cell),
        typeof(DeltaCell), new PropertyMetadata(null, (d, _) => ((DeltaCell)d).Show()));

    /// <summary>Cells under this show A's figure on a second line.</summary>
    public static readonly DependencyProperty ShowAProperty = DependencyProperty.RegisterAttached("ShowA", typeof(bool),
        typeof(DeltaCell), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits,
            (d, _) => (d as DeltaCell)?.Show()));

    /// <summary>The runs' names, for the hover card.</summary>
    public static readonly DependencyProperty NameAProperty = DependencyProperty.RegisterAttached("NameA", typeof(string),
        typeof(DeltaCell), new FrameworkPropertyMetadata("A", FrameworkPropertyMetadataOptions.Inherits));
    public static readonly DependencyProperty NameBProperty = DependencyProperty.RegisterAttached("NameB", typeof(string),
        typeof(DeltaCell), new FrameworkPropertyMetadata("B", FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetShowA(DependencyObject d) => (bool)d.GetValue(ShowAProperty);
    public static void SetShowA(DependencyObject d, bool value) => d.SetValue(ShowAProperty, value);
    public static string GetNameA(DependencyObject d) => (string)d.GetValue(NameAProperty);
    public static void SetNameA(DependencyObject d, string value) => d.SetValue(NameAProperty, value);
    public static string GetNameB(DependencyObject d) => (string)d.GetValue(NameBProperty);
    public static void SetNameB(DependencyObject d, string value) => d.SetValue(NameBProperty, value);

    readonly TextBlock figure = new(), change = new(), a = new();

    public DeltaCell()
    {
        ColumnDefinitions.Add(new ColumnDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        VerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(2, 0, 8, 0);
        Background = Brushes.Transparent;

        foreach (var t in new[] { figure, change, a })
        {
            t.TextAlignment = TextAlignment.Right;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
        }
        change.MinWidth = 48;
        change.Margin = new Thickness(4, 0, 0, 0);
        change.FontSize = 10.5;
        a.FontSize = 10.5;
        a.Visibility = Visibility.Collapsed;
        SetColumn(change, 1);
        SetRow(a, 1);
        a.SetResourceReference(TextBlock.ForegroundProperty, "RunATextBrush");
        Children.Add(figure);
        Children.Add(change);
        Children.Add(a);

        ToolTip = new ToolTip();
        ToolTipOpening += (_, e) =>
        {
            if (Float.GetOn(this) || Cell is not { } c || c.Change == Change.Blank && c.A == c.B)
            {
                e.Handled = true;
                return;
            }
            ((ToolTip)ToolTip).Content = Card(c);
        };
    }

    public Cell? Cell { get => (Cell?)GetValue(CellProperty); set => SetValue(CellProperty, value); }

    void Show()
    {
        if (Cell is not { } c)
        {
            figure.Text = change.Text = a.Text = "";
            a.Visibility = Visibility.Collapsed;
            return;
        }
        bool onlyA = c.B == CompareSheet.Dash && c.A != CompareSheet.Dash && c.A.Length > 0;
        figure.Text = onlyA ? c.A : c.B;
        if (onlyA) figure.SetResourceReference(TextBlock.ForegroundProperty, "RunATextBrush");
        else if (figure.Text == CompareSheet.Dash) figure.SetResourceReference(TextBlock.ForegroundProperty, "Text4Brush");
        else figure.ClearValue(TextBlock.ForegroundProperty);

        change.Text = onlyA || c.Change.Tone == ChangeTone.None ? "" : c.Change.Main;
        change.SetResourceReference(TextBlock.ForegroundProperty, c.Change.Tone switch
        {
            ChangeTone.Better => "LiveBrush",
            ChangeTone.Worse => "CritBrush",
            _ => "Text3Brush",
        });

        bool second = GetShowA(this) && !onlyA && c.Change != Change.Blank;
        a.Text = second ? c.A : "";
        a.Visibility = second ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The hover card: each run's figure by name, and the change. Numbers only.</summary>
    Grid Card(Cell c)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        void Line(int row, string label, string value, string? ink)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Margin = new Thickness(0, 1, 14, 1) };
            var v = new TextBlock { Text = value, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 1, 0, 1) };
            Typography.SetNumeralAlignment(v, FontNumeralAlignment.Tabular);
            if (ink != null)
            {
                l.SetResourceReference(TextBlock.ForegroundProperty, ink);
                v.SetResourceReference(TextBlock.ForegroundProperty, ink);
            }
            SetRow(l, row);
            SetRow(v, row);
            SetColumn(v, 1);
            g.Children.Add(l);
            g.Children.Add(v);
        }
        Line(0, GetNameA(this), c.A, "RunATextBrush");
        Line(1, GetNameB(this), c.B, null);
        if (c.Change.Tone != ChangeTone.None)
            Line(2, "Δ", c.Spoken, c.Change.Tone switch { ChangeTone.Better => "LiveBrush", ChangeTone.Worse => "CritBrush", _ => "Text3Brush" });
        return g;
    }
}

/// <summary>
/// Run A's tick on a Compare row: where A's figure would end the row's shade,
/// as a short upright mark in A's colour. Paints itself; takes no input.
/// </summary>
public sealed class TickMark : FrameworkElement
{
    public static readonly DependencyProperty AtProperty = DependencyProperty.Register(nameof(At), typeof(double),
        typeof(TickMark), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush),
        typeof(TickMark), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public TickMark()
    {
        IsHitTestVisible = false;
        SetResourceReference(FillProperty, "RunABrush");
    }

    /// <summary>A's figure over B's leader, 0 to 1; 0 draws nothing.</summary>
    public double At { get => (double)GetValue(AtProperty); set => SetValue(AtProperty, value); }
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double at = At, w = RenderSize.Width, h = RenderSize.Height;
        if (!(at > 0) || Fill is null || w < 2 || h < 6) return;
        double x = Math.Clamp(at * w, 1, w - 1);
        dc.DrawRectangle(Fill, null, new Rect(x - 1, 3, 2, h - 6));
    }
}

/// <summary>
/// One line of Deltas, drawn: from the axis in the middle, the line's change
/// in total, to the right if it added and to the left if it took away; green
/// and red by sign, or grey (healing, a character in one run only). Paints
/// itself; takes no input.
/// </summary>
public sealed class DeltaBar : FrameworkElement
{
    static FrameworkPropertyMetadata Paints(object value) => new(value, FrameworkPropertyMetadataOptions.AffectsRender);

    public static readonly DependencyProperty DeltaProperty = DependencyProperty.Register(nameof(Delta), typeof(double), typeof(DeltaBar), Paints(0.0));
    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(nameof(Scale), typeof(double), typeof(DeltaBar), Paints(1.0));
    public static readonly DependencyProperty GreyProperty = DependencyProperty.Register(nameof(Grey), typeof(bool), typeof(DeltaBar), Paints(false));
    static readonly DependencyProperty BetterProperty = DependencyProperty.Register("Better", typeof(Brush), typeof(DeltaBar), Paints(Brushes.Green));
    static readonly DependencyProperty WorseProperty = DependencyProperty.Register("Worse", typeof(Brush), typeof(DeltaBar), Paints(Brushes.Red));
    static readonly DependencyProperty QuietProperty = DependencyProperty.Register("Quiet", typeof(Brush), typeof(DeltaBar), Paints(Brushes.Gray));
    static readonly DependencyProperty AxisProperty = DependencyProperty.Register("Axis", typeof(Brush), typeof(DeltaBar), Paints(Brushes.Gray));

    const double Thick = 10, Gap = 2;

    public DeltaBar()
    {
        IsHitTestVisible = false;
        SetResourceReference(BetterProperty, "LiveBrush");
        SetResourceReference(WorseProperty, "CritBrush");
        SetResourceReference(QuietProperty, "Text3Brush");
        SetResourceReference(AxisProperty, "Line3Brush");
    }

    public double Delta { get => (double)GetValue(DeltaProperty); set => SetValue(DeltaProperty, value); }
    /// <summary>The largest change the pane draws: it reaches the bar's end.</summary>
    public double Scale { get => (double)GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }
    /// <summary>Grey whichever way it went.</summary>
    public bool Grey { get => (bool)GetValue(GreyProperty); set => SetValue(GreyProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = RenderSize.Width, h = RenderSize.Height;
        if (w < 2 * Gap + 2 || h < 2) return;
        double mid = Math.Round(w / 2), top = Math.Round((h - Thick) / 2);
        dc.DrawRectangle((Brush)GetValue(AxisProperty), null, new Rect(mid, 2, 1, Math.Max(0, h - 4)));
        double scale = Scale > 0 && double.IsFinite(Scale) ? Scale : 1;
        double len = Math.Min(Math.Abs(Delta) / scale * (w / 2 - Gap), w / 2 - Gap);
        if (len < 0.5) return;
        len = Math.Max(1, len);
        var fill = (Brush)GetValue(Grey ? QuietProperty : Delta >= 0 ? BetterProperty : WorseProperty);
        dc.DrawRectangle(fill, null, new Rect(Delta >= 0 ? mid + 1 : mid - len, top, len, Thick));
    }
}
