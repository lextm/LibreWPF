# Source-built WPF path collection ABI

The bridge is compiled against the ProGPU PresentationCore compatibility assembly,
then runs with the original source-built WPF assembly in package applications.
The compatibility `PathFigureCollection` and `PathSegmentCollection` inherit
`List<T>`. Original WPF collections inherit `Animatable` and implement `IList<T>`.
Their object layouts are different despite the matching public collection names.

Path adaptation and rectangle construction therefore append through
`ICollection<PathFigure>` and `ICollection<PathSegment>`. Primitive classification,
cached line validation and native geometry cache keys read through `IList<T>`.
These are interfaces implemented by both assemblies. Indexed traversal, original
segment identities, fill/stroke flags, transforms and exact geometry tests remain
unchanged. Collection initializers or direct inherited `List<T>` members must not
cross this assembly substitution boundary. No reflection or replacement geometry
is needed.

## October 2026 integration validation

The original Fluent theme runtime harness reproduced SIGSEGV on macOS ARM64 with
.NET 11 preview 5. A diagnostic run under the CI runtime, .NET 11 RC1, reported
`List<PathSegment>.AddWithResize` inside portable geometry adaptation. The caller
had been compiled against the compatibility collection's inherited List method.

After interface dispatch was applied, the unchanged source-built Fluent harness
completed successfully under both exact runtimes. The same public interfaces are
used by the related path readers so subsequent replay does not access List fields.
The complete local bridge suite passed 2,805 tests without skips. Compiled IL
contains no `List<PathFigure>` or `List<PathSegment>` references; the collection
operations use the shared interfaces. Full SDK/package and native application
gates remain required for release; this
local source runtime check alone does not qualify other platforms or rendering.
