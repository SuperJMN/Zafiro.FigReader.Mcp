using System.Text.Json.Nodes;
using Zafiro.FigReader.Core.Extraction;
using Zafiro.FigReader.Core.Kiwi;
using Xunit;

namespace Zafiro.FigReader.Core.Tests;

public class KiwiJsonTests
{
    [Fact]
    public void Serializes_non_finite_floats_as_strings_instead_of_throwing()
    {
        var node = new KiwiObject("Transform", new Dictionary<string, object?>
        {
            ["finite"] = 1.5f,
            ["infinity"] = float.PositiveInfinity,
            ["negInfinity"] = double.NegativeInfinity,
            ["nan"] = float.NaN,
        });

        var json = KiwiJson.ToJson(node);
        // The point of the test: this must not throw.
        var serialized = KiwiJson.Serialize(json);

        Assert.Contains("Infinity", serialized);
        Assert.Contains("NaN", serialized);
        var obj = Assert.IsType<JsonObject>(json);
        Assert.Equal(1.5f, obj["finite"]!.GetValue<float>());
    }
}
