# Transparent popup owner-input routing

A popup can overlap its owner through an empty margin reserved for a shadow.
The owner window must not give a pointer event to that popup solely because the
point falls inside the popup's outer rectangle. In particular, opening a menu
on mouse-down must not move the matching mouse-up over the header into an empty
popup margin. LibreWPF issue [#268](https://github.com/wieslawsoltes/LibreWPF/issues/268)
contains the original 30-DIP shadow-margin reproduction.

`WpfPortablePopupBridge` first applies its existing visibility, hit-testability
and coordinate bounds checks. For transparent popups it then queries
`IPortablePresentationSourceHost.TryHitTestInput` in the popup's root coordinates.
The source uses its ordinary `UIElement.InputHitTest`: original presented-owner
callbacks, input visibility filtering and raw input promotion remain authoritative.
An unavailable query or a real miss leaves the original owner event untouched so
the next popup or the owner can process it. Opaque surfaces retain their existing
surface input behavior.

The bridge does not read alpha pixels, invent visual bounds, or classify opacity
as input visibility. Disabled content retains its raw hit region. Source geometry
and custom source hit overrides determine actual coverage. Existing popup
first-frame callback behavior remains unchanged; no additional managed geometry
index substitutes for a presented native index.

Hit queries do not dispatch input or alter capture. Capture routing remains in
the existing source input service, with both original and capture-routed source
identity. The native-popup event route continues to retain client-local captured
drags, mouse-up, leave and cancellation outside the popup rectangle. Native event
metadata is carried through the same coordinate mapping as before.

Popup state or source invalidation retires an in-progress owner-input admission,
including a reentrant hide and reopen. The bridge also verifies root identity,
position, size and scale before forwarding the queried event. The private
admission version does not replace or mutate native provider input generations.

## Validation scope

Host controls cover transparent misses, lower-popup fallback, fractional points
and independent DPI axes, missing query capability, opaque surfaces, and reentrant
hide/reopen. PresentationCore controls exercise source-owned hit overrides,
visibility, disabled/transparent content, unchanged capture, original presented
owners, handled misses, invalid coordinates and disposed/detached sources. The
existing pointer-ownership CI lane runs all three new source controls without
skipping the original controls.

These controls do not qualify the original application. Closing #268 still
requires the original menu XAML in native and owner-surface popup modes, actual
mouse-down/up over the header and submenu items, capture and keyboard controls,
and an independent Microsoft WPF comparison. Original geometry, drop-shadow
rendering, native input and package closure must be checked through that consumer.
