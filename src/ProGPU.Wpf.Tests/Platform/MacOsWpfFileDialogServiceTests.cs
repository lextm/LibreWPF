using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.ProGPU.Platform;
using Xunit;

namespace ProGPU.Wpf.Tests.Platform;

public sealed class MacOsWpfFileDialogServiceTests
{
    [Fact]
    public void RequestFreezesOriginalOptionsAndDeduplicatesExtensions()
    {
        string[] patterns = ["*.png", ".jpeg", "*.PNG"];
        var options = new WpfFileDialogOptions
        {
            Title = "Choose images", InitialDirectory = "/tmp/images",
            SuggestedFileName = "/tmp/other/one.png", FileTypePatterns = patterns,
            AllowMultipleSelection = true
        };
        var request = MacOsWpfFileDialogService.CreateRequest(WpfFileDialogKind.OpenFile, options);
        options.Title = "Changed";
        patterns[0] = "*.txt";
        Assert.Equal("Choose images", request.Title);
        Assert.Equal("/tmp/images", request.InitialDirectory);
        Assert.Equal("one.png", request.FileName);
        Assert.Equal(new[] { "png", "jpeg" }, request.Extensions);
        Assert.True(request.AllowMultipleSelection);
    }

    [Theory]
    [InlineData(null, null, "/tmp/save.txt", "/tmp", "save.txt")]
    [InlineData(null, "/tmp/default", "save.txt", "/tmp/default", "save.txt")]
    [InlineData("/tmp/initial", "/tmp/default", "save.txt", "/tmp/initial", "save.txt")]
    public void SavePreservesLocationAndCannotBecomeMultipleSelection(
        string? initial, string? fallback, string fileName, string expectedDirectory, string expectedName)
    {
        var request = MacOsWpfFileDialogService.CreateRequest(WpfFileDialogKind.SaveFile, new()
        {
            InitialDirectory = initial, DefaultDirectory = fallback,
            SuggestedFileName = fileName, AllowMultipleSelection = true
        });
        Assert.Equal(expectedDirectory, request.InitialDirectory);
        Assert.Equal(expectedName, request.FileName);
        Assert.False(request.AllowMultipleSelection);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.*")]
    public void AllFilesPatternKeepsSelectionUnrestricted(string allFiles)
    {
        var request = MacOsWpfFileDialogService.CreateRequest(WpfFileDialogKind.OpenFile,
            new() { FileTypePatterns = ["*.txt", allFiles] });
        Assert.Empty(request.Extensions);
    }

    [Theory]
    [InlineData("foo*.txt")]
    [InlineData("?.txt")]
    [InlineData("../txt")]
    [InlineData("*.txt;*.png")]
    public void UnrepresentableFiltersFailBeforeNativePublication(string pattern)
        => Assert.Throws<NotSupportedException>(() =>
            MacOsWpfFileDialogService.CreateRequest(WpfFileDialogKind.OpenFile,
                new() { FileTypePatterns = [pattern] }));

    [Fact]
    public void EmbeddedNullFailsBeforeNativePublication()
        => Assert.Throws<ArgumentException>(() =>
            MacOsWpfFileDialogService.CreateRequest(WpfFileDialogKind.SaveFile,
                new() { SuggestedFileName = "before\0after" }));

    [Fact]
    public async Task MultipleSelectionReturnsIndependentOriginalPaths()
    {
        string[] original = ["/tmp/a\nfile.txt", "/tmp/unicode-ą.txt"];
        var service = new MacOsWpfFileDialogService((request, _) =>
        {
            Assert.Equal(WpfFileDialogKind.OpenFile, request.Kind);
            Assert.True(request.AllowMultipleSelection);
            return original;
        });
        string[]? result = await service.OpenFilesAsync(new() { AllowMultipleSelection = true });
        Assert.Equal(original, result);
        Assert.NotSame(original, result);
        original[0] = "/tmp/changed";
        Assert.Equal("/tmp/a\nfile.txt", result![0]);
    }

    [Fact]
    public async Task FolderRequestPreservesMultipleSelection()
    {
        var service = new MacOsWpfFileDialogService((request, _) =>
        {
            Assert.Equal(WpfFileDialogKind.PickFolder, request.Kind);
            Assert.True(request.AllowMultipleSelection);
            return ["/tmp/one", "/tmp/two"];
        });
        Assert.Equal(new[] { "/tmp/one", "/tmp/two" },
            await service.PickFoldersAsync(new() { AllowMultipleSelection = true }));
    }

    [Fact]
    public async Task NativeCancelReturnsNoSelection()
    {
        var service = new MacOsWpfFileDialogService((_, _) => null);
        Assert.Null(await service.OpenFilesAsync(new()));
    }

    [Fact]
    public void CanceledTokenDoesNotOpenPanel()
    {
        var service = new MacOsWpfFileDialogService((_, _) => throw new Exception("Must not run"));
        Assert.Throws<OperationCanceledException>(() => service.SaveFileAsync(new(), new(true)));
    }

    [Fact]
    public void CancellationBeforePublicationCannotReturnAcceptedPath()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new MacOsWpfFileDialogService((_, _) =>
        {
            cancellation.Cancel();
            return ["/tmp/accepted.txt"];
        });
        Assert.Throws<OperationCanceledException>(() => service.SaveFileAsync(new(), cancellation.Token));
    }

    [Theory]
    [InlineData("relative.txt")]
    [InlineData("")]
    [InlineData("/tmp/a\0b")]
    public void InvalidNativePathsAreNotPublished(string path)
    {
        var service = new MacOsWpfFileDialogService((_, _) => [path]);
        Assert.Throws<InvalidOperationException>(() => service.OpenFileAsync(new()));
    }

    [Fact]
    public void ExtraNativeSelectionsAreNotSilentlyTruncated()
    {
        var service = new MacOsWpfFileDialogService((_, _) => ["/tmp/one", "/tmp/two"]);
        Assert.Throws<InvalidOperationException>(() => service.OpenFileAsync(new()));
    }

    [Fact]
    public void RuntimeBindingsResolveOnMacOsWithoutOpeningPanel()
    {
        if (OperatingSystem.IsMacOS())
            MacOsWpfFilePanel.VerifyRuntimeBindings();
        else
            Assert.Throws<PlatformNotSupportedException>(MacOsWpfFilePanel.VerifyRuntimeBindings);
    }
}
