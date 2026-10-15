# DeskFrame 1.0.0

First public stable release candidate. Publication awaits human approval.

DeskFrame displays local pictures in a floating desktop frame with Auto-fit transitions, Fixed Fit/Fill, optional click-through and topmost behavior, tray and Controls access, movement buttons, portable preferences and missing-source recovery. No application behavior or dependencies changed from the 0.1.0 baseline.

Verified platform scope is Windows 11 x64 with .NET Framework 4.8/4.8.1 supplied by Windows. Windows 10 1903+ is a compatibility target only; Windows 10 and Windows on ARM are untested. This is not a self-contained runtime distribution.

Extract DeskFrame-1.0.0.zip and keep DeskFrame.exe.config beside DeskFrame.exe. Read README.txt for controls and removal instructions. For portable use, exit any existing instance and run Start portable.cmd in a writable directory. Check the ZIP against SHA256SUMS.txt before extraction. The executable is unsigned; checksums establish artifact consistency, not publisher identity.

## Version and verification scope

The candidate is rebuilt using the existing Windows Framework compiler, with assembly version, file version and embedded application manifest identity 1.0.0.0. Runtime v4.0, .NET Framework v4.8, XML version 1.0 and manifest schema version 1.0 are dependency/schema identifiers, not application release numbers.

Fresh focused checks passed on Windows 11 x64: 2,371 existing rendering assertions compiled from candidate source, and 29 isolated smoke assertions loading the actual rebuilt executable. The smoke checks cover native frame creation, opacity, controls, movement, Settings, Fixed Fit/Fill, pause/hide/show and saved-position restoration. All 166 compiled method bodies retain baseline IL; application source differs only in version literals. Assembly/file/product metadata and the embedded manifest confirm 1.0.0.0. Package contents, hashes, source correspondence and privacy checks are verified separately. The binary smoke harness invokes the shipped controller with a generated image, private test profile and unique event names; it does not invoke Program.Main. Normal process startup, single-instance activation, the portable launcher and fresh-ZIP launch were not rerun for 1.0.0 because an existing instance was left untouched. Their historical evidence remains historical, not certification of the rebuilt binary. The package verifier pins the new ZIP, checksum and payload hashes. The release notes intentionally do not embed their own package hash: use the separately distributed SHA256SUMS.txt.

## Known limitations and remaining acceptance

Start with Windows is opt-in; actual sign-in startup is unverified. Settings needs a readable image source before saving any change, including disabling startup. Reselect a readable source, disable startup, and save before moving or deleting the program.

Screen-reader speech, clean-account installation, downloaded-file/SmartScreen behavior, deliberate mixed-DPI changes, monitor hot-plug, sleep/wake, overnight use and full multiple-image picker automation remain unverified. System DPI awareness is used, not per-monitor awareness. Always-on-top does not override secure desktops, exclusive fullscreen or all other topmost windows.

Large images, large folders and unavailable network locations can block synchronous UI operations; local folders are recommended. Folders are nonrecursive. Only the first GIF frame is shown; HEIC, WebP, RAW, video and animated GIF playback are unsupported. There is no mouse drag mode. Hiding does not pause the slideshow. Missing sources retry every 10 seconds; a paused readable slideshow does not refresh its collection.

Normal and portable modes share a single-instance guard. Exit before switching profiles. Image paths are absolute and can need reselection after moving machines or drives. Preferences contain private paths and must not be posted publicly.

Report issues using the public repository's Issues tab once available, with version, Windows architecture, display scaling, reproduction steps and expected/actual behavior. Redact paths and account names; omit preferences, personal pictures and desktop captures. No public URL has been assigned to this candidate.

MIT licensed; see LICENSE and THIRD_PARTY_NOTICES.md.