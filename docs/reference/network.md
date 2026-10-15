# Network and scoreboards

`Network`, `HttpConnection`, `TcpConnection`, `Scoreboards`. Full member list:
[Network.md](../api/Network.md). Working example: `examples/Networking` (HTTP and TCP against the SDK's
test server).

Networking needs Playdate OS 2.7 or later and Wi-Fi set up on the device. Everything is asynchronous:
calls return immediately and results arrive in callbacks or events, on the main thread. Up to four
connections can be open at once.

## Permission

The player must allow each server. Call `RequestAccess` before creating a connection; the callback
receives the answer (the player is only asked once; the answer is remembered). The return value is
`AccessReply.Ask` while the dialog is up, or `Allow`/`Deny` when the answer was already known.

## Network

| Member | Meaning |
|---|---|
| `Network.Status` | `NotConnected`, `Connected` or `NotAvailable` (no configured access point) |
| `Network.SetEnabled(true, callback)` | start connecting now (it can take 10 s or more); the callback gets the result |
| `Network.SetEnabled(false)` | turn Wi-Fi off early (it also turns off by itself after 30 s idle) |

## HttpConnection

```csharp
public sealed class WeatherGame : Game
{
    private HttpConnection _http;
    private string _status = "asking for permission...";
    private readonly System.Text.StringBuilder _body = new System.Text.StringBuilder();

    protected override void Initialize()
    {
        Network.SetEnabled(true);                                 // start Wi-Fi early

        HttpConnection.RequestAccess("example.com", 443, true, "to show today's weather", allowed =>
        {
            if (allowed) StartRequest();
            else _status = "no permission";
        });
    }

    private void StartRequest()
    {
        _http = new HttpConnection("example.com", 443, true);   // port 0 = default (80/443)
        _http.ConnectTimeout = 10000;                             // ms

        // Events arrive on the main thread.
        _http.OnHeadersRead = () => _status = "HTTP " + _http.ResponseStatus;
        _http.OnResponse = () => _body.Append(_http.ReadAvailableText());   // data available
        _http.OnRequestComplete = () =>
        {
            _status = "done, " + _body.Length + " characters";
            _http.Dispose();                                      // release the connection
        };
        _http.OnConnectionClosed = () => Sys.Log("server closed the connection");

        NetErr err = _http.Get("/weather.json", "Accept: application/json\r\n");
        if (err != NetErr.Ok)
            _status = "request failed: " + err;
    }

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);
        Graphics.DrawText(_status, 10, 10);
        return true;
    }
}
```

| Member | Meaning |
|---|---|
| `Get(path, headers)` | GET; `headers` are extra lines, each ending with `\r\n` |
| `Post(path, headers, body)` / `Query(method, path, headers, body)` | POST / any method with a body |
| `OnHeaderReceived(name, value)`, `OnHeadersRead`, `OnResponse`, `OnRequestComplete`, `OnConnectionClosed` | events |
| `ResponseStatus`, `GetProgress(out read, out total)`, `BytesAvailable` | response state |
| `Read(buffer)`, `ReadAvailableText()` | read the body (returns bytes or a negative `NetErr`) |
| `KeepAlive`, `ConnectTimeout`, `ReadTimeout`, `ReadBufferSize`, `SetByteRange` | options |
| `Error` | last error |
| `Close()` / `Dispose()` | end the request / release the connection |

## TcpConnection

```csharp
public sealed class ChatClient
{
    private TcpConnection _tcp;

    public void Connect()
    {
        TcpConnection.RequestAccess("chat.example.com", 7000, false, "to chat with friends", allowed =>
        {
            if (!allowed)
                return;
            _tcp = new TcpConnection("chat.example.com", 7000, false);
            _tcp.OnConnectionClosed = err => Sys.Log("closed: " + err);
            NetErr result = _tcp.Open(err =>
            {
                if (err == NetErr.Ok)
                    _tcp.Write("HELLO duck\n");                   // returns bytes queued or a negative NetErr
                else
                    Sys.Log("could not connect: " + err);
            });
            if (result != NetErr.Ok)
                Sys.Log("open failed immediately: " + result);
        });
    }

    // Call every frame: read whatever arrived.
    public void Poll()
    {
        if (_tcp != null && _tcp.BytesAvailable > 0)
            Sys.Log("server: " + _tcp.ReadAvailableText());
    }

    public void Disconnect()
    {
        _tcp?.Close();
        _tcp?.Dispose();
        _tcp = null;
    }
}
```

`Write` is asynchronous: `SentBytesPending` tells how much is still queued. `Read(buffer)` waits up to
`ReadTimeout` ms (default 1000) when asking for more than is available, so prefer reading
`BytesAvailable` bytes.

## Scoreboards

Online leaderboards for games distributed through Catalog (the board IDs are set up with Panic).
Each call returns false if the request couldn't be sent; the result comes later in the callback with
either data or an error message.

```csharp
public static class Leaderboard
{
    public static void Submit(uint score)
    {
        Scoreboards.AddScore("highscores", score, (result, error) =>
        {
            if (error != null)
                Sys.Log("score not sent: " + error);       // e.g. offline: retry later
            else if (result.HasValue)
                Sys.Log("you are #" + result.Value.Rank);
        });
    }

    public static void ShowTop()
    {
        Scoreboards.GetScores("highscores", (scores, error) =>
        {
            if (scores == null)
                return;
            foreach (Scoreboards.Score s in scores)
                Sys.Log(s.Rank + ". " + s.Player + " " + s.Value);
        });
    }
}
```

`GetPersonalBest(boardId, callback)` returns the player's best score, `GetScoreboards(callback)` the
game's boards.
