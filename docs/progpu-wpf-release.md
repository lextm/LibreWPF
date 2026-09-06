# LibreWPF Preview Release Workflow

The LibreWPF preview release uses the package list in `eng/progpu-preview-package-list.sh`.
That package set is what users need to consume the custom `LibreWPF.Sdk` and run normal WPF
projects on the ProGPU/Silk.NET platform.

## NuGet Packages

- `LibreWPF.Transport`
- `ProGPU.Backend`
- `ProGPU.Backend.Native`
- `ProGPU.Native.Sdk`
- `ProGPU.Backend.Dawn`
- `ProGPU.Text.Shaping`
- `ProGPU.DirectX`
- `ProGPU.Transpiler`
- `ProGPU.Compute`
- `ProGPU.Vector`
- `ProGPU.Text`
- `ProGPU.Scene`
- `ProGPU.Scene.Native`
- `ProGPU.Layout`
- `ProGPU.Virtualization`
- `ProGPU.WinRT`
- `ProGPU.Media`
- `ProGPU.Media.Scene`
- `ProGPU.WinUI`
- `ProGPU.Avalonia`
- `ProGPU.SkiaSharp`
- `ProGPU.System.Drawing.Common`
- `LibreWPF.Interop`
- `LibreWPF.ProGPU`
- `LibreWPF.Sdk`

## Local Preview Build

```bash
PROGPU_WPF_DEV_PACKAGE_VERSION=0.1.0-preview.69 \
PROGPU_WPF_PROGPU_PACKAGE_VERSION="0.1.0-source.$(git -C external/ProGPU rev-parse --short=8 HEAD)" \
  ./eng/progpu-wpf-sdk-ci.sh
```

The SDK CI script stages ProGPU runtime packages, builds the managed WPF transport assemblies,
`LibreWPF.ProGPU`, and `LibreWPF.Sdk`, then audits the packages, writes the preview manifest,
creates a release bundle, verifies the bundle, and runs package-mode SDK smoke tests. Pull-request
builds assign the checked-out ProGPU submodule a commit-qualified `0.1.0-source.<sha>` version and
pack the complete ProGPU dependency closure from that exact source. This prevents a newer LibreWPF
assembly from loading an ABI-incompatible older ProGPU binary. The release workflow instead downloads
immutable ProGPU release packages for the matching `v<version>` tag and requires the tag commit to
equal the checked-out ProGPU submodule commit before packaging. The release job also stages the
native runtimes and SDK inputs from the exact `ProGPU.Backend.Native` /
`ProGPU.Native.Sdk` pair for source-host smoke tests; a
NuGet package in the feed alone does not populate the source-host runtime directory.
Both packages must match the release version and producer commit. Shared notices
must have identical bytes; failed extraction publishes no partial staging directory.
The complete package inventory is size-checked before any upload. Every
downloaded package is audited against that tag commit recorded in its nuspec.
Package audit and extracted-bundle verification also read every selected nuspec:
each `ProGPU.*` or `LibreWPF.*` dependency must be present at the same declared
version in that selected feed. These offline closure checks require Python 3 and
do not replace runtime payload/provenance checks or the clean package-mode SDK
restore and application smoke. Third-party dependencies still use the configured
NuGet sources. `ProGPU.Scene.Native` is required by the existing WPF bridge; it
must be packed/staged, snapshotted and bundled with the other exact ProGPU inputs.
For aligned tag releases, `progpu_version` defaults to the LibreWPF tag version;
an explicit override is reserved for a deliberate recovery publication. This
prevents an older workflow input default from silently mixing package versions.

Canonical WinForms packages built for a public LibreWPF release carry that same ProGPU
release version; their temporary source-built ProGPU dependencies are replaced by the
byte-exact published ProGPU packages before mixed WPF/WinForms package smoke.

### Package production before qualification

During implementation, package production can stop before running applications or
qualification scripts:

```bash
PROGPU_WPF_SERIAL_BUILD=1 ./eng/progpu-wpf-sdk-ci.sh --build-packages-only
./.dotnet/dotnet build samples/ProGPU.Wpf.ShowcaseApp/ProGPU.Wpf.ShowcaseApp.csproj -m:1 -p:ProGpuWpfRendererMode=NativeMilWgpu
```

This uses the same package builders, managed transport/theme graph and harness
compilation as the full gate. It does not execute the protocol verifier, Avalonia
consumer smoke, WPF harnesses, native host, package audits, release verifiers,
applications or tests. It produces no release manifest or release bundle, and a
successful exit means **package production only**, not qualified application
startup or renderer parity. The flag is command-line-only; the no-argument CI and
release workflows retain all existing gates, including Toolkit/AvalonDock, the
license-controlled Xceed lane and SciChart.

All normal pack-time payload requirements remain mandatory: stage the ProGPU
native runtimes/adapters for every required RID and the source-built Windows
managed/native payloads before packing. Missing payloads still fail packaging;
do not set runtime-validation bypass properties or substitute old artifacts to
claim exact-head packages. Prebuilt ProGPU packages can use the existing
`PROGPU_WPF_PREPACKAGED_PROGPU_DIR` input, subject to final provenance qualification.

Native payload preparation has its own explicit build-only modes:
`external/ProGPU/eng/build-progpu-native.sh --build-only` on a matching macOS/Linux
host, and `eng/build-progpu-native-windows.ps1 -Rid <win-x64|win-arm64> -BuildOnly`
inside a Windows ProGPU checkout. These compile both providers and the required
SDK payloads without running the native qualification scripts or executables.
On Linux, `--build-only --rid linux-x64` or `--build-only --rid linux-arm64`
selects an explicit target using Clang plus its target GNU toolchain, with a
separate default build directory and target-matched wgpu input/staging label.
Cross-compilation is not target runtime qualification and requires no emulator.
They do not produce Windows managed transport/IJW payloads or qualify the staged
files. Use ProGPU's required SDK when running from its checkout, and retain the
full package gate after freeze. See the
[native payload build contract](../external/ProGPU/docs/native-mil-build-only-payloads.md).

Produce Windows managed/IJW inputs in a Windows checkout using PowerShell 7:

```powershell
./eng/progpu-wpf-windows-managed-runtime.ps1
```

This restores/builds PresentationCore and DirectWriteForwarder for win-x86,
win-x64 and win-arm64 and stages their matching IJW hosts. It uses the repository's
pinned SDK, installing it locally when missing. SDK installation clears Arcade's
runtime-only default rather than passing the unsupported `-Runtime sdk` option.
The SDK build host is x64 even on ARM64 Windows, matching Arcade's pinned x64
runtime restoration into that root; all three output architectures remain.
An incompatible existing repository SDK host is rejected before installation.
Use a clean checkout or preserve/move that generated SDK directory explicitly;
do not overlay a different host architecture. Visual Studio MSBuild must meet
the minimum recorded in the selected SDK's bundled MSBuild information.
The current pinned SDK requires MSBuild 18.6 or newer; the source C++ props require
Visual Studio 2026's v145 toolset and Windows SDK 10.0.26100.0. Install C++/CLI
support and x86/x64/ARM64 compiler targets as well as the managed build tools.
The dotnet engine alone cannot build DirectWriteForwarder's Visual Studio C++
project; compiling PresentationBuildTasks does not satisfy that prerequisite.
Each Arcade restore/build runs in a child of the current PowerShell installation,
without requesting an execution-policy override. The existing host policy must
permit the trusted scripts; script rejection remains an explicit failure.
No user/machine policy change or legacy wrapper fallback is performed. This is
package input production, not Windows native SDK admission or application testing.

The script rebuilds its configured local package output and transport staging
directory just as the full gate does. Use a dedicated checkout/feed for isolated
development; packages from a dirty checkout are not exact-commit release evidence.
Do not run the Showcase launcher to obtain build-only behavior: it launches the app and
its automatic package rebuild uses the full SDK gate. After feature freeze, run
the normal no-argument gate on the delivery commits and record its full results.

The independent `eng/progpu-wpf-canonical-winforms-integration.sh` gate proves
the source-first path without replacing normal NuGet development. It checks that
LibreWinForms and LibreWPF pin the same ProGPU source, builds canonical
`System.Windows.Forms` with the ProGPU drawing implementation, materializes the
clean-cache WPF reference/cycle ordering, and compiles the real
`WindowsFormsIntegration` reference and implementation assemblies with assembly
conflicts promoted to errors. ProGPU API, correctness, and allocation gates are
owned by the matching ProGPU source PR and can also be enabled locally through
the script's default `PROGPU_WPF_RUN_DRAWING_QUALITY_GATES=1` behavior.
The drawing adapter's native backend dependency requires the complete native
runtime package, including Dawn. The canonical CI job stages its native payload
from the successful whole ProGPU Build for the exact source gitlink before
packing; it does not build native binaries or bypass runtime validation. Local
canonical packaging likewise requires that exact native payload to be staged.
The ProGPU `drawing-runtime` package group must include both native backend
packages; staging alone cannot repair an incomplete upstream group manifest.
The main SDK smoke downloads that canonical package closure and uses it for the
mixed WPF/WinForms application. It does not rebuild or reference the retired
`src/LibreWinForms.Portable` projects.

## GitHub Actions

- `LibreWPF Build` runs the canonical WinForms source gate on Linux and the SDK package/no-source-change smoke on macOS with submodules checked out.
- `LibreWPF Docs` verifies that this document and README stay aligned with the preview package list.
- `LibreWPF Release` rebuilds the immutable tagged source against the matching ProGPU GitHub release packages, verifies the complete release bundle and exact source/package provenance, runs the clean Windows AnyCPU package smoke, publishes to NuGet.org, and creates a GitHub Release with generated release notes. The `LibreWPF Build` artifact retains commit-qualified `source.<sha>` ProGPU packages for CI consumers and must not be promoted as a public release.
- Manual `LibreWPF Release` dispatch uses the same full SDK gate for recovery publication from an explicitly selected immutable ref.

## NuGet Publishing

Publishing is gated by repository secret `NUGET_API_KEY`.

- Manual workflow runs publish only when the `publish` input is true.
- Tags named `librewpf-v*` publish after validation.
- ProGPU and `LibreWPF.Interop` are published first by the ProGPU release. LibreWPF then publishes only `LibreWPF.Transport`, `LibreWPF.ProGPU`, and `LibreWPF.Sdk`; the offline bundle carries the hash-identical ProGPU release packages without republishing them.
- Tag runs create the matching GitHub Release with `gh release create --generate-notes` and attach the preview packages, manifest, bundle, checksum, README, and NuGet.config.

## SDK Switch Contract

Existing WPF applications should be able to switch only the project SDK:

```xml
<Project Sdk="LibreWPF.Sdk/0.1.0-preview.69">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
</Project>
```

No application source or XAML changes should be required for normal WPF code. Windows-specific interop,
unsupported DirectX features, and native-hosting edge cases remain tracked in `reports/`.

## Architecture neutrality of the transport assemblies

`LibreWPF.Transport` is an arch-neutral package (`PlatformIndependentPackage`), so whatever sits in
its `lib/net10.0` is what **every consumer without a RuntimeIdentifier loads, on every
architecture**. That makes the PE machine stamp of those assemblies part of the package contract,
not a build detail.

The managed transport projects are built per-`Platform` (`<Platforms>x86;x64;arm64</Platforms>`),
and by default that stamps each output with the platform it was built for. Because packaging picked
up whichever platform the machine last built, the published package worked on exactly **one**
architecture:

- an x64 host failed during startup with
  `Could not load file or assembly 'System.Windows.Controls.Ribbon'` — the copy in `lib/` carried an
  arm64 stamp, and **a managed assembly stamped for the wrong architecture will not load** (it is not
  "just metadata" that the runtime ignores);
- stamping them x64 instead moved the same failure onto arm64.

None of these projects has a single architecture-conditional `#if`, and the x64 and arm64 builds of
each come out byte-identical in length — `PlatformTarget` changes the PE header's machine field, not
the emitted IL. They are therefore pinned to `AnyCPU` in
`eng/WpfArcadeSdk/Sdk/Sdk.props` (`LibreWpfArchNeutralTransportAssemblies`, a deliberate whitelist),
plus `ProGPU.Wpf.Interop.csproj` for the interop assembly that ships in the same `lib/` but uses the
plain .NET SDK. One assembly then loads everywhere, and the per-RID payload under
`runtimes/<rid>/lib/net10.0/` carries only what is genuinely architecture-specific.

**What must stay per-RID:** `DirectWriteForwarder` and `System.Printing` are C++/CLI and cannot be
AnyCPU. `DirectWriteForwarder` ships for all three RIDs, and a RID-specific publish overlays the
right one (see the transport-payload sync in a consuming repo's packaging script). `lib/` still needs
a copy for non-RID builds, and that one is unavoidably the packaging machine's architecture — so
**cross-architecture consumers must publish with a RuntimeIdentifier**, not rely on `lib/`.

### Checking a package

Verify the PE machine field, not the file size — the wrong-architecture copies are the *same size*
as the right ones, which is exactly why this went unnoticed:

```powershell
# 0x014C = AnyCPU (good), 0x8664 = x64, 0xAA64 = arm64
$fs=[IO.File]::OpenRead($dll); $br=New-Object IO.BinaryReader($fs)
$fs.Position=0x3C; $o=$br.ReadInt32(); $fs.Position=$o+4; '0x{0:X4}' -f $br.ReadUInt16()
```

Expected for `lib/net10.0`: every managed assembly `0x014C`, with `DirectWriteForwarder` the sole
architecture-stamped exception.

### Two packaging traps found alongside this

- **Pack can run before the implementation exists.** One published package contained a 5 KB
  `PresentationFramework.dll` — the API-cycle stub from the bootstrap build — because packing
  happened an hour *before* the real 6 MB assembly was produced. A stub is a plausible-looking file
  of the wrong size; assert sizes, not just presence.
- **`eng/progpu-wpf-windows-managed-runtime.ps1` stages with `WARNING ... skipping`.** When build
  output is missing it warns and continues, so the script exits 0 having staged 2 of 22 assemblies.
  Treat a low staged count as a failure.
