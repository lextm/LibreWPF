#Requires -Version 7.0

param(
    [string] $Configuration = "Release",
    [ValidateSet("vs", "dotnet")]
    [string] $MSBuildEngine = "vs",
    [switch] $Rebuild,
    [switch] $NativeToolsOnMachine
)

$ErrorActionPreference = "Stop"
if (-not $IsWindows) {
    throw "Windows managed runtime production requires a Windows PowerShell 7 host."
}

# MSBuild's /m does not constrain cl.exe's own /MP workers, and the RID builds below drive
# DirectWriteForwarder through the same obj directory. Without this, several cl.exe processes
# write one .pdb and the build dies with "C1041: cannot open program database ... please use /FS".
# dist.local.sh exports the same thing; setting it here keeps a standalone run correct too.
# Two separate reasons System.Printing's precompiled header fails with "C3859: Failed to create
# virtual memory for PCH" / "C1076: compiler limit: internal heap limit reached", and both have to
# be handled or the error looks intermittent - it succeeded for one project and failed for the
# next in the same run.
#
# 1. Address space. MSBuild's VC targets still default PreferredToolArchitecture to x86, so a
#    64-bit machine gets the 32-bit cl.exe and this PCH does not fit in it. Use the host's own
#    64-bit toolset, rather than hardcoding x64, which would run emulated on ARM64.
# 2. Actual free memory, which is the one that makes it look random. This PCH wants a large
#    contiguous allocation, and Roslyn's persistent VBCSCompiler server had grown to ~1.6 GB by
#    the time the theme projects ran - on an 8 GB workstation that is the difference between
#    success and failure. UseSharedCompilation=false keeps the C# compilations in-process and
#    short-lived instead. It costs some C# throughput and buys the C++ step its memory back.
$preferredToolArchitecture = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "arm64" } else { "x64" }

# Shut down any server left over from an earlier build before starting; the flag above only stops
# new ones from being used.
Get-Process -Name VBCSCompiler -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }

# /FS is the one that actually matters: Arcade's build.ps1 drives MSBuild multi-process, so /MP1
# constrains each cl.exe but not how many of them MSBuild starts against the same DirectWriteForwarder
# obj directory. /FS serializes the .pdb writes, which is exactly what C1041 asks for.
if ([string]::IsNullOrWhiteSpace($env:CL) -or $env:CL -notmatch '/FS') {
    $env:CL = ($env:CL, "/MP1 /FS" | Where-Object { ![string]::IsNullOrWhiteSpace($_) }) -join " "
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildCommand = Join-Path $repoRoot "eng/common/build.ps1"
$buildPowerShell = Join-Path $PSHOME "pwsh.exe"
if (!(Test-Path $buildPowerShell -PathType Leaf)) {
    throw "The current PowerShell host has no child executable at $buildPowerShell."
}
# The per-RID build loop below resolves every project against $srcDir. It was never defined, so
# the very first Join-Path failed with "Cannot bind argument to parameter 'Path' because it is
# null" - AFTER the script had already deleted $outputDirectory, and, because a terminating error
# in a -File invocation still left the exit code at 0, without failing the dist.local.sh step that
# calls it. That combination is why artifacts/windows-managed-runtime sat weeks out of date while
# every packing run looked clean.
$srcDir = Join-Path $repoRoot "src/Microsoft.DotNet.Wpf/src"
$buildTasksProject = Join-Path $srcDir "PresentationBuildTasks/PresentationBuildTasks.csproj"
$project = Join-Path $srcDir "PresentationCore/PresentationCore.csproj"
$outputDirectory = Join-Path $repoRoot "artifacts/windows-managed-runtime"
$versionDetailsPath = Join-Path $repoRoot "eng/Version.Details.props"
$globalJsonPath = Join-Path $repoRoot "global.json"
$packagesDirectory = Join-Path $repoRoot ".packages"
$globalJson = Get-Content -Path $globalJsonPath -Raw | ConvertFrom-Json

function Test-X64DotNetHost([string] $path) {
    if (!(Test-Path $path -PathType Leaf)) { return $false }
    $stream = [System.IO.File]::OpenRead($path)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        if ($stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5a4d) { return $false }
        $stream.Position = 0x3c
        $peOffset = $reader.ReadUInt32()
        if ($peOffset -gt $stream.Length - 6) { return $false }
        $stream.Position = $peOffset
        return $reader.ReadUInt32() -eq 0x00004550 -and $reader.ReadUInt16() -eq 0x8664
    }
    finally {
        $stream.Dispose()
    }
}

function Initialize-BuildSdk {
    $sdkVersion = [string]$globalJson.sdk.version
    if ([string]::IsNullOrWhiteSpace($sdkVersion)) {
        throw "sdk.version is missing from $globalJsonPath."
    }

    # Arcade restores tools.runtimes dotnet/x64 into the SDK root, so an x64 build host is the
    # expected shape. It is not a hard requirement though: the compilation is done by whichever
    # MSBuild -MSBuildEngine selects (Visual Studio by default, which supplies the C++/CLI cross
    # tools), and every project here is built with an explicit RuntimeIdentifier, so the targets
    # do not depend on the host's own architecture. Verified by building PresentationCore and its
    # C++/CLI DirectWriteForwarder for win-arm64 from an ARM64 host.
    #
    # Refusing outright was actively harmful on an ARM64 workstation, whose .dotnet is ARM64: this
    # script is the ONLY producer of artifacts/windows-managed-runtime, the transport package takes
    # its per-RID PresentationCore/DirectWriteForwarder from there, and dist.local.sh kept packing
    # a snapshot that was weeks old. That went unnoticed for as long as it did only because the
    # consumer SDK used to flatten lib/ over the per-RID copy - once it correctly preferred
    # runtimes/<rid>, the stale PresentationCore won and the IDE died at startup with
    # "MissingMethodException: InputManager.get_UsesPortableInput()".
    #
    # So: prefer an x64 host when one is present, and warn rather than throw otherwise.
    $localDotnetHost = Join-Path $repoRoot ".dotnet/dotnet.exe"
    if ((Test-Path $localDotnetHost) -and !(Test-X64DotNetHost $localDotnetHost)) {
        Write-Warning "The repository .dotnet host is not x64. Building with $MSBuildEngine MSBuild and explicit RuntimeIdentifiers; the produced payload is still per-RID."
    }
    $sdkDirectory = Join-Path $repoRoot ".dotnet/sdk/$sdkVersion"
    if (!(Test-Path (Join-Path $sdkDirectory "Sdks/Microsoft.NET.Sdk/Sdk"))) {
        $sdkDirectory = $null
    }

    $dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($sdkDirectory) -and $null -ne $dotnetCommand -and
        (Test-X64DotNetHost $dotnetCommand.Source)) {
        Push-Location $repoRoot
        try {
            $effectiveSdkVersion = (& $dotnetCommand.Source --version 2>$null | Select-Object -Last 1)
            $sdkResolutionExitCode = $LASTEXITCODE
        }
        finally {
            Pop-Location
        }

        if ($sdkResolutionExitCode -eq 0 -and ![string]::IsNullOrWhiteSpace($effectiveSdkVersion)) {
            $sdkLine = & $dotnetCommand.Source --list-sdks |
                Where-Object { $_ -like "$effectiveSdkVersion *" } |
                Select-Object -Last 1
            if ($sdkLine -match '^\S+\s+\[(.+)\]$') {
                $candidate = Join-Path $Matches[1] $effectiveSdkVersion
                if (Test-Path (Join-Path $candidate "Sdks/Microsoft.NET.Sdk/Sdk")) {
                    $sdkDirectory = $candidate
                }
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($sdkDirectory)) {
        $dotnetInstall = Join-Path $repoRoot "eng/common/dotnet-install.ps1"
        # Arcade's wrapper defaults to a runtime-only install. An empty runtime
        # selects the SDK and omits -Runtime from the upstream installer call.
        & $dotnetInstall -version $sdkVersion -architecture x64 -runtime ''
        if ($LASTEXITCODE -ne 0) {
            throw "Installing the pinned .NET SDK $sdkVersion failed."
        }

        $sdkDirectory = Join-Path $repoRoot ".dotnet/sdk/$sdkVersion"
    }

    $sdkResolverPath = Join-Path $sdkDirectory "Sdks"
    if (!(Test-Path (Join-Path $sdkResolverPath "Microsoft.NET.Sdk/Sdk"))) {
        throw "The pinned .NET SDK resolver is missing from $sdkResolverPath."
    }

    $dotnetRoot = Split-Path -Parent (Split-Path -Parent $sdkDirectory)
    if (!(Test-Path (Join-Path $dotnetRoot "dotnet.exe") -PathType Leaf)) {
        throw "The selected SDK has no build host at $dotnetRoot."
    }
    $env:DOTNET_ROOT = $dotnetRoot
    # MSBuild.exe does not populate the CLI's DOTNET_HOST_PATH. SDK tasks
    # hosted out-of-process on .NET require the executable, not just DOTNET_ROOT
    # or MSBuildSDKsPath. Keep task hosting on the same SDK host that was selected
    # above, whatever its architecture - mixing hosts is what breaks task loading.
    $env:DOTNET_HOST_PATH = Join-Path $dotnetRoot "dotnet.exe"
    $env:PATH = "$dotnetRoot;$env:PATH"
    $env:MSBuildSDKsPath = $sdkResolverPath
    # PresentationCore does not consume SDK workloads. Visual Studio MSBuild
    # otherwise asks its own resolver for workload locator SDKs that are not
    # part of the standalone pinned SDK layout used by clean Build Tools VMs.
    $env:MSBuildEnableWorkloadResolver = "false"
}

Initialize-BuildSdk

Remove-Item -Path $outputDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

# PerlCommand is a misnomer inherited from upstream dotnet/wpf. This fork converted the generator
# scripts it prefixes to PowerShell - PresentationUI's ThemeGenerator.ps1 and the SDK's
# GenTraceStrings.ps1 are both `param(...)` PowerShell - and WpfArcadeSdk/Sdk/Sdk.props accordingly
# defaults the property to "pwsh -NoProfile -File". Passing a real perl interpreter here therefore
# OVERRODE a correct default with a wrong one, and the failure mode changed with whatever the
# machine had: Git for Windows' perl gave "exited with code 9009", the pinned Strawberry Perl gave
# 25 with "syntax error ... near ']['" - perl trying to parse PowerShell. Leave the SDK's default
# alone - the build invocation below passes no /p:PerlCommand at all.

$versionDetails = [xml](Get-Content -Path $versionDetailsPath -Raw)
$netCoreAppVersion = [string]($versionDetails.Project.PropertyGroup.MicrosoftNETCoreAppRefPackageVersion | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($netCoreAppVersion)) {
    throw "MicrosoftNETCoreAppRefPackageVersion is missing from $versionDetailsPath."
}

# win-x86 is deliberately absent. The pinned Microsoft.NETCore.App.Host.win-x86 pack cannot be
# restored from this repository's feed (indexed but 401 on content, so NuGet does not fall back),
# and without it DirectWriteForwarder fails to build for x86 with NETSDK1114 "Unable to find a
# .NET Core IJW host". The transport package no longer ships a runtimes/win-x86 folder either -
# a RID folder missing PresentationCore/DirectWriteForwarder is worse than no RID folder, because
# NuGet would select that incomplete set.
$runtimeIdentifiers = @("win-x64", "win-arm64")
$restoreRoot = Join-Path ([System.IO.Path]::GetTempPath()) "librewpf-ijw-host-$([guid]::NewGuid().ToString('N'))"
$restoreProject = Join-Path $restoreRoot "IjwHostRestore.csproj"
New-Item -ItemType Directory -Path $restoreRoot -Force | Out-Null
try {
    $packageDownloads = ($runtimeIdentifiers | ForEach-Object {
        "    <PackageDownload Include=`"Microsoft.NETCore.App.Host.$_`" Version=`"[$netCoreAppVersion]`" />"
    }) -join [Environment]::NewLine

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RestorePackagesPath>$packagesDirectory</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
$packageDownloads
  </ItemGroup>
</Project>
"@ | Set-Content -Path $restoreProject -Encoding utf8

    dotnet restore $restoreProject --configfile (Join-Path $repoRoot "NuGet.config") --force --no-cache
    if ($LASTEXITCODE -ne 0) {
        throw "Restoring the Windows IJW host packs failed."
    }
}
finally {
    Remove-Item -Path $restoreRoot -Recurse -Force -ErrorAction SilentlyContinue
}

function Invoke-WpfProjectBuild([string] $projectPath, [string] $platform, [string] $runtimeIdentifier, [string] $ijwHostSourcePath = "", [bool] $buildProjectReferences = $true) {
    # Compiler/SDK changes require real compilation, not reuse of assemblies
    # previously produced with missing analyzers. Use Arcade's scoped Rebuild
    # action without deleting unrelated checkout outputs or running tests.
    $buildAction = @("-restore", "-build")
    if ($Rebuild) {
        $buildAction = @("-restore", "-rebuild")
    }

    $runtimeIdentifierArgument = @()
    if (![string]::IsNullOrWhiteSpace($runtimeIdentifier)) {
        $runtimeIdentifierArgument = "/p:RuntimeIdentifier=$runtimeIdentifier"
    }

    # The theme leaves pass $false. They reference PresentationFramework, whose graph reaches the
    # C++/CLI System.Printing, so each of the seven re-entered that vcxproj and recompiled its
    # precompiled header - seven needless C++ builds whose peak memory is what actually broke the
    # run on an 8 GB machine (C3859/C1076). This loop already builds every dependency, in order,
    # before the themes, so there is nothing left for them to build.
    $projectReferencesArgument = @()
    if (-not $buildProjectReferences) {
        $projectReferencesArgument = "/p:BuildProjectReferences=false"
    }

    $ijwHostArgument = @()
    if (![string]::IsNullOrWhiteSpace($ijwHostSourcePath)) {
        $ijwHostArgument = "/p:IjwHostSourcePath=$ijwHostSourcePath"
    }

    $nativeToolsArgument = @()
    if ($NativeToolsOnMachine) {
        # This is an optimization for prepared build images only. Clean agents
        # and integration VMs must let Arcade restore the versions pinned by
        # global.json instead of depending on mutable machine-wide tools.
        $nativeToolsArgument = @("-nativeToolsOnMachine")
    }

    # Arcade exits its process; keep each build isolated and preserve the
    # current host's execution policy rather than requesting an override.
    # The repository's inherited Framework-hosted compiler cannot load the
    # pinned SDK's native-image analyzers on this cross-architecture lane.
    # Use its .NET compiler through DOTNET_HOST_PATH; VS still owns C++/CLI.
    # One node. Several of these projects reference PresentationCore and therefore re-enter
    # DirectWriteForwarder.vcxproj; built concurrently they fight over one obj directory and the
    # run dies on whichever file lost - C1041 on the .pdb, LNK1104 on the .dll, or MSB3061 failing
    # to delete the generated AssemblyAttributes.cpp. dist.local.sh passes -m:1 to its own MSBuild
    # invocations for the same reason.
    & $buildPowerShell -NoProfile -NonInteractive -File $buildCommand `
        $buildAction `
        -ci `
        /m:1 `
        -configuration $Configuration `
        -platform $platform `
        -projects $projectPath `
        -msbuildEngine $MSBuildEngine `
        $nativeToolsArgument `
        -excludeCIBinarylog `
        '-warnAsError:$false' `
        "/p:PreferredToolArchitecture=$preferredToolArchitecture" `
        /p:UseSharedCompilation=false `
        $projectReferencesArgument `
        $runtimeIdentifierArgument `
        $ijwHostArgument `
        /p:BuildWithNetFrameworkHostedCompiler=false `
        /p:RunNetFrameworkApiCompat=false `
        /p:RunRefApiCompat=false
    if ($LASTEXITCODE -ne 0) {
        throw "Building $projectPath for $platform failed."
    }
}

$buildTasksProject = Join-Path $srcDir "PresentationBuildTasks/PresentationBuildTasks.csproj"
Invoke-WpfProjectBuild $buildTasksProject "x86" ""

# All managed transport assemblies that must be built per-RID.
# PresentationCore is built first (with DirectWriteForwarder via project ref).
# The remaining top-level assemblies are built separately after PresentationCore.
$transportProjects = @(
    "PresentationCore/PresentationCore.csproj",
    "PresentationFramework/PresentationFramework.csproj",
    "PresentationUI/PresentationUI.csproj",
    "ReachFramework/ReachFramework.csproj",
    "System.Windows.Controls.Ribbon/System.Windows.Controls.Ribbon.csproj"
)

$themeProjects = @(
    "Themes/PresentationFramework.Aero/PresentationFramework.Aero.csproj",
    "Themes/PresentationFramework.Aero2/PresentationFramework.Aero2.csproj",
    "Themes/PresentationFramework.AeroLite/PresentationFramework.AeroLite.csproj",
    "Themes/PresentationFramework.Classic/PresentationFramework.Classic.csproj",
    "Themes/PresentationFramework.Fluent/PresentationFramework.Fluent.csproj",
    "Themes/PresentationFramework.Luna/PresentationFramework.Luna.csproj",
    "Themes/PresentationFramework.Royale/PresentationFramework.Royale.csproj"
)

# Assemblies produced by PresentationCore's dependency graph (built transitively).
# Collected from the build output so we don't re-build them.
$transitiveAssemblies = @(
    "WindowsBase",
    "System.Xaml",
    "System.Windows.Primitives",
    "System.Windows.Input.Manipulations",
    "System.Windows.Presentation",
    "UIAutomationProvider",
    "UIAutomationTypes",
    "System.Private.Windows.Core",
    "Microsoft.Win32.SystemEvents",
    "System.Printing"
)

$runtimePlatforms = [ordered]@{
    "win-x64" = "x64"
    "win-arm64" = "arm64"
}

foreach ($entry in $runtimePlatforms.GetEnumerator()) {
    $runtimeIdentifier = $entry.Key
    $platform = $entry.Value
    $ijwHost = Join-Path $packagesDirectory "microsoft.netcore.app.host.$runtimeIdentifier/$netCoreAppVersion/runtimes/$runtimeIdentifier/native/ijwhost.dll"
    if (!(Test-Path $ijwHost)) {
        throw "The $runtimeIdentifier IJW host was not restored at $ijwHost."
    }

    Write-Host "`n==> Building managed transport for $runtimeIdentifier ($platform)..."

    # Build PresentationCore (also builds DirectWriteForwarder + transitive dependencies)
    $presentationCoreProject = Join-Path $srcDir "PresentationCore/PresentationCore.csproj"
    Invoke-WpfProjectBuild $presentationCoreProject $platform $runtimeIdentifier $ijwHost

    # Every one of these reaches DirectWriteForwarder.vcxproj through PresentationCore, and that
    # vcxproj needs IjwHostSourcePath (the SDK's _GetIjwHostPaths errors with NETSDK1114 when it is
    # empty or missing). Passing it only to the PresentationCore build above left every later
    # project in the loop to fail on a project it merely references.
    foreach ($proj in $transportProjects) {
        if ($proj -like "PresentationCore/*") { continue }
        $projectPath = Join-Path $srcDir $proj
        Write-Host "  Building $proj..."
        Invoke-WpfProjectBuild $projectPath $platform $runtimeIdentifier $ijwHost
    }

    # Build theme assemblies. Their dependencies are all built above, so skip project references
    # and keep each of these a managed-only compile.
    foreach ($proj in $themeProjects) {
        $projectPath = Join-Path $srcDir $proj
        Write-Host "  Building $proj..."
        Invoke-WpfProjectBuild $projectPath $platform $runtimeIdentifier $ijwHost $false
    }

    # Stage the output: collect all built assemblies into the RID-specific payload directory.
    $runtimeOutput = Join-Path $outputDirectory "$runtimeIdentifier/net10.0"
    New-Item -ItemType Directory -Path $runtimeOutput -Force | Out-Null

    # Helper: copy a DLL from the build output to the runtime output.
    # Handles the different output path conventions (platform subfolder, RID suffix, etc.)
    function Copy-IfBuilt([string] $dllName, [string[]] $searchRoots) {
        foreach ($root in $searchRoots) {
            $candidates = @(
                (Join-Path $root "$platform/$Configuration/net10.0/$runtimeIdentifier/$dllName"),
                (Join-Path $root "$platform/$Configuration/net10.0/$dllName"),
                (Join-Path $root "$Configuration/net10.0/$dllName")
            )
            foreach ($candidate in $candidates) {
                if (Test-Path $candidate) {
                    Copy-Item $candidate (Join-Path $runtimeOutput $dllName) -Force
                    return $true
                }
            }
        }
        return $false
    }

    # Copy PresentationCore (with RID suffix in output path)
    $pcDll = Join-Path $repoRoot "artifacts/bin/PresentationCore/$platform/$Configuration/net10.0/$runtimeIdentifier/PresentationCore.dll"
    if (!(Test-Path $pcDll)) { throw "PresentationCore.dll not found for $runtimeIdentifier at $pcDll" }
    Copy-Item $pcDll (Join-Path $runtimeOutput "PresentationCore.dll") -Force

    # Copy DirectWriteForwarder (no RID suffix)
    $dwfRoot = Join-Path $repoRoot "artifacts/bin/DirectWriteForwarder"
    if ($platform -ne "x86") { $dwfRoot = Join-Path $dwfRoot $platform }
    $dwfDll = Join-Path $dwfRoot "$Configuration/net10.0/DirectWriteForwarder.dll"
    if (!(Test-Path $dwfDll)) { throw "DirectWriteForwarder.dll not found for $runtimeIdentifier at $dwfDll" }
    Copy-Item $dwfDll (Join-Path $runtimeOutput "DirectWriteForwarder.dll") -Force

    # Copy top-level transport assemblies
    foreach ($proj in $transportProjects) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($proj)
        if ($name -eq "PresentationCore") { continue }
        $searchRoots = @((Join-Path $repoRoot "artifacts/bin/$name"))
        if (!(Copy-IfBuilt "$name.dll" $searchRoots)) {
            Write-Host "  WARNING: $name.dll not found in build output, skipping."
        }
    }

    # Copy theme assemblies
    foreach ($proj in $themeProjects) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($proj)
        $searchRoots = @((Join-Path $repoRoot "artifacts/bin/$name"))
        if (!(Copy-IfBuilt "$name.dll" $searchRoots)) {
            Write-Host "  WARNING: $name.dll not found in build output, skipping."
        }
    }

    # Copy transitive dependency assemblies
    foreach ($name in $transitiveAssemblies) {
        $searchRoots = @((Join-Path $repoRoot "artifacts/bin/$name"))
        if (!(Copy-IfBuilt "$name.dll" $searchRoots)) {
            Write-Host "  WARNING: $name.dll not found in build output, skipping."
        }
    }

    # Copy native IJW host
    $nativeRuntimeOutput = Join-Path $outputDirectory "$runtimeIdentifier/native"
    New-Item -ItemType Directory -Path $nativeRuntimeOutput -Force | Out-Null
    Copy-Item $ijwHost (Join-Path $nativeRuntimeOutput "ijwhost.dll") -Force

    $stagedCount = (Get-ChildItem $runtimeOutput -Filter "*.dll").Count
    Write-Host "  Staged $stagedCount assemblies for $runtimeIdentifier"
}

Write-Host "`nStaged Windows managed runtime payload at $outputDirectory."
