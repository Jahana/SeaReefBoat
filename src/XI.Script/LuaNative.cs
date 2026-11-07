using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace XI.Script
{
    // The Lua 5.1 C API as LuaJIT exports it. The library is libluajit_64.dll (LuaJIT 2.1's lua51.dll under the C++
    // server's name), built by tools/build_luajit.sh from the official source at a pinned v2.1 commit, found in
    // LuaNative.LibraryDirectory or the usual DLL search path. The C API is LuaJIT's lua.h / lauxlib.h, unchanged since
    // the C++ server's 2022 build; x64 has one calling convention, so Cdecl only names it.
    public static unsafe class LuaNative
    {
        private const string Lib = "libluajit_64";

        public const int LUA_REGISTRYINDEX = -10000;
        public const int LUA_ENVIRONINDEX = -10001;
        public const int LUA_GLOBALSINDEX = -10002;
        public const int LUA_MULTRET = -1;

        public const int LUA_TNONE = -1, LUA_TNIL = 0, LUA_TBOOLEAN = 1, LUA_TLIGHTUSERDATA = 2, LUA_TNUMBER = 3,
                         LUA_TSTRING = 4, LUA_TTABLE = 5, LUA_TFUNCTION = 6, LUA_TUSERDATA = 7, LUA_TTHREAD = 8;

        public const int LUA_GCCOLLECT = 2, LUA_GCCOUNT = 3, LUA_GCSTEP = 5;

        public static int UpvalueIndex(int i) => LUA_GLOBALSINDEX - i;

        // where libluajit_64.dll lives (the server root); set before the first LuaState
        public static string LibraryDirectory;

        static LuaNative()
        {
            NativeLibrary.SetDllImportResolver(typeof(LuaNative).Assembly, (name, asm, path) =>
            {
                if (name != Lib) return IntPtr.Zero;
                if (LibraryDirectory != null)
                {
                    var full = Path.Combine(LibraryDirectory, Lib + ".dll");
                    if (File.Exists(full)) return NativeLibrary.Load(full);
                }
                return NativeLibrary.TryLoad(name, asm, path, out var h) ? h : IntPtr.Zero;
            });
        }

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr luaL_newstate();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_close(IntPtr L);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void luaL_openlibs(IntPtr L);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int luaL_loadbuffer(IntPtr L, byte* buff, nuint sz, byte* name);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int lua_pcall(IntPtr L, int nargs, int nresults, int errfunc);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int lua_gc(IntPtr L, int what, int data);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int lua_gettop(IntPtr L);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_settop(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_pushvalue(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_remove(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_insert(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int lua_checkstack(IntPtr L, int sz);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int lua_type(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern double lua_tonumber(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int lua_toboolean(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern byte* lua_tolstring(IntPtr L, int idx, out nuint len);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr lua_touserdata(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern nuint lua_objlen(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int lua_rawequal(IntPtr L, int a, int b);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_pushnil(IntPtr L);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_pushnumber(IntPtr L, double n);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_pushboolean(IntPtr L, int b);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_pushlstring(IntPtr L, byte* s, nuint len);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_pushcclosure(IntPtr L, delegate* unmanaged[Cdecl]<IntPtr, int> fn, int n);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_pushlightuserdata(IntPtr L, IntPtr p);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_gettable(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_getfield(IntPtr L, int idx, byte* k);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_rawget(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_rawgeti(IntPtr L, int idx, int n);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_createtable(IntPtr L, int narr, int nrec);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr lua_newuserdata(IntPtr L, nuint sz);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int lua_getmetatable(IntPtr L, int idx);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_settable(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_setfield(IntPtr L, int idx, byte* k);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_rawset(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void lua_rawseti(IntPtr L, int idx, int n);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int lua_setmetatable(IntPtr L, int idx);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int lua_next(IntPtr L, int idx);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int luaL_ref(IntPtr L, int t);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void luaL_unref(IntPtr L, int t, int r);
    }
}
