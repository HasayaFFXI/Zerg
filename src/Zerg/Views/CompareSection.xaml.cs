using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Zerg.Views;

/// <summary>
/// The Compare section's bands: the runs band (each side's chip, which opens
/// its picker and is renamed in place), the band of eight figures, and, while
/// B is empty, the ways to fill it. The panes under them are
/// <see cref="ComparePanes"/>. Its data context is the
/// <see cref="CompareViewModel"/>.
///
/// <para>It is one of the things that stand above a section's panes in the
/// main window's page (<see cref="PageStack"/>), so it is as tall as it asks
/// to be and the panes have what is left.</para>
/// </summary>
public partial class CompareSection : UserControl
{
    public static readonly DependencyProperty BandColumnsProperty =
        DependencyProperty.Register(nameof(BandColumns), typeof(int), typeof(CompareSection), new PropertyMetadata(8));

    public CompareSection()
    {
        InitializeComponent();
        SizeChanged += (_, e) => BandColumns = e.NewSize.Width >= 700 ? 8 : 2;
    }

    /// <summary>The band's figures across: eight, or two in a narrow window.</summary>
    public int BandColumns { get => (int)GetValue(BandColumnsProperty); set => SetValue(BandColumnsProperty, value); }

    // ------------------------------------------------------ dropping a file

    static RunSide? SideOf(object sender) => (sender as FrameworkElement)?.DataContext as RunSide;

    internal static string? FileIn(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files
            ? files[0] : null;

    /// <summary>Only a file can be dropped on a chip; anything else is turned away.</summary>
    void OnSlotDrag(object sender, DragEventArgs e)
    {
        bool file = FileIn(e) != null;
        e.Effects = file ? DragDropEffects.Copy : DragDropEffects.None;
        if (SideOf(sender) is { } side) side.IsOver = file;
        e.Handled = true;
    }

    void OnSlotLeave(object sender, DragEventArgs e)
    {
        if (SideOf(sender) is { } side) side.IsOver = false;
    }

    void OnSlotDrop(object sender, DragEventArgs e)
    {
        if (SideOf(sender) is not { } side) return;
        side.IsOver = false;
        if (FileIn(e) is { } path) side.Take(path);
        e.Handled = true;
    }

    // ------------------------------------------------------ renaming in place

    void OnNameShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox box || !(bool)e.NewValue) return;
        Dispatcher.BeginInvoke(() =>
        {
            box.Focus();
            box.SelectAll();
        });
    }

    void OnNameKey(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape) || SideOf(sender) is not { } side) return;
        side.DoneCommand.Execute(null);
        e.Handled = true;
    }

    void OnNameLeft(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (SideOf(sender) is { Renaming: true } side) side.DoneCommand.Execute(null);
    }

    // ------------------------------------------------------------ the picker

    /// <summary>The picker opens on its Name field, the name selected.</summary>
    void OnPickerOpened(object? sender, EventArgs e)
    {
        if (sender is not Popup { Child: FrameworkElement child }) return;
        Dispatcher.BeginInvoke(() =>
        {
            if ((child.FindName("PickName") as TextBox ?? Find<TextBox>(child)) is { } box)
            {
                box.Focus();
                box.SelectAll();
            }
        });
    }

    static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            if (Find<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    /// <summary>Esc shuts the picker; Enter in the name keeps it and shuts
    /// it; Down from the name goes to the first source.</summary>
    void OnPickerKey(object sender, KeyEventArgs e)
    {
        if (SideOf(sender) is not { } side) return;
        if (e.Key == Key.Escape || e.Key == Key.Enter && e.OriginalSource is TextBox)
        {
            side.Picking = false;
            side.DoneCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && e.OriginalSource is TextBox box)
        {
            box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
    }
}
