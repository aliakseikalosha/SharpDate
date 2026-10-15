// Port of the Playdate SDK's C_API/Examples/JSON: decodes test.json and logs every decoder callback.

using System.Runtime.CompilerServices;

namespace JsonExample;

public sealed class JsonGame : Game
{
#if !SHARPDATE_DEMOS // the Demos launcher starts this game itself
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new JsonGame());
#endif

    private static string TypeToName(JsonValueType type) => type switch
    {
        JsonValueType.Null => "null",
        JsonValueType.True => "true",
        JsonValueType.False => "false",
        JsonValueType.Integer => "integer",
        JsonValueType.Float => "float",
        JsonValueType.String => "string",
        JsonValueType.Array => "array",
        JsonValueType.Table => "table",
        _ => "???",
    };

    private sealed unsafe class LoggingDecoder : JsonHandler
    {
        public override void DecodeError(string error, int lineNumber)
            => Sys.Log($"decode error line {lineNumber}: {error}");

        public override void WillDecodeSublist(string name, JsonValueType type)
            => Sys.Log($"{Path} willDecodeSublist {TypeToName(type)} {name}");

        public override bool ShouldDecodeTableValueForKey(string key)
        {
            Sys.Log($"{Path} shouldDecodeTableValueForKey {key}");
            return true;
        }

        public override void DidDecodeTableValue(string key, JsonValue value)
            => Sys.Log($"{Path} didDecodeTableValue {key} {TypeToName(value.Type)}");

        public override bool ShouldDecodeArrayValueAtIndex(int position)
        {
            Sys.Log($"{Path} shouldDecodeArrayValueAtIndex {position}");
            return true;
        }

        public override void DidDecodeArrayValue(int position, JsonValue value)
            => Sys.Log($"{Path} didDecodeArrayValue {position} {TypeToName(value.Type)}");

        public override void* DidDecodeSublist(string name, JsonValueType type)
        {
            Sys.Log($"{Path} didDecodeSublist {TypeToName(type)} {name}");
            return null;
        }
    }

    protected override void Initialize()
    {
        // Same as the C example: decode the file through pd->file->read.
        PDFile file = FileSystem.Open("test.json", FileOptions.Read);
        Json.Decode(new LoggingDecoder(), file);
        file.Dispose();

        // SharpDate also offers a document API built on the same decoder.
        JsonNode root = Json.ParseFile("test.json");
        JsonNode servlets = root?["web-app"]?["servlet"];
        Sys.Log($"Parsed tree: {servlets?.Count ?? 0} servlets, first is \"{servlets?[0]?["servlet-name"]}\"");
    }

    public override bool Update() => true;
}
