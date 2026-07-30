using Zafiro.FigReader.Core.Geometry;
using Xunit;

namespace Zafiro.FigReader.Core.Tests;

public class VectorPathDecoderTests
{
    private static void AppendFloat(List<byte> buffer, float value) =>
        buffer.AddRange(BitConverter.GetBytes(value));

    [Fact]
    public void Decodes_move_line_close_into_svg_path()
    {
        // A 16x16 rectangle: move(0,0) line(16,0) line(16,16) line(0,16) close.
        var bytes = new List<byte>();
        bytes.Add(1); AppendFloat(bytes, 0); AppendFloat(bytes, 0);
        bytes.Add(2); AppendFloat(bytes, 16); AppendFloat(bytes, 0);
        bytes.Add(2); AppendFloat(bytes, 16); AppendFloat(bytes, 16);
        bytes.Add(2); AppendFloat(bytes, 0); AppendFloat(bytes, 16);
        bytes.Add(0);

        var result = VectorPathDecoder.DecodeBlob(bytes.ToArray(), "NONZERO");

        Assert.Equal("F1", result.FillRule);
        Assert.Equal("F1 M0,0 L16,0 L16,16 L0,16 Z", result.Svg);
        Assert.Equal(0, result.MinX);
        Assert.Equal(0, result.MinY);
        Assert.Equal(16, result.MaxX);
        Assert.Equal(16, result.MaxY);
    }

    [Fact]
    public void Decodes_cubic_and_quadratic_ops()
    {
        var bytes = new List<byte>();
        bytes.Add(1); AppendFloat(bytes, 1); AppendFloat(bytes, 2);
        bytes.Add(4); // cubic
        AppendFloat(bytes, 3); AppendFloat(bytes, 4);
        AppendFloat(bytes, 5); AppendFloat(bytes, 6);
        AppendFloat(bytes, 7); AppendFloat(bytes, 8);
        bytes.Add(3); // quadratic
        AppendFloat(bytes, 9); AppendFloat(bytes, 10);
        AppendFloat(bytes, 11); AppendFloat(bytes, 12);

        var result = VectorPathDecoder.DecodeBlob(bytes.ToArray(), null);

        Assert.Equal("F1 M1,2 C3,4 5,6 7,8 Q9,10 11,12", result.Svg);
    }

    [Fact]
    public void Odd_winding_rule_maps_to_even_odd_prefix()
    {
        var bytes = new List<byte> { 1 };
        AppendFloat(bytes, 0);
        AppendFloat(bytes, 0);

        var result = VectorPathDecoder.DecodeBlob(bytes.ToArray(), "ODD");

        Assert.Equal("F0", result.FillRule);
        Assert.StartsWith("F0 ", result.Svg);
    }

    [Fact]
    public void Truncated_blob_stops_without_throwing()
    {
        // Move op that claims coordinates the buffer does not contain.
        var bytes = new byte[] { 1, 0x00, 0x00 };

        var result = VectorPathDecoder.DecodeBlob(bytes, "NONZERO");

        Assert.Equal("F1", result.Svg.Trim());
    }
}
