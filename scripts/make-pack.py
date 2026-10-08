"""
Build KeyShot sound packs from raw recordings.

Requires: Python 3.9+, numpy, and ffmpeg on PATH (decodes any format).

Usage:
    python scripts/make-pack.py recipe.json [--out src/KeyShot.App/Sounds]

Recipe format (times in milliseconds):
{
  "source_dir": "sounds-raw",
  "packs": {
    "Laser Pistol": [
      {"src": "laser.mp3", "name": "pew-1",      "start": 1979, "end": 2240},
      {"src": "laser.mp3", "name": "space-zap",  "start": 5253, "end": 5607, "fade_out": 120},
      {"src": "laser.mp3", "name": "enter-volley", "start": 1979, "end": 2970}
    ]
  }
}

Per slice:
  start / end    rough cut points; "start" snaps to the real attack within +-snap_ms
  fade_out       fade length at the end (default 30 ms; prevents clicks)
  snap_ms        onset search window (default 15; 0 disables)
  peak_db        normalize the slice peak to this level (default -1 dBFS)

Names starting with "space" / "enter" become KeyShot's heavy / strong shots.
Output: 48 kHz, 16-bit stereo WAV.
"""
import argparse
import json
import subprocess
import sys
import wave
from pathlib import Path

import numpy as np

SR = 48000


def decode(path: Path) -> np.ndarray:
    raw = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
        capture_output=True, check=True,
    ).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).copy()


def snap_to_attack(x: np.ndarray, start: int, window: int) -> int:
    """Move `start` to 1 ms before the steepest rise in [start-window, start+window]."""
    if window <= 0:
        return start
    a, b = max(0, start - window), min(len(x), start + window)
    if b - a < 8:
        return start
    env = np.abs(x[a:b]).max(axis=1)
    smooth = np.convolve(env, np.ones(48) / 48, mode="same")  # 1 ms
    rise = np.diff(smooth, prepend=smooth[0])
    attack = a + int(np.argmax(rise))
    return max(0, attack - SR // 1000)


def make_slice(x: np.ndarray, spec: dict) -> np.ndarray:
    ms = SR // 1000
    start = snap_to_attack(x, int(spec["start"]) * ms, int(spec.get("snap_ms", 15)) * ms)
    end = min(len(x), int(spec["end"]) * ms)
    if end - start < 10 * ms:
        raise ValueError(f"slice {spec.get('name')} is shorter than 10 ms")
    s = x[start:end].copy()

    s -= s.mean(axis=0)  # remove DC offset so the fade lands on zero
    fade = min(len(s), int(spec.get("fade_out", 30)) * ms)
    if fade > 0:
        s[-fade:] *= np.linspace(1.0, 0.0, fade, dtype=np.float32)[:, None] ** 2
    fade_in = min(len(s), ms // 2)  # 0.5 ms: removes a cut click, keeps the transient
    s[:fade_in] *= np.linspace(0.0, 1.0, fade_in, dtype=np.float32)[:, None]

    peak = np.abs(s).max()
    if peak > 0:
        s *= (10 ** (float(spec.get("peak_db", -1.0)) / 20)) / peak
    return s


def write_wav(path: Path, s: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = (np.clip(s, -1, 1) * 32767).round().astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("recipe", type=Path)
    ap.add_argument("--out", type=Path, default=Path(__file__).resolve().parent.parent / "src" / "KeyShot.App" / "Sounds")
    args = ap.parse_args()

    recipe = json.loads(args.recipe.read_text(encoding="utf-8"))
    source_dir = (args.recipe.parent / recipe.get("source_dir", ".")).resolve()
    cache: dict[str, np.ndarray] = {}

    for pack, slices in recipe["packs"].items():
        for spec in slices:
            src = spec["src"]
            if src not in cache:
                cache[src] = decode(source_dir / src)
            s = make_slice(cache[src], spec)
            out = args.out / pack / f"{spec['name']}.wav"
            write_wav(out, s)
            print(f"{pack:>14} / {spec['name']:<16} {len(s) * 1000 // SR:5d} ms  <- {src} @{spec['start']} ms")
    return 0


if __name__ == "__main__":
    sys.exit(main())
