#!/usr/bin/env python3
"""Fast C# compile check without Unity (MASTER_PROMPT Part 3, "fast compile loop").

Generates SDK-style projects under Temp/fastcompile/ that mirror the Megastructure assemblies
(Runtime, Editor, Tests) with the reference list Unity wrote into Assembly-CSharp-Editor.csproj,
then builds them with the dotnet SDK that ships inside the Unity editor.
Runtime is compiled WITHOUT UNITY_EDITOR (player semantics) and without UnityEditor references.
This is a pre-filter only; the real compile gate is always a Unity batchmode compile.

Usage: python -I Tools/fastcompile.py [runtime|editor|tests|all]   (default all)
Exit code 0 = no errors.
"""
import glob
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOTNET = r"C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\DotNetSdk\dotnet.exe"
OUT = os.path.join(ROOT, "Temp", "fastcompile")


def read_refs():
    src = os.path.join(ROOT, "Assembly-CSharp-Editor.csproj")
    with open(src, "r", encoding="utf-8-sig") as f:
        text = f.read()
    refs = re.findall(r'<Reference Include="([^"]+)">\s*<HintPath>([^<]+)</HintPath>', text)
    defines = re.search(r"<DefineConstants>([^<]+)</DefineConstants>", text).group(1)
    refs = [(n, p if os.path.isabs(p) else os.path.join(ROOT, p)) for n, p in refs]
    return refs, defines


def is_editor_ref(name, path):
    p = path.replace("\\", "/").lower()
    n = name.lower()
    if "unityeditor" in n or n.endswith(".editor") or ".editor." in n or "editor" in os.path.basename(p).lower():
        return True
    if "/managed/unityeditor" in p:
        return True
    if "nunit" in n or "testrunner" in n or "testframework" in n:
        return None  # tests only
    return False


def write_proj(name, sources, refs, defines, project_refs=()):
    os.makedirs(os.path.join(OUT, name), exist_ok=True)
    lines = [
        '<Project Sdk="Microsoft.NET.Sdk">',
        "  <PropertyGroup>",
        "    <TargetFramework>netstandard2.1</TargetFramework>",
        "    <LangVersion>9.0</LangVersion>",
        "    <EnableDefaultItems>false</EnableDefaultItems>",
        "    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>",
        "    <NoStdLib>true</NoStdLib><NoConfig>true</NoConfig>",
        "    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>",
        "    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>",
        "    <NoWarn>0169;0649;0414;CS8632</NoWarn>",
        f"    <AssemblyName>Fast.{name}</AssemblyName>",
        f"    <DefineConstants>{defines}</DefineConstants>",
        "    <MSBuildWarningsAsMessages>MSB3277</MSBuildWarningsAsMessages>",
        "  </PropertyGroup>",
        "  <ItemGroup>",
    ]
    for s in sources:
        lines.append(f'    <Compile Include="{s}" />')
    lines.append("  </ItemGroup>")
    lines.append("  <ItemGroup>")
    for n, p in refs:
        lines.append(f'    <Reference Include="{n}"><HintPath>{p}</HintPath><Private>false</Private></Reference>')
    for pr in project_refs:
        lines.append(f'    <ProjectReference Include="..\\{pr}\\{pr}.csproj" />')
    lines.append("  </ItemGroup>")
    lines.append("</Project>")
    path = os.path.join(OUT, name, name + ".csproj")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    return path


def sources(*patterns):
    out = []
    for pat in patterns:
        out.extend(glob.glob(os.path.join(ROOT, pat), recursive=True))
    return sorted(set(out))


def build(path):
    cmd = [DOTNET, "build", path, "-nologo", "-v:q", "-clp:ErrorsOnly;NoSummary", "-p:WarningLevel=0"]
    env = dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en", VSLANG="1033", DOTNET_NOLOGO="1")
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env)
    out = (r.stdout or "") + (r.stderr or "")
    errs = [l for l in out.splitlines() if "error" in l.lower()]
    return r.returncode, errs


def main():
    which = sys.argv[1] if len(sys.argv) > 1 else "all"
    refs, defines = read_refs()
    rt_defines = ";".join(d for d in defines.split(";") if not d.startswith("UNITY_EDITOR") and d != "UNITY_INCLUDE_TESTS")
    rt_refs = [(n, p) for n, p in refs if is_editor_ref(n, p) is False and not n.startswith("Megastructure") and not n.startswith("Assembly-CSharp")]
    all_refs = [(n, p) for n, p in refs if not n.startswith("Megastructure") and not n.startswith("Assembly-CSharp")]
    rc = 0
    rt = write_proj("Runtime", sources("Assets/_Game/Scripts/Runtime/**/*.cs", "Assets/_Game/Beats/**/*.cs"), rt_refs, rt_defines)
    projects = []
    if which in ("runtime", "all", "editor", "tests"):
        projects.append(("Runtime", rt))
    if which in ("editor", "all"):
        projects.append(("Editor", write_proj("Editor", sources("Assets/_Game/Scripts/Editor/**/*.cs"), all_refs, defines, ["Runtime"])))
    if which in ("tests", "all"):
        if not any(n == "Editor" for n, _ in projects):
            projects.append(("Editor", write_proj("Editor", sources("Assets/_Game/Scripts/Editor/**/*.cs"), all_refs, defines, ["Runtime"])))
        projects.append(("Tests", write_proj("Tests", sources("Assets/_Game/Scripts/Tests/**/*.cs"), all_refs, defines, ["Runtime", "Editor"])))
    for name, path in projects:
        code, errs = build(path)
        seen = set()
        uniq = []
        for e in errs:
            k = re.sub(r"\[.*?\]$", "", e).strip()
            if k not in seen:
                seen.add(k)
                uniq.append(k.replace(ROOT + os.sep, ""))
        print(f"[fastcompile] {name}: {'OK' if code == 0 else 'FAILED'} ({len(uniq)} error lines)")
        for e in uniq[:60]:
            print("  " + e)
        if code != 0:
            rc = 1
    return rc


if __name__ == "__main__":
    sys.exit(main())
