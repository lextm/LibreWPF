// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Windows.Media.ProGPU.Composition.Mil;

internal static class NativeMilTypographySmoke
{
    // Diagnostic-only public binding to the actual side-by-side source WPF
    // assemblies. Measurement and glyph recording use the real native provider.
    internal static void Run(Assembly framework, Assembly core, Assembly windowsBase)
    {
        Type textBlock = framework.GetType("System.Windows.Controls.TextBlock", true)!;
        Type typography = framework.GetType("System.Windows.Documents.Typography", true)!;
        Type fontFamily = core.GetType("System.Windows.Media.FontFamily", true)!;
        Type size = windowsBase.GetType("System.Windows.Size", true)!;
        Type rect = windowsBase.GetType("System.Windows.Rect", true)!;
        string font = Path.Combine(AppContext.BaseDirectory, "CollapseFonts", "Inter-Medium.ttf");
        if (!File.Exists(font)) throw new FileNotFoundException("Missing original typography font.", font);
        object family = Activator.CreateInstance(fontFamily, font + "#Inter")!;
        object constraint = Activator.CreateInstance(size, double.PositiveInfinity, double.PositiveInfinity)!;
        MethodInfo measure = textBlock.GetMethod("Measure", new[] { size })!;
        MethodInfo arrange = textBlock.GetMethod("Arrange", new[] { rect })!;
        var compiler = new WpfNativeMilSceneCompiler();
        int enumCases = 0;

        foreach (string property in new[] { "Capitals", "NumeralAlignment", "NumeralStyle", "Fraction",
            "Variants", "EastAsianWidths", "EastAsianLanguage" })
        {
            MethodInfo setter = typography.GetMethod("Set" + property)!;
            foreach (object value in Enum.GetValues(setter.GetParameters()[1].ParameterType))
            {
                object block = Create("Text 0123 1/2");
                setter.Invoke(null, new[] { block, value });
                RequireSourceGlyphs(block);
                enumCases++;
            }
        }
        foreach (string property in new[] { "AnnotationAlternates", "ContextualSwashes", "StylisticAlternates", "StandardSwashes" })
        {
            object block = Create("Text 0123 1/2");
            typography.GetMethod("Set" + property)!.Invoke(null, new[] { block, (object)3 });
            RequireSourceGlyphs(block);
        }

        MethodInfo alignmentSetter = typography.GetMethod("SetNumeralAlignment")!;
        Type alignment = alignmentSetter.GetParameters()[1].ParameterType;
        object tabular = Enum.Parse(alignment, "Tabular"), proportional = Enum.Parse(alignment, "Proportional");
        double[] tabularWidths = new double[10], proportionalWidths = new double[10];
        for (int digit = 0; digit != 10; digit++)
        {
            object block = Create(new string((char)('0' + digit), 3));
            double original = Measure(block).Width;
            alignmentSetter.Invoke(null, new[] { block, tabular });
            tabularWidths[digit] = Measure(block).Width;
            alignmentSetter.Invoke(null, new[] { block, proportional });
            proportionalWidths[digit] = Measure(block).Width;
            alignmentSetter.Invoke(null, new[] { block, Enum.Parse(alignment, "Normal") });
            if (Measure(block).Width != original)
                throw new InvalidOperationException("Restoring Normal retained a prior typography feature.");
        }
        if (tabularWidths.Distinct().Count() != 1 || proportionalWidths.Distinct().Count() <= 1)
            throw new InvalidOperationException("Original Inter digits did not preserve tabular/proportional native advances.");
        Console.WriteLine($"Native source typography passed: {enumCases} enum values, four numeric alternates, " +
            "source glyph recording, ten tabular/proportional digit controls and Normal invalidation.");
        Console.WriteLine("Tabular widths: " + string.Join(",", tabularWidths));
        Console.WriteLine("Proportional widths: " + string.Join(",", proportionalWidths));

        object Create(string text)
        {
            object block = Activator.CreateInstance(textBlock)!;
            textBlock.GetProperty("Text")!.SetValue(block, text);
            textBlock.GetProperty("FontFamily")!.SetValue(block, family);
            textBlock.GetProperty("FontSize")!.SetValue(block, 24.0);
            return block;
        }

        (double Width, double Height) Measure(object block)
        {
            measure.Invoke(block, new[] { constraint });
            object desired = textBlock.GetProperty("DesiredSize")!.GetValue(block)!;
            double width = (double)size.GetProperty("Width")!.GetValue(desired)!;
            double height = (double)size.GetProperty("Height")!.GetValue(desired)!;
            if (!(width > 0 && height > 0 && double.IsFinite(width) && double.IsFinite(height)))
                throw new InvalidOperationException("Typography produced invalid source measurement.");
            return (width, height);
        }

        void RequireSourceGlyphs(object block)
        {
            var bounds = Measure(block);
            arrange.Invoke(block, new[] { Activator.CreateInstance(rect, 0.0, 0.0, bounds.Width, bounds.Height) });
            var batch = compiler.BuildBatch(block, checked((uint)Math.Ceiling(bounds.Width)), checked((uint)Math.Ceiling(bounds.Height)));
            if (batch.GlyphRunFonts is not { Count: > 0 })
                throw new InvalidOperationException("Measured source typography did not record original glyph runs.");
        }
    }
}
