# Portable text typography and embedded font references

Source `TextBlock` measurement carries every `TextRunTypographyProperties`
enumeration through the shared native paragraph's OpenType feature inputs.
Capitals, numeral alignment/style, fractions, variants, East Asian language/width
and numeric swash/alternate values follow the existing `LineServicesRun` mappings.
The existing 32 explicit Boolean settings remain unchanged. Numeric alternate
indices keep their exact values; invalid enums and negative indices fail before
native paragraph publication. No source characters, font tables or glyphs are
rewritten to simulate a feature.

Each source run owns its complete feature range. A Normal run after a styled run
therefore restores its own typography, including when the paragraph default has
nondefault features. Native shaping, line layout and interaction still consume
the same retained generation. This does not select or qualify Display metrics.

Absolute `pack://application:,,,/` font references preserve their three authority
commas through family tokenization and normalization. Ordinary comma-delimited
fallbacks and doubled literal commas in paths/family names keep their existing
meaning, including previously escaped pack authorities. Absolute application
resources use the same resource admission as relative references resolved from
an application base URI. Other absolute pack containers and HTTP(S) locations
remain unsupported. Public `FontFamily.Source` is preserved.

Source controls cover the full enum inventory, exact alternate values, mixed
Normal runs, invalid inputs, all reported absolute URI shapes, fallback lists,
literal commas and relative/absolute identity. The real PresentationFramework
harness's `--text-properties-only` lane uses the native text provider to measure
and record source TextBlocks, compare original Inter tabular/proportional advances,
and resolve actual WPF Resource fonts in the entry and referenced assemblies.
Static and variable font streams must equal the original file bytes.
The same controls run before the normal native MIL host smoke; package/desktop
qualification remains part of the complete Build and release gates.
