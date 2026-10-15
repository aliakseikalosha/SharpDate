// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>A Lua-callable function: <c>int f(lua_State* L)</c>, returning the number of values pushed.</summary>
    public readonly unsafe struct LuaFunction
    {
        /// <summary>The function's name inside the class, e.g. "new" or "__gc".</summary>
        public readonly string Name;
        /// <summary>The <c>[UnmanagedCallersOnly]</c> implementation.</summary>
        public readonly delegate* unmanaged[Cdecl]<void*, int> Function;

        /// <summary>
        /// Pairs a name with an implementation for
        /// <see cref="Lua.RegisterClass(string, ReadOnlySpan{LuaFunction}, out string, bool)"/>.
        /// </summary>
        /// <param name="name">Name inside the class, e.g. "new", "__gc", "__index".</param>
        /// <param name="function">
        /// Static <c>[UnmanagedCallersOnly]</c> method (lua_State*) returning the number of values pushed.
        /// </param>
        public LuaFunction(string name, delegate* unmanaged[Cdecl]<void*, int> function)
        {
            Name = name;
            Function = function;
        }
    }

    /// <summary>
    /// Wraps <c>pd->lua</c>: register C# functions and classes with the Lua runtime and exchange values.
    /// Register in <see cref="Game.InitializeLua"/>. Lua functions are <c>[UnmanagedCallersOnly]</c>
    /// static methods taking a <c>void*</c> (lua_State) and returning the number of results.
    /// </summary>
    public static unsafe class Lua
    {
        private static playdate_lua* Api => PD.Lua;

        /// <summary>Adds a global Lua function, e.g. "playdate.graphics.image.popcount".</summary>
        /// <param name="function">
        /// Static <c>[UnmanagedCallersOnly]</c> method returning the number of values pushed.
        /// </param>
        /// <param name="name">Global name; dots create or extend tables, e.g. "mygame.distance".</param>
        /// <param name="error">Receives the reason on failure.</param>
        public static bool AddFunction(delegate* unmanaged[Cdecl]<void*, int> function, string name, out string error)
        {
            byte* err = null;
            int ok = Api->addFunction(function, Utf8.Alloc(name), &err);
            error = Utf8.ToString(err);
            return ok != 0;
        }

        /// <summary>
        /// Registers a Lua class. Methods like <c>__gc</c>, <c>__index</c>, <c>new</c> are included in
        /// <paramref name="methods"/>. Static classes have no instances (functions only).
        /// </summary>
        /// <param name="name">
        /// Class name as used in Lua (dots allowed), and in
        /// <see cref="PushObject"/>/<see cref="GetArgObject(int, string)"/>.
        /// </param>
        /// <param name="methods">The methods and metamethods.</param>
        /// <param name="error">Receives the reason on failure.</param>
        /// <param name="isStatic">True for a plain table of functions instead of a class.</param>
        public static bool RegisterClass(string name, ReadOnlySpan<LuaFunction> methods, out string error, bool isStatic = false)
        {
            // The runtime keeps pointers to the registration tables, so they are allocated permanently.
            LuaReg* regs = (LuaReg*)NativeMemory.AllocZeroed((nuint)(sizeof(LuaReg) * (methods.Length + 1)));
            for (int i = 0; i < methods.Length; i++)
            {
                regs[i].name = Utf8.Alloc(methods[i].Name);
                regs[i].func = methods[i].Function;
            }
            byte* err = null;
            int ok = Api->registerClass(Utf8.Alloc(name), regs, null, isStatic ? 1 : 0, &err);
            error = Utf8.ToString(err);
            return ok != 0;
        }

        /// <summary>Registers a class with constant values (e.g. enums) in addition to methods.</summary>
        /// <param name="name">Class name as used in Lua.</param>
        /// <param name="methods">The methods and metamethods.</param>
        /// <param name="values">Constants, ending with an entry whose name is null; must stay valid.</param>
        /// <param name="error">Receives the reason on failure.</param>
        /// <param name="isStatic">True for a plain table of functions instead of a class.</param>
        public static bool RegisterClass(string name, ReadOnlySpan<LuaFunction> methods, LuaVal* values, out string error, bool isStatic = false)
        {
            LuaReg* regs = (LuaReg*)NativeMemory.AllocZeroed((nuint)(sizeof(LuaReg) * (methods.Length + 1)));
            for (int i = 0; i < methods.Length; i++)
            {
                regs[i].name = Utf8.Alloc(methods[i].Name);
                regs[i].func = methods[i].Function;
            }
            byte* err = null;
            int ok = Api->registerClass(Utf8.Alloc(name), regs, values, isStatic ? 1 : 0, &err);
            error = Utf8.ToString(err);
            return ok != 0;
        }

        /// <summary>
        /// Pushes a C# function onto the Lua stack (to return it, or before <see cref="CallFunction"/>).
        /// </summary>
        /// <param name="function">Static <c>[UnmanagedCallersOnly]</c> method.</param>
        public static void PushFunction(delegate* unmanaged[Cdecl]<void*, int> function) => Api->pushFunction(function);

        /// <summary>
        /// For a class's __index function: looks the key up in the metatable first. If it returns true the
        /// value is on the stack and __index should return 1; otherwise do your own lookup.
        /// </summary>
        public static bool IndexMetatable() => Api->indexMetatable() != 0;

        /// <summary>Stops the Lua run loop (playdate.update is no longer called).</summary>
        public static void Stop() => Api->stop();
        /// <summary>Restarts the Lua run loop.</summary>
        public static void Start() => Api->start();

        // ---- Arguments (1-based positions) ----

        /// <summary>Number of arguments passed to the current function.</summary>
        public static int ArgCount => Api->getArgCount();

        /// <summary>Type of argument <paramref name="pos"/> (1-based).</summary>
        /// <param name="pos">1-based argument position (1 = first; <c>self</c> for methods).</param>
        public static LuaType GetArgType(int pos) => Api->getArgType(pos, null);

        /// <summary>Type of argument <paramref name="pos"/>; for objects also their class name.</summary>
        /// <param name="pos">1-based argument position.</param>
        /// <param name="className">Receives the class name for objects, otherwise null.</param>
        public static LuaType GetArgType(int pos, out string className)
        {
            byte* cls = null;
            LuaType type = Api->getArgType(pos, &cls);
            className = Utf8.ToString(cls);
            return type;
        }

        /// <summary>True if argument <paramref name="pos"/> is nil or missing.</summary>
        /// <param name="pos">1-based argument position.</param>
        public static bool ArgIsNil(int pos) => Api->argIsNil(pos) != 0;
        /// <summary>Argument <paramref name="pos"/> as a bool.</summary>
        /// <param name="pos">1-based argument position.</param>
        public static bool GetArgBool(int pos) => Api->getArgBool(pos) != 0;
        /// <summary>Argument <paramref name="pos"/> as an int.</summary>
        /// <param name="pos">1-based argument position.</param>
        public static int GetArgInt(int pos) => Api->getArgInt(pos);
        /// <summary>Argument <paramref name="pos"/> as a float.</summary>
        /// <param name="pos">1-based argument position.</param>
        public static float GetArgFloat(int pos) => Api->getArgFloat(pos);
        /// <summary>Argument <paramref name="pos"/> as a string.</summary>
        /// <param name="pos">1-based argument position.</param>
        public static string GetArgString(int pos) => Utf8.ToString(Api->getArgString(pos));

        /// <summary>
        /// Argument <paramref name="pos"/> as raw bytes (strings may contain zeros). Valid only during the
        /// call.
        /// </summary>
        /// <param name="pos">1-based argument position.</param>
        public static ReadOnlySpan<byte> GetArgBytes(int pos)
        {
            nuint len;
            byte* p = Api->getArgBytes(pos, &len);
            return new ReadOnlySpan<byte>(p, (int)len);
        }

        /// <summary>Returns the native pointer stored in a Lua object of class <paramref name="type"/> (null if mismatched).</summary>
        /// <param name="pos">1-based argument position.</param>
        /// <param name="type">Expected class name; null is returned for other classes.</param>
        public static void* GetArgObject(int pos, string type) => Api->getArgObject(pos, Utf8.Arg(type), null);

        /// <summary>
        /// Like <see cref="GetArgObject(int, string)"/>, also returning the Lua userdata wrapper (for
        /// <see cref="RetainObject"/> and user values).
        /// </summary>
        /// <param name="pos">1-based argument position.</param>
        /// <param name="type">Expected class name.</param>
        /// <param name="userdataObject">Receives the Lua userdata wrapper.</param>
        public static void* GetArgObject(int pos, string type, out LuaUDObject* userdataObject)
        {
            LuaUDObject* ud = null;
            void* p = Api->getArgObject(pos, Utf8.Arg(type), &ud);
            userdataObject = ud;
            return p;
        }

        /// <summary>Returns a managed object previously pushed with <see cref="PushManagedObject"/>.</summary>
        /// <param name="pos">1-based argument position.</param>
        /// <param name="type">Class name it was pushed with.</param>
        /// <typeparam name="T">The object's C# type; null is returned for other types.</typeparam>
        public static T GetArgManagedObject<T>(int pos, string type) where T : class
        {
            void* p = GetArgObject(pos, type);
            return p == null ? null : GCHandle.FromIntPtr((IntPtr)p).Target as T;
        }

        /// <summary>
        /// Argument <paramref name="pos"/> as a bitmap (a playdate.graphics.image). Owned by Lua; don't
        /// dispose it.
        /// </summary>
        /// <param name="pos">1-based argument position.</param>
        public static Bitmap GetBitmap(int pos) => new Bitmap(Api->getBitmap(pos));
        /// <summary>
        /// Argument <paramref name="pos"/> as a native sprite (a playdate.graphics.sprite).
        /// </summary>
        /// <param name="pos">1-based argument position.</param>
        public static LCDSprite* GetSprite(int pos) => Api->getSprite(pos);

        // ---- Return values ----

        /// <summary>Pushes nil. Count it in your return value.</summary>
        public static void PushNil() => Api->pushNil();
        /// <summary>Pushes a boolean.</summary>
        /// <param name="value">The boolean.</param>
        public static void PushBool(bool value) => Api->pushBool(value ? 1 : 0);
        /// <summary>Pushes an integer.</summary>
        /// <param name="value">The integer.</param>
        public static void PushInt(int value) => Api->pushInt(value);
        /// <summary>Pushes a float.</summary>
        /// <param name="value">The number.</param>
        public static void PushFloat(float value) => Api->pushFloat(value);
        /// <summary>Pushes a string.</summary>
        /// <param name="value">The string (null pushes nil).</param>
        public static void PushString(string value) => Api->pushString(Utf8.Arg(value));

        /// <summary>Pushes bytes as a Lua string (may contain zeros).</summary>
        /// <param name="bytes">The bytes.</param>
        public static void PushBytes(ReadOnlySpan<byte> bytes)
        {
            fixed (byte* p = bytes)
                Api->pushBytes(p, (nuint)bytes.Length);
        }

        /// <summary>Pushes a bitmap as a playdate.graphics.image.</summary>
        /// <param name="bitmap">The image; Lua doesn't take ownership.</param>
        public static void PushBitmap(Bitmap bitmap) => Api->pushBitmap(bitmap.Handle);
        /// <summary>Pushes a native sprite as a playdate.graphics.sprite.</summary>
        /// <param name="sprite">A native sprite.</param>
        public static void PushSprite(LCDSprite* sprite) => Api->pushSprite(sprite);

        /// <summary>Pushes a native pointer as a Lua object of class <paramref name="type"/>.</summary>
        /// <param name="obj">Native pointer stored in the object.</param>
        /// <param name="type">
        /// A class registered with
        /// <see cref="RegisterClass(string, ReadOnlySpan{LuaFunction}, out string, bool)"/>.
        /// </param>
        /// <param name="nValues">Number of user-value slots to reserve, 0 or more.</param>
        public static LuaUDObject* PushObject(void* obj, string type, int nValues = 0) => Api->pushObject(obj, Utf8.Arg(type), nValues);

        /// <summary>
        /// Pushes a managed object as a Lua object. The object is kept alive by a GC handle until
        /// <see cref="ReleaseManagedObject"/> is called, typically from the class's <c>__gc</c> method.
        /// </summary>
        /// <param name="obj">The C# object (not null).</param>
        /// <param name="type">A registered class name.</param>
        /// <param name="nValues">Number of user-value slots to reserve, 0 or more.</param>
        public static LuaUDObject* PushManagedObject(object obj, string type, int nValues = 0)
            => Api->pushObject((void*)GCHandle.ToIntPtr(GCHandle.Alloc(obj)), Utf8.Arg(type), nValues);

        /// <summary>Frees the GC handle of a managed object at argument <paramref name="pos"/> (call from __gc).</summary>
        /// <param name="pos">1-based argument position (1 in __gc).</param>
        /// <param name="type">The class name.</param>
        public static void ReleaseManagedObject(int pos, string type)
        {
            void* p = GetArgObject(pos, type);
            if (p != null)
                GCHandle.FromIntPtr((IntPtr)p).Free();
        }

        /// <summary>
        /// Keeps a Lua object alive while C# holds on to it. Balance with <see cref="ReleaseObject"/>.
        /// </summary>
        /// <param name="obj">The userdata wrapper.</param>
        public static LuaUDObject* RetainObject(LuaUDObject* obj) => Api->retainObject(obj);
        /// <summary>Releases a retain from <see cref="RetainObject"/>.</summary>
        /// <param name="obj">The userdata wrapper.</param>
        public static void ReleaseObject(LuaUDObject* obj) => Api->releaseObject(obj);
        /// <summary>
        /// Stores the value on top of the stack in the object's user value <paramref name="slot"/> (1-based;
        /// the object needs nValues slots from <see cref="PushObject"/>).
        /// </summary>
        /// <param name="obj">The userdata wrapper.</param>
        /// <param name="slot">1-based slot, up to nValues.</param>
        public static void SetUserValue(LuaUDObject* obj, uint slot) => Api->setUserValue(obj, slot);
        /// <summary>
        /// Pushes the object's user value <paramref name="slot"/> and returns its stack position.
        /// </summary>
        /// <param name="obj">The userdata wrapper.</param>
        /// <param name="slot">1-based slot, up to nValues.</param>
        public static int GetUserValue(LuaUDObject* obj, uint slot) => Api->getUserValue(obj, slot);

        /// <summary>Calls a global Lua function with <paramref name="nargs"/> arguments already pushed.</summary>
        /// <param name="name">Global function name; dots allowed, e.g. "game.onScore".</param>
        /// <param name="nargs">Number of arguments already pushed, 0 or more.</param>
        /// <param name="error">Receives the Lua error on failure.</param>
        public static bool CallFunction(string name, int nargs, out string error)
        {
            byte* err = null;
            int ok = Api->callFunction(Utf8.Arg(name), nargs, &err);
            error = Utf8.ToString(err);
            return ok != 0;
        }
    }
}
