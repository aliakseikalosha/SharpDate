using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// Wraps <c>pd->file</c>. Paths are relative to the game's data folder (writable) and its pdx
    /// (read-only); see "Inside Playdate with C" for the lookup rules.
    /// </summary>
    public static unsafe class FileSystem
    {
        private static playdate_file* Api => PD.File;

        /// <summary>The last file error message, or null.</summary>
        public static string LastError => Utf8.ToString(Api->geterr());

        /// <summary>Opens a file. Returns a null <see cref="PDFile"/> on failure (see <see cref="LastError"/>).</summary>
        public static PDFile Open(string path, FileOptions mode) => new PDFile(Api->open(Utf8.Arg(path), mode));

        public static bool Exists(string path)
        {
            FileStat stat;
            return Api->stat(Utf8.Arg(path), &stat) == 0;
        }

        public static bool Stat(string path, out FileStat stat)
        {
            FileStat s;
            int result = Api->stat(Utf8.Arg(path), &s);
            stat = s;
            return result == 0;
        }

        public static bool Mkdir(string path) => Api->mkdir(Utf8.Arg(path)) == 0;

        public static bool Unlink(string path, bool recursive = false) => Api->unlink(Utf8.Arg(path), recursive ? 1 : 0) == 0;

        public static bool Rename(string from, string to) => Api->rename(Utf8.Arg(from, 0), Utf8.Arg(to, 1)) == 0;

        private static List<string> s_listResult;

        /// <summary>Lists the files in <paramref name="path"/> (directories end with '/').</summary>
        public static List<string> ListFiles(string path, bool showHidden = false)
        {
            var result = new List<string>();
            s_listResult = result;
            Api->listfiles(Utf8.Arg(path), &ListCallback, null, showHidden ? 1 : 0);
            s_listResult = null;
            return result;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void ListCallback(byte* path, void* userdata) => s_listResult?.Add(Utf8.ToString(path));

        /// <summary>Reads a whole file into a byte array (null if it doesn't exist).</summary>
        public static byte[] ReadAllBytes(string path)
        {
            PDFile file = Open(path, FileOptions.Read | FileOptions.ReadData);
            if (file.IsNull)
                return null;
            var chunks = new List<byte[]>();
            int total = 0;
            while (true)
            {
                byte[] chunk = new byte[4096];
                int n = file.Read(chunk);
                if (n <= 0)
                    break;
                if (n < chunk.Length)
                    Array.Resize(ref chunk, n);
                chunks.Add(chunk);
                total += n;
            }
            file.Dispose();
            byte[] result = new byte[total];
            int pos = 0;
            foreach (byte[] c in chunks)
            {
                Array.Copy(c, 0, result, pos, c.Length);
                pos += c.Length;
            }
            return result;
        }

        public static string ReadAllText(string path)
        {
            byte[] bytes = ReadAllBytes(path);
            return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
        }

        public static bool WriteAllBytes(string path, ReadOnlySpan<byte> data)
        {
            PDFile file = Open(path, FileOptions.Write);
            if (file.IsNull)
                return false;
            bool ok = file.Write(data) == data.Length;
            file.Dispose();
            return ok;
        }

        public static bool WriteAllText(string path, string text) => WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(text));
    }

    /// <summary>An open file (<c>SDFile*</c>). Close it with <see cref="Dispose"/>.</summary>
    public readonly unsafe struct PDFile : IDisposable
    {
        public readonly SDFile* Handle;

        public PDFile(SDFile* handle) { Handle = handle; }

        public bool IsNull => Handle == null;

        public int Read(Span<byte> buffer)
        {
            fixed (byte* p = buffer)
                return PD.File->read(Handle, p, (uint)buffer.Length);
        }

        public int Read(void* buffer, uint length) => PD.File->read(Handle, buffer, length);

        public int Write(ReadOnlySpan<byte> data)
        {
            fixed (byte* p = data)
                return PD.File->write(Handle, p, (uint)data.Length);
        }

        public int Write(string text)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
            return Write(bytes);
        }

        public int Flush() => PD.File->flush(Handle);
        public int Tell() => PD.File->tell(Handle);
        public int Seek(int position, SeekOrigin origin = SeekOrigin.Begin) => PD.File->seek(Handle, position, (int)origin);

        public void Dispose()
        {
            if (Handle != null)
                PD.File->close(Handle);
        }
    }
}
