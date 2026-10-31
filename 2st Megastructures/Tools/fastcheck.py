"""Fast C# compile check for The Ring, without launching Unity.

Builds throw-away SDK-style projects under Temp/fastcheck against the Unity engine and package assemblies
referenced by the Unity-generated Assembly-CSharp-Editor.csproj, so builders can check their code in seconds
and concurrently (Unity itself is serialised through Tools/unity-queue.ps1). A real Unity batchmode compile is
always the final gate.

Usage:  python -I Tools/fastcheck.py [runtime|editor|all] [--only <folders relative to Assets/_Game> ...]
  runtime: Runtime + Beats code with player defines (no UNITY_EDITOR, no editor assemblies)
  editor : everything (Runtime, Beats, Editor, Tests) with editor defines and all assemblies
  all    : both (default)
Exit code 0 when every requested check compiles.
"""
import os
import re
import subprocess
import sys
import glob

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
GAME = os.path.join(ROOT, "Assets", "_Game")
OUT = os.path.join(ROOT, "Temp", "fastcheck")


def read(p):
    with open(p, "r", encoding="utf-8-sig") as f:
        return f.read()


def hint_paths(csproj):
    txt = read(csproj)
    out = []
    for m in re.finditer(r"<HintPath>([^<]+)</HintPath>", txt):
        p = m.group(1)
        if not os.path.isabs(p):
            p = os.path.join(ROOT, p)
        out.append(os.path.normpath(p))
    return out


def defines(csproj):
    m = re.search(r"<DefineConstants>([^<]*)</DefineConstants>", read(csproj))
    return m.group(1).split(";") if m else []


def asmdef_refs(kind):
    """Package assemblies our asmdefs actually reference (asmdefs do not see auto-referenced packages)."""
    import json
    names = set()
    paths = [os.path.join(GAME, "Scripts", "Runtime", "Megastructure.Runtime.asmdef")]
    if kind == "editor":
        paths += glob.glob(os.path.join(GAME, "Scripts", "Editor", "*.asmdef"))
        paths += glob.glob(os.path.join(GAME, "Scripts", "Tests", "**", "*.asmdef"), recursive=True)
    for ap in paths:
        try:
            with open(ap, "r", encoding="utf-8-sig") as f:
                d = json.load(f)
            names.update(d.get("references", []))
        except (OSError, ValueError):
            pass
    return names


def package_dlls(kind="editor"):
    sa = os.path.join(ROOT, "Library", "ScriptAssemblies")
    allowed = asmdef_refs(kind)
    res = []
    for p in glob.glob(os.path.join(sa, "*.dll")):
        n = os.path.basename(p)
        if n.startswith("Assembly-CSharp") or n.startswith("Megastructure"):
            continue
        if os.path.splitext(n)[0] not in allowed:
            continue
        res.append(p)
    return res


def is_editor_dll(p):
    n = os.path.basename(p)
    low = p.lower()
    return ("editor" in n.lower()) or ("\\managed\\unityeditor" in low) or n.startswith("UnityEditor") or "nunit" in low or "testrunner" in low or "TestFramework" in n


ONLY = []   # optional folder filters relative to Assets/_Game (Core and Editor/Contracts are always included)


def sources(kind):
    files = []
    if ONLY:
        subs = ["Scripts/Runtime/Core"] + [o for o in ONLY if kind == "editor" or not (o.startswith("Scripts/Editor") or o.startswith("Scripts/Tests"))]
        if kind == "editor":
            subs.append("Scripts/Editor/Contracts")
    else:
        subs = ["Scripts/Runtime", "Beats"] + (["Scripts/Editor", "Scripts/Tests"] if kind == "editor" else [])
    for sub in subs:
        full = os.path.join(GAME, sub)
        if os.path.isfile(full):
            files.append(full)
        else:
            files += glob.glob(os.path.join(full, "**", "*.cs"), recursive=True)
    return sorted(set(os.path.normpath(f) for f in files))


def make_proj(kind):
    base = os.path.join(ROOT, "Assembly-CSharp-Editor.csproj")
    if not os.path.exists(base):
        print("fastcheck: Assembly-CSharp-Editor.csproj missing (open the project once or run SyncSolution)")
        sys.exit(2)
    refs = [r for r in hint_paths(base) if "ScriptAssemblies" not in r and "PackageCache" not in r or "nunit" in r.lower()] + package_dlls(kind)
    defs = defines(os.path.join(ROOT, "Assembly-CSharp.csproj")) if os.path.exists(os.path.join(ROOT, "Assembly-CSharp.csproj")) else defines(base)
    if kind == "runtime":
        refs = [r for r in refs if not is_editor_dll(r)]
        defs = [d for d in defs if not d.startswith("UNITY_EDITOR") and d not in ("UNITY_INCLUDE_TESTS",)]
    else:
        defs = defines(base)
    seen = set()
    uniq = []
    for r in refs:
        k = os.path.basename(r).lower()
        if k in seen or not os.path.exists(r):
            continue
        seen.add(k)
        uniq.append(r)
    src = sources(kind)
    d = os.path.join(OUT, kind + ("_" + str(abs(hash(tuple(ONLY))) % 100000) if ONLY else ""))
    os.makedirs(d, exist_ok=True)
    lines = [
        '<Project Sdk="Microsoft.NET.Sdk">',
        "  <PropertyGroup>",
        "    <TargetFramework>netstandard2.1</TargetFramework>",
        "    <LangVersion>9.0</LangVersion>",
        "    <EnableDefaultItems>false</EnableDefaultItems>",
        "    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>",
        "    <NoStdLib>true</NoStdLib><NoConfig>true</NoConfig><DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>",
        "    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>",
        "    <NoWarn>0169;0414;0649;CS8632;USG0001</NoWarn>",
        "    <Nullable>disable</Nullable>",
        "    <DefineConstants>" + ";".join(defs) + "</DefineConstants>",
        "    <OutputPath>bin</OutputPath><BaseIntermediateOutputPath>obj\\</BaseIntermediateOutputPath>",
        "  </PropertyGroup>",
        "  <ItemGroup>",
    ]
    for s in src:
        lines.append('    <Compile Include="%s" />' % s)
    lines.append("  </ItemGroup>")
    lines.append("  <ItemGroup>")
    for r in uniq:
        name = os.path.splitext(os.path.basename(r))[0]
        lines.append('    <Reference Include="%s"><HintPath>%s</HintPath><Private>false</Private></Reference>' % (name, r))
    lines.append("  </ItemGroup>")
    lines.append("</Project>")
    p = os.path.join(d, "fastcheck_%s.csproj" % kind)
    with open(p, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    return p, len(src)


def run(kind):
    proj, n = make_proj(kind)
    if n == 0:
        print("fastcheck[%s]: no sources" % kind)
        return True
    r = subprocess.run(["dotnet", "build", proj, "-nologo", "-v", "q", "-clp:NoSummary;ErrorsOnly", "/p:WarningLevel=0"],
                       capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=os.path.dirname(proj))
    out = (r.stdout or "") + (r.stderr or "")
    errs = sorted(set(l.strip() for l in out.splitlines() if " error " in l or ": error" in l))
    if r.returncode == 0:
        print("fastcheck[%s]: OK (%d files)" % (kind, n))
        return True
    print("fastcheck[%s]: FAILED (%d files)" % (kind, n))
    for e in errs[:120]:
        e = e.replace(ROOT + os.sep, "")
        e = re.sub(r"\s*\[[^\]]*fastcheck_[a-z]+\.csproj\]$", "", e)
        print("  " + e)
    if not errs:
        print(out[-4000:])
    return False


if __name__ == "__main__":
    argv = sys.argv[1:]
    if "--only" in argv:
        k = argv.index("--only")
        ONLY.extend(a.replace("\\", "/").strip("/") for a in argv[k + 1:])
        argv = argv[:k]
    kind = argv[0] if argv else "all"
    kinds = ["runtime", "editor"] if kind == "all" else [kind]
    ok = all([run(k) for k in kinds])
    sys.exit(0 if ok else 1)
