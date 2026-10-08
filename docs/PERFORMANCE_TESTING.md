# Performance testing

## 1. Automated device tests (≈ 10 s)

These play quiet shots on the default output device.

```powershell
$env:KEYSHOT_DEVICE_TESTS = "1"
dotnet test KeyShot.sln --filter DeviceTests --logger "console;verbosity=detailed"
```

| Test | Checks | Pass |
|---|---|---|
| `Software_pipeline_latency_is_low` | `Trigger()` → samples handed to WASAPI, plus the engine period | < 30 ms (typical ≈ 11 ms) |
| `Real_device_plays_audible_shot` | The shot shows up in the system loopback mix | heard |

Reference result (Realtek speakers, 48 kHz, 10 ms engine period):
`median pickup 1.3 ms + period 10.0 ms ≈ 11.3 ms`.

## 2. Manual: extremely fast typing

**Setup:** wired headphones or speakers (not Bluetooth). Volume around 50%. Close other audio apps.
Open Task Manager → *Details*, and add the CPU and Memory columns.

| # | Step | Expected |
|---|---|---|
| 1 | Launch KeyShot and confirm **ACTIVE**. Run `scripts\keyshot-ctl.ps1 STATS`. | `audio=Running hook=True` |
| 2 | **Idle:** leave it alone for 60 s. | KeyShot CPU ≈ 0%, memory steady |
| 3 | **Normal typing:** type a paragraph in Notepad. | Each character gets one shot; the sound feels simultaneous with the keypress |
| 4 | **Burst:** roll fingers across `asdfghjkl;` as fast as possible for 10 s. | No characters lost or reordered in Notepad; shots overlap; no crackle/distortion; CPU < 3% |
| 5 | **Mash:** press 4–6 keys at once, repeatedly, for 10 s. | No missing characters; no stuck keys; audio stays clean (soft-clipped, never harsh) |
| 6 | **Hold:** hold `a` for 3 s. | Notepad auto-repeats `aaaa…`; KeyShot fires **once** |
| 7 | **Shortcuts:** Ctrl+C, Ctrl+V, Alt+Tab, Win+E. | Shortcuts work normally; no shots |
| 8 | **Space/Enter:** type words and press Enter. | Space is deeper, Enter strongest |
| 9 | **Toggle under load:** while typing, press Ctrl+Shift+K twice. | Silence while inactive, then sound again; no characters lost |
| 10 | **Cooldown:** set Cooldown to 150 ms and repeat step 4. | Fewer shots (at most one per 150 ms); typing unaffected |
| 11 | **Device switch:** plug/unplug headphones (or change the default device) while typing. | Audio follows the new device within about 1 s; typing unaffected |
| 12 | **Hook health:** after the burst tests, run `STATS`. | `hook=True`, and `keys` keeps increasing as you type |

Compare **typing latency** with KeyShot enabled and disabled (for example on <https://www.typingtest.com>).
There should be no noticeable difference, and no words-per-minute drop caused by input lag.

### Typing-latency A/B (optional, more rigorous)

1. Record the screen at 240 fps (phone slow-motion works) with the keyboard in frame.
2. Count frames between the key bottoming out and the character appearing, with KeyShot on and off.
3. The difference should be 0 frames (< 4 ms). KeyShot never delays input: its hook only enqueues work and
   returns.

## Reporting

Include in PRs that affect input or audio: device name and type (wired/BT), `DeviceTests` output,
CPU during step 4, and any deviation from the expected results.
