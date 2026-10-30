using System.Windows;
using System.Windows.Controls;

namespace Zerg.Views;

/// <summary>The actions table and the heals table have two kinds of line: a
/// character's heading, and an action under it. The Support table has a
/// third, a person an action reached (Under), drawn as Action where there
/// is none.</summary>
public sealed class ActionRowTemplates : DataTemplateSelector
{
    public DataTemplate? Heading { get; set; }
    public DataTemplate? Action { get; set; }
    public DataTemplate? Under { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item switch
        {
            IGroupedRow { IsHeading: true } => Heading,
            IGroupedRow { IsUnder: true } when Under != null => Under,
            _ => Action,
        };
}
