using Zafiro.FigReader.Core.Kiwi;
using Xunit;

namespace Zafiro.FigReader.Core.Tests;

public class ByteBufferTests
{
    [Theory]
    [InlineData(new byte[] { 0x00 }, 0u)]
    [InlineData(new byte[] { 0x7F }, 127u)]
    [InlineData(new byte[] { 0x80, 0x01 }, 128u)]
    [InlineData(new byte[] { 0xE9, 0x04 }, 617u)]
    public void ReadVarUint_matches_kiwi_encoding(byte[] bytes, uint expected)
    {
        Assert.Equal(expected, new ByteBuffer(bytes).ReadVarUint());
    }

    [Theory]
    [InlineData(new byte[] { 0x00 }, 0)]
    [InlineData(new byte[] { 0x01 }, -1)]
    [InlineData(new byte[] { 0x02 }, 1)]
    [InlineData(new byte[] { 0x03 }, -2)]
    [InlineData(new byte[] { 0x04 }, 2)]
    public void ReadVarInt_uses_zigzag(byte[] bytes, int expected)
    {
        Assert.Equal(expected, new ByteBuffer(bytes).ReadVarInt());
    }

    [Fact]
    public void ReadVarFloat_decodes_zero_as_single_byte()
    {
        var bb = new ByteBuffer(new byte[] { 0x00 });
        Assert.Equal(0f, bb.ReadVarFloat());
        Assert.Equal(1, bb.Index);
    }

    [Theory]
    [InlineData(1.5f)]
    [InlineData(-1f)]
    [InlineData(2f)]
    [InlineData(3.14159f)]
    public void ReadVarFloat_roundtrips_known_values(float value)
    {
        Assert.Equal(value, new ByteBuffer(EncodeVarFloat(value)).ReadVarFloat());
    }

    [Fact]
    public void ReadString_reads_utf8_until_null_terminator()
    {
        var bb = new ByteBuffer(new byte[] { 0x48, 0x69, 0x00, 0x41 }); // "Hi\0A"
        Assert.Equal("Hi", bb.ReadString());
        Assert.Equal(0x41, bb.ReadByte());
    }

    private static byte[] EncodeVarFloat(float value)
    {
        var bits = BitConverter.SingleToUInt32Bits(value);
        bits = (bits >> 23) | (bits << 9);
        if ((bits & 255) == 0)
        {
            return new byte[] { 0 };
        }

        return new[] { (byte)bits, (byte)(bits >> 8), (byte)(bits >> 16), (byte)(bits >> 24) };
    }
}
