<p align="center">
  <img src="docs/logo.png" width="96" alt="KeyShot logo" />
</p>

<h1 align="center">KeyShot</h1>

<p align="center"><b>Every keystroke is a gunshot.</b><br/>
A tiny Windows app that plays shotgun, machine-gun and laser sounds as you type, in every app, with almost no delay.</p>

<p align="center">
  <a href="https://github.com/prabindersinghh/keyshot-typing/actions/workflows/ci.yml"><img src="https://github.com/prabindersinghh/keyshot-typing/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="https://github.com/prabindersinghh/keyshot-typing/releases/latest"><img src="https://img.shields.io/github/v/release/prabindersinghh/keyshot-typing" alt="Latest release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT license" /></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11%20(64--bit)-0078D6" alt="Windows 10/11 64-bit" />
</p>

<p align="center">
  <img src="docs/screenshots/main-window.png" width="380" alt="KeyShot main window" />
</p>

---

## Install in 30 seconds

**You need:** Windows 10 or 11 (64-bit). Nothing else: no admin rights, no extra downloads.

1. **Open PowerShell:** press the <kbd>Windows</kbd> key, type `powershell`, press <kbd>Enter</kbd>.
2. **Paste this line and press <kbd>Enter</kbd>:**

   ```powershell
   irm https://raw.githubusercontent.com/prabindersinghh/keyshot-typing/main/scripts/get-keyshot.ps1 | iex
   ```

3. **KeyShot opens by itself.** Click **ENABLE** and start typing anywhere. 🔫

The installer also puts a **KeyShot** icon on your Desktop and in the Start Menu. To update later, run the same
line again; your settings are kept.

<details>
<summary><b>Prefer not to use PowerShell? Install manually</b></summary>

1. Download **`KeyShot-win-x64.zip`** from the [latest release](https://github.com/prabindersinghh/keyshot-typing/releases/latest).
2. Right-click the zip → **Extract All…** → **Extract**.
3. Open the extracted folder and double-click **`KeyShot.exe`**.

Keep the `Sounds` folder next to `KeyShot.exe`; that's where the sounds live.
</details>

> **"Windows protected your PC"?** KeyShot isn't code-signed yet, so Windows SmartScreen asks first.
> Click **More info → Run anyway**. You'll only see this once.

## How to use

| To… | Do this |
|---|---|
| Turn sounds **on / off** | Click **ENABLE / DISABLE**, or press **Ctrl+Shift+K** in any app |
| Change the **sound** | Pick a pack under **Mode** |
| Change **loudness** | Drag **Volume** |
| Hear a sample | Click the ▷ button |
| Hide KeyShot | Close the window. It keeps running in the system tray (bottom-right, near the clock) |
| Bring it back | Double-click the **KeyShot** Desktop icon, or click the tray icon |
| Quit completely | Right-click the tray icon → **Exit** |

KeyShot remembers whether it was on or off, and all your settings.

<details>
<summary><b>All controls</b></summary>

| Control | What it does |
|---|---|
| **ENABLE / DISABLE** | Turns shots on or off |
| **Volume** | Master volume |
| **Variation** | Random pitch (up to ±2 semitones) and volume spread per shot, so it never sounds robotic |
| **Cooldown** | Minimum time between shots (0–250 ms). Raise it if fast typing feels too busy |
| **Mode** | Sound pack |
| **Heavy Space / Strong Enter** | Bigger shots for Space and Enter |
| ▷ | Test shot |
| **Hotkey → Change** | Press a new key combination (Esc cancels) |

Command line (also controls an already-running KeyShot):

```text
KeyShot.exe [--enable | --disable | --toggle] [--minimized]
```

Settings live in `%APPDATA%\KeyShot\settings.json`. A few advanced options (`FireOnOtherKeys`, `FireOnModifiers`,
`IgnoreShortcuts`, `IgnoreInjected`, `IgnoreAutoRepeat`, `AudioLatencyMs`, `CloseToTray`) can be changed there.
</details>

## Sound packs

Six packs are included:

| Mode | Normal keys | Space | Enter |
|---|---|---|---|
| **Pump Shotgun** *(default)* | full shotgun blast | deeper blast | blast + second sound |
| **Shotgun** | 3 synthesized blasts | deeper blast | deepest blast |
| **Echo Gun** | dry gunshot | shot + room | shot + long echo |
| **Assault Rifle** | 6 single rounds | last round ringing out | 3-round burst |
| **Minigun** | end of a burst | longer burst | full burst |
| **Laser Pistol** | 4 different pews | zap with tail | rapid volley |

The **Shotgun** pack is original, synthesized by [`scripts/generate-sounds.ps1`](scripts/generate-sounds.ps1)
(CC0). The other packs are edited from free recordings on Pixabay. Thanks to **Universfield**, **flutie8211**,
**qubodup**, **ScottishPerson** and **NXRT**. Sources and licenses are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

<details>
<summary><b>Add your own sounds</b></summary>

Open KeyShot's install folder (paste `%LOCALAPPDATA%\Programs\KeyShot` into File Explorer's address bar), go into
`Sounds`, and create a new folder. The folder name becomes the **Mode** name:

```text
Sounds/
  MyPack/
    shot1.wav         ← one or more files (.wav or .mp3), one picked at random per keystroke
    shot2.wav
    space-boom.wav    ← optional: files starting with "space" play on Space
    enter-burst.wav   ← optional: files starting with "enter" play on Enter
```

Restart KeyShot (tray icon → **Exit**, then open it again) and pick your pack under **Mode**. Silence at the start of a
file is trimmed automatically, so shots fire instantly.

To cut packs from long raw recordings, use [`scripts/make-pack.py`](scripts/make-pack.py). It snaps each cut to
the real attack, fades the tail and matches levels. Describe the cuts in a JSON recipe:

```json
{ "source_dir": "sounds-raw",
  "packs": { "Laser Pistol": [
    { "src": "laser.mp3", "name": "pew-1",        "start": 2246, "end": 2470, "fade_out": 40 },
    { "src": "laser.mp3", "name": "enter-volley", "start": 2973, "end": 3680, "fade_out": 80 } ] } }
```

```powershell
python scripts\make-pack.py my-recipe.json      # needs Python with numpy, and ffmpeg
```
</details>

## Troubleshooting

| Problem | Fix |
|---|---|
| **No sound** | Check the status pill says **ACTIVE** (press Ctrl+Shift+K). Click ▷ to test. Check Windows' volume and that your speakers/headphones are the default output device. |
| **Sound is late** | Bluetooth headphones add 100–250 ms of delay that no app can remove. Use wired headphones or speakers for instant shots. |
| **Nothing plays in one app** | That app is probably running *as administrator*. Windows hides its keys from normal apps. Run KeyShot as administrator too if you need it there. |
| **Ctrl+Shift+K does nothing** | Another app is using that hotkey. KeyShot shows a warning. Click **Change** next to *Hotkey* and pick another combination. |
| **"An Application Control policy has blocked this file"** | Windows 11's **Smart App Control** blocks apps that aren't code-signed, and KeyShot isn't yet. You can turn Smart App Control off in *Windows Security → App & browser control → Smart App Control*. Note that Windows may not let you turn it back on without a reset. |
| **Window is partly off-screen** | Drag it by its title bar (happens on small screens with high display scaling). |
| **Something else** | [Open an issue](https://github.com/prabindersinghh/keyshot-typing/issues/new/choose) and attach `%APPDATA%\KeyShot\keyshot.log` (it never contains what you typed). |

## Uninstall

Paste into PowerShell:

```powershell
Stop-Process -Name KeyShot -ErrorAction SilentlyContinue; Remove-Item "$env:LOCALAPPDATA\Programs\KeyShot", "$env:APPDATA\KeyShot", "$([Environment]::GetFolderPath('Desktop'))\KeyShot.lnk", "$([Environment]::GetFolderPath('Programs'))\KeyShot.lnk", "$([Environment]::GetFolderPath('Startup'))\KeyShot.lnk" -Recurse -Force -ErrorAction SilentlyContinue
```

This removes the app, its settings and its shortcuts. Nothing else on your PC is touched.

## VS Code extension (optional)

Shows KeyShot's status in VS Code's status bar, and adds **KeyShot: Enable / Disable / Toggle / Launch** commands.
You need the KeyShot app installed first (above).

1. Download **`keyshot-vscode-<version>.vsix`** from the [latest release](https://github.com/prabindersinghh/keyshot-typing/releases/latest).
2. In VS Code, open **Extensions** (<kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>X</kbd>) → **⋯** menu → **Install from VSIX…** → pick the file.

Click the **KeyShot** item in the status bar to turn sounds on or off. See [vscode-extension/README.md](vscode-extension/README.md).

## Privacy & safety

KeyShot only reacts to *which kind* of key was pressed (letter, Space, Enter…). It does **not** record, store,
log or send what you type. It makes no network connections, and it never sends keystrokes to Windows.
See [SECURITY.md](SECURITY.md).

## For developers

**Build & install from source** (needs the [.NET 10 SDK](https://dot.net/download)):

```powershell
git clone https://github.com/prabindersinghh/keyshot-typing.git
cd keyshot-typing
powershell -ExecutionPolicy Bypass -File scripts\install.ps1 -Launch     # add -Autostart to start with Windows
```

```powershell
dotnet build KeyShot.sln -c Release
dotnet test  KeyShot.sln
# tests that play sound on the real device and measure latency
$env:KEYSHOT_DEVICE_TESTS = "1"; dotnet test KeyShot.sln --filter DeviceTests
```

Run without installing: `dotnet run --project src/KeyShot.App`.

**Architecture:**

```text
KeyShot.App    WPF UI, tray, hotkey, control pipe, composition root
KeyShot.Input  WH_KEYBOARD_LL hook on a dedicated high-priority thread
KeyShot.Audio  Sample decoding, lock-free polyphonic mixer, WASAPI output
KeyShot.Core   Key classification, filtering, cooldown, settings, IKeyEffect
```

A key press reaches the audio driver in about 11 ms: samples are pre-decoded to the sound card's native format,
one WASAPI stream stays open, and the keyboard hook only enqueues work. See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/PERFORMANCE_TESTING.md](docs/PERFORMANCE_TESTING.md).

## Contributing

PRs are welcome. New sound packs (with licenses that allow use in software), new effects and fixes are all good
places to start. Read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md).
Report security issues privately as described in [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE) for the code. The sound packs have their own licenses (CC0 and the Pixabay Content License), listed
in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

KeyShot (this project) isn't affiliated with or endorsed by Luxion or its KeyShot® 3D rendering software.
