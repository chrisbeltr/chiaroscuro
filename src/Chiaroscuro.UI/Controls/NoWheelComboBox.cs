using Avalonia.Controls;
using Avalonia.Input;

namespace Chiaroscuro.UI.Controls;

/// <summary>
/// A <see cref="ComboBox"/> that ignores mouse wheel input instead of using it to change
/// <see cref="SelectingItemsControl.SelectedItem"/>, so scrolling over it bubbles up to a
/// containing <see cref="ScrollViewer"/> instead.
/// </summary>
public class NoWheelComboBox : ComboBox
{
    // Without this, Avalonia looks up the implicit ControlTheme using this subclass's own
    // type instead of ComboBox, finds none, and renders with no template at all.
    protected override Type StyleKeyOverride => typeof(ComboBox);

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // Deliberately do not call base and do not mark the event handled, so the wheel
        // event bubbles up to any containing ScrollViewer instead of changing the selection.
    }
}
