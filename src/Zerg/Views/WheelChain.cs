using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zerg.Views;

/// <summary>
/// Lets the mouse wheel through a scrolling area that has nowhere left to go.
/// A scroll viewer swallows the wheel even when it cannot scroll that way, so
/// a table inside the page would stop the page under the pointer. With
/// <c>WheelChain.On="True"</c> it hands the wheel to whatever it sits in once
/// it is at its end, or when it only scrolls sideways.
///
/// <para>The preview runs outermost first, so a sideways scroller round a
/// list that scrolls up and down (a wide table) is asked before the list is.
/// It lets the wheel go on down to the list while any scroller between it
/// and the pointer can still move that way.</para>
/// </summary>
public static class WheelChain
{
    public static readonly DependencyProperty OnProperty = DependencyProperty.RegisterAttached("On", typeof(bool),
        typeof(WheelChain), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not UIElement el) return;
            if ((bool)e.NewValue) el.PreviewMouseWheel += OnWheel;
            else el.PreviewMouseWheel -= OnWheel;
        }));

    public static bool GetOn(DependencyObject d) => (bool)d.GetValue(OnProperty);
    public static void SetOn(DependencyObject d, bool value) => d.SetValue(OnProperty, value);

    static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || FindViewer((DependencyObject)sender) is not { } viewer) return;
        if (!Stuck(viewer, e.Delta) || InnerMoves(e.OriginalSource as DependencyObject, viewer, (DependencyObject)sender, e.Delta)
            || System.Windows.Media.VisualTreeHelper.GetParent(viewer) is not UIElement parent) return;

        e.Handled = true;
        parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender,
        });
    }

    /// <summary>The viewer cannot move the way the wheel turned.</summary>
    static bool Stuck(ScrollViewer viewer, int delta) =>
        viewer.ScrollableHeight <= 0
        || (delta > 0 && viewer.VerticalOffset <= 0)
        || (delta < 0 && viewer.VerticalOffset >= viewer.ScrollableHeight);

    /// <summary>A scroll viewer between the pointer and <paramref name="outer"/>
    /// (or the element holding it, <paramref name="holder"/>)
    /// can still move the way the wheel turned.</summary>
    static bool InnerMoves(DependencyObject? from, ScrollViewer outer, DependencyObject holder, int delta)
    {
        for (var d = from; d != null && d != outer && d != holder;
             d = d is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                 ? System.Windows.Media.VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is ScrollViewer inner && !Stuck(inner, delta)) return true;
        return false;
    }

    /// <summary>The element itself, or the scroll viewer inside its template
    /// (a virtualised list keeps its own).</summary>
    static ScrollViewer? FindViewer(DependencyObject d)
    {
        if (d is ScrollViewer s) return s;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(d); i++)
            if (FindViewer(System.Windows.Media.VisualTreeHelper.GetChild(d, i)) is { } inner) return inner;
        return null;
    }
}
