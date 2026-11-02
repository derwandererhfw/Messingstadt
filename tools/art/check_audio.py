"""Misst den Ton und schlägt fehl, wenn eine Altlast zurückkommt: lauter Regen, klackernde Stiefel, laute Musik.

Aufruf: python tools/art/check_audio.py [--neu]
--neu erzeugt zusätzlich alles in einen Wegwerfordner und vergleicht Byte für Byte.
Die Messung ersetzt kein Hören. dB(A) ist nur eine Näherung ans Gehör.
"""
import hashlib
import re
import sys
import tempfile
import wave
from pathlib import Path

import numpy as np

import generate_audio

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT / "game" / "assets" / "audio"
SOUNDSCAPE = (ROOT / "game" / "SoundScape.cs").read_text(encoding="utf-8")

MUSIC_LAYERS = ("music_calm", "music_dark", "music_suspect", "music_tension")
LOOPS = ("rain", "wind", "wind_gust", "lamp", "neon", "steam", "fire", "diesel", "machine", "television", "airship", "weld", "radio", "heartbeat", "lockpick", "gutter", "piano") + MUSIC_LAYERS
STEPS = tuple(f"step_{kind}_{i}" for kind in ("soft", "boot", "run") for i in range(4))
CITY = ("distant_shots", "tram", "foghorn", "church_bell", "shutter")

MAX_STEP_ABOVE_2K = 0.2
MAX_RAIN_GAME_DBA = -45.0
MAX_MUSIC_GAME_DBA = -43.0
MAX_MUSIC_ABOVE_2K = 0.1
MAX_CITY_ABOVE_2K = 1.0
MAX_SEAM = 6.0
MAX_FIGHT_BELOW_120 = 60.0
MAX_HEART_BELOW_120 = 75.0
MUSIC_SECONDS = 96


def load(name):
    with wave.open(str(AUDIO / f"{name}.wav")) as f:
        rate, channels = f.getframerate(), f.getnchannels()
        data = np.frombuffer(f.readframes(f.getnframes()), "<i2").astype(float) / 32768
    return data.reshape(-1, channels), rate


def db(value):
    return 20 * np.log10(max(value, 1e-9))


def a_weight(f):
    f2 = f ** 2
    return (12194 ** 2 * f2 ** 2) / ((f2 + 20.6 ** 2) * np.sqrt((f2 + 107.7 ** 2) * (f2 + 737.9 ** 2)) * (f2 + 12194 ** 2) + 1e-30) * 10 ** 0.1


def spectrum(x, rate):
    power = np.abs(np.fft.rfft(x)) ** 2
    return power, np.fft.rfftfreq(len(x), 1 / rate)


def dba(x, rate):
    """Der lautere Kanal zählt."""
    best = -200.0
    for channel in x.T:
        power, f = spectrum(channel, rate)
        best = max(best, db(np.sqrt((power * a_weight(f) ** 2).sum() * 2) / len(channel)))
    return best


def share(x, rate, low, high):
    power, f = spectrum(x.mean(axis=1), rate)
    return power[(f >= low) & (f < high)].sum() / power.sum() * 100


def code_db(name):
    """Liest eine Lautstärke aus SoundScape.cs, damit Prüfung und Spiel nicht auseinanderlaufen."""
    return float(re.search(rf"const float {name} = (-?[\d.]+)f", SOUNDSCAPE).group(1))


def main():
    failures = []

    def check(ok, text):
        print(("ok    " if ok else "FEHLER") + " " + text)
        if not ok:
            failures.append(text)

    for name in STEPS:
        x, rate = load(name)
        check(share(x, rate, 2000, 1e9) <= MAX_STEP_ABOVE_2K, f"{name}: über 2 kHz {share(x, rate, 2000, 1e9):.2f} %")

    rain_db = code_db("RainDb")
    x, rate = load("rain")
    check(dba(x, rate) + rain_db <= MAX_RAIN_GAME_DBA, f"Regen im Schauer: {dba(x, rate) + rain_db:.1f} dB(A), Grenze {MAX_RAIN_GAME_DBA}")

    music_db = code_db("MusicDb")
    calm_power = 0.0
    for name in MUSIC_LAYERS:
        x, rate = load(name)
        level = dba(x, rate) + music_db
        check(abs(len(x) / rate - MUSIC_SECONDS) < 1e-9, f"{name}: Länge {len(x) / rate:.3f} s")
        check(share(x, rate, 2000, 1e9) <= MAX_MUSIC_ABOVE_2K, f"{name}: über 2 kHz {share(x, rate, 2000, 1e9):.3f} %")
        check(level <= MAX_MUSIC_GAME_DBA, f"{name}: {level:.1f} dB(A), Grenze {MAX_MUSIC_GAME_DBA}")
        if name in ("music_calm", "music_suspect"):
            calm_power += 10 ** (level / 10)
    both = 10 * np.log10(calm_power)
    check(both <= MAX_MUSIC_GAME_DBA, f"Ruhe und Verdacht zusammen: {both:.1f} dB(A), Grenze {MAX_MUSIC_GAME_DBA}")

    x, rate = load("music_tension")
    check(share(x, rate, 0, 120) <= MAX_FIGHT_BELOW_120, f"Gefecht unter 120 Hz: {share(x, rate, 0, 120):.0f} %")
    x, rate = load("heartbeat")
    check(share(x, rate, 0, 120) <= MAX_HEART_BELOW_120, f"Herzschlag unter 120 Hz: {share(x, rate, 0, 120):.0f} %")

    for name in CITY:
        x, rate = load(name)
        check(share(x, rate, 2000, 1e9) <= MAX_CITY_ABOVE_2K, f"{name}: über 2 kHz {share(x, rate, 2000, 1e9):.2f} %")

    for name in LOOPS:
        x, _ = load(name)
        seam = np.abs(x[0] - x[-1]).max() / (np.median(np.abs(np.diff(x, axis=0))) + 1e-9)
        check(seam <= MAX_SEAM, f"{name}: Sprung an der Naht {seam:.1f}-fach")

    for path in sorted(AUDIO.glob("*.wav")):
        x, _ = load(path.stem)
        check(np.abs(x).max() < 0.999, f"{path.stem}: Spitze {db(np.abs(x).max()):.1f} dBFS")

    if "--neu" in sys.argv:
        with tempfile.TemporaryDirectory() as folder:
            generate_audio.OUT = Path(folder)
            generate_audio.main()
            for path in sorted(AUDIO.glob("*.wav")):
                other = Path(folder) / path.name
                same = other.exists() and hashlib.sha256(other.read_bytes()).digest() == hashlib.sha256(path.read_bytes()).digest()
                check(same, f"{path.stem}: bytegleich neu erzeugt")

    print(f"\n{len(failures)} Fehler")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
