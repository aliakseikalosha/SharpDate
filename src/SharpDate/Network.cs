// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// Online scoreboards for games distributed through Catalog (C: <c>pd->scoreboards</c>, Lua:
    /// <c>playdate.scoreboards</c>). Every call is asynchronous: the result arrives later in the callback, on
    /// the main thread. Board IDs are set up with Panic.
    /// </summary>
    public static unsafe class Scoreboards
    {
        private static playdate_scoreboards* Api => PD.Scoreboards;

        /// <summary>A score on a board.</summary>
        public readonly struct Score
        {
            /// <summary>Rank, 1 = best.</summary>
            public readonly uint Rank;
            /// <summary>The score.</summary>
            public readonly uint Value;
            /// <summary>Player name.</summary>
            public readonly string Player;
            /// <summary>Board identifier.</summary>
            public readonly string BoardID;

            internal Score(uint rank, uint value, string player, string boardId)
            {
                Rank = rank;
                Value = value;
                Player = player;
                BoardID = boardId;
            }
        }

        /// <summary>A scoreboard.</summary>
        public readonly struct Board
        {
            /// <summary>Board identifier.</summary>
            public readonly string BoardID;
            /// <summary>Display name.</summary>
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
        /// <param name="boardId">The board's identifier.</param>
        /// <param name="value">The score.</param>
        /// <param name="callback">
        /// Receives (score as recorded or null, error or null) on the main thread.
        /// </param>
        public static bool AddScore(string boardId, uint value, Action<Score?, string> callback)
        {
            s_addScore.Enqueue(callback);
            return Api->addScore(Utf8.Arg(boardId), value, &AddScoreCallback) != 0;
        }

        /// <summary>
        /// Gets the player's best score on a board; the callback receives it (null if none) or an error
        /// message.
        /// </summary>
        /// <param name="boardId">The board's identifier.</param>
        /// <param name="callback">Receives (best score or null, error or null).</param>
        public static bool GetPersonalBest(string boardId, Action<Score?, string> callback)
        {
            s_personalBest.Enqueue(callback);
            return Api->getPersonalBest(Utf8.Arg(boardId), &PersonalBestCallback) != 0;
        }

        /// <summary>Lists the game's boards; the callback receives them or an error message.</summary>
        /// <param name="callback">Receives (boards or null, error or null).</param>
        public static bool GetScoreboards(Action<Board[], string> callback)
        {
            s_boards.Enqueue(callback);
            return Api->getScoreboards(&BoardsCallback) != 0;
        }

        /// <summary>Gets the top scores of a board; the callback receives them or an error message.</summary>
        /// <param name="boardId">The board's identifier.</param>
        /// <param name="callback">Receives (scores or null, error or null).</param>
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

    /// <summary>
    /// Wi-Fi state and control (C: <c>pd->network</c>, Lua: <c>playdate.network</c>). Connections are
    /// <see cref="HttpConnection"/> and <see cref="TcpConnection"/>; the device supports four at a time.
    /// </summary>
    public static unsafe class Network
    {
        /// <summary>Whether the device is connected to an access point.</summary>
        public static WifiStatus Status => PD.Network->getStatus();

        private static Action<NetErr> s_enabledCallback;

        /// <summary>
        /// Turns Wi-Fi on early (connecting can take 10 seconds or more), or off when the game no longer
        /// needs it. Wi-Fi also turns on by itself when needed and off after 30 seconds of inactivity.
        /// </summary>
        /// <param name="callback">Receives the result of connecting (only when enabling).</param>
        /// <param name="enabled">True to connect now, false to turn Wi-Fi off.</param>
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

    /// <summary>
    /// An HTTP or HTTPS connection to one server (C: HTTPConnection, Lua: playdate.network.http).
    /// </summary>
    /// <remarks>
    /// Ask for permission first with <see cref="RequestAccess"/>. Then send a request with <see cref="Get"/>,
    /// <see cref="Post"/> or <see cref="Query"/> and read the response in the events
    /// (<see cref="OnResponse"/> when data is available, <see cref="OnRequestComplete"/> at the end). Events
    /// run on the main thread. <see cref="Dispose"/> the connection when done.
    /// </remarks>
    public sealed unsafe class HttpConnection : IDisposable
    {
        private static playdate_http* Api => PD.Network->http;

        private HTTPConnection* _handle;
        private GCHandle _self;

        /// <summary>
        /// Asks the player for permission to connect to <paramref name="server"/> (null = any server).
        /// Required before creating a connection; the callback receives the answer.
        /// </summary>
        /// <param name="purpose">Shown in the permission dialog.</param>
        /// <returns>
        /// <see cref="AccessReply.Ask"/> while waiting for the player, or the remembered answer.
        /// </returns>
        /// <param name="server">
        /// Host name, a parent domain of the hosts you'll use, or null for any server.
        /// </param>
        /// <param name="port">Port, 1-65535; 0 for the default.</param>
        /// <param name="useSsl">Request access for HTTPS.</param>
        /// <param name="callback">Receives true if the player allowed access (main thread).</param>
        public static AccessReply RequestAccess(string server, int port, bool useSsl, string purpose, Action<bool> callback)
            => Api->requestAccess(Utf8.Arg(server, 0), port, useSsl ? (byte)1 : (byte)0, Utf8.Arg(purpose, 1), &Network.AccessCallback, Network.AccessUserdata(callback));

        /// <summary>Creates a connection; nothing is sent until a request is made.</summary>
        /// <param name="port">0 for the default (80, or 443 with SSL).</param>
        /// <param name="server">Host name, e.g. "example.com" (no scheme).</param>
        /// <param name="useSsl">Use HTTPS.</param>
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

        /// <summary>The native connection.</summary>
        public HTTPConnection* Handle => _handle;

        /// <summary>Raised for each response header (name, value).</summary>
        public Action<string, string> OnHeaderReceived;
        /// <summary>
        /// Raised when all headers are read; <see cref="ResponseStatus"/> and <see cref="GetProgress"/> are
        /// valid.
        /// </summary>
        public Action OnHeadersRead;
        /// <summary>Raised when response data is available to <see cref="Read"/>.</summary>
        public Action OnResponse;
        /// <summary>
        /// Raised when the whole response has arrived (when its length is known) or the request timed out.
        /// </summary>
        public Action OnRequestComplete;
        /// <summary>Raised when the server closes the connection.</summary>
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

        /// <summary>Milliseconds to wait for the connection to be made, above 0.</summary>
        public int ConnectTimeout { set => Api->setConnectTimeout(_handle, value); }
        /// <summary>
        /// Sends "Connection: keep-alive" so the connection can be reused for the next request.
        /// </summary>
        public bool KeepAlive { set => Api->setKeepAlive(_handle, value ? (byte)1 : (byte)0); }
        /// <summary>Milliseconds <see cref="Read"/> waits for data, above 0 (default 1000).</summary>
        public int ReadTimeout { set => Api->setReadTimeout(_handle, value); }
        /// <summary>
        /// Size of the read buffer in bytes, above 0 (default 64 KB); <see cref="Read"/> returns at most this
        /// much at once.
        /// </summary>
        public int ReadBufferSize { set => Api->setReadBufferSize(_handle, value); }
        /// <summary>
        /// Requests only bytes <paramref name="start"/> to <paramref name="end"/> (a Range header).
        /// </summary>
        /// <param name="start">First byte, 0 or more.</param>
        /// <param name="end">Last byte (included).</param>
        public void SetByteRange(int start, int end) => Api->setByteRange(_handle, start, end);

        /// <summary>
        /// Sends a GET request. <paramref name="headers"/> are extra header lines ("Name: value\r\n...").
        /// </summary>
        /// <param name="path">Request path starting with '/', with the query string.</param>
        /// <param name="headers">Extra header lines, each ending with "\r\n"; null for none.</param>
        public NetErr Get(string path, string headers = null)
        {
            byte* h = Utf8.Arg(headers, 1);
            return Api->get(_handle, Utf8.Arg(path, 0), h, headers == null ? 0 : (nuint)System.Text.Encoding.UTF8.GetByteCount(headers));
        }

        /// <summary>Sends a POST request with a body.</summary>
        /// <param name="path">Request path starting with '/'.</param>
        /// <param name="headers">Extra header lines, each ending with "\r\n"; null for none.</param>
        /// <param name="body">The request body.</param>
        public NetErr Post(string path, string headers, ReadOnlySpan<byte> body)
        {
            byte* h = Utf8.Arg(headers, 1);
            fixed (byte* b = body)
                return Api->post(_handle, Utf8.Arg(path, 0), h, headers == null ? 0 : (nuint)System.Text.Encoding.UTF8.GetByteCount(headers), b, (nuint)body.Length);
        }

        /// <summary>Sends a request with any method (PUT, DELETE, ...).</summary>
        /// <param name="method">HTTP method, e.g. "PUT" or "DELETE".</param>
        /// <param name="path">Request path starting with '/'.</param>
        /// <param name="headers">Extra header lines, each ending with "\r\n"; null for none.</param>
        /// <param name="body">The request body (may be empty).</param>
        public NetErr Query(string method, string path, string headers, ReadOnlySpan<byte> body)
        {
            byte* h = Utf8.Arg(headers, 2);
            fixed (byte* b = body)
                return Api->query(_handle, Utf8.Arg(method, 0), Utf8.Arg(path, 1), h, headers == null ? 0 : (nuint)System.Text.Encoding.UTF8.GetByteCount(headers), b, (nuint)body.Length);
        }

        /// <summary>The connection's last error, or <see cref="NetErr.Ok"/>.</summary>
        public NetErr Error => Api->getError(_handle);

        /// <summary>Bytes read so far and the total the server announced (if known).</summary>
        /// <param name="read">Receives bytes received so far.</param>
        /// <param name="total">Receives the announced size, or 0 if unknown.</param>
        public void GetProgress(out int read, out int total)
        {
            int r, t;
            Api->getProgress(_handle, &r, &t);
            read = r;
            total = t;
        }

        /// <summary>The HTTP status code (200, 404, ...) once the headers are read.</summary>
        public int ResponseStatus => Api->getResponseStatus(_handle);
        /// <summary>Bytes waiting to be read.</summary>
        public int BytesAvailable => (int)Api->getBytesAvailable(_handle);

        /// <summary>
        /// Reads up to buffer.Length bytes, waiting up to <see cref="ReadTimeout"/> for more.
        /// </summary>
        /// <returns>Bytes read, or a negative <see cref="NetErr"/>.</returns>
        /// <param name="buffer">Receives up to buffer.Length bytes (at most the read buffer size).</param>
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

        /// <summary>Closes the connection (it can be used for another request).</summary>
        public void Close() => Api->close(_handle);

        /// <summary>Releases the connection.</summary>
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

    /// <summary>
    /// A TCP (optionally TLS) connection (C: TCPConnection, Lua: playdate.network.tcp). Ask for permission
    /// with <see cref="RequestAccess"/>, then <see cref="Open"/>. <see cref="Dispose"/> it when done.
    /// </summary>
    public sealed unsafe class TcpConnection : IDisposable
    {
        private static playdate_tcp* Api => PD.Network->tcp;

        private TCPConnection* _handle;
        private GCHandle _self;

        /// <summary>
        /// Asks the player for permission to connect to <paramref name="server"/> (null = any) on
        /// <paramref name="port"/> (0 = any).
        /// </summary>
        /// <returns>
        /// <see cref="AccessReply.Ask"/> while waiting for the player, or the remembered answer.
        /// </returns>
        /// <param name="server">Host name, a parent domain, or null for any server.</param>
        /// <param name="port">Port, 1-65535; 0 for any port.</param>
        /// <param name="useSsl">Request access for TLS connections.</param>
        /// <param name="purpose">Shown in the permission dialog.</param>
        /// <param name="callback">Receives true if the player allowed access (main thread).</param>
        public static AccessReply RequestAccess(string server, int port, bool useSsl, string purpose, Action<bool> callback)
            => Api->requestAccess(Utf8.Arg(server, 0), port, useSsl ? (byte)1 : (byte)0, Utf8.Arg(purpose, 1), &Network.AccessCallback, Network.AccessUserdata(callback));

        /// <summary>Creates a connection; call <see cref="Open"/> to connect.</summary>
        /// <param name="server">Host name.</param>
        /// <param name="port">Port, 1-65535.</param>
        /// <param name="useSsl">Use TLS.</param>
        public TcpConnection(string server, int port, bool useSsl)
        {
            _handle = Api->newConnection(Utf8.Arg(server), port, useSsl ? (byte)1 : (byte)0);
            _self = GCHandle.Alloc(this);
            Api->setUserdata(_handle, (void*)GCHandle.ToIntPtr(_self));
            Api->setConnectionClosedCallback(_handle, &Closed);
        }

        /// <summary>The native connection.</summary>
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
        /// <param name="onOpen">
        /// Receives <see cref="NetErr.Ok"/> or the error when connecting finishes (main thread).
        /// </param>
        public NetErr Open(Action<NetErr> onOpen)
        {
            _onOpen = onOpen;
            return Api->open(_handle, &Opened, null);
        }

        /// <summary>Closes the connection (it can be opened again).</summary>
        public NetErr Close() => Api->close(_handle);
        /// <summary>The last error, or <see cref="NetErr.Ok"/>.</summary>
        public NetErr Error => Api->getError(_handle);
        /// <summary>Milliseconds to wait for the connection, above 0.</summary>
        public int ConnectTimeout { set => Api->setConnectTimeout(_handle, value); }
        /// <summary>Milliseconds <see cref="Read"/> waits for data, above 0 (default 1000).</summary>
        public int ReadTimeout { set => Api->setReadTimeout(_handle, value); }
        /// <summary>Read buffer size in bytes, above 0 (default 64 KB).</summary>
        public int ReadBufferSize { set => Api->setReadBufferSize(_handle, value); }
        /// <summary>Bytes waiting to be read.</summary>
        public int BytesAvailable => (int)Api->getBytesAvailable(_handle);
        /// <summary>Bytes written but not yet sent (writes are asynchronous).</summary>
        public int SentBytesPending => (int)Api->getSentBytesPending(_handle);

        /// <summary>Returns bytes read, or a negative <see cref="NetErr"/>.</summary>
        /// <param name="buffer">Receives up to buffer.Length bytes.</param>
        public int Read(Span<byte> buffer)
        {
            fixed (byte* p = buffer)
                return Api->read(_handle, p, (nuint)buffer.Length);
        }

        /// <summary>Returns bytes written, or a negative <see cref="NetErr"/>.</summary>
        /// <param name="data">The bytes to send.</param>
        public int Write(ReadOnlySpan<byte> data)
        {
            fixed (byte* p = data)
                return Api->write(_handle, p, (nuint)data.Length);
        }

        /// <summary>Writes text as UTF-8.</summary>
        /// <returns>Bytes accepted, or a negative <see cref="NetErr"/>.</returns>
        /// <param name="text">Text sent as UTF-8.</param>
        public int Write(string text) => Write(System.Text.Encoding.UTF8.GetBytes(text));

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

        /// <summary>Releases the connection.</summary>
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
