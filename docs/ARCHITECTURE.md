# KeyShot architecture

## Projects

| Project | Target | Responsibility | Depends on |
|---|---|---|---|
| `KeyShot.Core` | `net10.0` | Key classification, filtering, cooldown, settings/JSON, hotkey parsing, `IKeyEffect` | — |
| `KeyShot.Input` | `net10.0-windows` | Global `WH_KEYBOARD_LL` hook on its own thread | Core |
| `KeyShot.Audio` | `net10.0-windows` | Decoding, `ShotMixer`, WASAPI output, `AudioEngine` | Core, NAudio |
| `KeyShot.App` | `net10.0-windows` (WPF) | UI, tray, hotkey, control pipe, composition root (`AppHost`) | all |
| `KeyShot.Tests` | `net10.0-windows` | xUnit tests (unit + opt-in device tests) | Core, Audio |

`Core` has no Windows dependencies, so all of the decision logic is unit-testable.

## Data flow

```text
 physical key
     │
     ▼
 Windows input  ──►  WH_KEYBOARD_LL hook  (KeyShot.Input, "hook thread", priority Highest)
                        │ 1. read KBDLLHOOKSTRUCT (pointer, no allocation)
                        │ 2. KeyShotController.HandleKey   (Core)
                        │      classify → filter → cooldown → for each IKeyEffect: Fire(kind)
                        │ 3. CallNextHookEx  ◄── always; input is never altered
                        ▼
                 AudioEngine.Fire  →  ShotMixer.Trigger  →  ConcurrentQueue.Enqueue   (lock-free)
                                                                │
 WASAPI event (every ~10 ms, "audio thread")                    ▼
     └──► ShotMixer.Read: drain queue → start voices → mix (linear-interp pitch) → soft clip
                │
                ▼
          audio driver → speakers
```

## Threads

| Thread | Work | Rules |
|---|---|---|
| **Hook** (dedicated, `Highest`) | `GetMessage` loop + hook callback | No locks, no I/O, no allocation. Windows silently removes LL hooks that take longer than `LowLevelHooksTimeout`. |
| **Audio** (NAudio WASAPI, event-driven) | `ShotMixer.Read` | Never blocks or throws. Always returns a full buffer (returning 0 would stop playback). |
| **UI** (WPF dispatcher) | Window, tray, hotkey (`WM_HOTKEY`), settings, enable/disable | Changes reach the other threads through `volatile` fields and immutable settings snapshots. |
| **Pipe** (thread pool) | `\\.\pipe\KeyShot.Control` | Commands are marshalled onto the UI thread. |

Hook → audio handoff is a `ConcurrentQueue<ShotKind>` bounded to 64 entries: one producer (hook), one
consumer (audio). Voice allocation and random selection happen on the audio thread, so the hook only
does the enqueue.

## Latency budget

| Stage | Typical |
|---|---|
| Hook callback (classify + enqueue) | < 0.05 ms |
| Wait for the next mixer pull | 0–10 ms (avg ≈ 5 ms) |
| WASAPI shared-mode engine period | 10 ms |
| **Key → audio driver (measured, see `DeviceTests`)** | **≈ 11 ms median** |
| Wired output / DAC | 1–5 ms |
| Bluetooth headphones (codec + radio) | +100–250 ms (hardware, can't be avoided) |

Choices that keep latency low:

- **One stream that stays open.** Opening an audio device costs 30–100 ms. KeyShot opens it once when
  enabled and closes it when disabled.
- **Native mix format.** Samples are decoded at the device's own rate and the stream uses the device mix
  format (usually 48 kHz float), so Windows doesn't add a resampler stage.
- **Leading-silence trim.** MP3 encoders add about 25–50 ms of silence at the start. `SoundLoader`
  removes it, leaving a 1 ms pre-roll.
- **Event-driven WASAPI**, with a 10 ms requested buffer (`AudioLatencyMs`).

## Filtering rules (`KeyShotController.Evaluate`)

1. Key-up → silent (but clears the key's "down" flag).
2. Key already down → auto-repeat → silent (`IgnoreAutoRepeat`).
3. Disabled → silent.
4. Injected (`LLKHF_INJECTED`) → silent (`IgnoreInjected`).
5. Modifier → silent unless `FireOnModifiers`.
6. Ctrl / Alt / Win held → shortcut → silent (`IgnoreShortcuts`). Ctrl+Alt together is treated as **AltGr**
   (typing on international layouts) and is allowed.
7. Printable → Normal · Space → Heavy · Enter → Strong · other keys → only with `FireOnOtherKeys`.
8. Cooldown gate (measured from the last *accepted* shot).

## Resilience

- Every effect is called inside `try/catch`, and errors are reported through `EffectFailed`. An exception
  never reaches the hook.
- `AudioEngine` never throws. Failures move it to `Faulted`. Device removal or a default-device change
  triggers an automatic re-open on a worker thread, and a 5 s watchdog retries while `Faulted`.
- Corrupt sound files are skipped. A corrupt `settings.json` falls back to the defaults.
- When disabled, the hook is **uninstalled** and the audio stream **closed**, so KeyShot uses nothing.

## Extensibility

New features implement `IKeyEffect` (`Name`, `Fire(ShotKind)`) and are registered with
`KeyShotController.AddEffect`. Ideas: muzzle-flash overlay, recoil cursor shake, a shot counter, RGB keyboard flash.

## Control pipe

`\\.\pipe\KeyShot.Control`, restricted to the current user (`PipeOptions.CurrentUserOnly`). One line in,
one line out:

| Command | Reply |
|---|---|
| `PING` | `PONG` |
| `STATUS`, `ENABLE`, `DISABLE`, `TOGGLE`, `SHOW` | `ACTIVE` / `INACTIVE` |
| `STATS` | `ACTIVE keys=… shots=… voices=… audio=Running hook=True hotkey=… device="…"` |

The second-instance launcher, `scripts/keyshot-ctl.ps1` and the VS Code extension all use it.

## Smart App Control

On Windows 11 with **Smart App Control = On**, unsigned binaries are judged one at a time against
Microsoft's cloud reputation. Locally built files have no reputation, so each build gets its own verdict:
some run, some are blocked with `0x800711C7`.

- `install.ps1` publishes a **single-file** exe. The managed DLLs are bundled inside it and read from there
  instead of being loaded as separate images, so only `KeyShot.exe` itself is checked.
- Builds are deterministic: the same source produces the same file, so a build that was allowed stays allowed.
- For public releases, sign the exe with a certificate trusted by Microsoft (for example Azure Trusted Signing).
