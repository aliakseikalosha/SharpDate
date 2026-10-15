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
    /// <summary>Reading and writing files (C: <c>pd->file</c>, Lua: <c>playdate.file</c>).</summary>
    /// <remarks>
    /// Each game can read its own pdx (read-only) and read and write its data folder (Data/&lt;bundleID&gt;
    /// on the device, Disk/Data/&lt;bundleID&gt; next to the Simulator). Paths are relative, e.g. "save.json"
    /// or "levels/1.bin". <see cref="ReadAllBytes"/> and <see cref="ReadAllText"/> look in the data folder
    /// first, then in the pdx; with <see cref="Open"/> the <see cref="FileOptions"/> decide. Methods
    /// returning bool report failure with false; <see cref="LastError"/> has the reason. At most 64 files
    /// can be open at once.
    /// </remarks>
    public static unsafe class FileSystem
    {
        private static playdate_file* Api => PD.File;

        /// <summary>The last file error message, or null.</summary>
        public static string LastError => Utf8.ToString(Api->geterr());

        /// <summary>Opens a file. Returns a null <see cref="PDFile"/> on failure (see <see cref="LastError"/>).</summary>
        /// <param name="path">Relative path, e.g. "save.json".</param>
        /// <param name="mode">Where to look and how to open (see <see cref="FileOptions"/>).</param>
        public static PDFile Open(string path, FileOptions mode) => new PDFile(Api->open(Utf8.Arg(path), mode));

        /// <summary>True if a file or folder exists at <paramref name="path"/>.</summary>
        /// <param name="path">Relative path of a file or folder.</param>
        public static bool Exists(string path)
        {
            FileStat stat;
            return Api->stat(Utf8.Arg(path), &stat) == 0;
        }

        /// <summary>
        /// Gets size, folder flag and modification time; false if nothing exists at <paramref name="path"/>.
        /// </summary>
        /// <param name="path">Relative path.</param>
        /// <param name="stat">Receives size, folder flag and modification time.</param>
        public static bool Stat(string path, out FileStat stat)
        {
            FileStat s;
            int result = Api->stat(Utf8.Arg(path), &s);
            stat = s;
            return result == 0;
        }

        /// <summary>Creates a folder in the data folder. Intermediate folders are not created.</summary>
        /// <param name="path">The folder to create; its parent must exist.</param>
        public static bool Mkdir(string path) => Api->mkdir(Utf8.Arg(path)) == 0;

        /// <summary>
        /// Deletes a file, or a folder with everything in it when <paramref name="recursive"/> is true.
        /// </summary>
        /// <param name="path">The file or folder to delete.</param>
        /// <param name="recursive">
        /// Delete a folder with all its contents (a non-empty folder fails without it).
        /// </param>
        public static bool Unlink(string path, bool recursive = false) => Api->unlink(Utf8.Arg(path), recursive ? 1 : 0) == 0;

        /// <summary>Renames or moves a file, replacing an existing file at <paramref name="to"/>.</summary>
        /// <param name="from">Current path.</param>
        /// <param name="to">New path; its folder must exist.</param>
        public static bool Rename(string from, string to) => Api->rename(Utf8.Arg(from, 0), Utf8.Arg(to, 1)) == 0;

        private static List<string> s_listResult;

        /// <summary>
        /// Lists the names in a folder (not recursive); folder names end with '/'. "" lists the top level.
        /// </summary>
        /// <param name="showHidden">Include names starting with a dot.</param>
        /// <param name="path">Folder to list; "" for the top level.</param>
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

        /// <summary>Reads a whole file (data folder first, then the pdx).</summary>
        /// <returns>The contents, or null if the file doesn't exist.</returns>
        /// <param name="path">Relative path.</param>
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

        /// <summary>Reads a whole UTF-8 text file (data folder first, then the pdx).</summary>
        /// <returns>The text, or null if the file doesn't exist.</returns>
        /// <param name="path">Relative path.</param>
        public static string ReadAllText(string path)
        {
            byte[] bytes = ReadAllBytes(path);
            return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
        }

        /// <summary>Creates or replaces a file in the data folder.</summary>
        /// <param name="path">Relative path in the data folder; its folder must exist.</param>
        /// <param name="data">The bytes to write.</param>
        public static bool WriteAllBytes(string path, ReadOnlySpan<byte> data)
        {
            PDFile file = Open(path, FileOptions.Write);
            if (file.IsNull)
                return false;
            bool ok = file.Write(data) == data.Length;
            file.Dispose();
            return ok;
        }

        /// <summary>Creates or replaces a UTF-8 text file in the data folder.</summary>
        /// <param name="path">Relative path in the data folder; its folder must exist.</param>
        /// <param name="text">The text, written as UTF-8 without BOM.</param>
        public static bool WriteAllText(string path, string text) => WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// An open file (C: SDFile, Lua: playdate.file.file), from <see cref="FileSystem.Open"/>. A struct around
    /// the native handle; close it with <see cref="Dispose"/> (a <c>using</c> statement does it).
    /// </summary>
    public readonly unsafe struct PDFile : IDisposable
    {
        /// <summary>The native handle.</summary>
        public readonly SDFile* Handle;

        /// <summary>Wraps a native file handle.</summary>
        /// <param name="handle">A native file handle; this struct doesn't take ownership.</param>
        public PDFile(SDFile* handle) { Handle = handle; }

        /// <summary>True if the file could not be opened.</summary>
        public bool IsNull => Handle == null;

        /// <summary>Reads up to buffer.Length bytes.</summary>
        /// <returns>Bytes read, 0 at the end of the file, -1 on error.</returns>
        /// <param name="buffer">Receives the bytes; reads up to its length.</param>
        public int Read(Span<byte> buffer)
        {
            fixed (byte* p = buffer)
                return PD.File->read(Handle, p, (uint)buffer.Length);
        }

        /// <summary>Reads up to <paramref name="length"/> bytes into native memory.</summary>
        /// <returns>Bytes read, 0 at the end of the file, -1 on error.</returns>
        /// <param name="buffer">Native memory of at least <paramref name="length"/> bytes.</param>
        /// <param name="length">Maximum bytes to read.</param>
        public int Read(void* buffer, uint length) => PD.File->read(Handle, buffer, length);

        /// <summary>Writes bytes at the current position.</summary>
        /// <returns>Bytes written, or -1 on error.</returns>
        /// <param name="data">The bytes to write.</param>
        public int Write(ReadOnlySpan<byte> data)
        {
            fixed (byte* p = data)
                return PD.File->write(Handle, p, (uint)data.Length);
        }

        /// <summary>Writes text as UTF-8.</summary>
        /// <returns>Bytes written, or -1 on error.</returns>
        /// <param name="text">Text written as UTF-8.</param>
        public int Write(string text)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
            return Write(bytes);
        }

        /// <summary>Writes buffered data to the disk now.</summary>
        /// <returns>Bytes written, or -1 on error.</returns>
        public int Flush() => PD.File->flush(Handle);
        /// <summary>The current read/write position, or -1 on error.</summary>
        public int Tell() => PD.File->tell(Handle);
        /// <summary>Moves the read/write position.</summary>
        /// <returns>0 on success, -1 on error.</returns>
        /// <param name="position">
        /// Byte offset relative to <paramref name="origin"/> (may be negative from
        /// <see cref="SeekOrigin.Current"/> or <see cref="SeekOrigin.End"/>).
        /// </param>
        /// <param name="origin">Start, current position or end.</param>
        public int Seek(int position, SeekOrigin origin = SeekOrigin.Begin) => PD.File->seek(Handle, position, (int)origin);

        /// <summary>Closes the file.</summary>
        public void Dispose()
        {
            if (Handle != null)
                PD.File->close(Handle);
        }
    }
}
