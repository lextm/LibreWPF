namespace ProGPU.Wpf.Interop;

/// <summary>
/// Describes the system appearance reported by a platform-owned theme source.
/// </summary>
public enum PortableSystemTheme
{
    Unknown = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>
/// Supplies system appearance state without coupling retained UI code to a
/// platform-specific registry, notification window, or application framework.
/// </summary>
public interface IPortableSystemThemeSource
{
    PortableWpfServiceKey ServiceKey { get; }

    bool TryGetSystemTheme(out PortableSystemTheme theme);

    event EventHandler? SystemThemeChanged;
}
