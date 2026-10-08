# KeyShot for VS Code

Optional companion for the **KeyShot desktop app**. KeyShot itself works globally in every
Windows app; this extension just adds VS Code conveniences:

- Status bar indicator: `◎ KeyShot` (active), `⊘ KeyShot` (inactive), warning when not running. Click to toggle.
- Commands: **KeyShot: Enable / Disable / Toggle / Launch App / Open Settings Window**
- Optional auto-launch of KeyShot when VS Code starts (`keyshot.launchOnStartup`).

It talks to the app over the local named pipe `\\.\pipe\KeyShot.Control` (current user only).
The extension never reads or sends keystrokes.

## Install (from source)

```powershell
cd vscode-extension
npx @vscode/vsce package          # produces keyshot-vscode-1.0.0.vsix
code --install-extension keyshot-vscode-1.0.0.vsix
```

Or for development: open this folder in VS Code and press <kbd>F5</kbd>.

## Settings

| Setting | Default | Description |
|---|---|---|
| `keyshot.executablePath` | `%LOCALAPPDATA%\Programs\KeyShot\KeyShot.exe` | Where to find KeyShot |
| `keyshot.launchOnStartup` | `false` | Launch KeyShot (minimized) with VS Code |
| `keyshot.pollIntervalSeconds` | `3` | Status refresh interval |
