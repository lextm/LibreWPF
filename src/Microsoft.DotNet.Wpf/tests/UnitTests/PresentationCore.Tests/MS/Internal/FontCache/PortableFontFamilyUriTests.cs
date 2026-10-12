// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MS.Internal.FontFace;
using System.Windows.Navigation;

namespace MS.Internal.FontCache.Tests;

public sealed class PortableFontFamilyUriTests
{
    [Theory]
    [InlineData("pack://application:,,,/Fonts/#Roboto Condensed", "pack://application:,,,/Fonts/", "ROBOTO CONDENSED")]
    [InlineData("pack://application:,,,/FontLib;component/Fonts/#Roboto Condensed", "pack://application:,,,/FontLib;component/Fonts/", "ROBOTO CONDENSED")]
    [InlineData("pack://application:,,,/FontLib;component/Fonts/RobotoCondensed-Regular.ttf#Roboto Condensed", "pack://application:,,,/FontLib;component/Fonts/RobotoCondensed-Regular.ttf", "ROBOTO CONDENSED")]
    [InlineData("pack://application:,,,/FontLib;component/Fonts/#Inter Variable Text", "pack://application:,,,/FontLib;component/Fonts/", "INTER VARIABLE TEXT")]
    [InlineData("  PACK://APPLICATION:,,,/FontLib;component/Fonts/#Inter  ", "pack://APPLICATION:,,,/FontLib;component/Fonts/", "INTER")]
    [InlineData("pack://application:,,,/FontLib;component/Fonts/Face,,One.ttf#Family,, One", "pack://application:,,,/FontLib;component/Fonts/Face,One.ttf", "FAMILY, ONE")]
    [InlineData("pack://application:,,,,,,/FontLib;component/Fonts/#Inter", "pack://application:,,,/FontLib;component/Fonts/", "INTER")]
    public void AbsoluteApplicationFontKeepsItsAuthorityAndIndependentFallbacks(string source, string location, string family)
    {
        // Initialize the source pack parser without opening any resource or font.
        _ = BaseUriHelper.PackAppBaseUri;
        var identifier = new FontFamilyIdentifier(source + ", Arial, Family,, With Comma", null!);
        Assert.Equal(3, identifier.Count);
        var first = identifier[0];
        Assert.NotSame(CanonicalFontFamilyReference.Unresolved, first);
        Assert.Equal(new Uri(location), first.LocationUri);
        Assert.Equal(family, first.FamilyName);
        Assert.Equal("ARIAL", identifier[1].FamilyName);
        Assert.Null(identifier[1].LocationUri);
        Assert.Equal("FAMILY, WITH COMMA", identifier[2].FamilyName);
        identifier.Canonicalize();
        Assert.Equal(first, identifier[0]);
        Assert.Equal(new Uri(location), identifier[0].LocationUri);
    }

    [Theory]
    [InlineData("./Fonts/#Roboto Condensed", "pack://application:,,,/FontLib;component/Fonts/#Roboto Condensed")]
    [InlineData("./Fonts/#Inter Variable Text", "pack://application:,,,/FontLib;component/Fonts/#Inter Variable Text")]
    [InlineData("./Fonts/Face.ttf#Inter", "pack://application:,,,/FontLib;component/Fonts/Face.ttf#Inter")]
    public void AbsoluteAndRelativeApplicationFontsResolveToTheSameIdentity(string relative, string absolute)
    {
        _ = BaseUriHelper.PackAppBaseUri;
        var relativeFamily = new FontFamilyIdentifier(relative, new Uri("pack://application:,,,/FontLib;component/"));
        var absoluteFamily = new FontFamilyIdentifier(absolute, null!);
        Assert.Equal(1, relativeFamily.Count);
        Assert.Equal(1, absoluteFamily.Count);
        Assert.NotSame(CanonicalFontFamilyReference.Unresolved, absoluteFamily[0]);
        Assert.Equal(relativeFamily[0], absoluteFamily[0]);
        Assert.Equal(relativeFamily[0].GetHashCode(), absoluteFamily[0].GetHashCode());
    }

    [Theory]
    [InlineData("https://example.test/Fonts/#Inter")]
    [InlineData("http://example.test/Fonts/#Inter")]
    [InlineData("pack://siteoforigin:,,,/Fonts/#Inter")]
    [InlineData("pack://https:,,example.test,fonts.zip/Fonts/#Inter")]
    public void ApplicationPackAdmissionDoesNotAdmitOtherAbsoluteSchemes(string normalized)
    {
        _ = BaseUriHelper.PackAppBaseUri;
        Assert.Same(CanonicalFontFamilyReference.Unresolved, CanonicalFontFamilyReference.Create(null!, normalized));
    }

    [Fact]
    public void FileUriAndLiteralFamilyCommasKeepTheirExistingMeaning()
    {
        var identifier = new FontFamilyIdentifier("file:///tmp/Fonts/Face,,One.ttf#Family,, One, Arial", null!);
        Assert.Equal(2, identifier.Count);
        Assert.Equal(new Uri("file:///tmp/Fonts/Face,One.ttf"), identifier[0].LocationUri);
        Assert.Equal("FAMILY, ONE", identifier[0].FamilyName);
        Assert.Equal("ARIAL", identifier[1].FamilyName);
    }
}
