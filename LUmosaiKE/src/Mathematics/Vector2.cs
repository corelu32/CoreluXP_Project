using System.Numerics;
using System.Runtime.CompilerServices;

namespace LUmosaiKE.Mathematics;

/// <summary>
///   Immutable 2D vector.
/// </summary>
public readonly record struct Vector2<T>(T X, T Y)
    : IEquatable<Vector2<T>> where T : struct, INumber<T>
{
    /// <summary>
    ///   The zero vector.
    /// </summary>
    public static readonly Vector2<T> Zero = new(T.Zero, T.Zero);

    /// <summary>
    ///   The one vector.
    /// </summary>
    public static readonly Vector2<T> One = new(T.One, T.One);

    /// <summary>
    ///   Calculates the dot product of two vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Dot(in Vector2<T> a, in Vector2<T> b) => 
        (a.X * b.X) + (a.Y * b.Y);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator +(in Vector2<T> a, in Vector2<T> b) => 
        new(a.X + b.X, a.Y + b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator -(in Vector2<T> a, in Vector2<T> b) => 
        new(a.X - b.X, a.Y - b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator -(in Vector2<T> a) => 
        new(-a.X, -a.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator *(in Vector2<T> a, T scalar) => 
        new(a.X * scalar, a.Y * scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator *(T scalar, in Vector2<T> a) => 
        new(scalar * a.X, scalar * a.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator /(in Vector2<T> a, T scalar) => 
        new(a.X / scalar, a.Y / scalar);
}