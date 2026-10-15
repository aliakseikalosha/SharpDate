# Files and JSON

`FileSystem`, `PDFile`, `Json`, `JsonNode`, `JsonHandler`, `JsonValue`, `JsonWriter`. Full member
lists: [File.md](../api/File.md), [Json.md](../api/Json.md).

## Where files live

Each game sees two folders:

| Folder | Access | Contains |
|---|---|---|
| the pdx | read-only | everything pdc copied from your `Source` folder |
| the data folder | read/write | your saves; `Data/<bundleID>/` on the device, `<SDK>/Disk/Data/<bundleID>/` for the Simulator |

Paths are relative (`"save.json"`, `"levels/3.bin"`). `ReadAllBytes`/`ReadAllText` look in the data
folder first, then in the pdx, so a file in the data folder overrides one shipped with the game.
Writing always goes to the data folder. At most 64 files can be open at once.

## FileSystem

| Member | Meaning | Lua |
|---|---|---|
| `ReadAllText(path)` / `ReadAllBytes(path)` | whole file, or null if missing | `file:read` loops |
| `WriteAllText(path, text)` / `WriteAllBytes(path, data)` | create or replace | `file:write` |
| `Exists(path)`, `Stat(path, out stat)` | existence, size, folder flag, modification time | `playdate.file.exists`, `getSize`, `modtime` |
| `ListFiles(path)` | names in a folder (folders end with `/`) | `playdate.file.listFiles` |
| `Mkdir(path)` | create a folder (parents must exist) | `playdate.file.mkdir` |
| `Rename(from, to)` | rename / move, replacing the target | `playdate.file.rename` |
| `Unlink(path, recursive)` | delete a file or a folder tree | `playdate.file.delete` |
| `Open(path, options)` | an open `PDFile` | `playdate.file.open` |
| `LastError` | why the last call failed | — |

`FileOptions`: `Read` (pdx), `ReadData` (data folder), `Read | ReadData` (data folder first),
`Write` (create/truncate), `Append`.

```csharp
public sealed class SaveDemo : Game
{
    private int _highScore;

    protected override void Initialize()
    {
        // Load: ReadAllText returns null on the first run, when there is no save yet.
        string saved = FileSystem.ReadAllText("highscore.txt");
        if (saved != null && int.TryParse(saved, out int value))
            _highScore = value;

        // Folders: create once, then list what's inside.
        if (!FileSystem.Exists("replays"))
            FileSystem.Mkdir("replays");
        foreach (string name in FileSystem.ListFiles("replays"))
            Sys.Log("replay: " + name);
    }

    private void GameOver(int score)
    {
        if (score <= _highScore)
            return;
        _highScore = score;
        // Write to a temporary file first, then rename: a crash mid-write can't corrupt the save.
        if (FileSystem.WriteAllText("highscore.tmp", score.ToString()))
            FileSystem.Rename("highscore.tmp", "highscore.txt");
        else
            Sys.Log("save failed: " + FileSystem.LastError);
    }

    public override bool Update()
    {
        if (Sys.JustPressed(Buttons.A))
            GameOver(_highScore + 10);
        Graphics.Clear(SolidColor.White);
        Graphics.DrawText("High score: " + _highScore, 10, 10);
        return true;
    }
}
```

## PDFile

An open file for streaming, appending or random access. Close it with `Dispose()` (or `using`).
`Read` returns the number of bytes read, 0 at the end, -1 on error; `Write` returns bytes written or -1.

```csharp
// Append a line to a log in the data folder.
using (PDFile log = FileSystem.Open("log.txt", FileOptions.Append))
{
    if (!log.IsNull)
        log.Write("started at " + Sys.SecondsSinceEpoch + "\n");
}

// Read a binary level file in chunks: a 4-byte header, then tiles.
using PDFile level = FileSystem.Open("levels/1.bin", FileOptions.Read | FileOptions.ReadData);
if (level.IsNull)
{
    Sys.Log("no level: " + FileSystem.LastError);
    return;
}
Span<byte> header = stackalloc byte[4];
if (level.Read(header) == 4)
{
    int width = header[0], height = header[1];
    byte[] tiles = new byte[width * height];
    level.Read(tiles);
    level.Seek(0, SeekOrigin.Begin);                 // back to the start
    Sys.Log("level " + width + "x" + height + ", now at " + level.Tell());
}
```

## Json: parsing into a tree

`Json.Parse(text)` and `Json.ParseFile(path)` decode a whole document into `JsonNode`s (null on a
syntax error). Indexers return null when a key or index doesn't exist, so lookups chain safely with
`?.`.

| `JsonNode` member | Meaning |
|---|---|
| `Type` | `Null`, `True`, `False`, `Integer`, `Float`, `String`, `Array`, `Table` |
| `IntValue`, `FloatValue`, `StringValue`, `BoolValue`, `IsNull` | the value |
| `node["key"]`, `node[index]` | members and elements (null if missing) |
| `Array` (`List<JsonNode>`), `Table` (`Dictionary<string, JsonNode>`), `Keys` (in document order) | containers |
| `Count` | number of elements or members |

```csharp
string text = "{ \"player\": { \"name\": \"Duck\", \"lives\": 3, \"speed\": 2.5 },"
            + "  \"levels\": [ { \"id\": 1, \"boss\": false }, { \"id\": 2, \"boss\": true } ] }";

JsonNode doc = Json.Parse(text);
if (doc == null)
{
    Sys.Log("invalid JSON");
    return;
}

string name = doc["player"]["name"].StringValue;               // "Duck"
int lives = doc["player"]["lives"].IntValue;                   // 3
float speed = doc["player"]["speed"].FloatValue;               // 2.5
int missing = doc["player"]["coins"]?.IntValue ?? 0;           // key not there: null -> 0

foreach (JsonNode level in doc["levels"].Array)
    Sys.Log("level " + level["id"].IntValue + (level["boss"].BoolValue ? " (boss)" : ""));

foreach (string key in doc["player"].Keys)                     // members in file order
    Sys.Log(key + " = " + doc["player"][key]);
```

Note that a whole number written as a float (`2.0`) may decode as `Integer`; `FloatValue` works for
both.

## JsonHandler: streaming large documents

`Json.Decode(handler, text)` / `Json.DecodeFile(handler, path)` call your handler as the decoder walks
the document, without building a tree. Override what you need:

| Callback | When |
|---|---|
| `WillDecodeSublist(name, type)` | an object or array starts (`name` is its key; the document is `_root`) |
| `ShouldDecodeTableValueForKey(key)` | return false to skip a member entirely |
| `DidDecodeTableValue(key, value)` | a member was decoded |
| `ShouldDecodeArrayValueAtIndex(position)` | return false to skip an element (positions start at 1) |
| `DidDecodeArrayValue(position, value)` | an element was decoded |
| `DidDecodeSublist(name, type)` | an object or array ended; the pointer you return is its value one level up |
| `DecodeError(message, line)` | the JSON is invalid |
| `Path` | where the decoder is, e.g. `levels[2].enemies` |
| `ReturnString` | set in `WillDecodeSublist` to receive that sublist as raw text instead |

`JsonValue` is only valid during the callback; read it with `IntValue`, `FloatValue`, `StringValue`,
`BoolValue` or `Type`.

```csharp
// Sums all "score" members anywhere in a big file, skipping the "history" arrays.
public sealed class ScoreSummer : JsonHandler
{
    public int Total;
    public int Errors;

    public override bool ShouldDecodeTableValueForKey(string key) => key != "history";

    public override void DidDecodeTableValue(string key, JsonValue value)
    {
        if (key == "score" && value.Type == JsonValueType.Integer)
            Total += value.IntValue;
    }

    public override void DecodeError(string error, int lineNumber)
    {
        Errors++;
        Sys.Log("JSON error line " + lineNumber + ": " + error + " at " + Path);
    }
}

public static class ScoreFile
{
    public static int Load()
    {
        var summer = new ScoreSummer();
        if (!Json.DecodeFile(summer, "scores.json"))
            Sys.Log("could not read scores.json");
        return summer.Total;
    }
}
```

## JsonWriter: writing JSON

Call the methods in document order: `StartTable`/`EndTable` for objects with `AddTableMember(name)`
before each value, `StartArray`/`EndArray` with `AddArrayMember()` before each element. `ToString()`
returns the text. Use `WriteFloat`, not `WriteDouble`, in games (double code crashes on the device).
The writer doesn't check that the structure is valid.

```csharp
int[] scores = { 120, 340, 90 };

using var writer = new JsonWriter(pretty: true);   // pretty = indented
writer.StartTable();

writer.AddTableMember("player");
writer.WriteString("Duck");

writer.AddTableMember("speed");
writer.WriteFloat(2.5f);

writer.AddTableMember("scores");
writer.StartArray();
foreach (int s in scores)
{
    writer.AddArrayMember();
    writer.WriteInt(s);
}
writer.EndArray();

writer.AddTableMember("finished");
writer.WriteBool(true);

writer.EndTable();
FileSystem.WriteAllText("save.json", writer.ToString());
```
