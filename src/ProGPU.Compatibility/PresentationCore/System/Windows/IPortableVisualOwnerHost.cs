namespace System.Windows;

public interface IPortableVisualOwnerHost
{
    object? PortableVisualParent { get; }
    bool IsPortableInputEnabled { get; }
    PortableVisualOwnerKind PortableVisualOwnerKind { get; }
}
