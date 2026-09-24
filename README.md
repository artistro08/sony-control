# Sony Control

A Windows 11 tray app that controls Sony headphones from a native WinUI flyout.

## Introduction

Click the headphones icon in the tray and you get battery, noise control, ambient sound, Focus on Voice, scenes, equalizer and DSEE in a flyout that looks like Windows' own. Everything else lives in a settings window. The app talks to the headphones over Bluetooth with protocol code based on [sony-device-center](https://github.com/marconvcm/sony-device-center) (MIT).

Supported headphones:

- WF-1000XM6 (verified first)
- WH-1000XM4

## Installing

Grab the latest release from the [Releases](https://github.com/artistro08/sony-control/releases) page. Pick `x64` for most PCs, or `arm64` for Snapdragon/ARM PCs. There are two ways to install:

### Installer (easiest)

Download `SonyControl_<version>_x64.msi` and run it. It installs just for you by default, with no admin prompt. Choose **Advanced** in the wizard if you want to install it for everyone on the PC instead.

> Windows may say it protected your PC because the installer isn't from a known publisher. Click **More info**, then **Run anyway**.

> If you turned on **Start when I sign in**, turn it off before uninstalling. Otherwise a dead entry stays in Settings > Apps > Startup (Windows just skips it).

### MSIX package

The MSIX is the Windows 11 app package. It's signed with a self-made certificate, so you trust that certificate once first:

1. Download `SonyControl.cer` and `SonyControl_<version>_x64.msix`.
2. From an elevated PowerShell in the download folder, trust the certificate:

    ```powershell
    Import-Certificate -FilePath .\SonyControl.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
    ```

3. Double-click the `.msix`.

> Logs live in the app's local data folder. Open them from Settings > General > Open log folder.

## Building From Source

### Prerequisites

- Windows 11 22H2 or newer
- Visual Studio 2026 with these workloads and components:
  - Desktop development with C++ (plus the ARM64 build tools)
  - .NET desktop development
  - Windows App SDK C# and C++ templates

You can install Visual Studio with everything in one go:

```powershell
winget install --id Microsoft.VisualStudio.Community --exact --override "--wait --passive --add Microsoft.VisualStudio.Workload.NativeDesktop --add Microsoft.VisualStudio.Workload.ManagedDesktop --add Microsoft.VisualStudio.ComponentGroup.WindowsAppSDK.Cs --add Microsoft.VisualStudio.ComponentGroup.WindowsAppSDK.Cpp --add Microsoft.VisualStudio.Component.VC.Tools.ARM64 --add Microsoft.VisualStudio.Component.Windows11SDK.26100 --includeRecommended"
```

### Building

Build everything and run the tests:

```powershell
.\scripts\Build.ps1
```

That's it for day-to-day work. XM4 tests are left out by default; add `-IncludeXm4` to run them.

To test against real headphones, set their Bluetooth address first. The hardware tests are skipped without it:

```powershell
$env:SONY_TEST_XM6_ADDRESS = "AC:80:0A:12:34:56"
.\scripts\Build.ps1
```

### Packaging

1. Once, from an elevated PowerShell, create and trust the signing certificate:

    ```powershell
    .\scripts\New-DevCertificate.ps1
    ```

2. Build the signed MSIX packages only:

    ```powershell
    .\scripts\Build-Package.ps1
    ```

    Or build every release file (MSIX and MSI, x64 and ARM64, plus the certificate) into `artifacts\release`:

    ```powershell
    .\scripts\Build-Release.ps1 -Version 1.0.0.0
    ```

## License

MIT, see [LICENSE](LICENSE). The Bluetooth protocol code is based on [sony-device-center](https://github.com/marconvcm/sony-device-center), also MIT; its notice is in [LICENSE-THIRD-PARTY](LICENSE-THIRD-PARTY).

> Sony and the product names are trademarks of Sony Group Corporation. This is an unofficial app, not made or endorsed by Sony.

## Documentation

- Design: [docs/superpowers/specs/2026-09-23-sony-control-design.md](docs/superpowers/specs/2026-09-23-sony-control-design.md)
- Plan: [docs/superpowers/plans/2026-09-23-sony-control.md](docs/superpowers/plans/2026-09-23-sony-control.md)
