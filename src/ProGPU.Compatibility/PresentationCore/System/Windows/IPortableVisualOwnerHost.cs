namespace System.Windows;

public interface IPortableVisualOwnerHost
{
    object? PortableVisualParent { get; }
    bool IsPortableInputEnabled { get; }
    PortableVisualOwnerKind PortableVisualOwnerKind { get; }

    /// <summary>
    /// Whether the pointer can hit this owner at all, as opposed to passing straight through it
    /// (WPF's UIElement.IsHitTestVisible). Deliberately narrower than
    /// <see cref="IsPortableInputEnabled"/>, which also folds in IsEnabled and IsVisible: a
    /// DISABLED element is still hit-testable in WPF and must keep swallowing input rather than
    /// letting it reach what sits behind it, whereas IsHitTestVisible=false can never be a pointer
    /// target and must not even be reported as a candidate - owners come back in a fixed-size
    /// buffer, so decorative visuals reporting themselves here starve it and truncate away the real
    /// targets behind them. Defaults to IsPortableInputEnabled so existing implementors keep working.
    /// </summary>
    bool IsPortableHitTestVisible => IsPortableInputEnabled;
}
