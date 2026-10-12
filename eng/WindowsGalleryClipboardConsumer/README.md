# Original Gallery clipboard-image acceptance

This opt-in Windows acceptance case hosts the original WPFGallery ClipboardPage,
its ViewModel, ControlExample/PageHeader templates and **unchanged pack-resource
PNG** from microsoft/WPF-Samples commit
`811d01e95c8c929e68539d698d0a0609e94fd185`. The twelve-file manifest includes the
upstream MIT license, original byte lengths, Git blob IDs and SHA256 values.
No substitute control, reconstructed image or copied clipboard handler is used.

The reported action in [issue117](https://github.com/wieslawsoltes/LibreWPF/issues/117)
is Copy Image → Paste Image → displayed result on Windows11/preview45. Current
main already includes the Windows OLE bitmap implementation and four real native
package contracts. This fixture covers the original page/resource action; it does
not establish the historical hang's cause or close the issue merely by compiling.

## Prepare or compile without running

The Python tools require Python3.11 or later. `prepare.py` performs no network requests, application launch, image decoding,
desktop operation or clipboard access. Supply locally retained **complete**
`gh run view --json databaseId,name,headSha,status,conclusion,jobs` receipts for
Build and Docs, the original artifact metadata from `gh api .../actions/artifacts/ID`,
and that exact downloaded package ZIP. Do not fetch one successful job's output
from a failed, canceled, incomplete or different-head Build.

The preparer requires at least all thirteen current Build jobs, every additional
reported Build job, and the Docs job to have succeeded, including the SDK bundle
upload. Docs remains a separate documentation check, never a package producer.
It binds the artifact's run/head/name
and SHA256 to the archive, checks source-package commit/version identities, and
uses a fresh private feed, SDK resolver cache, NuGet cache, CLI home and temporary
directory. Every build explicitly disables development-certificate generation,
global-tools PATH addition and CLI telemetry; these exact environment overrides
are retained in the receipt. It retains the original package bytes. Missing receipts, packages,
source files or a missing original job fail explicitly; there is no source
DLL substitution or automatic producer selection.

`--original-source` names an already staged directory with the twelve relative
paths in `source-manifest.json`. The existing reviewed original fixture is
`/Volumes/1TB-macOS/librewpf-gallery-clipboard-probe.k3C6Hsjz/original`.
Alternatively stage those exact paths from the pinned upstream commit (the MIT
LICENSE is from its repository root). Every file is verified before and after
copying; the asset must retain its original pack URI
`pack://application:,,,/Assets/ControlImages/Clipboard.png`.

Example **preparation command**, with explicit reviewed values substituted:

```text
python prepare.py --original-source <original-directory> --build-receipt <build.json> --docs-receipt <docs.json> --artifact-receipt <artifact.json> --package-archive <original.zip> --expected-head <40-hex-head> --expected-build <run-id> --expected-docs <run-id> --architecture arm64 --output <new-consumer-directory>
```

Adding `--build-only --dotnet <explicit-installed-dotnet>` compiles the real
installed-SDK consumer with its normal bootstrap, XAML compiler and original
CommunityToolkit8.2.2 source generator. The compiler SDK is pinned from the
repository's global.json. The build is single-node, has a300-second limit and
never executes its output. The explicit
`AppendRuntimeIdentifierToOutputPath=false` build property matches the verifier's
`bin/Release/net10.0-windows` directory while retaining the requested Windows RID.
It checks the actual native apphost architecture,
requested NativeMilWgpu mode, and exact package bytes for PresentationCore,
PresentationFramework, WindowsBase, WinCore, PresentationNative, bridge, interop
and native engine. `preparation.json` and `build.log` retain failures; even a
successful compile keeps `qualified:false` and `applicationExecuted:false`.

Build staging is separate from the existing shared Showcase gates. The preparer
does not change their gates, timeouts or product code.

## Disposable Windows CI execution

The opt-in `Original Gallery clipboard acceptance` workflow runs this same
consumer on a fresh GitHub-hosted Windows11 ARM64 runner. Supply the exact
qualified LibreWPF head and its whole successful Build and Docs run IDs. All
sixteen current Build jobs are required, including the three SDK bootstrap
hosts, and every additional reported job must succeed. It
checks out that head, fetches the original digest-bound package archive and the
twelve pinned upstream files, and invokes the same preparer and capture runner.
It never selects the latest successful job or substitutes source DLLs.

This workflow uses `--require-empty-ci-clipboard`. Both runner and host require
the repository's [GitHub-hosted Windows environment](https://docs.github.com/en/actions/reference/workflows-and-actions/variables); the host rejects a nonempty
clipboard without reading its contents. Its sequence must remain unchanged
from the empty preflight until the original copy action. Existing sequence
ownership checks still govern all subsequent writes and cleanup. This mode
does not authorize replacing a local or self-hosted machine's clipboard.

The original60-second deadline, whole-image visibility, foreground/window
ownership, unobstructed desktop capture, exact retained-image comparison and
original page handlers remain unchanged. Uploaded receipts and native captures
still require manual review against the original PNG. A completed probe keeps
`qualified:false`; routed button events do not establish native-pointer input
qualification. An unavailable desktop or changed clipboard fails the run.
Capture admission additionally proves the complete image rectangle is covered
by the actual physical monitors before and after BitBlt. A monitor bounding box
cannot hide an off-screen region or a gap between displays; changing monitor
geometry during the capture fails explicitly.

## Later execution requires explicit coordination and clipboard permission

Do **not** invoke the following during the implementation/compile-only phase.
The actual clipboard may contain user data. This case cannot preserve/restore all
OLE formats or delayed renderers; obtain explicit permission to replace its
contents before using the opt-in. It never logs previous contents or format names.

```text
python run-windows.py --app <verified-GalleryClipboardApp.exe> --output <fresh-evidence-directory> --allow-replace-clipboard
```

The real page is displayed in a source Window. The host raises routed Click on
the page's original Copy Image and Paste Image Buttons, invoking their unchanged
handlers (not OS pointer injection). It validates the decoded72×73 original,
all copied opaque RGB pixels, original status/visibility, a newer actual presented
frame and changed source revision. It brings the actual pasted Image into view.
Then it clears the clipboard only while its sequence number is still owned,
verifies retained/frozen image ownership, captures the displayed image again,
and republishes it through the original paste handler. External clipboard
mutation fails without clearing or overwriting that new state.

The existing Windows runner retains the original60-second total limit and
57-second host cleanup reserve. It checks actual HWND/PID/foreground ownership,
exact native/source client geometry without inferred scale/titlebar offsets,
the entire visible image rectangle, and before/after obstruction inventories.
It captures actual desktop pixels with BitBlt, never source pixels as a substitute.
Nonzero child exits, timeout, clipboard cleanup, image/geometry/ownership failures
and changed retained capture remain failures. Only the owned child is terminated.

**Rendered-result qualification remains explicit:** two nonuniform, equal RGB
captures are not an oracle for correct rendering. The original Image stretches
inside its real198×198 content area. A reviewer must inspect the retained actual
image-region captures against the original image before qualifying that result.
Receipts deliberately remain `qualified:false`; no DPI/alpha/Gallery-wide/native
input parity or issue closure is implied. Conservative obstruction checks can
reject transparent/shadow rectangles, and endpoint checks cannot prove the absence
of every intervening obstruction.

## Authored checks and current status

The current twenty-three offline admission tests pass, including the original
seventeen controls, all three required bootstrap jobs, empty-CI clipboard
selection and complete physical-monitor coverage. The updated C# host also
compiled against a retained whole-qualified producer with zero errors and six
original-source nullable warnings. These checks do not execute the application
or qualify clipboard or desktop behavior. The opt-in CI workflow still requires
a complete qualified producer and review of its original native captures.

The initial `test_contract.py` contained seventeen authored offline tests: the reused
native-geometry admission controls and new complete-producer rejection controls.
These were not app or clipboard validation and were not executed in the
initial compile-only phase. All three initial Python files were syntax-compiled
at commit14092e1a8 without importing/executing the fixture. The explicit RID-layout
command and fixed first-run environment subsequently completed both real installed
consumer builds; the offline source-contract tests themselves remain unexecuted.
The C# consumer compiled successfully for win-arm64 and win-x64 against the exact
PR217 package bundle, using `/usr/local/share/dotnet/dotnet` and SDK
11.0.100-preview.5.26302.115. ARM64 completed in18.92s, x64 in19.22s, each with
0 errors and6 unsuppressed warnings from three original-source nullable sites
emitted in both markup and final compilation. The original twelve files remained
unchanged; actual XAML compilation and the CommunityToolkit generator were used.
Each fresh private feed contains all23 unchanged producer packages, and all eight
required package/output asset comparisons, apphost PE architecture and native-mode
checks passed. The workload-verification advisory was retained without running
update/repair. These are PR217 compile receipts, **not PR218 qualification**.

The earlier first attempt
used the repository-local SDK11.0.100-preview.5 and its isolated CLI home emitted
the first-run development-certificate banner. The original log is retained; this
does not establish actual keychain installation. No trust/cleanup action was taken.
Both subsequent builds used the explicit first-run overrides above and emitted no
certificate-generation banner. The consumer has not been executed; no new desktop evidence is claimed. Original package CI
coverage remains separate from this exact Gallery acceptance case.

The initial identified qualified input was PR217 head
`45b1cffaa47c3c5e4eb7fe05c938c977886128aa`, whole Build36604077464 and
Docs36604077406. Its package artifact11053355676 is118464911 bytes with SHA256
`24c794bb80c975aad4b58a7c3072b3fb74e3c22d1e4359fc6648a691050d648d`.
The original archive was subsequently downloaded and its full byte length/digest
verified before staging the first compile-only attempt. Evidence is retained in
`/Volumes/1TB-macOS/librewpf-gallery-installed.a83ZsqZ0/`:

- `arm64-attempt1/`: earlier SDK-path/first-run banner, preserved unchanged.
- `arm64-isolated/preparation.json`: SHA256
  `9648e5a7ea01e56a9ede0cc251f938743aad78f8361ffd5f0c1dbed2a5645888`;
  `build.log`: `eb3d4de4f840164b769c2a0932d57f42265ee7cd3c681ac1034c69f777b09cbe`.
- `x64-isolated/preparation.json`: SHA256
  `1f722ab7770ff3c17e670547216f80b2470151f3a0fe3bb5f1d368a5966690ec`;
  `build.log`: `9da778c203da2f6f266e8e8654623e8ea7ebb4e1221e2ad56057c0d4dbf2d03a`.

Both final receipts retain `compiled:true`, `qualified:false`,
`applicationExecuted:false` and `clipboardAccessed:false`, with the exact compiler
command/environment and app/package hashes. No tests, runner, clipboard, captures,
image viewers or VM operations were executed during this compile-only preparation.

Revalidate it when staging. A later producer requires explicit
matching arguments and complete receipts, never an implicit latest-success lookup.
