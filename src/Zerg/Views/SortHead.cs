using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Zerg.Core;

namespace Zerg.Views;

/// <summary>
/// A column heading that orders its table. Pressed, the table is listed by
/// its column, largest first (a column of words, A to Z); again, the other
/// way; a third time, in the table's own order (<see cref="TableOrder.Press"/>).
/// A small caret beside the heading's words says which column, and which
/// way: pointing down while the largest (or Z) is at the top.
///
/// <para>It holds the heading as it was, a <c>TextBlock</c> in the heading's
/// own style, and takes no more room than it: the caret stands in the gap
/// before a figure's heading (whose words are set against the column's right
/// end) or after a word's. So a column is as wide sorted as not, and a
/// heading as narrow as its column's least is not cut short by the caret.</para>
///
/// <para>Which table it orders is inherited (<see cref="SortProperty"/>,
/// set once on the row of headings); which column of it is its own. A
/// column that goes when the table is narrow takes this with it: the style
/// that drops it is set here, not on the words inside.</para>
///
/// <para>It takes the keyboard, as a button does (Enter or Space), except
/// in a floating panel, which never does; a click there still orders.</para>
/// </summary>
public sealed class SortHead : Decorator
{
    /// <summary>The table the headings under this order: inherited.</summary>
    public static readonly DependencyProperty SortProperty = DependencyProperty.RegisterAttached("Sort", typeof(ColumnSort),
        typeof(SortHead), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static ColumnSort? GetSort(DependencyObject d) => (ColumnSort?)d.GetValue(SortProperty);
    public static void SetSort(DependencyObject d, ColumnSort? value) => d.SetValue(SortProperty, value);

    /// <summary>The column's key, as the table's view model names it ("damage").</summary>
    public static readonly DependencyProperty ColumnProperty = DependencyProperty.Register(nameof(Column), typeof(string),
        typeof(SortHead), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>A column of words, which goes A to Z first.</summary>
    public static readonly DependencyProperty WordsProperty = DependencyProperty.Register(nameof(Words), typeof(bool),
        typeof(SortHead), new FrameworkPropertyMetadata(false));

    /// <summary>The table's order as it stands, bound to the table's <see cref="ColumnSort.By"/>.</summary>
    static readonly DependencyProperty ByProperty = DependencyProperty.Register("By", typeof(TableOrder?),
        typeof(SortHead), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, e) => ((SortHead)d).Told()));

    /// <summary>The caret's ink and the heading's under the pointer: the theme's, kept as resource references.</summary>
    static readonly DependencyProperty CaretBrushProperty = DependencyProperty.Register("CaretBrush", typeof(Brush),
        typeof(SortHead), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    static readonly DependencyProperty HoverBrushProperty = DependencyProperty.Register("HoverBrush", typeof(Brush),
        typeof(SortHead), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The caret: this wide and half as tall, and this far from the words.</summary>
    const double CaretWidth = 7, Gap = 3;

    static SortHead()
    {
        // Not a tab stop in a floating panel, which must never take the keyboard.
        FocusableProperty.OverrideMetadata(typeof(SortHead), new FrameworkPropertyMetadata(true));
        CursorProperty.OverrideMetadata(typeof(SortHead), new FrameworkPropertyMetadata(Cursors.Hand));
    }

    public SortHead()
    {
        SetBinding(ByProperty, new Binding { Path = new PropertyPath("(0).By", SortProperty), RelativeSource = RelativeSource.Self });
        SetResourceReference(CaretBrushProperty, "Text1Brush");
        SetResourceReference(HoverBrushProperty, "RowHoverBrush");
        SetResourceReference(FocusVisualStyleProperty, "ZFocusInset");
    }

    public string Column { get => (string)GetValue(ColumnProperty); set => SetValue(ColumnProperty, value); }
    public bool Words { get => (bool)GetValue(WordsProperty); set => SetValue(WordsProperty, value); }

    /// <summary>Which way this column orders the table now; null while it does not.</summary>
    bool? Way => GetValue(ByProperty) is TableOrder by && by.Column == Column ? by.Descending : null;

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == Float.OnProperty) Focusable = !(bool)e.NewValue;
    }

    void Press() => GetSort(this)?.Press(Column, Words);

    // ---------------------------------------------------------------- input

    bool pressed;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // Handled, so a heading inside a button (the drill-down's list
        // opens under one) orders the list and does not fold it.
        pressed = true;
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!pressed) return;
        pressed = false;
        e.Handled = true;
        Press();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        pressed = false;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Press();
            return;
        }
        base.OnKeyDown(e);
    }

    // -------------------------------------------------------------- drawing

    protected override void OnRender(DrawingContext dc)
    {
        // Ink over the whole heading, so all of it can be pressed.
        dc.DrawRectangle(IsMouseOver ? (Brush)GetValue(HoverBrushProperty) : Brushes.Transparent, null, new Rect(RenderSize));
        if (Way is not bool descending || Child is not FrameworkElement words) return;

        // Where the words are: a TextBlock is as wide as its words and its
        // padding, set against one end of the cell.
        var pad = words is TextBlock tb ? tb.Padding : default;
        double wide = Math.Max(0, words.DesiredSize.Width - pad.Left - pad.Right);
        bool right = words is TextBlock { TextAlignment: TextAlignment.Right };
        var at = VisualTreeHelper.GetOffset(words);
        double x = right ? at.X + words.RenderSize.Width - pad.Right - wide - Gap - CaretWidth
                         : at.X + pad.Left + wide + Gap;
        double y = at.Y + words.RenderSize.Height / 2, h = CaretWidth / 2;

        var caret = new StreamGeometry();
        using (var g = caret.Open())
        {
            double tip = descending ? y + h / 2 : y - h / 2, back = descending ? y - h / 2 : y + h / 2;
            g.BeginFigure(new Point(x, back), true, true);
            g.LineTo(new Point(x + CaretWidth, back), false, false);
            g.LineTo(new Point(x + CaretWidth / 2, tip), false, false);
        }
        caret.Freeze();
        dc.DrawGeometry((Brush)GetValue(CaretBrushProperty), null, caret);
    }

    // ------------------------------------------------------- UI Automation

    /// <summary>A reader that is listening hears that the order changed.</summary>
    void Told()
    {
        if (UIElementAutomationPeer.FromElement(this) is { } peer)
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, "", Status);
    }

    /// <summary>What a reader is told of the order: "sorted, largest first", or "".</summary>
    string Status => Way switch
    {
        true => Words ? "sorted, Z to A" : "sorted, largest first",
        false => Words ? "sorted, A to Z" : "sorted, smallest first",
        null => "",
    };

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    /// <summary>
    /// A column heading that can be pressed, named by its words (or by a
    /// name given in XAML, where two headings of a table say the same, as
    /// the weaponskill's Damage and the pet's do). The words inside are not
    /// a second thing to a reader: they are this.
    /// </summary>
    sealed class Peer(SortHead owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.HeaderItem;
        protected override string GetClassNameCore() => nameof(SortHead);

        protected override string GetNameCore()
        {
            var given = base.GetNameCore();
            if (!string.IsNullOrEmpty(given)) return given;
            return owner.Child is TextBlock tb ? tb.Text : "";
        }

        protected override string GetHelpTextCore() =>
            owner.Child is FrameworkElement { ToolTip: string tip } ? tip : "Press to sort by this column";

        protected override string GetItemStatusCore() => owner.Status;

        protected override List<AutomationPeer>? GetChildrenCore() => null;

        public override object? GetPattern(PatternInterface pattern) =>
            pattern == PatternInterface.Invoke ? this : base.GetPattern(pattern);

        public void Invoke() => owner.Press();
    }
}
