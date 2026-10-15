# Changelog

## 1.0.0 - 2026-10-15

- First public stable release, scoped to verified Windows 11 x64 configurations.
- Rebuild with assembly, file and application manifest version 1.0.0.0.
- Preserve application behavior from 0.1.0; no feature or dependency changes.
- Refresh package identity, checksums, platform disclosures and usage guidance.
- Known limitations and unverified configurations remain documented in release notes.

## 0.1.0 (pre-release history)

- Add movement buttons in Controls that work without global shortcuts and save the frame position.
- Report unavailable movement and show/hide shortcuts visibly, including with the tray icon disabled.
- Keep both images during Auto-fit crossfades while smoothly changing the shared viewport; preserve proportions, corner anchoring and exact final bounds.
- Present image pixels, geometry and opacity together through a layered surface to remove blank intervals and lingering old-size padding.

- Correct native layered alpha at 100% opacity in Auto-fit, Fixed Fit and Fixed Fill.
- Explain tray-free control access in Settings and show Controls after tray-free setup.
- Explain unavailable pictures, automatic retry and source replacement; keep recovery and Exit visible after cancelled setup.
- Expose accurate numeric accessibility ranges and validated UI Automation edits.
- Remove the incorrect fixed duration from the generic Fade checkbox.
- Add external desktop-capture and accessibility regressions for the beta findings.

- Initial preview of DeskFrame for Windows.
- Native folder/multi-file selection, settings, and system-tray controls.
- Borderless, no-focus picture viewer with optional topmost and click-through styles.
- Aspect-preserving slideshow, crossfade, pause, global hide/show and keyboard movement.
- Per-user preferences and opt-in portable mode and Windows startup.
- Missing-source recovery, corrupt-image skipping, and per-user/session single instance.
- MIT source release and simple ZIP distribution; no bundled personal images.
- Replace near-black Fit margins with a light edge-tinted matte, including transparent images; retain stable opaque-canvas crossfades.
- Add default Auto-fit sizing with full-image settled bounds and a continuous animated crossfade.
- Add Fixed frame Fit/Fill selection, monitor selection, and persistent corner anchoring.
- Bound extreme proportions to the monitor work area, including one-pixel windows at high DPI; retain lightweight opaque-cache rendering.
