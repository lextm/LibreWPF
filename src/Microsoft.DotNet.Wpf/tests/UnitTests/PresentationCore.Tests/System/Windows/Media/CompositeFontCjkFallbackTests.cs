// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Globalization;
using System.Linq;
using MS.Internal.Shaping;
using System.Windows.Media.TextFormatting;

namespace System.Windows.Media;

// Locks the macOS CJK fallback contract of the four default composite fonts. The composite
// maps used to list only Windows families for most CJK ranges, so a request that reached
// them fell through to the base family and drew .notdef (tofu). This exercises the actual
// physical face resolution: for each default family, language and
// representative character it requires a real physical face, a real glyph (never .notdef)
// and a positive advance. The physical-face cases require macOS and the portable media
// backend; the map-order cases also protect the original Windows font priority.
[Collection("Sequential")]
public class CompositeFontCjkFallbackTests
{
    // The four default (system composite) font families.
    private static readonly string[] s_defaultFamilies =
    {
        "Global User Interface", "Global Monospace", "Global Sans Serif", "Global Serif"
    };

    private static readonly string[] s_languages = { "zh-Hans", "zh-Hant" };

    // U+3002 the common Chinese full stop; U+FE10 a vertical form (was missing entirely);
    // U+FF61 halfwidth ideographic full stop (its Traditional map was mislabeled); U+6587 a
    // basic Han ideograph baseline.
    private static readonly string[] s_characters = { "\u3002", "\uFE10", "\uFF61", "\u6587" };

    public static IEnumerable<object[]> Cases()
    {
        foreach (string family in s_defaultFamilies)
            foreach (string language in s_languages)
                foreach (string text in s_characters)
                    yield return new object[] { family, language, text };
    }

    [Theory]
    [InlineData("Global User Interface")]
    [InlineData("Global Monospace")]
    [InlineData("Global Sans Serif")]
    [InlineData("Global Serif")]
    public void HongKongFallbackRetainsItsSpecificMap(string familyName)
    {
        var maps = new FontFamily(familyName).FamilyMaps;
        var culture = CultureInfo.GetCultureInfo("zh-HK");
        var specificMaps = maps.Where(map => map.Language?.IetfLanguageTag == "zh-hk").ToArray();
        Assert.NotEmpty(specificMaps);

        // Lookup can change only at a map boundary. Check every overlapping range,
        // including supplementary characters, through the actual source matchers.
        int[] boundaries = maps.SelectMany(map => map.Ranges)
            .SelectMany(range => new[] { range.First, range.Last }).Distinct().ToArray();
        foreach (var specific in specificMaps)
        {
            foreach (int character in boundaries.Where(specific.InRange))
            {
                var selected = maps.First(map => map.InRange(character) &&
                    FontFamilyMap.MatchCulture(map.Language, culture));
                Assert.True(ReferenceEquals(specific, selected),
                    $"{familyName}/zh-HK U+{character:X}: '{specific.Target}' was shadowed by '{selected.Target}'.");
            }
        }
    }

    // The portable backend must be selected before WPF is constructed. Other platforms
    // have different installed font inventories and do not qualify macOS face selection.
    private sealed class MacOSPortableMediaTheoryAttribute : TheoryAttribute
    {
        public MacOSPortableMediaTheoryAttribute([System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0) : base(sourceFilePath, sourceLineNumber)
        {
            if (!OperatingSystem.IsMacOS())
                Skip = "Requires the macOS font collection.";
            else if (ProGPU.Wpf.Interop.PortableWpfRuntime.ConfiguredMediaBackend != ProGPU.Wpf.Interop.PortableWpfMediaBackend.Portable)
                Skip = "Requires a process initialized with portable media before WPF construction.";
        }
    }

    [MacOSPortableMediaTheory]
    [MemberData(nameof(Cases))]
    public void DefaultCompositeFontResolvesChineseFallbackToARealGlyph(string familyName, string language, string text)
    {
        var family = new FontFamily(familyName);
        var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var culture = CultureInfo.GetCultureInfo(language);

        // The face the composite fallback actually selects for this character.
        var cache = new GlyphingCache(16);
        var runs = new List<TextSpan<ScaledShapeTypeface>>();
        cache.GetPortableFontRuns(typeface, new CharacterBufferRange(text, 0, text.Length), culture, runs);
        var face = runs.Select(run => run.Value?.ShapeTypeface?.GlyphTypeface).FirstOrDefault(f => f != null);

        Assert.True(face != null, $"{familyName}/{language}: no physical face resolved for '{text}' (U+{(int)text[0]:X4}).");
        Assert.True(face!.HasCharacter(text[0]),
            $"{familyName}/{language}: '{face.FamilyNames.Values.First()}' has no glyph for U+{(int)text[0]:X4} (would draw .notdef).");

        // Require a real glyph with positive advance. This does not measure ink or
        // establish shaping, control layout or rendered-pixel parity.
        Assert.True(face.CharacterToGlyphMap.TryGetValue(text[0], out ushort glyph) && glyph != 0 &&
            face.AdvanceWidths.TryGetValue(glyph, out double advance) && advance > 0,
            $"{familyName}/{language}: '{text}' (U+{(int)text[0]:X4}) has no positive advance width.");

        // The selected map must prioritize the matching macOS script. Original Windows
        // families remain ahead of the added macOS fallbacks if installed on this host.
        var map = family.FamilyMaps.First(candidate => candidate.InRange(text[0]) &&
            FontFamilyMap.MatchCulture(candidate.Language, culture));
        string[] targets = map.Target.Split(',', StringSplitOptions.TrimEntries);
        string[] traditionalFamilies = { "PingFang TC", "PingFang HK", "Heiti TC", "Songti TC" };
        string[] simplifiedFamilies = { "PingFang SC", "Hiragino Sans GB", "Heiti SC", "Songti SC" };
        string[] expectedFamilies = language == "zh-Hant" ? traditionalFamilies : simplifiedFamilies;
        string[] otherFamilies = language == "zh-Hant" ? simplifiedFamilies : traditionalFamilies;
        int expectedPriority = Array.FindIndex(targets, expectedFamilies.Contains);
        int otherPriority = Array.FindIndex(targets, otherFamilies.Contains);
        Assert.True(expectedPriority >= 0 && (otherPriority < 0 || expectedPriority < otherPriority),
            $"{familyName}/{language}: '{map.Target}' does not prioritize the matching macOS script.");
        string names = string.Join(" | ", face.FamilyNames.Values);
        Assert.True(face.FamilyNames.Values.Any(name => targets.Contains(name, StringComparer.OrdinalIgnoreCase)),
            $"{familyName}/{language}: '{text}' resolved outside the selected map to '{names}'.");

        // A full control layout (TextBlock.DesiredSize.Width > 0) needs the real text-shaping
        // provider, which only a host process registers; the unit-test process has no such
        // provider. These assertions leave full control layout and pixel qualification
        // to a host integration run.
    }
}
