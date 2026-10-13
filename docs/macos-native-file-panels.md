# In-process macOS file panels

The acceptance application for issue257 is an ordinary LibreWPF SDK application
calling `Microsoft.Win32.OpenFileDialog`, `SaveFileDialog`, and `OpenFolderDialog`.
The action is to select or cancel a file/folder, then read or write the selected
file in that same application. The blocking path was
`WpfPortableWindowActivation.ShowPortableFileDialog` →
`ProcessWpfFileDialogService` → `osascript`.

The default macOS platform service now uses `MacOsWpfFileDialogService`. It creates
an owned `NSOpenPanel` or `NSSavePanel` inside the application and uses AppKit's
application-modal `runModal` API, matching the synchronous WPF call. A panel is
created for each request and released after completion. This implementation does
not select an asynchronous owner sheet or transport an Objective-C callback block.
Explicitly supplied platform services and other operating systems are unchanged.

The typed request preserves the title, initial/default directory, suggested name,
extension patterns and multiple-selection setting. AppKit uses `UTType` values
for extension filters; unsupported wildcard shapes fail before showing a panel.
The current portable source bridge continues to combine the WPF filter groups;
this change does not introduce a filter-selector accessory or claim FilterIndex
selection. Folder panels admit directories, and Save never permits multiple items.
Selected paths come from actual file URLs and are copied before native retirement.
They are not parsed from process output, so embedded newlines remain intact.

Native calls require the application's main thread. A canceled token cannot show
a panel or publish an accepted result. Cancellation during the native modal loop
queues `cancel:` on that exact panel using Foundation's main-thread selector API,
including the actual AppKit modal and Foundation default run-loop modes. The
native queue retains the receiver until dispatch; a fresh panel prevents delayed
cancellation from affecting a subsequent request. Cancellation registration ends
before the panel's managed owner releases it.

AppKit owns the operating-system file-selection authorization. This adapter does
not manufacture sandbox permission, persist security-scoped bookmarks, or claim
that arbitrary paths are authorized. Signed/sandboxed application behavior and
the original WPF file-action acceptance remain separate from authored controls.

Validation so far: source host and test graph compile; 36 focused tests pass,
including 21 native-service controls, existing process-service controls and the
default platform-selection control. The macOS run resolves the real AppKit and
UniformTypeIdentifiers binding without opening a dialog. Native selection,
cancellation during the modal loop, and selected-file I/O still require actual
application validation before issue257 can close.

References: [NSOpenPanel](https://developer.apple.com/documentation/appkit/nsopenpanel),
[NSSavePanel](https://developer.apple.com/documentation/appkit/nssavepanel),
[App Sandbox file access](https://developer.apple.com/documentation/security/accessing-files-from-the-macos-app-sandbox),
and [main-thread selectors](https://developer.apple.com/documentation/objectivec/nsobject-swift.class/performselector(onmainthread:with:waituntildone:modes:)).
