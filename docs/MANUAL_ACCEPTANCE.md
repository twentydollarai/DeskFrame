# Remaining manual acceptance

All items below remain deferred acceptance work for 1.0.0 unless a new signed-off result is explicitly recorded. Use the exact ZIP hash in SHA256SUMS.txt, disposable profiles and nonpersonal images. Record OS, architecture, scaling, artifact hash, steps and outcome. Do not infer a pass from source inspection. The focused candidate checks in RELEASE_NOTES.md do not complete this broader checklist.

- [ ] Clean Windows 11 x64 standard account without development tools: extract into a path containing spaces; launch without elevation; select pictures; save, exit and restart in normal and portable modes. Verify profile separation, single-instance activation and complete exit.
- [ ] Multiple-image picker: select several supported files; cancel before and after selection; verify cancellation preserves the source and saved selection cycles correctly.
- [ ] Missing source at startup: make a disposable source unavailable; launch and cancel setup; verify recovery controls and Exit. Restore the source while paused and confirm automatic recovery.
- [ ] Tray discovery: find the icon through Explorer's hidden-icons area; test menu and double-click; disable the tray and recover Controls by relaunching.
- [ ] Opt-in startup in a disposable account: enable, sign out/in, verify executable/profile; disable and verify no launch at the next sign-in. Test an unavailable source and the readable-source workaround before disabling startup. Confirm cleanup before deleting the application.
- [ ] Narrator: verify labels, numeric values/ranges, validation messages, shortcut warnings, navigation, Save/Cancel and Exit are spoken and usable.
- [ ] Displays and lifecycle: use deliberately differing DPI scales, unplug/reconnect, sleep/wake and an overnight slideshow. Verify bounds, focus, visibility, recovery and resource growth.
- [ ] Windows 10 and ARM: test separately before claiming verified support. Keep these platforms explicitly untested if deferred.
- [ ] Actual downloaded ZIP and SmartScreen behavior: test only with separately authorized download access. Record reputation/publisher warnings without promising their absence on other machines.

The 1.0.0 release decision accepts these disclosed coverage gaps within the Windows 11 x64 scope. Retain the limitations in release notes and address deferred checks in subsequent work. Publication still requires explicit human approval.
