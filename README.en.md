# PCL-CE-RegionPatch (English)

> Independent third-party tool. **Not affiliated with, authorized by, or endorsed by**
> PCL-Community or LTCat (龙腾猫跃). Please do not report issues caused by this tool to the
> upstream repositories.

**This repository contains no PCL source code, binaries, or credentials, and offers no
pre-patched downloads.** It provides a small command-line tool that applies a controlled,
local modification to a **user-owned** official PCL CE single-file executable, removing the
"unavailable in mainland China" gate.

Upstream credits:

- **Plain Craft Launcher 2 (PCL)** by **LTCat (龙腾猫跃)** — [repository](https://github.com/Meloong-Git/PCL) · [sponsor](https://afdian.com/a/LTCat)
- **PCL Community Edition (PCL CE)** maintained by **PCL-Community** — [repository](https://github.com/PCL-Community/PCL-CE)

## Background

PCL CE v2.15.1 (2026-10-20, PRs #3673 / #3676) disabled Minecraft downloads, installation,
file repair and modpack installation in mainland China. Upstream has an unmerged, reserved
rollback PR #3677.

This project is an external utility with the same effect, applied to the **binary** so that no
full .NET build environment is needed. It does not touch any official embedded credentials.

## What it changes

It rewrites two tiny IL method bodies in `PCL.RestrictedFeaturePolicy` (3 bytes each):

| Method | Before | After |
| --- | --- | --- |
| `Check(string, bool)` | region-based gate | `ldc.i4.1; ret` (always `true`) |
| `get_IsRestricted()` | region-based flag | `ldc.i4.0; ret` (always `false`) |

The output file has exactly the same size as the original (6 bytes differ in total). PE layout,
bundle content and embedded credentials are left untouched.

## What it is not

- Not a fork or a redistributable modified build of PCL CE.
- Does not ship any patched executable — patch your own official copy.
- Provides no warranty and does not guarantee compliance with your local laws.

## Requirements

- Windows 10 1809+ (the requirement of official PCL CE)
- An official **single-file** PCL CE build (tested with v2.15.1 x64)
- .NET 10 SDK to build the tool; .NET 10 Runtime to run it

## Build

```powershell
dotnet build src/PclCeRegionPatch/PclCeRegionPatch.csproj -c Release
```

## Usage

```powershell
# default output: <input>.patched.exe next to the input
pcl-ce-regionpatch PCL2_CE_Release_x64.exe

# explicit output
pcl-ce-regionpatch -o PCL2_CE_Release_x64_patched.exe PCL2_CE_Release_x64.exe

# check whether a file is already patched (exit code 0 = patched)
pcl-ce-regionpatch --check PCL2_CE_Release_x64_patched.exe
```

## How it works

PCL CE is published as a .NET **single-file** executable (apphost PE + appended bundle; the main
assembly `Plain Craft Launcher 2.dll` is embedded uncompressed). The tool:

1. locates the embedded assembly via metadata strings unique to it, validating the PE and requiring
   an `IL-only` image (ReadyToRun / non-.NET candidates are rejected);
2. maps method RVAs to file offsets through the PE section table;
3. uses [Mono.Cecil](https://github.com/jbevain/cecil) to find the two methods;
4. writes tiny method bodies in place (`0x0A, 0x17, 0x2A` / `0x0A, 0x16, 0x2A`);
5. re-reads and prints the resulting IL for verification.

## Verified / limitations

Verified against the official PCL CE v2.15.1 x64 build (23,486,024 bytes).

Limitations: only works on single-file builds whose embedded main assembly is IL-only; if upstream
switches to ReadyToRun, compressed bundles, or renames the type/methods, the tool fails loudly
instead of producing a broken file. The patch invalidates the official sigstore signature (expected).

## Safety

- Only obtain the original executable from the official release page.
- Never download or share "pre-patched / unlocked" executables from third parties.
- Do not upload patched binaries or official executables to this repository's issues/PRs.

## License & responsibility

The tool source code in this repository is licensed under the [MIT License](LICENSE). It contains no
PCL code or binaries. PCL and PCL CE are copyright their respective owners.

The upstream *PCL Distribution Limited License* only grants redistribution of **unmodified copies**,
so the patched output must **not** be redistributed. Using this tool to modify software and then
running the modified software is your own responsibility; you must comply with your local laws and
the upstream licenses.

## Disclaimer

Read [DISCLAIMER.md](DISCLAIMER.md) before use. This project is provided "AS IS", without warranty
of any kind.

## Related links

- Upstream reserved rollback PR: <https://github.com/PCL-Community/PCL-CE/pull/3677>
- Restriction PRs: <https://github.com/PCL-Community/PCL-CE/pull/3673> · <https://github.com/PCL-Community/PCL-CE/pull/3676>
- Another author's similar tool (inspiration): <https://github.com/zhicheng233/PCL2-RegionPatch>
