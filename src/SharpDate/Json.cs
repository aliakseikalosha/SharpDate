using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// Streaming JSON decoding callbacks, mirroring the C <c>json_decoder</c>. Override the methods you
    /// need and pass the handler to <see cref="Json.Decode(JsonHandler, string)"/>.
    /// </summary>
    public abstract unsafe class JsonHandler
    {
        internal JsonDecoder* Decoder;

        /// <summary>Current position in the document, e.g. "web-app.servlet[1].init-param".</summary>
        public string Path => Decoder == null ? null : Utf8.ToString(Decoder->path);

        public virtual void DecodeError(string error, int lineNumber) { }
        public virtual void WillDecodeSublist(string name, JsonValueType type) { }
        public virtual bool ShouldDecodeTableValueForKey(string key) => true;
        public virtual void DidDecodeTableValue(string key, JsonValue value) { }
        public virtual bool ShouldDecodeArrayValueAtIndex(int position) => true;

        /// <summary>Array element (positions start at 1). Position 0 is a bare value at the root of the document.</summary>
        public virtual void DidDecodeArrayValue(int position, JsonValue value) { }

        /// <summary>
        /// End of a table or array. The returned pointer is handed to the parent as the sublist's value
        /// (<see cref="JsonValueData.TableValue"/> / <see cref="JsonValueData.ArrayValue"/>).
        /// </summary>
        public virtual void* DidDecodeSublist(string name, JsonValueType type) => null;

        /// <summary>Set to true to receive the current subtree as a raw string instead of decoding it.</summary>
        public bool ReturnString
        {
            get => Decoder != null && Decoder->returnString != 0;
            set
            {
                if (Decoder != null)
                    Decoder->returnString = value ? 1 : 0;
            }
        }
    }

    /// <summary>A decoded JSON value: null, bool, number, string, array (List) or table (Dictionary).</summary>
    public sealed class JsonNode
    {
        public JsonValueType Type { get; }
        public int IntValue { get; }
        public float FloatValue { get; }
        public string StringValue { get; }
        public List<JsonNode> Array { get; }
        public Dictionary<string, JsonNode> Table { get; }

        /// <summary>Table keys in document order.</summary>
        public List<string> Keys { get; }

        private JsonNode(JsonValueType type, int i = 0, float f = 0, string s = null)
        {
            Type = type;
            IntValue = i;
            FloatValue = type == JsonValueType.Integer ? i : f;
            StringValue = s;
            if (type == JsonValueType.Array)
                Array = new List<JsonNode>();
            if (type == JsonValueType.Table)
            {
                Table = new Dictionary<string, JsonNode>();
                Keys = new List<string>();
            }
        }

        internal static JsonNode Container(JsonValueType type) => new JsonNode(type);

        internal static unsafe JsonNode FromValue(JsonValue value) => value.Type switch
        {
            JsonValueType.Integer => new JsonNode(JsonValueType.Integer, value.Data.IntValue),
            JsonValueType.Float => new JsonNode(JsonValueType.Float, (int)value.Data.FloatValue, value.Data.FloatValue),
            JsonValueType.String => new JsonNode(JsonValueType.String, s: Utf8.ToString(value.Data.StringValue)),
            JsonValueType.True => new JsonNode(JsonValueType.True, 1),
            JsonValueType.False => new JsonNode(JsonValueType.False),
            _ => new JsonNode(JsonValueType.Null),
        };

        public bool IsNull => Type == JsonValueType.Null;
        public bool BoolValue => Type == JsonValueType.True;
        public int Count => Array?.Count ?? Table?.Count ?? 0;

        public JsonNode this[int index] => Array != null && (uint)index < (uint)Array.Count ? Array[index] : null;

        public JsonNode this[string key] => Table != null && Table.TryGetValue(key, out JsonNode node) ? node : null;

        public override string ToString() => Type switch
        {
            JsonValueType.Null => "null",
            JsonValueType.True => "true",
            JsonValueType.False => "false",
            JsonValueType.Integer => IntValue.ToString(),
            JsonValueType.Float => FloatValue.ToString(),
            JsonValueType.String => StringValue,
            JsonValueType.Array => "[" + Count + " items]",
            _ => "{" + Count + " keys}",
        };
    }

    /// <summary>Wraps <c>pd->json</c>.</summary>
    public static unsafe class Json
    {
        /// <summary>Decodes a JSON string with streaming callbacks. Returns false on error.</summary>
        public static bool Decode(JsonHandler handler, string json)
        {
            JsonDecoder* decoder = CreateDecoder(handler);
            JsonValue result;
            int ok = PD.Json->decodeString(decoder, Utf8.Arg(json), &result);
            FreeDecoder(handler, decoder);
            return ok != 0;
        }

        /// <summary>Decodes a JSON file (read through <c>pd->file</c>) with streaming callbacks.</summary>
        public static bool DecodeFile(JsonHandler handler, string path)
        {
            PDFile file = FileSystem.Open(path, FileOptions.Read | FileOptions.ReadData);
            if (file.IsNull)
                return false;
            bool ok = Decode(handler, file);
            file.Dispose();
            return ok;
        }

        /// <summary>Decodes from an open file, as the C example does with <c>pd->file->read</c> as the reader.</summary>
        public static bool Decode(JsonHandler handler, PDFile file)
        {
            JsonDecoder* decoder = CreateDecoder(handler);
            JsonReader reader;
            reader.read = (delegate* unmanaged[Cdecl]<void*, byte*, int, int>)PD.File->read;
            reader.userdata = file.Handle;
            JsonValue result;
            int ok = PD.Json->decode(decoder, reader, &result);
            FreeDecoder(handler, decoder);
            return ok != 0;
        }

        /// <summary>Parses a JSON string into a <see cref="JsonNode"/> tree (null on error).</summary>
        public static JsonNode Parse(string json)
        {
            var builder = new TreeBuilder();
            return Decode(builder, json) && builder.Error == null ? builder.Root : null;
        }

        /// <summary>Parses a JSON file into a <see cref="JsonNode"/> tree (null on error).</summary>
        public static JsonNode ParseFile(string path)
        {
            var builder = new TreeBuilder();
            return DecodeFile(builder, path) && builder.Error == null ? builder.Root : null;
        }

        private static JsonDecoder* CreateDecoder(JsonHandler handler)
        {
            JsonDecoder* decoder = (JsonDecoder*)NativeMemory.AllocZeroed((nuint)sizeof(JsonDecoder));
            decoder->decodeError = &DecodeErrorCallback;
            decoder->willDecodeSublist = &WillDecodeSublistCallback;
            decoder->shouldDecodeTableValueForKey = &ShouldDecodeTableValueForKeyCallback;
            decoder->didDecodeTableValue = &DidDecodeTableValueCallback;
            decoder->shouldDecodeArrayValueAtIndex = &ShouldDecodeArrayValueAtIndexCallback;
            decoder->didDecodeArrayValue = &DidDecodeArrayValueCallback;
            decoder->didDecodeSublist = &DidDecodeSublistCallback;
            decoder->userdata = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(handler));
            handler.Decoder = decoder;
            return decoder;
        }

        private static void FreeDecoder(JsonHandler handler, JsonDecoder* decoder)
        {
            // decoder is our allocation; the handler may point at the runtime's copy by now.
            GCHandle.FromIntPtr((IntPtr)decoder->userdata).Free();
            handler.Decoder = null;
            NativeMemory.Free(decoder);
        }

        // The runtime passes callbacks its own copy of the decoder (whose path it keeps updated).
        private static JsonHandler HandlerOf(JsonDecoder* decoder)
        {
            var handler = (JsonHandler)GCHandle.FromIntPtr((IntPtr)decoder->userdata).Target;
            handler.Decoder = decoder;
            return handler;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void DecodeErrorCallback(JsonDecoder* decoder, byte* error, int line) => HandlerOf(decoder).DecodeError(Utf8.ToString(error), line);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void WillDecodeSublistCallback(JsonDecoder* decoder, byte* name, JsonValueType type) => HandlerOf(decoder).WillDecodeSublist(Utf8.ToString(name), type);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int ShouldDecodeTableValueForKeyCallback(JsonDecoder* decoder, byte* key) => HandlerOf(decoder).ShouldDecodeTableValueForKey(Utf8.ToString(key)) ? 1 : 0;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void DidDecodeTableValueCallback(JsonDecoder* decoder, byte* key, JsonValue value) => HandlerOf(decoder).DidDecodeTableValue(Utf8.ToString(key), value);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int ShouldDecodeArrayValueAtIndexCallback(JsonDecoder* decoder, int pos) => HandlerOf(decoder).ShouldDecodeArrayValueAtIndex(pos) ? 1 : 0;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void DidDecodeArrayValueCallback(JsonDecoder* decoder, int pos, JsonValue value) => HandlerOf(decoder).DidDecodeArrayValue(pos, value);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void* DidDecodeSublistCallback(JsonDecoder* decoder, byte* name, JsonValueType type) => HandlerOf(decoder).DidDecodeSublist(Utf8.ToString(name), type);

        // Builds a JsonNode tree. Containers are handed between callbacks as indexes into _nodes.
        private sealed class TreeBuilder : JsonHandler
        {
            private readonly List<JsonNode> _stack = new List<JsonNode>();
            private readonly List<JsonNode> _finished = new List<JsonNode>();
            public JsonNode Root;
            public string Error;

            public override void DecodeError(string error, int lineNumber) => Error = error;

            public override void WillDecodeSublist(string name, JsonValueType type) => _stack.Add(JsonNode.Container(type));

            public override void* DidDecodeSublist(string name, JsonValueType type)
            {
                JsonNode node = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);
                _finished.Add(node);
                if (_stack.Count == 0)
                    Root = node;
                return (void*)_finished.Count; // 1-based index, never null
            }

            private JsonNode Resolve(JsonValue value)
            {
                if (value.Type == JsonValueType.Array || value.Type == JsonValueType.Table)
                {
                    int index = (int)(nint)value.Data.TableValue;
                    return index > 0 && index <= _finished.Count ? _finished[index - 1] : JsonNode.Container(value.Type);
                }
                return JsonNode.FromValue(value);
            }

            public override void DidDecodeTableValue(string key, JsonValue value)
            {
                JsonNode parent = _stack[_stack.Count - 1];
                if (!parent.Table.ContainsKey(key))
                    parent.Keys.Add(key);
                parent.Table[key] = Resolve(value);
            }

            public override void DidDecodeArrayValue(int position, JsonValue value)
            {
                if (position == 0 || _stack.Count == 0)
                {
                    Root = Resolve(value);
                    return;
                }
                _stack[_stack.Count - 1].Array.Add(Resolve(value));
            }
        }
    }

    /// <summary>Writes JSON through <c>pd->json->initEncoder</c> into a string.</summary>
    public sealed unsafe class JsonWriter : IDisposable
    {
        private JsonEncoder* _encoder;
        private GCHandle _self;
        private readonly System.Text.StringBuilder _output = new System.Text.StringBuilder();

        public JsonWriter(bool pretty = false)
        {
            _encoder = (JsonEncoder*)NativeMemory.AllocZeroed((nuint)sizeof(JsonEncoder));
            _self = GCHandle.Alloc(this);
            PD.Json->initEncoder(_encoder, &WriteCallback, (void*)GCHandle.ToIntPtr(_self), pretty ? 1 : 0);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void WriteCallback(void* userdata, byte* str, int len)
        {
            var writer = (JsonWriter)GCHandle.FromIntPtr((IntPtr)userdata).Target;
            writer._output.Append(Utf8.ToString(str, len));
        }

        public JsonEncoder* Encoder => _encoder;

        public void StartArray() => _encoder->startArray(_encoder);
        public void AddArrayMember() => _encoder->addArrayMember(_encoder);
        public void EndArray() => _encoder->endArray(_encoder);
        public void StartTable() => _encoder->startTable(_encoder);

        public void AddTableMember(string name)
        {
            byte* p = Utf8.Arg(name);
            _encoder->addTableMember(_encoder, p, System.Text.Encoding.UTF8.GetByteCount(name));
        }

        public void EndTable() => _encoder->endTable(_encoder);
        public void WriteNull() => _encoder->writeNull(_encoder);
        public void WriteBool(bool value)
        {
            if (value)
                _encoder->writeTrue(_encoder);
            else
                _encoder->writeFalse(_encoder);
        }

        public void WriteInt(int value) => _encoder->writeInt(_encoder, value);
        public void WriteDouble(double value) => _encoder->writeDouble(_encoder, value);

        public void WriteString(string value)
        {
            byte* p = Utf8.Arg(value);
            _encoder->writeString(_encoder, p, System.Text.Encoding.UTF8.GetByteCount(value));
        }

        public override string ToString() => _output.ToString();

        public void Dispose()
        {
            if (_encoder == null)
                return;
            NativeMemory.Free(_encoder);
            _encoder = null;
            _self.Free();
        }
    }
}
