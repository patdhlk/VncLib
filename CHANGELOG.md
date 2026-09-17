# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-09-16

### Added
- **VncLib.Avalonia**: New cross-platform control for Avalonia 12, supporting Windows, Linux, and macOS
- Multi-targeting support for .NET 8.0, 9.0, and 10.0 across all packages
- NuGet packaging with SourceLink, symbol packages (snupkg), embedded README, and BSD-3-Clause license expression
- `VncConnection.GetFramebuffer()` method returning raw BGR32 framebuffer data
- `VncConnection.FrameArrived` event for frame notifications
- `VncConnection.FramebufferWidth` and `VncConnection.FramebufferHeight` properties

### Changed
- **VncLib core is now UI-framework neutral**: removed all WPF and System.Drawing dependencies
- Core library targets netstandard2.0, net8.0, net9.0, and net10.0
- Dropped .NET 7.0 support
- VncLib.Wpf now targets net48, net8.0-windows, net9.0-windows, and net10.0-windows

### Breaking Changes
- **`RfbClient.SendKey`**: replaced `SendKey(KeyEventArgs)` with `SendKey(VncKey key, bool isDown)`
- **`IRfbClient.SendKey`**: signature changed to `SendKey(VncKey key, bool isDown)`
- **`VncLibUserCallback`**: signature changed to `void VncLibUserCallback(VncPointerEventArgs e, double x, double y)`
- **`VncConnection.Screenshot`**: removed `System.Drawing.Bitmap` property; use `GetFramebuffer()` + `FramebufferWidth`/`FramebufferHeight` + `FrameArrived` event instead

[2.0.0]: https://github.com/patdhlk/vnclib/releases/tag/v2.0.0
