using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Zerg.Views;

/// <summary>What a <see cref="TabStrip"/> says when a tab is moved: from
/// one place to another, and whether that is where it was left.</summary>
public sealed class TabMovedEventArgs(RoutedEvent routed, object source, int from, int to, bool done)
    : RoutedEventArgs(routed, source)
{
    /// <summary>The tab's place before, counted from the left.</summary>
    public int From { get; } = from;
    /// <summary>Its place now.</summary>
    public int To { get; } = to;
    /// <summary>
    /// False for each step of a drag, which moves the tab as it goes; true
    /// once, when it is let go (or moved by a key), with <see cref="From"/>
    /// where it began and <see cref="To"/> where it was left. That one
    /// moves nothing: it says what was done.
    /// </summary>
    public bool Done { get; } = done;
}

/// <summary>
/// The parses' tabs in the title bar, side by side, sharing the room the bar
/// leaves them, and put in another order by dragging one along the bar or
/// by Ctrl+Shift+Left and Right.
///
/// <para>While they fit, each is as wide as it asks to be. When they do not,
/// the widest give way first: every tab wider than an even share is measured
/// again at that share, and its name is cut short with an ellipsis (a text
/// block is trimmed to the width it was measured in). A tab is never made
/// narrower than <see cref="Least"/>; past that the row runs on under the
/// end of the room it was given and is cut off there, so it never lies over
/// the gear or the window's buttons. Its tooltip names a tab cut short.</para>
///
/// <para><see cref="Spare"/> units at the end are kept empty, so a bar full
/// of tabs still has a piece of caption to drag the window by.</para>
///
/// <para><b>A tab dragged</b> follows the pointer along the bar, and the
/// others step aside as it passes the middle of each: the panel does not
/// hold the order, it says each step (<see cref="MovedEvent"/>) and the
/// list it shows follows. The tab is picked as the drag begins, as a
/// browser's is. Esc puts it back where it began. The panel holds the
/// mouse for the drag, not the tab, because a list that moves an item may
/// put its tab back in the panel as another child, and a child taken out
/// loses the mouse; which tab is in the hand is kept as the item it shows.
/// Taking the mouse from the tab is also what keeps the press from being a
/// click when it is let go.</para>
/// </summary>
public sealed class TabStrip : Panel
{
    public TabStrip() => ClipToBounds = true;

    /// <summary>The narrowest a tab is made, its mark included.</summary>
    const double Least = 76;
    /// <summary>The caption kept at the end of a full row.</summary>
    const double Spare = 48;

    /// <summary>A tab was moved. The main window hands it to the view model,
    /// which moves the parse in its list.</summary>
    public static readonly RoutedEvent MovedEvent = EventManager.RegisterRoutedEvent("Moved", RoutingStrategy.Bubble,
        typeof(EventHandler<TabMovedEventArgs>), typeof(TabStrip));

    // ---------------------------------------------------------------- layout

    /// <summary>Where each tab was put by the last arrange, by its child.</summary>
    readonly Dictionary<UIElement, double> lefts = [];

    protected override Size MeasureOverride(Size available)
    {
        var tabs = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToList();
        double height = 0, total = 0;
        foreach (var tab in tabs)
        {
            tab.Measure(new Size(double.PositiveInfinity, available.Height));
            height = Math.Max(height, tab.DesiredSize.Height);
            total += tab.DesiredSize.Width;
        }

        double room = Math.Max(0, available.Width - Spare);
        if (double.IsInfinity(available.Width) || total <= room) return new Size(total, height);

        double share = Share(tabs.Select(t => t.DesiredSize.Width).ToList(), room);
        total = 0;
        foreach (var tab in tabs)
        {
            if (tab.DesiredSize.Width > share) tab.Measure(new Size(share, available.Height));
            total += tab.DesiredSize.Width;
        }
        return new Size(Math.Min(total, available.Width), height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        lefts.Clear();
        var held = Held();
        double x = 0;
        foreach (UIElement tab in InternalChildren)
        {
            if (tab.Visibility == Visibility.Collapsed) continue;
            tab.Arrange(new Rect(x, 0, tab.DesiredSize.Width, final.Height));
            lefts[tab] = x;
            // The tab in the hand is drawn where the pointer has it, its
            // place in the row left for it.
            tab.RenderTransform = ReferenceEquals(tab, held)
                ? new TranslateTransform(Math.Clamp(dragLeft, 0, Math.Max(0, Ends() - tab.DesiredSize.Width)) - x, 0)
                : Transform.Identity;
            x += tab.DesiredSize.Width;
        }
        return final;
    }

    /// <summary>Where the row ends: the right-hand edge of its last tab, or
    /// of the room, if the row runs past it.</summary>
    double Ends()
    {
        double end = InternalChildren.Cast<UIElement>().Sum(c => c.Visibility == Visibility.Collapsed ? 0 : c.DesiredSize.Width);
        return Math.Min(end, RenderSize.Width);
    }

    /// <summary>
    /// The widest any tab may be for <paramref name="widths"/> to fit in
    /// <paramref name="room"/>: tabs narrower than it keep their width, and
    /// the rest share what those leave evenly. Never under <see cref="Least"/>.
    /// </summary>
    static double Share(List<double> widths, double room)
    {
        widths.Sort();
        double left = room;
        for (int i = 0; i < widths.Count; i++)
        {
            double even = left / (widths.Count - i);
            if (widths[i] > even) return Math.Max(Least, even);
            left -= widths[i];
        }
        return Math.Max(Least, widths[^1]);
    }

    // ------------------------------------------------------------------ drag

    /// <summary>The item whose tab was pressed, until the button comes up.</summary>
    object? pressed;
    /// <summary>Where the press was, in the panel, and how far into its tab.</summary>
    Point pressedAt;
    double grab;
    /// <summary>The tab is in the hand: the mouse has gone far enough since the press.</summary>
    bool dragging;
    /// <summary>Where the tab in the hand is drawn: its left-hand edge, in the panel.</summary>
    double dragLeft;
    /// <summary>Its place when the drag began, for Esc and for the last word.</summary>
    int began;
    Window? keys;

    ItemsControl? Owner => ItemsControl.GetItemsOwner(this);

    /// <summary>The tab of <paramref name="item"/>, as the panel holds it now.</summary>
    UIElement? TabOf(object? item) =>
        item != null && Owner?.ItemContainerGenerator.ContainerFromItem(item) is UIElement tab &&
        InternalChildren.Contains(tab) ? tab : null;

    UIElement? Held() => dragging ? TabOf(pressed) : null;

    int PlaceOf(object item) => Owner?.Items.IndexOf(item) ?? -1;

    /// <summary>The child of the panel that <paramref name="from"/> is inside, if any.</summary>
    UIElement? ChildOf(DependencyObject? from)
    {
        while (from != null)
        {
            if (from is UIElement child && VisualTreeHelper.GetParent(child) == this) return child;
            from = Up(from);
        }
        return null;
    }

    /// <summary>A press on a tab may begin a drag, but nothing happens yet:
    /// let go where it was, it is a click. A press on the tab's mark is the
    /// mark's alone.</summary>
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        if (dragging || Above<ButtonBase>(e.OriginalSource as DependencyObject, b => b is not ToggleButton) != null) return;
        if (ChildOf(e.OriginalSource as DependencyObject) is not { } tab) return;
        pressed = Owner?.ItemContainerGenerator.ItemFromContainer(tab);
        if (pressed == DependencyProperty.UnsetValue) pressed = null;
        pressedAt = e.GetPosition(this);
        grab = pressedAt.X - (lefts.TryGetValue(tab, out var left) ? left : 0);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (pressed == null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            End(keep: true);
            return;
        }
        var at = e.GetPosition(this);
        if (!dragging)
        {
            if (Math.Abs(at.X - pressedAt.X) < SystemParameters.MinimumHorizontalDragDistance) return;
            Begin();
            if (!dragging) return;
        }
        dragLeft = at.X - grab;
        Step();
        InvalidateArrange();
        e.Handled = true;
    }

    void Begin()
    {
        if (TabOf(pressed) is not { } tab)
        {
            pressed = null;
            return;
        }
        // In the hand before the mouse is taken: taking it sends a move of
        // its own there and then, which must find the drag begun, not
        // begin it a second time.
        dragging = true;
        began = PlaceOf(pressed!);
        if (!CaptureMouse())
        {
            // Without the mouse the drag would never end: no drag.
            dragging = false;
            pressed = null;
            return;
        }
        Panel.SetZIndex(tab, 1);
        // Picked as it is taken, as a browser's tab is.
        if (Under<ToggleButton>(tab) is { } toggle) toggle.IsChecked = true;
        keys = Window.GetWindow(this);
        if (keys != null) keys.PreviewKeyDown += OnDragKey;
    }

    /// <summary>
    /// The middle of the tab in the hand passes the middle of a neighbour,
    /// where that neighbour stands with the held tab in its place: the two
    /// change places, and the next neighbour is asked the same, as many as
    /// the pointer went past. Once a neighbour has stepped aside it stands
    /// a held tab's width further back, so the pointer has to come that far
    /// back for it to step again: a tab does not flicker between two places.
    /// </summary>
    void Step()
    {
        if (TabOf(pressed) is not { } held) return;
        int at = PlaceOf(pressed!);
        double middle = dragLeft + held.DesiredSize.Width / 2;
        var widths = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed)
                                     .Select(c => c.DesiredSize.Width).ToList();
        if (at < 0 || at >= widths.Count) return;
        double w = widths[at];
        widths.RemoveAt(at);
        int to = at;
        // The left-hand edge of the place at `to` in the row without the held tab.
        double Left(int i) => widths.Take(i).Sum();
        while (to < widths.Count && middle > Left(to) + w + widths[to] / 2) to++;
        while (to > 0 && middle < Left(to - 1) + widths[to - 1] / 2) to--;
        if (to != at) Move(at, to, done: false);
    }

    void Move(int from, int to, bool done) =>
        RaiseEvent(new TabMovedEventArgs(MovedEvent, this, from, to, done));

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (dragging) e.Handled = true;
        End(keep: true);
    }

    /// <summary>The mouse taken away mid-drag (another window, a menu):
    /// the tab is left where it is. Only the panel's own loss: the tab's,
    /// when the panel takes the mouse from it, comes up through here too.</summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (ReferenceEquals(e.OriginalSource, this)) End(keep: true);
    }

    void OnDragKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        End(keep: false);
    }

    /// <summary>The drag is over: the tab is left where it is, or put back
    /// where it began (Esc). Said once, if it was moved at all.</summary>
    void End(bool keep)
    {
        var item = pressed;
        bool was = dragging;
        pressed = null;
        dragging = false;
        if (!was || item == null) return;
        if (keys != null) keys.PreviewKeyDown -= OnDragKey;
        keys = null;
        if (TabOf(item) is { } tab) Panel.SetZIndex(tab, 0);
        if (IsMouseCaptured) ReleaseMouseCapture();
        int at = PlaceOf(item);
        if (!keep && at != began && at >= 0)
        {
            Move(at, began, done: false);
            at = began;
        }
        if (at != began && at >= 0) Move(began, at, done: true);
        InvalidateArrange();
    }

    // -------------------------------------------------------------- keyboard

    /// <summary>Ctrl+Shift+Left and Right move the tab that has the keyboard
    /// one place, and it keeps the keyboard.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (dragging || Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift)) return;
        int step = e.Key switch { Key.Left => -1, Key.Right => 1, _ => 0 };
        if (step == 0 || ChildOf(e.OriginalSource as DependencyObject) is not { } tab) return;
        e.Handled = true;
        var item = Owner?.ItemContainerGenerator.ItemFromContainer(tab);
        if (item == null || item == DependencyProperty.UnsetValue) return;
        int at = PlaceOf(item), to = at + step;
        if (at < 0 || to < 0 || to >= Owner!.Items.Count) return;
        // The keyboard is on the tab, or on its mark: it stays on that.
        bool onMark = Above<ButtonBase>(e.OriginalSource as DependencyObject, b => b is not ToggleButton) != null;
        Move(at, to, done: false);
        Move(at, to, done: true);
        UpdateLayout();
        if (TabOf(item) is { } moved &&
            (onMark ? Under<ButtonBase>(moved, b => b is not ToggleButton) : Under<ToggleButton>(moved)) is { } back)
            back.Focus();
    }

    // ------------------------------------------------------------- the tree

    /// <summary>The first <typeparamref name="T"/> at or under <paramref name="from"/>.</summary>
    static T? Under<T>(DependencyObject from, Func<T, bool>? also = null) where T : DependencyObject
    {
        if (from is T hit && (also == null || also(hit))) return hit;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++)
            if (Under(VisualTreeHelper.GetChild(from, i), also) is { } found) return found;
        return null;
    }

    /// <summary>The first <typeparamref name="T"/> at or above <paramref name="from"/>, in the panel.</summary>
    T? Above<T>(DependencyObject? from, Func<T, bool> also) where T : DependencyObject
    {
        while (from != null && from != this)
        {
            if (from is T hit && also(hit)) return hit;
            from = Up(from);
        }
        return null;
    }

    /// <summary>Up one, whether it is drawn (a visual) or only text in one.</summary>
    static DependencyObject? Up(DependencyObject from) =>
        from is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(from) : LogicalTreeHelper.GetParent(from);
}
