using Chiaroscuro.Core.Geometry;
using Xunit;

namespace Chiaroscuro.Core.Tests.Geometry;

public class Vector3Tests
{
    [Fact]
    public void Length_ComputesEuclideanNorm()
    {
        var vector = new Vector3(3, 4, 0);

        Assert.Equal(5.0, vector.Length, precision: 9);
    }

    [Fact]
    public void Normalized_ProducesUnitLengthVectorInSameDirection()
    {
        var vector = new Vector3(0, 0, 5);

        var normalized = vector.Normalized();

        Assert.Equal(1.0, normalized.Length, precision: 9);
        Assert.Equal(new Vector3(0, 0, 1), normalized);
    }

    [Fact]
    public void Normalized_OfZeroVector_ReturnsZero()
    {
        // Guards the division-by-zero edge case explicitly handled in Vector3.Normalized().
        Assert.Equal(Vector3.Zero, Vector3.Zero.Normalized());
    }

    [Fact]
    public void Dot_OfPerpendicularVectors_IsZero()
    {
        var x = new Vector3(1, 0, 0);
        var y = new Vector3(0, 1, 0);

        Assert.Equal(0.0, x.Dot(y), precision: 9);
    }

    [Fact]
    public void AngleTo_OfIdenticalVectors_IsZero()
    {
        var vector = new Vector3(2, 3, 4);

        Assert.Equal(0.0, vector.AngleTo(vector), precision: 9);
    }

    [Fact]
    public void AngleTo_OfOpposingVectors_IsPi()
    {
        var vector = new Vector3(1, 0, 0);

        Assert.Equal(Math.PI, vector.AngleTo(-vector), precision: 9);
    }

    [Fact]
    public void AngleTo_OfPerpendicularVectors_IsHalfPi()
    {
        var x = new Vector3(1, 0, 0);
        var y = new Vector3(0, 1, 0);

        Assert.Equal(Math.PI / 2, x.AngleTo(y), precision: 9);
    }

    [Fact]
    public void ArithmeticOperators_BehaveComponentwise()
    {
        var a = new Vector3(1, 2, 3);
        var b = new Vector3(4, 5, 6);

        Assert.Equal(new Vector3(5, 7, 9), a + b);
        Assert.Equal(new Vector3(-3, -3, -3), a - b);
        Assert.Equal(new Vector3(-1, -2, -3), -a);
        Assert.Equal(new Vector3(2, 4, 6), a * 2);
        Assert.Equal(new Vector3(2, 4, 6), 2 * a);
    }
}
