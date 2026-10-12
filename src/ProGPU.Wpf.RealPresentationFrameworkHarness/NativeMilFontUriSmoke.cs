// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Reflection;

internal static class NativeMilFontUriSmoke
{
    // Public side-by-side source WPF binding only. Both assemblies contain real
    // WPF Resource items; every resolved font stream must match its original file.
    internal static void Run(Assembly framework, Assembly core, Assembly fontLibrary)
    {
        Type application = framework.GetType("System.Windows.Application", true)!;
        Assembly entry = (Assembly)application.GetProperty("ResourceAssembly")!.GetValue(null)!;
        if (entry != typeof(NativeMilFontUriSmoke).Assembly)
            throw new InvalidOperationException("Font URI controls require their original entry assembly.");
        Type glyphTypeface = core.GetType("System.Windows.Media.GlyphTypeface", true)!;
        Type fontFamily = core.GetType("System.Windows.Media.FontFamily", true)!;
        Type typeface = core.GetType("System.Windows.Media.Typeface", true)!;
        object normalStyle = core.GetType("System.Windows.FontStyles", true)!.GetProperty("Normal")!.GetValue(null)!;
        object normalWeight = core.GetType("System.Windows.FontWeights", true)!.GetProperty("Normal")!.GetValue(null)!;
        object normalStretch = core.GetType("System.Windows.FontStretches", true)!.GetProperty("Normal")!.GetValue(null)!;
        string libraryBase = "pack://application:,,,/" + fontLibrary.GetName().Name + ";component/";
        int count = 0;

        foreach (string file in new[] { "Inter-Medium.ttf", "InterVariable.ttf" })
        {
            string path = Path.Combine(AppContext.BaseDirectory, "CollapseFonts", file);
            byte[] original = File.ReadAllBytes(path);
            object originalGlyph = Activator.CreateInstance(glyphTypeface, new Uri(path))!;
            object names = glyphTypeface.GetProperty("FamilyNames")!.GetValue(originalGlyph)!;
            string family = ((IEnumerable)names.GetType().GetProperty("Values")!.GetValue(names)!).Cast<string>().First();
            string entryFolder = "pack://application:,,,/Fonts/#" + family;
            string libraryFolder = libraryBase + "Fonts/#" + family;
            var references = new (Uri? Base, string Source)[]
            {
                (null, entryFolder),
                (null, "pack://application:,,,/Fonts/" + file + "#" + family),
                (null, libraryFolder),
                (null, libraryBase + "Fonts/" + file + "#" + family),
                (new Uri("pack://application:,,,/"), "./Fonts/#" + family),
                (new Uri(libraryBase), "./Fonts/#" + family),
                (null, libraryFolder.Replace(":,,,/", ":,,,,,,/")),
                (null, libraryFolder + ", Arial")
            };
            foreach (var reference in references)
            {
                object resolvedFamily = reference.Base is null
                    ? Activator.CreateInstance(fontFamily, reference.Source)!
                    : Activator.CreateInstance(fontFamily, reference.Base, reference.Source)!;
                if (!Equals(reference.Source, fontFamily.GetProperty("Source")!.GetValue(resolvedFamily)))
                    throw new InvalidOperationException("Font resolution rewrote the source family reference.");
                object resolvedTypeface = Activator.CreateInstance(typeface,
                    resolvedFamily, normalStyle, normalWeight, normalStretch)!;
                object?[] output = [null];
                if (!Equals(true, typeface.GetMethod("TryGetGlyphTypeface")!.Invoke(resolvedTypeface, output)))
                    throw new InvalidOperationException("Embedded font did not resolve: " + reference.Source);
                object glyph = output[0]!;
                Uri uri = (Uri)glyphTypeface.GetProperty("FontUri")!.GetValue(glyph)!;
                if (uri.Scheme != "pack" || !uri.AbsoluteUri.EndsWith("/Fonts/" + file, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Embedded font selected a fallback or different resource: " + uri);
                using var stream = (Stream)glyphTypeface.GetMethod("GetFontStream")!.Invoke(glyph, null)!;
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                if (!buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)).SequenceEqual(original))
                    throw new InvalidOperationException("Embedded font stream differs from its original resource bytes.");
                count++;
            }
        }
        Console.WriteLine($"Native source font URI controls passed: {count} static/variable, entry/library, " +
            "folder/file, absolute/relative, escaped-authority and fallback-list references; exact original font bytes.");
    }
}
