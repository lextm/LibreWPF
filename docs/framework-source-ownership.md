# Framework project ownership

LibreWPF owns `src/ProGPU.Wpf.Interop` and the two compile-time compatibility
projects under `src/ProGPU.Compatibility/WindowsBase` and
`src/ProGPU.Compatibility/PresentationCore`. They were moved from ProGPU without
changing their runtime implementation, assembly identities or signing key.
The original source history is retained at ProGPU commit
`3f1454a01dba3d6338c56fabfde7bf6a160c5c62`.
`LibreWPF.Interop` remains the public interop package. The compatibility projects
remain private build inputs; they do not replace the canonical WPF runtime under
`src/Microsoft.DotNet.Wpf`.

The projects select their rendering dependencies through `ProGpuSourceRoot`,
defaulting to the pinned `external/ProGPU` submodule. Core rendering and native
payload production remain in ProGPU. The coordinated producer can consume exact
LibreWPF and LibreWinForms source revisions without initializing their nested
submodules. This avoids a recursive repository dependency while retaining a
single authoritative implementation in each framework repository.

Portable `System.Drawing.Common` is owned by LibreWinForms under
`external/LibreWinForms/src/ProGPU.Drawing`. Its package and assembly identities
remain unchanged. WPF source tests and package construction use these explicit
source owners; published package consumers still resolve the same IDs and exact
versions.

The full producer Build and WPF source/package/application gates remain required.
A successful compatibility project build is not canonical WPF, native rendering,
input or application qualification. Preserve original source history and every
independent reference input when cleaning generated outputs.
