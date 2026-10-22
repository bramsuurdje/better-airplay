<img src="assets/icon.svg" width="64" height="64" alt="">

# Better AirPlay

Mirror your iPhone, iPad or Mac to a Windows PC. Better AirPlay is a small tray app around the
open-source [UxPlay](https://github.com/FDH2/UxPlay) receiver. You install one file; there is no MSYS2 setup and no
compiling on your PC.

## Use it

1. Download `BetterAirPlay-Setup-x.y.z.exe` from the [releases](../../releases) and run it.
2. The app starts in the notification area. The icon is red while it receives and grey when it's off.
3. On your iPhone or Mac: **Control Center → Screen Mirroring →** your PC's name.

Right-click the tray icon to:

- start or stop receiving (double-clicking the icon does this too)
- change the name Apple devices see
- require a 4-digit connection code. The code is shown in the menu, and devices enter it once.
- switch low latency on or off. It is on by default and makes mirroring feel snappier. Turn it off when watching
  films, so the sound stays in sync with the picture.
- start the app when you sign in to Windows
- open the receiver log
- check for updates. The app also checks at startup and offers to install new versions; you can turn that off in the menu.

The video opens in its own dark window, titled with the name of the connected device. Press **Alt+Enter** there to
toggle full screen.

### If your PC doesn't show up

- The PC and the Apple device must be on the same network. Guest Wi-Fi with client isolation and VPNs block discovery.
- The installer allows the receiver through Windows Firewall on **Private** networks only. Set your home
  network to Private in Windows Settings → Network & internet.
- Check **Show log** for errors.

DRM-protected video (for example, the Apple TV app) can't be mirrored. One device can connect at a time.

## How it's built

| Part | What it is |
| --- | --- |
| `src/BetterAirPlay` | The tray app. C# WinForms on .NET Framework 4.8, which every Windows 10/11 PC already has. |
| `scripts/bundle-receiver.sh` | Builds UxPlay at a pinned commit in MSYS2. Copies it, the GStreamer plugins, and their DLLs into `receiver/`. |
| `scripts/smoke-test-receiver.ps1` | Checks that the bundle runs without MSYS2 and that all required GStreamer elements are present. |
| `installer/BetterAirPlay.iss` | Inno Setup installer. Installs to Program Files and adds the firewall rule. |
| `.github/workflows/build.yml` | Builds everything on `windows-latest` and uploads the installer as an artifact. Every push to `main` publishes a GitHub release. |

The tray app runs `receiver\bin\uxplay.exe -n "<name>" -nh -vs d3d11videosink [-pin nnnn] [-vsync no]`. It sets the `GST_*`
variables so GStreamer loads only the bundled plugins.

### Develop

```sh
dotnet build src/BetterAirPlay        # compiles on Windows, macOS or Linux
```

To run the app you need Windows and a receiver bundle in `receiver\` next to the exe. Download the CI artifact, or
run `scripts/bundle-receiver.sh` in an MSYS2 UCRT64 shell.

### Release

Every push to `main` builds and publishes a release tagged `v<VERSION_PREFIX>.<run number>` (for example `v0.1.2`).
To start a new minor or major version, change `VERSION_PREFIX` in the workflow. Pull requests only build.

To update UxPlay, change `UXPLAY_COMMIT` in the workflow.

## License

Better AirPlay bundles UxPlay (GPL-3.0), GStreamer, and FFmpeg. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
