namespace RedBolt.Tests.MathTests;

public class MatrixParityTests
{
    [Fact]
    public void Matrix3ExposesRbfxConstructionInspectionAndInverse()
    {
        var matrix = new Matrix3([2, 0, 0, 0, 3, 0, 0, 0, 4]);

        Assert.Equal(24.0f, matrix.Determinant);
        Assert.Equal(new Vector3f(0, 3, 0), matrix.Column(1));
        Assert.True((matrix * matrix.Inverse).ApproximatelyEquivalent(Matrix3.Identity));
        Assert.False(matrix.IsNaN);
        Assert.False(matrix.IsInf);
    }

    [Fact]
    public void AffineMatrixFactoriesAndInverseRoundTripPoint()
    {
        var matrix = new Matrix3x4(new Vector3f(1, 2, 3), Quaternion.FromEulerAngles(10, 20, 30), new Vector3f(2, 3, 4));
        var point = new Vector3f(4, 5, 6);

        Assert.True((matrix.Inverse * (matrix * point)).ApproximatelyEquivalent(point, 0.0001f));
        Assert.Equal(new Vector3f(1, 2, 3), Matrix3x4.FromTranslation(new Vector3f(1, 2, 3)).Translation);
        Assert.Equal(new Vector3f(2, 2, 2), Matrix3x4.FromScale(2).Scale);
    }

    [Fact]
    public void BulkTransposeMatchesIndividualTranspose()
    {
        var source = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var destination = new float[9];

        Matrix3.BulkTranspose(destination, source, 1);

        Assert.Equal(new float[] { 1, 4, 7, 2, 5, 8, 3, 6, 9 }, destination);
    }

    [Fact]
    public void Matrix4DeterminantAndInverseMatchRbfxContract()
    {
        var matrix = new Matrix4(
            2, 0, 0, 1,
            0, 3, 0, 2,
            0, 0, 4, 3,
            0, 0, 0, 1);

        Assert.Equal(24.0f, matrix.Determinant);
        Assert.True((matrix * matrix.Inverse).ApproximatelyEquivalent(Matrix4.Identity, 0.0001f));
        Assert.Equal(matrix[2, 1], matrix.Element(2, 1));
    }
}
