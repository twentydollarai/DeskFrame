# Architecture and distribution decision

DeskFrame 1.0.0 retains C# and .NET Framework Windows Forms. The viewer keeps the previously exercised Windows layered/tool/no-activate styles, native global shortcuts, no-activation restore, two-image display-sized cache, high-quality GDI+ scaling and timer-driven crossfade. An ApplicationContext owns the frame, optional NotifyIcon, setup/settings window and compact control window.

Source responsibilities:

- `Core.cs`: preferences, atomic persistence, source enumeration, instance/events, startup registration, window and tray lifetime.
- `Viewer.cs`: the floating window, image decoding, Auto-fit and fixed-canvas transitions, shortcuts, source recovery.
- `LayeredSurface.cs`: reusable premultiplied DIB and atomic UpdateLayeredWindow presentation.
- `ImageLayout.cs`: physical-pixel Fit/Fill and corner/work-area geometry, with no window dependencies.
- `SettingsWindow.cs`: native image pickers and editable preferences.
- `ControlWindow.cs`: visible running-state actions, opened by a second executable launch.

Settings default to LocalApplicationData; portable mode uses a directory beside the executable. No configuration is shipped with a personal/default source. Startup uses the current-user Run registry value only after an explicit settings change. No third-party libraries or network dependencies are used.

## Why not migrate to modern .NET now?

A modern .NET framework-dependent build would require the corresponding desktop runtime. A self-contained build includes runtime/framework libraries and is substantially larger; it also changes build tooling and requires revalidating native window behavior. This release can use .NET Framework 4.8 already included in the target Windows versions and ship a small executable plus runtime declaration.

This is **not** a self-contained build: Windows supplies the framework. There is no claim that it runs on systems where that component is missing or disabled. A modern self-contained prototype was not built, so no invented size comparison is reported. Measure it before considering a future migration for a concrete platform need.

References: [Microsoft .NET Framework requirements](https://learn.microsoft.com/dotnet/framework/get-started/system-requirements), [single-file/self-contained deployment tradeoffs](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview).

## Sizing and resource behavior

Auto-fit is the default. EXIF orientation is applied before computing one bounded bitmap matching each photograph's proportions. Only Fixed frame scans the collection to choose the maximum fitted width and height. Fixed Fit centers a complete scaled image; Fixed Fill computes a centered source crop matching the canvas ratio, avoiding huge off-screen intermediate bitmaps. Drawing remains high-quality GDI+ bicubic with mirrored-edge sampling. Sources are released after rendering; at most two display-sized bitmaps are retained.

Sizing uses the selected monitor's work area, inset by margin. On very small work areas the margin is limited to preserve at least one available pixel. Double-precision scale calculations avoid integer overflow and zero dimensions; output dimensions are rounded and bounded to at least one pixel. A borderless WM_GETMINMAXINFO override permits that one-pixel minimum even at 150% scaling. UpdateLayeredWindow submits complete pixels, geometry and global alpha together. Managed bounds are synchronized afterward without a second native resize.

Auto-fit does not scan every photo at startup. The 16 by 16 temporary edge sample and full-resolution decode are disposed immediately. Decoding remains synchronous, so exceptionally large or slow/offline inputs can delay the UI. No persistent photo copies, thumbnail database or network services are added.

## Transition state

A 250 ms timer checks rotation time. Auto-fit uses 16 ms fade ticks and smoothstep easing over 440 ms; Fixed frame retains its 1.5-second whole-canvas dissolve. Both Auto-fit images remain available while a shared viewport interpolates between their fitted bounds. Each image covers the changing viewport through proportional cropping, without independent-axis stretching. The selected corner anchors presentation, and the incoming image settles to its exact complete fitted bounds. A reusable premultiplied DIB and UpdateLayeredWindow present pixels, size, position and opacity together. A late tick completes the fade directly. Disabling fade swaps immediately. Two bounded image caches and one reusable presentation surface are retained; no worker thread or animation framework is introduced.


Pause stops the slideshow clock, including a fade. Hide does not pause. Reconfiguring replaces transition state and restores configured opacity; missing-source recovery renders the recovered image at its own Auto-fit size. Native no-activation, click-through, topmost and borderless styles remain in use.

## Position, persistence and compatibility

Preferences add FrameSizing (Auto-fit/Fixed frame), ImageFitting (Fit/Fill), Monitor (empty means primary; otherwise Windows device name), and nullable AnchorX/AnchorY. Missing fields retain initializer defaults, so old settings migrate to Auto-fit. Invalid mode strings normalize safely without discarding the image source or other valid preferences. X/Y remain saved for compatibility. An old valid top-left position becomes a corner anchor on first use; future saves retain the anchor across image-size changes.

The monitor is selected explicitly. Corner geometry uses the monitor's work area, including negative virtual-desktop coordinates. A saved anchor is reused when on that monitor; otherwise the selected corner/margin is used. Placement clamps when a manually moved anchor would put a larger image outside the work area. Monitor/corner/width/margin edits reset saved position. Monitor loss falls back to primary. DisplaySettingsChanged rebuilds both caches invisibly for the new work area (and restarts from the first readable source).

The process remains system-DPI-aware, not per-monitor-DPI-aware. Sizes are physical pixels in the process coordinate space; OS scaling on monitors with differing DPI remains a compatibility boundary. Secure desktops and exclusive fullscreen are outside normal topmost guarantees. Single instance remains per user and interactive session across normal/portable mode.

## Transparency and mattes

Transparent source pixels are flattened onto a light opaque matte in all modes; Auto-fit adds no matte outside source bounds. Fixed Fit also uses it for unavoidable unused space. The matte is the perimeter average of a 16 by 16 source sample flattened onto RGB (232,232,232), blended at 25% with that neutral at 75%. Every channel is at least 174. Source borders are preserved. Opaque cached images ensure transparent inputs cannot reveal an older photograph after transitions.

## Beta corrections, 0.1.0

Opacity is submitted through UpdateLayeredWindow SourceConstantAlpha, including 255 at 100%. This path is not mixed with SetLayeredWindowAttributes; its attribute getter is inapplicable. Regression tests measure desktop RGB against a verified synthetic backdrop.

Controls movement buttons and available global shortcuts share Viewer.MoveFrame. It clamps to the selected work area, retains the corner anchor and emits PositionChanged for existing persistence. Movement buttons are disabled while the frame is hidden or pictures are unavailable. Failed shortcut registrations are listed in Controls, which opens even with the tray disabled.

Viewer notifies StateChanged on source loss/recovery and configuration. Controls distinguishes unavailable, intentionally hidden, paused, and running states. Cancelling setup without pictures or saving tray-free settings exposes the existing Controls window.

NumericSetting retains NumericUpDown editing and supplies a standard HWND UI Automation RangeValue/Value provider. Values and bounds come from the control, calls marshal to its UI thread, and invalid edits are rejected. The build references the installed .NET Framework UIAutomationProvider, UIAutomationTypes and WindowsBase assemblies; no additional DLLs are packaged.
