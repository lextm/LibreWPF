# NativeAOT, accessibility and printing support

LibreWPF does not currently provide a qualified end-to-end NativeAOT WPF
application configuration. Its normal SDK/application validation runs managed
WPF assemblies on .NET. A reflection-free rendering bridge, a successful native
renderer test, or an assembly copied into a package does not establish full
application support.

This answers [#256](https://github.com/wieslawsoltes/LibreWPF/issues/256).
Implementation and application qualification remain tracked in
[#261](https://github.com/wieslawsoltes/LibreWPF/issues/261).

| Area | Current boundary |
| --- | --- |
| ProGPU native renderer | ProGPU's Build/release workflows publish and execute explicit NativeAOT package consumers. Those consumers exercise their recorded renderer/interop scenarios, not the complete WPF application stack. |
| LibreWPF application | Full XAML, binding, resources, controls, platform services and third-party application NativeAOT compatibility remain unqualified. Switching to `LibreWPF.Sdk` does not make these paths AOT-compatible automatically. |
| Accessibility | Managed automation assemblies and peer metadata exist. They do not qualify OS accessibility-provider publication, events, text patterns, focus or screen-reader behavior through a portable window host. |
| Printing | Public printing signatures remain available, with an incomplete portable runtime surface. Non-Windows `PrintDialog.ShowDialog()` returns `false`; `PrintVisual` and `PrintDocument` reject unsupported execution. |

## COM migration

The source retains upstream Windows COM interop. The presence of some generated
WinRT wrappers does not mean the remaining COM boundaries have been migrated.
Microsoft documents that NativeAOT excludes built-in COM and C++/CLI;
`ComWrappers` or generated COM interop provide an alternative for suitable
interfaces. Migration still requires matching interface identity, marshalling,
reference ownership, callbacks and apartment/thread behavior, rather than a
mechanical attribute replacement. See the
[NativeAOT limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment)
and [COM source-generation documentation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/comwrappers-source-generation).

There is a useful distinction in this repository's own dialog adapter:
[`ProcessWpfFileDialogService`](../src/ProGPU.Wpf/Platform/ProcessWpfFileDialogService.cs)
contains `ComImport` declarations inside a C# **source string** compiled by
PowerShell's `Add-Type` in a separate helper process for multi-folder selection.
These are not compiled COM declarations in the bridge assembly itself. Open/save
and ordinary folder selection also use the external PowerShell/WinForms route.
This helper dependency is not a self-contained AOT dialog implementation and
does not qualify a fully AOT-published WPF application.

The [roadmap](../roadmap.md#engineering-priorities) calls for typed or generated
metadata for trimming and NativeAOT. Delivery requires an inventory of the
reachable application/COM paths, bounded migrations of those paths, and full
publish-and-run application gates. No completion date is promised.

## UIAutomation and assistive technology

The source contains WPF's UIAutomation assemblies, control peers and Windows
provider code. Portable host and popup paths need their own native accessibility
integration; peer properties tested inside a managed process are only one part
of that contract. Current rendering/input or Showcase tests do not establish
Narrator, VoiceOver or Linux screen-reader compatibility.

Qualification must cover a real external client observing the provider tree,
stable element identity, focus and structure/property events, actions and text
patterns, popup/modal ownership, and provider retirement. Windows UIA and the
macOS/Linux accessibility bridges require separate implementation and evidence.
Applications that require accessibility should treat this as unfinished product
work, not infer support from the `UIAutomation*.dll` package entries.

## System.Printing

The portable transport includes reference contracts and API-cycle runtime stubs
to preserve WPF public signatures. It does not replace the complete upstream
C++/CLI printing implementation with a portable print engine. The current
[`PrintDialog`](../src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/PrintDialog.cs)
keeps non-Windows settings/page-range handling separate from unavailable native
print execution. Windows-specific paths remain dependent on their Windows
printing services and require their own qualification.

The [existing printing plan](../reports/progpu-wpf-progpu-branch-plan.md) keeps
the WPF API and moves printer discovery, settings/dialog UI, document delivery,
submission, cancellation and errors behind typed per-OS services. Keep unavailable
print commands explicit until those services exist; a stub or successful compile
must not be reported as a completed print job.

## Required delivery gates

- Publish and launch an ordinary SDK-switched WPF application with NativeAOT,
  preserving XAML, resources, bindings, templates, input and window lifetime;
  review every trimming/AOT warning and exercise reachable platform services.
- Migrate remaining required COM/platform boundaries with exact ABI, ownership,
  callback and thread controls, including the external dialog-helper dependency.
- Qualify accessibility using actual external assistive-technology clients on
  each supported OS, including text editing, popups, modality and teardown.
- Implement real printing services and verify discovery, settings, page output,
  cancellation and failure handling on each admitted platform.

These are open deliverables in #261. This documentation change does not admit
new NativeAOT, accessibility or printing capabilities.
