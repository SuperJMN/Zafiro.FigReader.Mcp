using System.IO.Compression;
using System.Text;
using Zafiro.FigReader.Core.Kiwi;

namespace Zafiro.FigReader.Core.Tests;

/// <summary>Creates a small fig-kiwi archive entirely from synthetic test data.</summary>
internal sealed class SyntheticFigFile : IDisposable
{
    public const string PageId = "1:2";
    public const string InstanceId = "1:50";
    public const string VectorId = "1:41";

    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), $"synthetic-{Guid.NewGuid():N}.fig");

    private static readonly string[] NativeTypes =
        ["bool", "byte", "int", "uint", "float", "string", "int64", "uint64"];

    private static readonly Dictionary<string, (string Name, string Type)[]> Schema = new()
    {
        ["Guid"] = [("sessionID", "uint"), ("localID", "uint")],
        ["Parent"] = [("guid", "Guid"), ("position", "string")],
        ["Text"] = [("characters", "string")],
        ["GuidPath"] = [("guids", "Guid[]")],
        ["Value"] = [("guidValue", "Guid")],
        ["Assignment"] = [("defID", "Guid"), ("value", "Value")],
        ["Override"] = [("guidPath", "GuidPath"), ("textData", "Text"), ("visible", "bool"),
            ("componentPropAssignments", "Assignment[]")],
        ["SymbolData"] = [("symbolID", "Guid"), ("symbolOverrides", "Override[]")],
        ["Color"] = [("r", "float"), ("g", "float"), ("b", "float"), ("a", "float")],
        ["Paint"] = [("type", "string"), ("color", "Color")],
        ["Geometry"] = [("commandsBlob", "uint"), ("windingRule", "string")],
        ["Node"] = [("guid", "Guid"), ("parentIndex", "Parent"), ("type", "string"), ("name", "string"),
            ("textData", "Text"), ("symbolData", "SymbolData"), ("fillPaints", "Paint[]"),
            ("fillGeometry", "Geometry[]")],
        ["Blob"] = [("bytes", "byte[]")],
        ["Message"] = [("nodeChanges", "Node[]"), ("blobs", "Blob[]")]
    };

    public SyntheticFigFile()
    {
        using var file = File.Create(Path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        using (var canvas = archive.CreateEntry("canvas.fig").Open())
        {
            canvas.Write(BuildCanvas());
        }

        using var metadata = new StreamWriter(archive.CreateEntry("meta.json").Open());
        metadata.Write("""{"file_name":"Synthetic fixture"}""");
    }

    public void Dispose() => File.Delete(Path);

    private static byte[] BuildCanvas()
    {
        var nodes = new[]
        {
            Node(1, "DOCUMENT", "Synthetic document"),
            Node(2, "CANVAS", "Fixture page", 1),
            Node(10, "SYMBOL", "List template", 2),
            Node(11, "INSTANCE", "First item", 10, ("symbolData", Obj("SymbolData", ("symbolID", Id(20))))),
            Node(12, "INSTANCE", "Second item", 10, ("symbolData", Obj("SymbolData", ("symbolID", Id(20))))),
            Node(20, "SYMBOL", "Item template", 2),
            Node(21, "TEXT", "Label", 20, ("textData", Obj("Text", ("characters", "Base label"))),
                ("fillPaints", new[] { Obj("Paint", ("type", "SOLID"),
                    ("color", Obj("Color", ("r", 0f), ("g", 1f), ("b", 0f), ("a", 1f)))) })),
            Node(22, "INSTANCE", "Icon", 20, ("symbolData", Obj("SymbolData", ("symbolID", Id(30))))),
            Node(30, "SYMBOL", "Circle icon", 2),
            Node(40, "SYMBOL", "Square icon", 2),
            Node(41, "VECTOR", "Curve", 40,
                ("fillGeometry", new[] { Obj("Geometry", ("commandsBlob", 0), ("windingRule", "NONZERO")) })),
            Node(50, "INSTANCE", "Example instance", 2, ("symbolData", Obj("SymbolData",
                ("symbolID", Id(10)), ("symbolOverrides", new[]
                {
                    Override([11, 21], ("textData", Obj("Text", ("characters", "First label")))),
                    Override([12, 21], ("textData", Obj("Text", ("characters", "Second label")))),
                    Override([12], ("visible", false)),
                    Override([11, 22], ("componentPropAssignments", new[]
                    {
                        Obj("Assignment", ("defID", Id(60)), ("value", Obj("Value", ("guidValue", Id(40)))))
                    }))
                }))))
        };
        var message = Obj("Message", ("nodeChanges", nodes),
            ("blobs", new[] { Obj("Blob", ("bytes", BuildVectorCommands())) }));

        using var canvas = new MemoryStream();
        using var writer = new BinaryWriter(canvas);
        writer.Write("fig-kiwi"u8);
        writer.Write(1u);
        WriteChunk(writer, Encode(WriteSchema));
        WriteChunk(writer, Encode(output => WriteObject(output, message)));
        return canvas.ToArray();
    }

    private static KiwiObject Node(int id, string type, string name, int? parent = null,
        params (string Name, object Value)[] extra)
    {
        var fields = new List<(string Name, object Value)>
        {
            ("guid", Id(id)), ("type", type), ("name", name)
        };
        if (parent is { } parentId)
        {
            fields.Add(("parentIndex", Obj("Parent", ("guid", Id(parentId)), ("position", id.ToString("D3")))));
        }

        fields.AddRange(extra);
        return Obj("Node", fields.ToArray());
    }

    private static KiwiObject Id(int localId) => Obj("Guid", ("sessionID", 1), ("localID", localId));

    private static KiwiObject Override(int[] path, params (string Name, object Value)[] values) =>
        Obj("Override", new[] { ("guidPath", (object)Obj("GuidPath", ("guids", path.Select(Id).ToArray()))) }
            .Concat(values).ToArray());

    private static KiwiObject Obj(string type, params (string Name, object Value)[] fields) =>
        new(type, fields.ToDictionary(field => field.Name, field => (object?)field.Value));

    private static byte[] Encode(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        write(writer);
        return stream.ToArray();
    }

    private static void WriteSchema(BinaryWriter writer)
    {
        var names = Schema.Keys.ToArray();
        WriteUnsigned(writer, (uint)Schema.Count);
        foreach (var (name, fields) in Schema)
        {
            WriteString(writer, name);
            writer.Write((byte)KiwiKind.Message);
            WriteUnsigned(writer, (uint)fields.Length);
            for (var i = 0; i < fields.Length; i++)
            {
                var (fieldName, fieldType) = fields[i];
                var isArray = fieldType.EndsWith("[]", StringComparison.Ordinal);
                var type = isArray ? fieldType[..^2] : fieldType;
                var native = Array.IndexOf(NativeTypes, type);
                var typeId = native >= 0 ? ~native : Array.IndexOf(names, type);
                WriteString(writer, fieldName);
                WriteUnsigned(writer, (uint)((typeId << 1) ^ (typeId >> 31)));
                writer.Write(isArray);
                WriteUnsigned(writer, (uint)(i + 1));
            }
        }
    }

    private static void WriteObject(BinaryWriter writer, KiwiObject value)
    {
        var fields = Schema[value.TypeName];
        for (var i = 0; i < fields.Length; i++)
        {
            var (name, type) = fields[i];
            if (value.Get(name) is not { } field)
            {
                continue;
            }

            WriteUnsigned(writer, (uint)(i + 1));
            WriteValue(writer, type, field);
        }

        writer.Write((byte)0);
    }

    private static void WriteValue(BinaryWriter writer, string type, object value)
    {
        if (value is byte[] bytes)
        {
            WriteUnsigned(writer, (uint)bytes.Length);
            writer.Write(bytes);
        }
        else if (value is KiwiObject[] items)
        {
            WriteUnsigned(writer, (uint)items.Length);
            foreach (var item in items)
            {
                WriteObject(writer, item);
            }
        }
        else if (type == "string")
        {
            WriteString(writer, (string)value);
        }
        else if (type == "uint")
        {
            WriteUnsigned(writer, Convert.ToUInt32(value));
        }
        else if (value is bool boolean)
        {
            writer.Write(boolean);
        }
        else if (value is float number)
        {
            if (number == 0)
            {
                writer.Write((byte)0);
            }
            else
            {
                var bits = BitConverter.SingleToUInt32Bits(number);
                writer.Write((bits << 9) | (bits >> 23));
            }
        }
        else
        {
            WriteObject(writer, (KiwiObject)value);
        }
    }

    private static void WriteUnsigned(BinaryWriter writer, uint value)
    {
        while (value >= 128)
        {
            writer.Write((byte)((value & 127) | 128));
            value >>= 7;
        }

        writer.Write((byte)value);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.UTF8.GetBytes(value));
        writer.Write((byte)0);
    }

    private static void WriteChunk(BinaryWriter writer, byte[] bytes)
    {
        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(bytes);
        }

        writer.Write((uint)compressed.Length);
        writer.Write(compressed.ToArray());
    }

    private static byte[] BuildVectorCommands() => Encode(writer =>
    {
        writer.Write((byte)1);
        writer.Write(1f);
        writer.Write(2f);
        writer.Write((byte)4);
        foreach (var coordinate in new[] { 3f, 4f, 5f, 6f, 7f, 8f })
        {
            writer.Write(coordinate);
        }

        writer.Write((byte)3);
        foreach (var coordinate in new[] { 9f, 10f, 11f, 12f })
        {
            writer.Write(coordinate);
        }

        writer.Write((byte)0);
    });
}
