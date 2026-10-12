using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Windows.Media.ProGPU.Platform;

/// <summary>Runs AppKit file panels in the calling application on its main thread.</summary>
public sealed class MacOsWpfFileDialogService : IWpfFileDialogService
{
    private readonly Func<MacOsWpfFileDialogRequest, CancellationToken, string[]?> _show;

    public MacOsWpfFileDialogService() : this(MacOsWpfFilePanel.Show) { }

    internal MacOsWpfFileDialogService(
        Func<MacOsWpfFileDialogRequest, CancellationToken, string[]?> show)
        => _show = show ?? throw new ArgumentNullException(nameof(show));

    public ValueTask<string?> OpenFileAsync(WpfFileDialogOptions options, CancellationToken cancellationToken = default)
        => new(First(Show(WpfFileDialogKind.OpenFile, options, cancellationToken)));

    public ValueTask<string[]?> OpenFilesAsync(WpfFileDialogOptions options, CancellationToken cancellationToken = default)
        => new(Show(WpfFileDialogKind.OpenFile, options, cancellationToken));

    public ValueTask<string?> SaveFileAsync(WpfFileDialogOptions options, CancellationToken cancellationToken = default)
        => new(First(Show(WpfFileDialogKind.SaveFile, options, cancellationToken)));

    public ValueTask<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        => new(First(Show(WpfFileDialogKind.PickFolder, new WpfFileDialogOptions(), cancellationToken)));

    public ValueTask<string[]?> PickFoldersAsync(WpfFileDialogOptions options, CancellationToken cancellationToken = default)
        => new(Show(WpfFileDialogKind.PickFolder, options, cancellationToken));

    private static string? First(string[]? paths) => paths is { Length: > 0 } ? paths[0] : null;

    private string[]? Show(WpfFileDialogKind kind, WpfFileDialogOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MacOsWpfFileDialogRequest request = CreateRequest(kind, options);
        string[]? result = _show(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (result == null)
            return null;
        if (result.Length == 0 || (!request.AllowMultipleSelection && result.Length != 1))
            throw new InvalidOperationException("AppKit returned an invalid number of selected paths.");
        foreach (string path in result)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf('\0') >= 0 || path[0] != '/')
                throw new InvalidOperationException("AppKit returned an invalid selected path.");
        }
        return (string[])result.Clone();
    }

    internal static MacOsWpfFileDialogRequest CreateRequest(WpfFileDialogKind kind, WpfFileDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string title = ValidateString(options.Title);
        string initialDirectory = ValidateString(options.InitialDirectory);
        string defaultDirectory = ValidateString(options.DefaultDirectory);
        string fileName = ValidateString(options.SuggestedFileName);
        if (initialDirectory.Length == 0)
            initialDirectory = defaultDirectory;
        if (fileName.StartsWith("/", StringComparison.Ordinal))
        {
            int separator = fileName.LastIndexOf('/');
            if (initialDirectory.Length == 0)
                initialDirectory = separator == 0 ? "/" : fileName[..separator];
            fileName = fileName[(separator + 1)..];
        }
        var extensions = new List<string>();
        bool unrestricted = false;
        ArgumentNullException.ThrowIfNull(options.FileTypePatterns);
        foreach (string patternValue in options.FileTypePatterns)
        {
            string pattern = ValidateString(patternValue).Trim();
            if (pattern is "*" or "*.*")
            {
                unrestricted = true;
                continue;
            }
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
                pattern = pattern[2..];
            else if (pattern.StartsWith(".", StringComparison.Ordinal))
                pattern = pattern[1..];
            if (pattern.Length == 0 || pattern.IndexOfAny(['*', '?', '/', '\\', ';']) >= 0)
                throw new NotSupportedException("AppKit file filters require filename extensions or an all-files pattern.");
            if (!extensions.Exists(value => string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase)))
                extensions.Add(pattern);
        }
        return new(kind, title, initialDirectory, fileName,
            unrestricted ? [] : extensions.ToArray(),
            kind != WpfFileDialogKind.SaveFile && options.AllowMultipleSelection);
    }

    private static string ValidateString(string? value)
    {
        value ??= string.Empty;
        if (value.IndexOf('\0') >= 0)
            throw new ArgumentException("File-panel values cannot contain a null character.");
        return value;
    }
}

internal sealed record MacOsWpfFileDialogRequest(
    WpfFileDialogKind Kind, string Title, string InitialDirectory,
    string FileName, string[] Extensions, bool AllowMultipleSelection);

// The Objective-C signatures follow the owned LibreWinForms AppKit adapter.
// This binding deliberately uses AppKit's synchronous application-modal API,
// matching WPF ShowDialog without transporting a callback block across its return.
internal static partial class MacOsWpfFilePanel
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private static readonly Lazy<nint> Frameworks = new(() =>
    {
        NativeLibrary.Load("/System/Library/Frameworks/UniformTypeIdentifiers.framework/UniformTypeIdentifiers");
        return NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");
    });
    private static readonly Lazy<nint> Foundation = new(() =>
        NativeLibrary.Load("/System/Library/Frameworks/Foundation.framework/Foundation"));

    internal static string[]? Show(MacOsWpfFileDialogRequest request, CancellationToken cancellationToken)
    {
        VerifyRuntimeBindings();
        if (SendBool(GetClass("NSThread"), Selector("isMainThread")) == 0)
            throw new InvalidOperationException("AppKit file panels must run on the application's main thread.");
        cancellationToken.ThrowIfCancellationRequested();
        nint pool = Send(Send(GetClass("NSAutoreleasePool"), Selector("alloc")), Selector("init"));
        nint panel = 0;
        try
        {
            // Own a fresh panel, so a queued cancellation cannot affect a later dialog.
            panel = Send(Send(GetClass(request.Kind == WpfFileDialogKind.SaveFile ? "NSSavePanel" : "NSOpenPanel"),
                Selector("alloc")), Selector("init"));
            if (panel == 0)
                throw new InvalidOperationException("AppKit could not create a file panel.");
            Configure(panel, request);
            using CancellationTokenRegistration registration = cancellationToken.Register(
                static state => QueueCancel((nint)state!), panel);
            cancellationToken.ThrowIfCancellationRequested();
            nint response = Send(panel, Selector("runModal"));
            cancellationToken.ThrowIfCancellationRequested();
            if (response != 1)
                return null;
            if (request.Kind == WpfFileDialogKind.SaveFile)
                return [ReadPath(Send(panel, Selector("URL")))];
            nint urls = Send(panel, Selector("URLs"));
            nuint count = SendUnsigned(urls, Selector("count"));
            var paths = new string[checked((int)count)];
            for (nuint index = 0; index < count; index++)
                paths[checked((int)index)] = ReadPath(SendIndex(urls, Selector("objectAtIndex:"), index));
            return paths;
        }
        finally
        {
            if (panel != 0)
            {
                SendVoid(panel, Selector("orderOut:"), 0);
                SendVoid(panel, Selector("release"));
            }
            if (pool != 0)
                SendVoid(pool, Selector("drain"));
        }
    }

    internal static void VerifyRuntimeBindings()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("AppKit file panels require macOS.");
        _ = Frameworks.Value;
        if (GetClass("NSOpenPanel") == 0 || GetClass("NSSavePanel") == 0 || GetClass("UTType") == 0
            || ClassGetInstanceMethod(GetClass("NSSavePanel"), Selector("setAllowedContentTypes:")) == 0)
            throw new PlatformNotSupportedException("The required AppKit file-panel APIs are unavailable.");
    }

    private static void Configure(nint panel, MacOsWpfFileDialogRequest request)
    {
        if (request.Title.Length != 0)
            SendVoid(panel, Selector("setTitle:"), String(request.Title));
        if (request.InitialDirectory.Length != 0)
        {
            nint url = SendObject(GetClass("NSURL"), Selector("fileURLWithPath:"), String(request.InitialDirectory));
            SendVoid(panel, Selector("setDirectoryURL:"), url);
        }
        if (request.FileName.Length != 0)
            SendVoid(panel, Selector("setNameFieldStringValue:"), String(request.FileName));
        SendBoolValue(panel, Selector("setCanCreateDirectories:"), 1);
        if (request.Extensions.Length != 0 && request.Kind != WpfFileDialogKind.PickFolder)
        {
            nint types = Send(GetClass("NSMutableArray"), Selector("array"));
            foreach (string extension in request.Extensions)
            {
                nint type = SendObject(GetClass("UTType"), Selector("typeWithFilenameExtension:"), String(extension));
                if (type == 0)
                    throw new NotSupportedException($"AppKit cannot represent the filename extension '{extension}'.");
                SendVoid(types, Selector("addObject:"), type);
            }
            SendVoid(panel, Selector("setAllowedContentTypes:"), types);
        }
        if (request.Kind != WpfFileDialogKind.SaveFile)
        {
            bool folders = request.Kind == WpfFileDialogKind.PickFolder;
            SendBoolValue(panel, Selector("setCanChooseFiles:"), folders ? (byte)0 : (byte)1);
            SendBoolValue(panel, Selector("setCanChooseDirectories:"), folders ? (byte)1 : (byte)0);
            SendBoolValue(panel, Selector("setAllowsMultipleSelection:"), request.AllowMultipleSelection ? (byte)1 : (byte)0);
            SendBoolValue(panel, Selector("setResolvesAliases:"), 1);
        }
    }

    private static void QueueCancel(nint panel)
    {
        nint pool = Send(Send(GetClass("NSAutoreleasePool"), Selector("alloc")), Selector("init"));
        try
        {
            nint modes = Send(GetClass("NSMutableArray"), Selector("array"));
            SendVoid(modes, Selector("addObject:"),
                Marshal.ReadIntPtr(NativeLibrary.GetExport(Frameworks.Value, "NSModalPanelRunLoopMode")));
            SendVoid(modes, Selector("addObject:"),
                Marshal.ReadIntPtr(NativeLibrary.GetExport(Foundation.Value, "NSDefaultRunLoopMode")));
            // Foundation retains the receiver and modes until the main-thread selector runs.
            PerformOnMainThread(panel, Selector("performSelectorOnMainThread:withObject:waitUntilDone:modes:"),
                Selector("cancel:"), 0, 0, modes);
        }
        finally
        {
            if (pool != 0)
                SendVoid(pool, Selector("drain"));
        }
    }

    private static nint String(string value) => SendString(GetClass("NSString"), Selector("stringWithUTF8String:"), value);

    private static string ReadPath(nint url)
    {
        if (url == 0 || SendBool(url, Selector("isFileURL")) == 0)
            throw new InvalidOperationException("AppKit returned a non-file URL.");
        nint bytes = Send(Send(url, Selector("path")), Selector("UTF8String"));
        return Marshal.PtrToStringUTF8(bytes)
            ?? throw new InvalidOperationException("AppKit returned a URL without a filesystem path.");
    }

    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetClass(string name);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint Selector(string name);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "class_getInstanceMethod")]
    private static partial nint ClassGetInstanceMethod(nint cls, nint selector);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static partial nint SendObject(nint receiver, nint selector, nint value);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static partial nint SendIndex(nint receiver, nint selector, nuint index);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint SendString(nint receiver, nint selector, string value);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static partial byte SendBool(nint receiver, nint selector);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static partial nuint SendUnsigned(nint receiver, nint selector);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static partial void SendVoid(nint receiver, nint selector);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static partial void SendVoid(nint receiver, nint selector, nint value);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static partial void SendBoolValue(nint receiver, nint selector, byte value);
    [LibraryImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static partial void PerformOnMainThread(nint receiver, nint selector, nint action, nint argument, byte wait, nint modes);
}
