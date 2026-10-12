namespace System.Windows;

public abstract class Freezable
{
    public Freezable Clone()
    {
        return CreateInstanceCore();
    }

    protected virtual Freezable CreateInstanceCore()
    {
        return (Freezable)MemberwiseClone();
    }
}
