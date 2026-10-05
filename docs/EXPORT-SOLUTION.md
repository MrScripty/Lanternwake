# Godot C# solution and partial export qualification

The official Godot 4.6.3 .NET data exporter requires `Lanternwake.sln` beside
the project. The accepted source at `852792c` had only a `.csproj`. An isolated
`--export-pack` run wrote a ZIP and returned 0 while logging missing-solution
C# errors. That artifact was rejected; process exit alone is not export acceptance.

The tracked solution now contains only `Lanternwake.csproj` and maps the Godot
SDK's actual `Debug`, `ExportDebug` and `ExportRelease` configurations to the
same project configurations. It adds no platform, runtime-version, story,
asset, save, provider or gameplay change. The .NET SDK generated the solution
structure; mappings follow the installed pinned Godot SDK, rather than the
generic solution template's inappropriate Release-to-Debug fallback.

All three solution builds and official Editor `--build-solutions` pass.
The exporter then reaches .NET publish. The offline Godot package feed alone
lacks the required Linux runtime packs; an initial publish diagnostic is retained.
The matching 8.0.31 packs restored from the normal official NuGet source using
SDK 8.0.425. No SDK upgrade, global network setting, credentials or template
download route is changed. A fresh official publish/data-only export then
finishes with zero warning/error lines.

The diagnostic data archive contains the exact canonical story JSON, mapped
scenes/resources and seven original imported audio samples. Its C# resource
entries are stubs; published managed/native runtime files are separate from
this data-only ZIP. Source/DLL/PDB bindings and the actual publish log qualify
the compilation. This is **not** a standalone executable or clean-machine
runtime receipt. The exact official 4.6.3 .NET player templates remain absent,
and the previously blocked template metadata route is not retried or bypassed.
The Linux preset exists only in an isolated diagnostic copy and is not a
supported-platform or distribution commitment.

```bash
dotnet restore Lanternwake.sln
dotnet build Lanternwake.sln --configuration Debug --no-restore
dotnet build Lanternwake.sln --configuration ExportDebug --no-restore
dotnet build Lanternwake.sln --configuration ExportRelease --no-restore
```

The coordinator must supply an approved exact template artifact or native export
environment for the next executable/exported-runtime milestone. Physical device,
human editorial/duration, loaded-model quality and speech retain their separate
acceptance gates. Parent owns PRs, review, integration, merge and release.
