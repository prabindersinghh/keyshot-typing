# Contributing to KeyShot

Thanks for helping! KeyShot is small on purpose. The best contributions keep it **fast**, **safe** and **simple**.

## Ground rules

1. **Input is sacred.** KeyShot must never block, delay, modify, swallow or inject keyboard input.
   Every hook callback ends in `CallNextHookEx`. PRs that call `SendInput`, `keybd_event` or anything
   similar will not be merged.
2. **The hook thread is real-time.** Code that runs from `LowLevelKeyboardHook` through
   `KeyShotController.HandleKey` to `IKeyEffect.Fire` must not allocate, lock, do I/O or throw.
   Hand work off to another thread.
3. **The audio thread is real-time too.** `ShotMixer.Read` must never block or throw.
4. **Privacy.** Never log, store or send *what* the user types.

## Development setup

- Windows 10/11, [.NET 10 SDK](https://dot.net/download), any editor (VS 2022+, Rider, VS Code + C# Dev Kit)
- Build: `dotnet build KeyShot.sln`
- Tests: `dotnet test KeyShot.sln`
- Device tests (play real sound): `$env:KEYSHOT_DEVICE_TESTS="1"; dotnet test --filter DeviceTests`
- Run: `dotnet run --project src/KeyShot.App`
- Control a running instance: `scripts/keyshot-ctl.ps1 STATS`

## Pull requests

- One focused change per PR, with tests for logic changes (`tests/KeyShot.Tests`).
- `dotnet build` must have no warnings; all tests must pass.
- If the change touches input or audio timing, run the
  [manual performance test](docs/PERFORMANCE_TESTING.md) and include the numbers.
- Follow the existing style (`.editorconfig`): file-scoped namespaces, `sealed` by default, comments explain *why*.

## Adding a new effect

New features (screen flash, stats, haptics…) should implement `KeyShot.Core.IKeyEffect` and be
registered in `AppHost.Start` with `_controller.AddEffect(...)`. `Fire` runs on the hook thread, so
it should only enqueue work.

## Contributing sound packs

- Only submit audio you made yourself or that's free to use in software (CC0, CC-BY, the Pixabay Content License…).
  Credit the creator in `THIRD_PARTY_NOTICES.md` and commit only the edited slices, not the raw downloads.
- Cut raw recordings with `scripts/make-pack.py` (snaps to the attack, fades tails, normalizes levels).
- Keep shots short: the attack should start within 3 ms (enforced by `Every_pack_loads_and_attacks_instantly`),
  and typical keystroke samples are 80–1500 ms.

## Reporting bugs

Use the issue templates. For crashes, attach `%APPDATA%\KeyShot\keyshot.log`. It contains no typed text.
