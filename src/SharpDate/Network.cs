using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>Wraps <c>pd->scoreboards</c>. Results arrive later through the callbacks.</summary>
    public static unsafe class Scoreboards
    {
        private static playdate_scoreboards* Api => PD.Scoreboards;

        public readonly struct Score
        {
            public readonly uint Rank;
            public readonly uint Value;
            public readonly string Player;
            public readonly string BoardID;

            internal Score(uint rank, uint value, string player, string boardId)
            {
                Rank = rank;
                Value = value;
                Player = player;
                BoardID = boardId;
            }
        }

        public readonly struct Board
        {
            public readonly string BoardID;
            public readonly string Name;

            internal Board(string id, string name)
            {
                BoardID = id;
                Name = name;
            }
        }

        private static readonly Queue<Action<Score?, string>> s_addScore = new Queue<Action<Score?, string>>();
        private static readonly Queue<Action<Score?, string>> s_personalBest = new Queue<Action<Score?, string>>();
        private static readonly Queue<Action<Board[], string>> s_boards = new Queue<Action<Board[], string>>();
        private static readonly Queue<Action<Score[], string>> s_scores = new Queue<Action<Score[], string>>();

        /// <summary>Submits a score; the callback receives the recorded score or an error message.</summary>
        public static bool AddScore(string boardId, uint value, Action<Score?, string> callback)
        {
            s_addScore.Enqueue(callback);
            return Api->addScore(Utf8.Arg(boardId), value, &AddScoreCallback) != 0;
        }

        public static bool GetPersonalBest(string boardId, Action<Score?, string> callback)
        {
            s_personalBest.Enqueue(callback);
            return Api->getPersonalBest(Utf8.Arg(boardId), &PersonalBestCallback) != 0;
        }

        public static bool GetScoreboards(Action<Board[], string> callback)
        {
            s_boards.Enqueue(callback);
            return Api->getScoreboards(&BoardsCallback) != 0;
        }

        public static bool GetScores(string boardId, Action<Score[], string> callback)
        {
            s_scores.Enqueue(callback);
            return Api->getScores(Utf8.Arg(boardId), &ScoresCallback) != 0;
        }

        private static Score? ToScore(PDScore* score)
            => score == null ? null : new Score(score->Rank, score->Value, Utf8.ToString(score->Player), Utf8.ToString(score->BoardID));

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void AddScoreCallback(PDScore* score, byte* error)
        {
            Score? s = ToScore(score);
            if (score != null)
                Api->freeScore(score);
            if (s_addScore.Count > 0)
                s_addScore.Dequeue()?.Invoke(s, Utf8.ToString(error));
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void PersonalBestCallback(PDScore* score, byte* error)
        {
            Score? s = ToScore(score);
            if (score != null)
                Api->freeScore(score);
            if (s_personalBest.Count > 0)
                s_personalBest.Dequeue()?.Invoke(s, Utf8.ToString(error));
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void BoardsCallback(PDBoardsList* boards, byte* error)
        {
            Board[] result = null;
            if (boards != null)
            {
                result = new Board[boards->Count];
                for (int i = 0; i < result.Length; i++)
                    result[i] = new Board(Utf8.ToString(boards->Boards[i].BoardID), Utf8.ToString(boards->Boards[i].Name));
                Api->freeBoardsList(boards);
            }
            if (s_boards.Count > 0)
                s_boards.Dequeue()?.Invoke(result, Utf8.ToString(error));
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void ScoresCallback(PDScoresList* scores, byte* error)
        {
            Score[] result = null;
            if (scores != null)
            {
                string boardId = Utf8.ToString(scores->BoardID);
                result = new Score[scores->Count];
                for (int i = 0; i < result.Length; i++)
                    result[i] = new Score(scores->Scores[i].Rank, scores->Scores[i].Value, Utf8.ToString(scores->Scores[i].Player), boardId);
                Api->freeScoresList(scores);
            }
            if (s_scores.Count > 0)
                s_scores.Dequeue()?.Invoke(result, Utf8.ToString(error));
        }
    }

    /// <summary>Wraps <c>pd->network</c>: Wi-Fi state, HTTP and TCP connections.</summary>
    public static unsafe class Network
    {
        public static WifiStatus Status => PD.Network->getStatus();

        private static Action<NetErr> s_enabledCallback;

        public static void SetEnabled(bool enabled, Action<NetErr> callback = null)
        {
            s_enabledCallback = callback;
            PD.Network->setEnabled(enabled ? (byte)1 : (byte)0, callback == null ? null : &EnabledCallback);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void EnabledCallback(NetErr err) => s_enabledCallback?.Invoke(err);

        // Access-request callbacks share one trampoline; userdata is a GC handle to the Action.
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static void AccessCallback(byte allowed, void* userdata)
        {
            GCHandle handle = GCHandle.FromIntPtr((IntPtr)userdata);
            var action = (Action<bool>)handle.Target;
            handle.Free();
            action?.Invoke(allowed != 0);
        }

        internal static void* AccessUserdata(Action<bool> callback) => (void*)GCHandle.ToIntPtr(GCHandle.Alloc(callback));
    }

    /// <summary>An HTTP connection. Events are raised on the main thread; call <see cref="Dispose"/> when done.</summary>
    public sealed unsafe class HttpConnection : IDisposable
    {
        private static playdate_http* Api => PD.Network->http;

        private HTTPConnection* _handle;
        private GCHandle _self;

        /// <summary>Asks the user for permission to connect (required before connecting on device).</summary>
        public static AccessReply RequestAccess(string server, int port, bool useSsl, string purpose, Action<bool> callback)
            => Api->requestAccess(Utf8.Arg(server, 0), port, useSsl ? (byte)1 : (byte)0, Utf8.Arg(purpose, 1), &Network.AccessCallback, Network.AccessUserdata(callback));

        public HttpConnection(string server, int port, bool useSsl)
        {
            _handle = Api->newConnection(Utf8.Arg(server), port, useSsl ? (byte)1 : (byte)0);
            _self = GCHandle.Alloc(this);
            Api->setUserdata(_handle, (void*)GCHandle.ToIntPtr(_self));
            Api->setHeaderReceivedCallback(_handle, &HeaderReceived);
            Api->setHeadersReadCallback(_handle, &HeadersRead);
            Api->setResponseCallback(_handle, &Response);
            Api->setRequestCompleteCallback(_handle, &RequestComplete);
            Api->setConnectionClosedCallback(_handle, &Closed);
        }

        public HTTPConnection* Handle => _handle;

        public Action<string, string> OnHeaderReceived;
        public Action OnHeadersRead;
        public Action OnResponse;
        public Action OnRequestComplete;
        public Action OnConnectionClosed;

        private static HttpConnection From(HTTPConnection* conn)
        {
            void* ud = Api->getUserdata(conn);
            return ud == null ? null : (HttpConnection)GCHandle.FromIntPtr((IntPtr)ud).Target;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void HeaderReceived(HTTPConnection* conn, byte* key, byte* value) => From(conn)?.OnHeaderReceived?.Invoke(Utf8.ToString(key), Utf8.ToString(value));

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void HeadersRead(HTTPConnection* conn) => From(conn)?.OnHeadersRead?.Invoke();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void Response(HTTPConnection* conn) => From(conn)?.OnResponse?.Invoke();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void RequestComplete(HTTPConnection* conn) => From(conn)?.OnRequestComplete?.Invoke();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void Closed(HTTPConnection* conn) => From(conn)?.OnConnectionClosed?.Invoke();

        public int ConnectTimeout { set => Api->setConnectTimeout(_handle, value); }
        public bool KeepAlive { set => Api->setKeepAlive(_handle, value ? (byte)1 : (byte)0); }
        public int ReadTimeout { set => Api->setReadTimeout(_handle, value); }
        public int ReadBufferSize { set => Api->setReadBufferSize(_handle, value); }
        public void SetByteRange(int start, int end) => Api->setByteRange(_handle, start, end);

        public NetErr Get(string path, string headers = null)
        {
            byte* h = Utf8.Arg(headers, 1);
            return Api->get(_handle, Utf8.Arg(path, 0), h, headers == null ? 0 : (nuint)System.Text.Encoding.UTF8.GetByteCount(headers));
        }

        public NetErr Post(string path, string headers, ReadOnlySpan<byte> body)
        {
            byte* h = Utf8.Arg(headers, 1);
            fixed (byte* b = body)
                return Api->post(_handle, Utf8.Arg(path, 0), h, headers == null ? 0 : (nuint)System.Text.Encoding.UTF8.GetByteCount(headers), b, (nuint)body.Length);
        }

        public NetErr Query(string method, string path, string headers, ReadOnlySpan<byte> body)
        {
            byte* h = Utf8.Arg(headers, 2);
            fixed (byte* b = body)
                return Api->query(_handle, Utf8.Arg(method, 0), Utf8.Arg(path, 1), h, headers == null ? 0 : (nuint)System.Text.Encoding.UTF8.GetByteCount(headers), b, (nuint)body.Length);
        }

        public NetErr Error => Api->getError(_handle);

        public void GetProgress(out int read, out int total)
        {
            int r, t;
            Api->getProgress(_handle, &r, &t);
            read = r;
            total = t;
        }

        public int ResponseStatus => Api->getResponseStatus(_handle);
        public int BytesAvailable => (int)Api->getBytesAvailable(_handle);

        public int Read(Span<byte> buffer)
        {
            fixed (byte* p = buffer)
                return Api->read(_handle, p, (uint)buffer.Length);
        }

        /// <summary>Reads everything currently available as UTF-8 text.</summary>
        public string ReadAvailableText()
        {
            var sb = new System.Text.StringBuilder();
            byte* buffer = stackalloc byte[256];
            while (BytesAvailable > 0)
            {
                int n = Api->read(_handle, buffer, 256);
                if (n <= 0)
                    break;
                sb.Append(Utf8.ToString(buffer, n));
            }
            return sb.ToString();
        }

        public void Close() => Api->close(_handle);

        public void Dispose()
        {
            if (_handle == null)
                return;
            Api->setUserdata(_handle, null);
            Api->release(_handle);
            _handle = null;
            _self.Free();
        }
    }

    /// <summary>A TCP connection. Events are raised on the main thread; call <see cref="Dispose"/> when done.</summary>
    public sealed unsafe class TcpConnection : IDisposable
    {
        private static playdate_tcp* Api => PD.Network->tcp;

        private TCPConnection* _handle;
        private GCHandle _self;

        public static AccessReply RequestAccess(string server, int port, bool useSsl, string purpose, Action<bool> callback)
            => Api->requestAccess(Utf8.Arg(server, 0), port, useSsl ? (byte)1 : (byte)0, Utf8.Arg(purpose, 1), &Network.AccessCallback, Network.AccessUserdata(callback));

        public TcpConnection(string server, int port, bool useSsl)
        {
            _handle = Api->newConnection(Utf8.Arg(server), port, useSsl ? (byte)1 : (byte)0);
            _self = GCHandle.Alloc(this);
            Api->setUserdata(_handle, (void*)GCHandle.ToIntPtr(_self));
            Api->setConnectionClosedCallback(_handle, &Closed);
        }

        public TCPConnection* Handle => _handle;

        /// <summary>Raised when the connection is closed, with the error (if any).</summary>
        public Action<NetErr> OnConnectionClosed;

        private Action<NetErr> _onOpen;

        private static TcpConnection From(TCPConnection* conn)
        {
            void* ud = Api->getUserdata(conn);
            return ud == null ? null : (TcpConnection)GCHandle.FromIntPtr((IntPtr)ud).Target;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void Closed(TCPConnection* conn, NetErr err) => From(conn)?.OnConnectionClosed?.Invoke(err);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void Opened(TCPConnection* conn, NetErr err, void* userdata) => From(conn)?._onOpen?.Invoke(err);

        /// <summary>Opens the connection; <paramref name="onOpen"/> runs when it's established (or fails).</summary>
        public NetErr Open(Action<NetErr> onOpen)
        {
            _onOpen = onOpen;
            return Api->open(_handle, &Opened, null);
        }

        public NetErr Close() => Api->close(_handle);
        public NetErr Error => Api->getError(_handle);
        public int ConnectTimeout { set => Api->setConnectTimeout(_handle, value); }
        public int ReadTimeout { set => Api->setReadTimeout(_handle, value); }
        public int ReadBufferSize { set => Api->setReadBufferSize(_handle, value); }
        public int BytesAvailable => (int)Api->getBytesAvailable(_handle);
        public int SentBytesPending => (int)Api->getSentBytesPending(_handle);

        /// <summary>Returns bytes read, or a negative <see cref="NetErr"/>.</summary>
        public int Read(Span<byte> buffer)
        {
            fixed (byte* p = buffer)
                return Api->read(_handle, p, (nuint)buffer.Length);
        }

        /// <summary>Returns bytes written, or a negative <see cref="NetErr"/>.</summary>
        public int Write(ReadOnlySpan<byte> data)
        {
            fixed (byte* p = data)
                return Api->write(_handle, p, (nuint)data.Length);
        }

        public int Write(string text) => Write(System.Text.Encoding.UTF8.GetBytes(text));

        public string ReadAvailableText()
        {
            var sb = new System.Text.StringBuilder();
            byte* buffer = stackalloc byte[256];
            while (BytesAvailable > 0)
            {
                int n = Api->read(_handle, buffer, 256);
                if (n <= 0)
                    break;
                sb.Append(Utf8.ToString(buffer, n));
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            if (_handle == null)
                return;
            Api->setUserdata(_handle, null);
            Api->release(_handle);
            _handle = null;
            _self.Free();
        }
    }
}
