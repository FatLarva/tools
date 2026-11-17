# Structured Process generator

This directory contains an ordinary .NET solution. Unity ignores the `~` directory, including its source, build output and compiler tests. The generator targets .NET Standard 2.0 and Roslyn 4.3.0; the tests use .NET 8. The integration is tested in Unity 6000.6.

The generator has no runtime reference to Tools, Unity or Source Gen Builder. It recognizes the declaration contracts by Roslyn symbols. The runtime contracts live in the neighboring `Declarative` directory and compile normally as part of the Tools UPM package.

## Build without Source Gen Builder

From this directory:

```text
dotnet build Generator/Tools.StructuredProcess.Generator.csproj -c Release
dotnet test Tools.StructuredProcess.sln -c Release
```

Use an installed SDK, or the `dotnet` executable in the Unity Editor's `Data/DotNetSdk` directory when available. The first build restores NuGet dependencies.

The analyzer is written to `Generator/bin/Release/netstandard2.0/Tools.StructuredProcess.Generator.dll`. Copy this DLL to `../Declarative/Tools.StructuredProcess.Generator.dll`, preserving the destination `.meta`, then refresh Unity. The package ships this prebuilt DLL and importer configuration, so consumers do not need a builder or SDK merely to use processes.

For a new installation path, disable **Any Platform** and **Editor** in the DLL's Plugin Inspector and assign the `RoslynAnalyzer` asset label. Do not copy `Microsoft.CodeAnalysis*.dll` into Unity; the compiler supplies those dependencies. Unity scopes this analyzer to its assembly and consumers, so a process assembly must reference `Tools.StructuredProcess`.

## Build with Source Gen Builder

Create a generator profile with:

| Setting | Value |
| --- | --- |
| Source Root | `Packages/com.fatlarva.tools/Scripts/StructuredProcess/Generator~` |
| Dependencies | Empty; runtime contracts already compile from C# source |
| Generator / Csproj Path | `Generator/Tools.StructuredProcess.Generator.csproj` |
| Generator / Install Dll Path | `Packages/com.fatlarva.tools/Scripts/StructuredProcess/Declarative/Tools.StructuredProcess.Generator.dll` |
| Generator / Dll Type | `Analyzer` |

Add the profile to Source Gen Builder settings and build it. This rebuild destination requires a writable Tools checkout. With a read-only package cache, install the updated analyzer under Assets and avoid retaining two active copies; use an editable checkout when rebuilding the bundled analyzer.

The SkillTree development project includes this configured profile. Source Gen Builder only automates building/copying/importing the DLL; it is not a package dependency of the generator or process runtime.

## Tests

The .NET test project exercises Roslyn diagnostics, compiles generated code, and executes it. Parallel flow tests are linked from the Unity sample and compiled with the generator as an analyzer. Its test-only `Logging.Log` replacement allows runtime code to be tested without a Unity installation; production uses the existing Tools logger.

The **Structured Process Tests** UPM sample runs generated linear and parallel processes in Unity Edit Mode, including main-thread continuations, thread-pool step construction, concurrent readiness and cancellation that waits for every started step's cleanup. It does not depend on the examples sample.
