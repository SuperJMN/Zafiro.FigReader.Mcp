using Zafiro.FigReader.Core.Kiwi;
using Xunit;

namespace Zafiro.FigReader.Core.Tests;

public class KiwiDecoderTests
{
    // Generated with the kiwi binary format (see project notes). Schema:
    //   struct Color { float r; float g; float b; }
    //   message Point { float x = 1; float y = 2; }
    //   message Doc { string title = 1; Point[] points = 2; bool flag = 3; string tag = 4; }
    private const string SchemaB64 =
        "A0NvbG9yAAEDcgAJAABnAAkAAGIACQAAUG9pbnQAAgJ4AAkAAXkACQACRG9jAAIEdGl0bGUACwABcG9pbnRzAAIBAmZsYWcAAQADdGFnAAsABA==";

    // Doc { title = "Hi", points = [ {x:1.5,y:2}, {x:0,y:-1} ], flag = true }  (tag omitted)
    private const string MessageB64 = "AUhpAAICAX8AAIACgAAAAAABAAJ/AQAAAAMBAA==";

    private static KiwiSchema Schema() => KiwiSchema.Decode(Convert.FromBase64String(SchemaB64));

    [Fact]
    public void Decodes_schema_definitions_and_kinds()
    {
        var schema = Schema();

        Assert.Equal(3, schema.Definitions.Count);
        Assert.Equal(KiwiKind.Struct, schema["Color"].Kind);
        Assert.Equal(KiwiKind.Message, schema["Point"].Kind);
        Assert.Equal(KiwiKind.Message, schema["Doc"].Kind);
    }

    [Fact]
    public void Resolves_native_and_reference_field_types()
    {
        var schema = Schema();

        var doc = schema["Doc"];
        Assert.Equal("string", doc.FieldById(1)!.Type);
        var points = doc.FieldById(2)!;
        Assert.Equal("Point", points.Type);
        Assert.True(points.IsArray);
        Assert.Equal("bool", doc.FieldById(3)!.Type);
    }

    [Fact]
    public void Decodes_message_with_scalars_arrays_and_nested_messages()
    {
        var schema = Schema();
        var decoder = new KiwiMessageDecoder(schema);

        var doc = decoder.Decode(Convert.FromBase64String(MessageB64), "Doc");

        Assert.Equal("Doc", doc.TypeName);
        Assert.Equal("Hi", doc.GetString("title"));
        Assert.Equal(true, doc.GetBool("flag"));
        Assert.False(doc.Has("tag")); // optional message field that was not written

        var points = doc.GetList("points");
        Assert.NotNull(points);
        Assert.Equal(2, points!.Count);

        var p0 = Assert.IsType<KiwiObject>(points[0]);
        Assert.Equal(1.5, p0.GetNumber("x"));
        Assert.Equal(2.0, p0.GetNumber("y"));

        var p1 = Assert.IsType<KiwiObject>(points[1]);
        Assert.Equal(0.0, p1.GetNumber("x"));
        Assert.Equal(-1.0, p1.GetNumber("y"));
    }
}
