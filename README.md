# DeskFrame

DeskFrame 1.0.0 is the first stable release of an unsigned Windows desktop application: a small, floating photo frame with local pictures, slideshow transitions, optional click-through, and movement controls.

**Verified platform scope: Windows 11 x64.** It uses .NET Framework 4.8 or 4.8.1 supplied by Windows. Windows 10 1903+ is a compatibility target, not a verified platform for this release. Windows on ARM is untested. This is not a self-contained runtime distribution.

![Generated geometric sample displayed by DeskFrame](docs/screenshot.png)

## Install and run

When a public release is available, obtain `DeskFrame-1.0.0.zip` and `SHA256SUMS.txt` from that repository's Releases page. No repository URL has been assigned in this candidate. Compare `Get-FileHash .\DeskFrame-1.0.0.zip -Algorithm SHA256` with the checksum file before extracting.

Extract the ZIP into a folder you can keep. Keep `DeskFrame.exe.config` beside `DeskFrame.exe`; double-click the executable. Do not run inside the ZIP. No installer or administrator access is required. Choose a folder or multiple images, then select Save.

The executable is unsigned. Windows or SmartScreen may show an unknown-publisher or reputation warning. Download/reputation behavior has not been verified for this release. A checksum establishes consistency with the published checksum, not publisher identity; use a trusted release source.

## Controls and pictures

Launch the executable again to open Controls for the running instance. Controls provides Show/Hide, Pause/Resume, movement buttons, Change Pictures, Settings and Exit. Closing Controls leaves the slideshow running. Exit DeskFrame stops it. A notification-area icon is enabled by default; double-click it for Controls or right-click for its menu. If the tray icon is disabled or hidden, relaunching the executable still opens Controls.

Movement buttons move 20 physical pixels within the selected display. Ctrl+Alt+D toggles visibility; Ctrl+Alt+Shift+Arrow moves when those bindings are available. Shortcut conflicts are reported in Controls; its buttons remain available. There is no mouse drag mode. Hiding does not pause the slideshow.

Auto-fit follows picture proportions with a roughly 440 ms transition. Fixed frame / Fit shows complete images with matte space; Fixed frame / Fill crops proportionally. Fixed modes crossfade for 1.5 seconds. Transparent source pixels use a light matte. PNG, JPEG, BMP and the first frame of GIF are supported; HEIC, WebP, RAW, video and animated GIF playback are not.

Folders are nonrecursive. Missing/corrupt files are skipped. If no readable pictures remain, the frame hides and Controls explains recovery; retry occurs every 10 seconds even while paused. A paused readable slideshow does not refresh its collection. Large images, large folders and unavailable network locations can block the UI during synchronous file access. Local folders are recommended.

## Preferences and portable mode

Normal preferences are stored in `%LOCALAPPDATA%\DeskFrame`. For portable use, launch **Start portable.cmd** or `DeskFrame.exe --portable`. Preferences then live in `DeskFrameData` beside the executable, in a writable folder. An empty `portable.flag` beside the executable also selects portable mode.

Exit the existing instance before switching profiles: normal and portable modes share a single-instance guard. Launching a second copy activates the existing instance regardless of its profile. After exiting, use the portable launcher again; opening the executable directly selects normal preferences unless the flag exists. Image paths are absolute and may need reselection on another machine or drive.

## Startup and removal

Start with Windows is opt-in and registers the current executable in the current user's startup entries. It is off by default and actual sign-in startup remains untested. Portable startup also points to the current executable location.

Disable startup before moving or deleting the application. Settings requires a readable image source before it saves any change, including disabling startup. If the source is unavailable, choose a readable source first, then disable startup and save. This limitation was identified by static review and still needs hands-on verification.

Exit through Controls, the tray menu, or `DeskFrame.exe --exit`; then delete the extracted folder. Optionally remove the normal preferences directory or portable `DeskFrameData`. Source pictures are not removed.

## Privacy

DeskFrame has no network client, accounts, telemetry, advertising or automatic updater. Pictures are read-only inputs. Preferences contain local paths and should not be attached to public issues. Windows may access a network share if you explicitly select one. Do not publish personal pictures, desktop captures, account paths or unsanitized diagnostic files.

## Verification and limitations

Existing Windows 11 x64 records support targeted rendering, movement, window behavior, portable process, opacity/recovery/provider and fresh-ZIP checks. The 1.0.0 candidate rebuild changes release metadata only. Fresh focused checks are described in the release notes; broader acceptance remains limited to the historical scope. See [testing scope](docs/TESTING.md), [release notes](RELEASE_NOTES.md), and [remaining manual acceptance](docs/MANUAL_ACCEPTANCE.md).

Screen-reader speech, clean-account installation, downloaded-file/SmartScreen behavior, Windows 10/ARM, deliberate mixed-DPI changes, monitor hot-plug, sleep/wake, overnight operation and sign-in startup remain unverified. Full multiple-image picker automation was not part of final certification. System DPI awareness is used, not per-monitor DPI awareness. Always-on-top does not override secure desktops, exclusive fullscreen or every other topmost window.

## Build and contribute

On Windows with .NET Framework 4.8 installed, run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`. This uses the installed Framework compiler, requires no NuGet downloads, and writes a developer build under `build/`. A local rebuild has its own binary identity and is not verified by the published artifact hashes.

`package.ps1` builds developer output under `release/` and refuses existing package, ZIP or checksum output. Do not use it to recreate or overwrite certified release attachments. The verified release ZIP is distributed separately. [Architecture](docs/ARCHITECTURE.md) explains the implementation; [testing guidance](docs/TESTING.md) distinguishes read-only checks from state-changing harnesses.

## Report an issue

Once the public repository URL exists, use its Issues tab. Include version, Windows version/architecture, display scaling, steps, expected/actual behavior and a minimal nonpersonal image if needed. Redact local paths and account names. Do not attach preferences or full desktop screenshots. Until a repository URL is supplied, this candidate has no public issue destination; no URL is implied.

## License

Source is MIT licensed; see [LICENSE](LICENSE). The included screenshot is generated nonpersonal geometric artwork. See [attribution](THIRD_PARTY_NOTICES.md). Detailed packaged instructions are in [USER_GUIDE.txt](USER_GUIDE.txt); the same startup caveat is included in that guide.
