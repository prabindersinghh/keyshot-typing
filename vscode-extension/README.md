# KeyShot for VS Code

Optional companion for the **KeyShot desktop app**. KeyShot itself works globally in every
Windows app; this extension just adds VS Code conveniences:

- Status bar indicator: `◎ KeyShot` (active), `⊘ KeyShot` (inactive), warning when not running. Click to toggle.
- Commands: **KeyShot: Enable / Disable / Toggle / Launch App / Open Settings Window**
- Optional auto-launch of KeyShot when VS Code starts (`keyshot.launchOnStartup`).

It talks to the app over the local named pipe `\\.\pipe\KeyShot.Control` (current user only).
The extension never reads or sends keystrokes.

## Install

1. Install the KeyShot app first. See [the main README](../README.md#install-in-30-seconds).
2. Download **`keyshot-vscode-<version>.vsix`** from the
   [latest release](https://github.com/prabindersinghh/keyshot-typing/releases/latest).
3. In VS Code: **Extensions** (<kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>X</kbd>) → **⋯** → **Install from VSIX…** → pick the file.

Build it yourself: `cd vscode-extension; npx @vscode/vsce package`. For development, open this folder in
VS Code and press <kbd>F5</kbd>.

## Settings

| Setting | Default | Description |
|---|---|---|
| `keyshot.executablePath` | `%LOCALAPPDATA%\Programs\KeyShot\KeyShot.exe` | Where to find KeyShot |
| `keyshot.launchOnStartup` | `false` | Launch KeyShot (minimized) with VS Code |
| `keyshot.pollIntervalSeconds` | `3` | Status refresh interval |
