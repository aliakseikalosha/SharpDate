// Port of the Playdate SDK's C_API/Examples/networking.
// Run the SDK's test server first: python3 <SDK>/Examples/networking/server/server.py

using System.Runtime.CompilerServices;

namespace Networking;

public sealed class NetworkingGame : Game
{
    private const string Server = "localhost";
    //private const string Server = "192.168.1.234";

    private enum State
    {
        RequestingHTTPAccess,
        WaitingForHTTPAccess,
        HTTPGet,
        WaitingForHTTPResponse,

        RequestingTCPAccess,
        WaitingForTCPAccess,
        TCPGet,
        WaitingForTCPResponse,

        ReportDone,
        Done,
    }

    private State _state = State.RequestingHTTPAccess;
    private HttpConnection _http;
    private TcpConnection _tcp;

#if !SHARPDATE_DEMOS // the Demos launcher starts this game itself
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new NetworkingGame());
#endif

    // --- HTTP ---

    private void RequestHTTP()
    {
        _http = new HttpConnection(Server, 65433, useSsl: false);
        _http.OnHeaderReceived = (header, value) => Sys.Log($"header: {header} = {value}");
        _http.OnHeadersRead = () => Sys.Log("headers read");
        _http.OnRequestComplete = HTTPRequestComplete;
        _http.OnConnectionClosed = () => Sys.Log("connection closed");

        NetErr err = _http.Get("/PING");
        Sys.Log($"http.Get() err={(int)err}");
    }

    private void HTTPRequestComplete()
    {
        int avail = _http.BytesAvailable;
        NetErr err = _http.Error;

        if (err != NetErr.Ok)
            Sys.Log($"http request complete, err={(int)err}");
        else
            Sys.Log($"http request complete, {avail} bytes available");

        if (avail > 0)
            Sys.Log(_http.ReadAvailableText());

        _state = State.RequestingTCPAccess;
    }

    // --- TCP ---

    private void RequestTCP()
    {
        _tcp = new TcpConnection(Server, 65431, useSsl: false);
        _tcp.OnConnectionClosed = TCPConnectionClosed;

        NetErr err = _tcp.Open(_ =>
        {
            Sys.Log("TCP connection open, sending PING");
            int wrote = _tcp.Write("PING\n");
            Sys.Log($"tcp.Write() returned {wrote}");
        });
        Sys.Log($"tcp.Open() err={(int)err}");
    }

    private void TCPConnectionClosed(NetErr err)
    {
        Sys.Log("TCP connection closed");

        int avail = _tcp.BytesAvailable;
        if (err != NetErr.Ok)
            Sys.Log($"tcp request complete, err={(int)err}");
        else
            Sys.Log($"tcp request complete, {avail} bytes available");

        if (avail > 0)
            Sys.Log(_tcp.ReadAvailableText());

        _state = State.ReportDone;
    }

    public override bool Update()
    {
        switch (_state)
        {
            case State.RequestingHTTPAccess:
            {
                _state = State.WaitingForHTTPAccess;

                AccessReply access = HttpConnection.RequestAccess(Server, 65433, false, "for testing", allowed =>
                {
                    Sys.Log($"HTTPAccessCallback: {allowed}");
                    _state = allowed ? State.HTTPGet : State.RequestingTCPAccess;
                });

                if (access == AccessReply.Deny)
                {
                    Sys.Log("http access denied");
                    _state = State.RequestingTCPAccess;
                }
                else if (access == AccessReply.Allow)
                {
                    _state = State.HTTPGet;
                }
                break;
            }

            case State.HTTPGet:
                RequestHTTP();
                _state = State.WaitingForHTTPResponse;
                break;

            case State.RequestingTCPAccess:
            {
                _state = State.WaitingForTCPAccess;

                AccessReply access = TcpConnection.RequestAccess(Server, 65431, false, "for testing", allowed =>
                {
                    Sys.Log($"TCPAccessCallback: {allowed}");
                    _state = allowed ? State.TCPGet : State.ReportDone;
                });

                if (access == AccessReply.Deny)
                {
                    Sys.Log("tcp access denied");
                    _state = State.ReportDone;
                }
                else if (access == AccessReply.Allow)
                {
                    _state = State.TCPGet;
                }
                break;
            }

            case State.TCPGet:
                RequestTCP();
                _state = State.WaitingForTCPResponse;
                break;

            case State.WaitingForTCPResponse:
            {
                int n = _tcp.SentBytesPending;
                if (n > 0)
                    Sys.Log($"waiting to send {n} bytes");
                break;
            }

            case State.ReportDone:
                Sys.Log("Done!");
                _state = State.Done;
                break;
        }

        return true;
    }
}
