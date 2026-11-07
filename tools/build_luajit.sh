#!/bin/sh
# usage: sh tools/build_luajit.sh <work dir>   (from the csharp folder, in Git Bash; needs git and Visual Studio with
#                                               the C++ x64 tools)
#
# Builds libluajit_64.dll from the official LuaJIT source at the pinned commit below and puts it in the csharp folder.
# It clones https://github.com/LuaJIT/LuaJIT into <work dir>/LuaJIT (or reuses that clone), checks out the commit, and
# runs src/msvcbuild.bat (a dynamic release build, LJ_GC64 on: msvcbuild's x64 default) inside vcvars64.bat, found
# with vswhere. The DLL is lua51.dll under another name: the C++ server's tools/rename_dll.py only copies it and
# makes an import .lib from its exports, which the C# host does not need (it loads the DLL by name).
#
# The commit is on the v2.1 branch, from 2026-10-30. It has the ITERN fixes the 2022-01 build lacked: 1cdff194
# (LuaJIT #827, a blacklisted hot pairs() loop stops early or starts over), 32984282, dad04f17, 119fd1fa and
# 27f169c6. tests/XI.Verify.Lua checks #827's two reduced cases. The exports are the old DLL's but for the version
# symbol (luaJIT_version_2_1_0_beta3 is now luaJIT_version_2_1_<commit time>), which neither server imports.
# One visible change from the C++ server's build (a deliberate difference, user ruling 2026-11-05): LuaJIT's own
# number formatting rounds an exact decimal tie to even (7152e154, 2025-12; -348.125 with '%.2f' is -348.12, was
# -348.13). It reaches .., string.format's %f / %g / %e and %s of a number, table.concat of numbers and lua_tolstring
# of a number from C# (LuaState.ToStringValue / ToStringArg / OptString / Describe). The globals tostring, print and
# fmt are C# here (ScriptEngine, LuaFmt.FormatG), so they do not change. The one site a player could see is the GM
# text of scripts/commands/pos.lua (%.4f of a coordinate that is an odd multiple of 1/32). Its last commit before
# that, 45b771bb, has the 2022 build's formatting and the four #827 fixes, but not 27f169c6 (2026-08: a
# despecialized pairs() loop whose next() is compiled skips one array element, LuaJIT #1510).
# The DLL embeds the path of its PDB (in the work dir it was built in); that path means nothing elsewhere.
set -e
commit=ea9eb7edb5eac4a9d175d74523db34fac0fa5180
work="$1"
if [ -z "$work" ]; then echo "usage: sh tools/build_luajit.sh <work dir>" >&2; exit 2; fi
out="$(pwd)/libluajit_64.dll"
if [ ! -f "$(pwd)/XI.sln" ]; then echo "run it from the csharp folder" >&2; exit 2; fi
mkdir -p "$work"
src="$work/LuaJIT"
official=https://github.com/LuaJIT/LuaJIT.git
if [ ! -d "$src/.git" ]; then git clone "$official" "$src"; fi
# a reused clone must come from the official repository
origin="$(git -C "$src" remote get-url origin)"
if [ "$origin" != "$official" ] && [ "$origin" != "${official%.git}" ]; then
    echo "$src's origin is $origin, not $official" >&2
    exit 1
fi
git -C "$src" fetch -q origin
git -C "$src" checkout -q "$commit"
git -C "$src" clean -q -fdx
pf86="$(printenv 'ProgramFiles(x86)' || true)"
vswhere="$(cygpath -u "${pf86:-C:\Program Files (x86)}")/Microsoft Visual Studio/Installer/vswhere.exe"
vs="$("$vswhere" -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 \
      -property installationPath | tr -d '\r')"
if [ -z "$vs" ]; then echo "no Visual Studio with the C++ x64 tools" >&2; exit 1; fi
bat="$work/build_luajit.bat"
# msvcbuild runs minilua and buildvm from its own folder, so cmd must search the current folder for programs
printf '@echo off\r\nset NoDefaultCurrentDirectoryInExePath=\r\nset "PATH=%s;%%PATH%%"\r\n' \
    "$(cygpath -w "$(dirname "$vswhere")")" > "$bat"
printf 'call "%s\\VC\\Auxiliary\\Build\\vcvars64.bat" >nul\r\ncd /d "%s"\r\ncall msvcbuild.bat\r\n' \
    "$vs" "$(cygpath -w "$src/src")" >> "$bat"
cmd //c "$(cygpath -w "$bat")"
cp "$src/src/lua51.dll" "$out"
echo "built $out from LuaJIT $commit"
