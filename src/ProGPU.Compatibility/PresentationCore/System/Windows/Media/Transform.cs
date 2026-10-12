using System;
using System.Numerics;
using ProGPU.Wpf.Interop;

namespace System.Windows.Media;

public abstract class Transform : IPortableTransformMatrixSource
{
    public static Transform Identity { get; } = new IdentityTransform();

    public abstract Matrix4x4 Value { get; }

    bool IPortableTransformMatrixSource.TryGetPortableTransformMatrix(out PortableMatrix3x2 matrix)
    {
        var value = Value;
        if (!IsAffine2D(value)
            || !float.IsFinite(value.M11)
            || !float.IsFinite(value.M12)
            || !float.IsFinite(value.M21)
            || !float.IsFinite(value.M22)
            || !float.IsFinite(value.M41)
            || !float.IsFinite(value.M42))
        {
            matrix = default;
            return false;
        }

        matrix = new PortableMatrix3x2(
            value.M11,
            value.M12,
            value.M21,
            value.M22,
            value.M41,
            value.M42);
        return true;
    }

    private static bool IsAffine2D(Matrix4x4 value)
    {
        return NearlyEqual(value.M13, 0)
            && NearlyEqual(value.M14, 0)
            && NearlyEqual(value.M23, 0)
            && NearlyEqual(value.M24, 0)
            && NearlyEqual(value.M31, 0)
            && NearlyEqual(value.M32, 0)
            && NearlyEqual(value.M33, 1)
            && NearlyEqual(value.M34, 0)
            && NearlyEqual(value.M43, 0)
            && NearlyEqual(value.M44, 1);
    }

    private static bool NearlyEqual(float left, float right)
    {
        return Math.Abs(left - right) <= 0.00001f;
    }

    private sealed class IdentityTransform : Transform
    {
        public override Matrix4x4 Value => Matrix4x4.Identity;
    }
}

public struct Matrix
{
    private const byte M11Set = 1 << 0;
    private const byte M12Set = 1 << 1;
    private const byte M21Set = 1 << 2;
    private const byte M22Set = 1 << 3;
    private const byte OffsetXSet = 1 << 4;
    private const byte OffsetYSet = 1 << 5;

    private byte _setFields;
    private double _m11;
    private double _m12;
    private double _m21;
    private double _m22;
    private double _offsetX;
    private double _offsetY;

    public double M11
    {
        readonly get => (_setFields & M11Set) != 0 ? _m11 : 1;
        set
        {
            _m11 = value;
            _setFields |= M11Set;
        }
    }

    public double M12
    {
        readonly get => (_setFields & M12Set) != 0 ? _m12 : 0;
        set
        {
            _m12 = value;
            _setFields |= M12Set;
        }
    }

    public double M21
    {
        readonly get => (_setFields & M21Set) != 0 ? _m21 : 0;
        set
        {
            _m21 = value;
            _setFields |= M21Set;
        }
    }

    public double M22
    {
        readonly get => (_setFields & M22Set) != 0 ? _m22 : 1;
        set
        {
            _m22 = value;
            _setFields |= M22Set;
        }
    }

    public double OffsetX
    {
        readonly get => (_setFields & OffsetXSet) != 0 ? _offsetX : 0;
        set
        {
            _offsetX = value;
            _setFields |= OffsetXSet;
        }
    }

    public double OffsetY
    {
        readonly get => (_setFields & OffsetYSet) != 0 ? _offsetY : 0;
        set
        {
            _offsetY = value;
            _setFields |= OffsetYSet;
        }
    }

    public static Matrix Identity => new Matrix { M11 = 1, M22 = 1 };

    public void Translate(double offsetX, double offsetY)
    {
        OffsetX += offsetX;
        OffsetY += offsetY;
    }

    public void Rotate(double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180.0;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);

        double m11 = M11 * cos - M12 * sin;
        double m12 = M11 * sin + M12 * cos;
        double m21 = M21 * cos - M22 * sin;
        double m22 = M21 * sin + M22 * cos;
        double offsetX = OffsetX * cos - OffsetY * sin;
        double offsetY = OffsetX * sin + OffsetY * cos;

        M11 = m11;
        M12 = m12;
        M21 = m21;
        M22 = m22;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public void Scale(double scaleX, double scaleY)
    {
        M11 *= scaleX;
        M12 *= scaleY;
        M21 *= scaleX;
        M22 *= scaleY;
        OffsetX *= scaleX;
        OffsetY *= scaleY;
    }
}
