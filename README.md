# VncLib

Implementation of the VNC Remote Framebuffer (RFB) Protocol

## Getting Started

These instructions will get you a copy of the project up and running on your local machine for development and testing purposes. See deployment for notes on how to deploy the project on a live system.

### Prerequisites

* .NET 8, 9, or 10 (for VncLib core and VncLib.Avalonia)
* .NET Framework 4.8 is also supported by VncLib core and VncLib.Wpf

### Installing

VncLib is available as NuGet packages:

```bash
# Core library (UI-neutral, netstandard2.0 + net8.0/9.0/10.0)
dotnet add package VncLib

# WPF control (net48 + net8.0/9.0/10.0-windows)
dotnet add package VncLib.Wpf

# Avalonia control (cross-platform, net8.0/9.0/10.0)
dotnet add package VncLib.Avalonia
```

### Packages

**VncLib** — The core UI-neutral library implementing the VNC Remote Framebuffer (RFB) Protocol. Provides raw BGR32 framebuffer data via `VncConnection.GetFramebuffer()` and the `FrameArrived` event, and input methods via `RfbClient.SendKey(VncKey, bool)` and `SendMouseClick(...)`. Targets netstandard2.0, net8.0, net9.0, and net10.0.

**VncLib.Wpf** — WPF control (`VncLibControl`) for Windows applications. Targets net48, net8.0-windows, net9.0-windows, and net10.0-windows.

**VncLib.Avalonia** — Cross-platform Avalonia 12 control (`VncControl`) supporting Windows, Linux, and macOS. Targets net8.0, net9.0, and net10.0.

See the VncLib.Client project for WPF usage examples.

## Built With

* [zlib.NET](http://www.componentace.com/zlib_.NET.htm/) - ZLIB.NET is a 100% managed version of ZLIB compression library which implements deflate and inflate compression algorithms. (currently not used - will be used when we support zlib encoding)

## Contributing

Please read [CONTRIBUTING.md](https://github.com/patdhlk/vnclib/blob/master/CONTRIBUTING.md) for details on our code of conduct, and the process for submitting pull requests to us.

## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, see the [releases on this repository](https://github.com/patdhlk/vnclib/releases). 

## Authors

* **patdhlk** - *Initial work* - [patdhlk](https://github.com/patdhlk)
* **theKBro** - *.NET support* - [theKBro](https://github.com/theKBro)

See also the list of [contributors](https://github.com/patdhlk/vnclib/contributors) who participated in this project.

## License

This project is licensed under the three-clause BSD license - see the [LICENSE](LICENSE.md) file for details
