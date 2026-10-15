using System;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>A Lua-callable function: <c>int f(lua_State* L)</c>, returning the number of values pushed.</summary>
    public readonly unsafe struct LuaFunction
    {
        public readonly string Name;
        public readonly delegate* unmanaged[Cdecl]<void*, int> Function;

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

        public static void PushFunction(delegate* unmanaged[Cdecl]<void*, int> function) => Api->pushFunction(function);

        /// <summary>For __index metamethods: pushes the metatable entry for the key if there is one (returns true).</summary>
        public static bool IndexMetatable() => Api->indexMetatable() != 0;

        public static void Stop() => Api->stop();
        public static void Start() => Api->start();

        // ---- Arguments (1-based positions) ----

        public static int ArgCount => Api->getArgCount();

        public static LuaType GetArgType(int pos) => Api->getArgType(pos, null);

        public static LuaType GetArgType(int pos, out string className)
        {
            byte* cls = null;
            LuaType type = Api->getArgType(pos, &cls);
            className = Utf8.ToString(cls);
            return type;
        }

        public static bool ArgIsNil(int pos) => Api->argIsNil(pos) != 0;
        public static bool GetArgBool(int pos) => Api->getArgBool(pos) != 0;
        public static int GetArgInt(int pos) => Api->getArgInt(pos);
        public static float GetArgFloat(int pos) => Api->getArgFloat(pos);
        public static string GetArgString(int pos) => Utf8.ToString(Api->getArgString(pos));

        public static ReadOnlySpan<byte> GetArgBytes(int pos)
        {
            nuint len;
            byte* p = Api->getArgBytes(pos, &len);
            return new ReadOnlySpan<byte>(p, (int)len);
        }

        /// <summary>Returns the native pointer stored in a Lua object of class <paramref name="type"/> (null if mismatched).</summary>
        public static void* GetArgObject(int pos, string type) => Api->getArgObject(pos, Utf8.Arg(type), null);

        public static void* GetArgObject(int pos, string type, out LuaUDObject* userdataObject)
        {
            LuaUDObject* ud = null;
            void* p = Api->getArgObject(pos, Utf8.Arg(type), &ud);
            userdataObject = ud;
            return p;
        }

        /// <summary>Returns a managed object previously pushed with <see cref="PushManagedObject"/>.</summary>
        public static T GetArgManagedObject<T>(int pos, string type) where T : class
        {
            void* p = GetArgObject(pos, type);
            return p == null ? null : GCHandle.FromIntPtr((IntPtr)p).Target as T;
        }

        public static Bitmap GetBitmap(int pos) => new Bitmap(Api->getBitmap(pos));
        public static LCDSprite* GetSprite(int pos) => Api->getSprite(pos);

        // ---- Return values ----

        public static void PushNil() => Api->pushNil();
        public static void PushBool(bool value) => Api->pushBool(value ? 1 : 0);
        public static void PushInt(int value) => Api->pushInt(value);
        public static void PushFloat(float value) => Api->pushFloat(value);
        public static void PushString(string value) => Api->pushString(Utf8.Arg(value));

        public static void PushBytes(ReadOnlySpan<byte> bytes)
        {
            fixed (byte* p = bytes)
                Api->pushBytes(p, (nuint)bytes.Length);
        }

        public static void PushBitmap(Bitmap bitmap) => Api->pushBitmap(bitmap.Handle);
        public static void PushSprite(LCDSprite* sprite) => Api->pushSprite(sprite);

        /// <summary>Pushes a native pointer as a Lua object of class <paramref name="type"/>.</summary>
        public static LuaUDObject* PushObject(void* obj, string type, int nValues = 0) => Api->pushObject(obj, Utf8.Arg(type), nValues);

        /// <summary>
        /// Pushes a managed object as a Lua object. The object is kept alive by a GC handle until
        /// <see cref="ReleaseManagedObject"/> is called, typically from the class's <c>__gc</c> method.
        /// </summary>
        public static LuaUDObject* PushManagedObject(object obj, string type, int nValues = 0)
            => Api->pushObject((void*)GCHandle.ToIntPtr(GCHandle.Alloc(obj)), Utf8.Arg(type), nValues);

        /// <summary>Frees the GC handle of a managed object at argument <paramref name="pos"/> (call from __gc).</summary>
        public static void ReleaseManagedObject(int pos, string type)
        {
            void* p = GetArgObject(pos, type);
            if (p != null)
                GCHandle.FromIntPtr((IntPtr)p).Free();
        }

        public static LuaUDObject* RetainObject(LuaUDObject* obj) => Api->retainObject(obj);
        public static void ReleaseObject(LuaUDObject* obj) => Api->releaseObject(obj);
        public static void SetUserValue(LuaUDObject* obj, uint slot) => Api->setUserValue(obj, slot);
        public static int GetUserValue(LuaUDObject* obj, uint slot) => Api->getUserValue(obj, slot);

        /// <summary>Calls a global Lua function with <paramref name="nargs"/> arguments already pushed.</summary>
        public static bool CallFunction(string name, int nargs, out string error)
        {
            byte* err = null;
            int ok = Api->callFunction(Utf8.Arg(name), nargs, &err);
            error = Utf8.ToString(err);
            return ok != 0;
        }
    }
}
