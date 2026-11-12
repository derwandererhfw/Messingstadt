"""Misst den Ton und schlägt fehl, wenn eine Altlast zurückkommt: lauter Regen, klackernde Stiefel, laute Musik.

Aufruf: python tools/art/check_audio.py [--neu]
--neu erzeugt zusätzlich alles in einen Wegwerfordner und vergleicht Byte für Byte.
Die Messung ersetzt kein Hören. dB(A) ist nur eine Näherung ans Gehör.
"""
import hashlib
import json
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
LOOPS = ("rain", "wind", "wind_gust", "lamp", "neon", "steam", "fire", "diesel", "machine", "television", "airship", "weld", "radio", "heartbeat", "lockpick", "gutter", "piano", "tram_hum", "music_title", "crowd_steps") + MUSIC_LAYERS
STEPS = tuple(f"step_{kind}_{i}" for kind in ("soft", "boot", "run") for i in range(4))
CITY = ("distant_shots", "tram", "foghorn", "church_bell", "shutter")

MAX_STEP_ABOVE_2K = 6.0
MAX_STEP_ABOVE_4K = 0.1
MAX_STEP_TONE = 40.0
MIN_STEP_ABOVE_250 = 30.0
GUNS = tuple(f"{kind}_{i}" for kind, count in (("shot", 3), ("shotgun", 3), ("rifle", 3), ("smg", 4)) for i in range(count))
MAX_GUN_ABOVE_4K = 8.0
MAX_GUN_BELOW_120 = 60.0
DULL = ("hit_0", "hit_1", "hit_2", "fall_0", "fall_1", "club_hit_0", "club_hit_1", "club_swing", "smoke_throw", "reload_pistole", "reload_schrotflinte", "reload_maschinenpistole", "bandage")
MAX_DULL_ABOVE_4K = 2.0
# Signale lagen bis zu 27 dB über der Musik. Jetzt haben sie eine Obergrenze in der Datei und nichts mehr über 4 kHz.
SIGNALS = {"whistle": -24.0, "boy_whistle": -24.0, "alarm_bell": -28.0, "coins": -28.0, "chain": -32.0, "click": -32.0, "lockpick": -30.0}
MAX_SIGNAL_ABOVE_4K = 1.0
MAX_RAIN_ABOVE_4K = 15.0
FOR_OTHERS = ("gasp_0", "gasp_1", "shop_buy", "shop_sell", "shop_equip", "map_select", "map_accept", "stamp", "crowd_steps", "tram_spark", "cell_door", "keys")
MAX_OTHERS_ABOVE_4K = 4.0
MAX_RAIN_GAME_DBA = -49.0
MAX_MUSIC_GAME_DBA = -43.0
MAX_MUSIC_ABOVE_2K = 0.1
MAX_CITY_ABOVE_2K = 1.0
MAX_SEAM = 6.0
MAX_MATCH_ABOVE_4K = 10.0
MAX_MENU_ABOVE_4K = 5.0
MAX_MENU_BELOW_120 = 50.0
MAX_PEIL_ABOVE_2K = 3.0
STINGERS = ("music_in_fight", "music_to_calm")
# Dampfstoß und Schweißen: nach dem Urteil des Nutzers gedeckelt, im Pegel und in den Höhen.
HISSES = {"steam_burst": (-30.0, 12.0), "weld": (-35.0, 3.0)}
MAX_STINGER_GAME_DBA = -38.0
MAX_PEIL_DBA = -28.0
MIN_PEIL_GAME_DBA = -50.0
MIN_PEIL_ABOVE_400 = 40.0
TITLE_SECONDS = 72
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


def tone_share(x):
    """Anteil der Energie in den fünf stärksten schmalen Linien. Hoch heißt: nackte Töne."""
    power = np.abs(np.fft.rfft(x.mean(axis=1))) ** 2
    total = power.sum()
    found = 0.0
    for _ in range(5):
        k = int(np.argmax(power))
        low, high = int(k * 0.985), int(k * 1.015) + 2
        found += power[low:high].sum()
        power[low:high] = 0
    return found / total * 100


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
        # Klackern war hell und tonal. Schritte bleiben deshalb Rauschen ohne Höhen, aber mit Mitten, damit man sie überhaupt hört.
        check(share(x, rate, 4000, 1e9) <= MAX_STEP_ABOVE_4K, f"{name}: über 4 kHz {share(x, rate, 4000, 1e9):.3f} %")
        check(tone_share(x) <= MAX_STEP_TONE, f"{name}: Tonanteil {tone_share(x):.0f} %")
        check(share(x, rate, 250, 1e9) >= MIN_STEP_ABOVE_250, f"{name}: über 250 Hz {share(x, rate, 250, 1e9):.0f} %")

    for name in GUNS:
        x, rate = load(name)
        check(share(x, rate, 4000, 1e9) <= MAX_GUN_ABOVE_4K, f"{name}: über 4 kHz {share(x, rate, 4000, 1e9):.1f} %")
        check(share(x, rate, 0, 120) <= MAX_GUN_BELOW_120, f"{name}: unter 120 Hz {share(x, rate, 0, 120):.0f} %")
    for name in DULL:
        x, rate = load(name)
        check(share(x, rate, 4000, 1e9) <= MAX_DULL_ABOVE_4K, f"{name}: über 4 kHz {share(x, rate, 4000, 1e9):.2f} %")

    for name, limit in SIGNALS.items():
        x, rate = load(name)
        check(dba(x, rate) <= limit, f"{name}: {dba(x, rate):.1f} dB(A) in der Datei, Grenze {limit}")
        check(share(x, rate, 4000, 1e9) <= MAX_SIGNAL_ABOVE_4K, f"{name}: über 4 kHz {share(x, rate, 4000, 1e9):.2f} %")
    for name in FOR_OTHERS:
        x, rate = load(name)
        check(share(x, rate, 4000, 1e9) <= MAX_OTHERS_ABOVE_4K, f"{name}: über 4 kHz {share(x, rate, 4000, 1e9):.2f} %")

    rain_db = code_db("RainDb")
    x, rate = load("rain")
    check(share(x, rate, 4000, 1e9) <= MAX_RAIN_ABOVE_4K, f"Regen über 4 kHz: {share(x, rate, 4000, 1e9):.1f} %")
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

    for name, (limit, bright) in HISSES.items():
        x, rate = load(name)
        check(dba(x, rate) <= limit, f"{name}: {dba(x, rate):.1f} dB(A) in der Datei, Grenze {limit}")
        check(share(x, rate, 4000, 1e9) <= bright, f"{name}: über 4 kHz {share(x, rate, 4000, 1e9):.1f} %")

    for name in STINGERS:
        x, rate = load(name)
        level = dba(x, rate) + code_db("MusicDb") + 3
        check(level <= MAX_STINGER_GAME_DBA, f"{name}: {level:.1f} dB(A) im Spiel, Grenze {MAX_STINGER_GAME_DBA}")
        check(share(x, rate, 2000, 1e9) <= MAX_MUSIC_ABOVE_2K, f"{name}: über 2 kHz {share(x, rate, 2000, 1e9):.3f} %")

    x, rate = load("music_tension")
    check(share(x, rate, 0, 120) <= MAX_FIGHT_BELOW_120, f"Gefecht unter 120 Hz: {share(x, rate, 0, 120):.0f} %")
    x, rate = load("heartbeat")
    check(share(x, rate, 0, 120) <= MAX_HEART_BELOW_120, f"Herzschlag unter 120 Hz: {share(x, rate, 0, 120):.0f} %")

    for name in CITY:
        x, rate = load(name)
        check(share(x, rate, 2000, 1e9) <= MAX_CITY_ABOVE_2K, f"{name}: über 2 kHz {share(x, rate, 2000, 1e9):.2f} %")

    x, rate = load("music_title")
    level = dba(x, rate) + music_db
    check(abs(len(x) / rate - TITLE_SECONDS) < 1e-9, f"music_title: Länge {len(x) / rate:.3f} s")
    check(share(x, rate, 2000, 1e9) <= MAX_MUSIC_ABOVE_2K, f"music_title: über 2 kHz {share(x, rate, 2000, 1e9):.3f} %")
    check(level <= MAX_MUSIC_GAME_DBA, f"music_title: {level:.1f} dB(A), Grenze {MAX_MUSIC_GAME_DBA}")
    for name in ("menu_move", "menu_select"):
        x, rate = load(name)
        check(share(x, rate, 4000, 1e9) <= MAX_MENU_ABOVE_4K, f"{name}: über 4 kHz {share(x, rate, 4000, 1e9):.1f} %")
        check(share(x, rate, 0, 120) <= MAX_MENU_BELOW_120, f"{name}: unter 120 Hz {share(x, rate, 0, 120):.0f} %")

    for i in range(3):
        x, rate = load(f"peil_ping_{i}")
        check(share(x, rate, 2000, 1e9) <= MAX_PEIL_ABOVE_2K, f"peil_ping_{i}: über 2 kHz {share(x, rate, 2000, 1e9):.2f} %")
        check(dba(x, rate) <= MAX_PEIL_DBA, f"peil_ping_{i}: {dba(x, rate):.1f} dB(A) in der Datei, Grenze {MAX_PEIL_DBA}")
        # Der Nutzer hat das Gerät nicht gehört. Es braucht Mitten und einen Mindestpegel im Spiel (Aufruf aus dem HUD bei -22 dB).
        in_game = dba(x, rate) - 22 + code_db("PeilBoostDb")
        check(in_game >= MIN_PEIL_GAME_DBA, f"peil_ping_{i}: im Spiel {in_game:.1f} dB(A), mindestens {MIN_PEIL_GAME_DBA}")
        check(share(x, rate, 400, 1e9) >= MIN_PEIL_ABOVE_400, f"peil_ping_{i}: über 400 Hz {share(x, rate, 400, 1e9):.0f} %")

    x, rate = load("match")
    check(share(x, rate, 4000, 1e9) <= MAX_MATCH_ABOVE_4K, f"Streichholz über 4 kHz: {share(x, rate, 4000, 1e9):.1f} %")

    for name in LOOPS:
        x, _ = load(name)
        seam = np.abs(x[0] - x[-1]).max() / (np.median(np.abs(np.diff(x, axis=0))) + 1e-9)
        check(seam <= MAX_SEAM, f"{name}: Sprung an der Naht {seam:.1f}-fach")

    for path in sorted(AUDIO.glob("*.wav")):
        x, _ = load(path.stem)
        check(np.abs(x).max() < 0.999, f"{path.stem}: Spitze {db(np.abs(x).max()):.1f} dBFS")

    # Klänge aus Aufnahmen lassen sich ohne die Rohdateien nicht neu erzeugen. Sie müssen der festgehaltenen Prüfsumme entsprechen.
    recorded = json.loads((Path(__file__).parent / "recordings.json").read_text(encoding="utf-8"))
    for name, entry in recorded.items():
        check(hashlib.sha256((AUDIO / f"{name}.wav").read_bytes()).hexdigest() == entry["sha256"], f"{name}: aus Aufnahme, Prüfsumme stimmt")
    for name in ("dog", "door_open"):
        check(not (AUDIO / f"{name}.wav").exists(), f"{name}.wav: ersetzt durch nummerierte Fassungen aus Aufnahmen")

    if "--neu" in sys.argv:
        with tempfile.TemporaryDirectory() as folder:
            generate_audio.OUT = Path(folder)
            generate_audio.main()
            for path in sorted(AUDIO.glob("*.wav")):
                if path.stem in recorded:
                    continue
                other = Path(folder) / path.name
                same = other.exists() and hashlib.sha256(other.read_bytes()).digest() == hashlib.sha256(path.read_bytes()).digest()
                check(same, f"{path.stem}: bytegleich neu erzeugt")

    print(f"\n{len(failures)} Fehler")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
