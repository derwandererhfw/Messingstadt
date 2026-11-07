"""Mischt Hörproben mit den Pegeln aus dem Spiel, damit sich der Ton ohne Spielstart beurteilen lässt.

Aufruf: python tools/art/hoerproben.py
Schreibt work/hoerproben/*.wav (nicht im Repository).
Ohne Positionsklang, Hall und Limiter des Spiels. Regen und Wind stehen auf dem jeweils genannten Wetter.
"""
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT / "game" / "assets" / "audio"
OUT = ROOT / "work" / "hoerproben"
RATE = 44100

MUSIC_DB = -19.0
DARK_DB = -21.0
RAIN_DB = -27.0
RAIN_CURVE_DB = 16.0
WIND_DB = -30.0
GUST_DB = -24.0
HEART_DB = -10.0
SHOWER, DRIZZLE, PAUSE = 1.0, 0.3, 0.03
CITY = (("dog", -26), ("siren", -31), ("train", -27), ("factory_horn", -29), ("tram", -29), ("foghorn", -29), ("church_bell", -30), ("shutter", -18), ("distant_shots", -16))


def load(name):
    with wave.open(str(AUDIO / f"{name}.wav")) as f:
        rate, channels = f.getframerate(), f.getnchannels()
        data = np.frombuffer(f.readframes(f.getnframes()), "<i2").astype(float) / 32768
    data = data.reshape(-1, channels)
    if channels == 1:
        data = np.repeat(data, 2, axis=1)
    if rate != RATE:
        at = np.arange(int(len(data) * RATE / rate)) * rate / RATE
        data = np.stack([np.interp(at, np.arange(len(data)), data[:, c]) for c in (0, 1)], axis=1)
    return data


def looped(name, seconds, start=0.0):
    x = load(name)
    index = (int(start * RATE) + np.arange(int(seconds * RATE))) % len(x)
    return x[index]


def gain(db):
    return 10 ** (db / 20)


def rain_gain(strength):
    return gain(RAIN_DB + RAIN_CURVE_DB * np.log10(strength))


def weather(seconds, strength):
    return looped("rain", seconds) * rain_gain(strength) + looped("wind", seconds) * gain(WIND_DB)


def save(name, x):
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(str(OUT / f"{name}.wav"), "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(data.tobytes())
    rms = 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)
    print(f"{name}: {len(x) / RATE:.0f} s, {rms:.1f} dBFS, Spitze {20 * np.log10(np.abs(x).max() + 1e-9):.1f} dBFS")


def layers(levels, seconds, start):
    """levels: Lage -> (Lautstärke, Hüllkurve oder Zahl)."""
    out = np.zeros((int(seconds * RATE), 2))
    for name, (db, env) in levels.items():
        out += looped(name, seconds, start) * gain(db) * (env[:, None] if isinstance(env, np.ndarray) else env)
    return out


def fades():
    """Eine Minute durch alle Lagen, mit den Überblendzeiten aus dem Spiel."""
    seconds = 80
    t = np.arange(seconds * RATE) / RATE
    sections = ((0, 16, "calm"), (16, 32, "dark"), (32, 48, "suspect"), (48, 68, "fight"), (68, 80, "calm"))
    want = {k: np.zeros_like(t) for k in ("calm", "dark", "suspect", "fight")}
    for a, b, kind in sections:
        hit = (t >= a) & (t < b)
        want["calm"][hit] = 1.0 if kind in ("calm", "suspect") else 0.0
        want["dark"][hit] = 1.0 if kind == "dark" else 0.7 if kind == "fight" else 0.0
        want["suspect"][hit] = 1.0 if kind == "suspect" else 0.0
        want["fight"][hit] = 1.0 if kind == "fight" else 0.0
    step = 0.35 / RATE
    levels = {}
    for kind, target in want.items():
        now = np.empty_like(target)
        value = target[0]
        for i, wanted in enumerate(target):
            value += np.clip(wanted - value, -step, step)
            now[i] = value
        levels[kind] = np.sin(now * np.pi / 2)
    return (looped("music_calm", seconds) * gain(MUSIC_DB) * levels["calm"][:, None]
            + looped("music_dark", seconds) * gain(DARK_DB) * levels["dark"][:, None]
            + looped("music_suspect", seconds) * gain(MUSIC_DB) * levels["suspect"][:, None]
            + looped("music_tension", seconds) * gain(MUSIC_DB) * levels["fight"][:, None]
            + weather(seconds, DRIZZLE))


def city():
    parts = []
    for name, db in CITY:
        x = load(name) * gain(db)
        parts.append(x)
        parts.append(np.zeros((RATE * 2, 2)))
    x = np.concatenate(parts)
    return x + weather(len(x) / RATE, DRIZZLE)[:len(x)]


def rain_compare():
    seconds = 10
    return np.concatenate([weather(seconds, s) for s in (SHOWER, DRIZZLE, PAUSE, SHOWER)])


def gusts():
    seconds = 30
    t = np.arange(seconds * RATE) / RATE
    env = np.clip(np.sin(2 * np.pi * t / 7) * 1.4 - 0.2, 0, 1)
    return weather(seconds, DRIZZLE) + looped("wind_gust", seconds) * gain(GUST_DB) * env[:, None]


def near(name, db, seconds=20):
    return looped(name, seconds) * gain(db) + weather(seconds, DRIZZLE)


def match():
    parts = []
    for _ in range(4):
        parts += [load("match") * gain(-14), np.zeros((RATE * 2, 2))]
    x = np.concatenate(parts)
    return x + weather(len(x) / RATE, DRIZZLE)[:len(x)]


def hushed():
    """Radio, Bildfunk und Klavier, dann Schüsse: Die Fenster gehen aus, nach einer Weile kommt alles wieder."""
    seconds = 40
    t = np.arange(seconds * RATE) / RATE
    hush = np.clip((t - 10) / 2, 0, 1) - np.clip((t - 28) / 8, 0, 1)
    quiet = np.zeros_like(t)
    value = 0.0
    for i, wanted in enumerate(hush):
        value += np.clip(wanted - value, -0.6 / RATE, 0.6 / RATE)
        quiet[i] = value
    city = looped("radio", seconds) * gain(-24) + looped("television", seconds) * gain(-32) + looped("piano", seconds) * gain(-26)
    shots = np.zeros((len(t), 2))
    for at in (9.6, 10.0, 10.9):
        x = load("shot_0") * gain(-8)
        i = int(at * RATE)
        shots[i:i + len(x)] += x[:len(shots) - i]
    return city * (1 - quiet)[:, None] + shots + weather(seconds, DRIZZLE)


def tram_light():
    """Das Licht in der Bahn stottert und geht aus: Das Brummen folgt der Helligkeit."""
    seconds = 20
    t = np.arange(seconds * RATE) / RATE
    light = np.ones_like(t)
    for a, b in ((4, 4.3), (4.5, 4.9), (9, 9.6)):
        light[(t >= a) & (t < b)] = 0.1
    light[t >= 14] = np.clip(1 - (t[t >= 14] - 14) * 1.5, 0, 1)
    env = np.maximum(10 ** ((-30 + 20 * np.log10(np.maximum(light, 0.001))) / 20), 10 ** (-60 / 20))
    return looped("tram_hum", seconds) * env[:, None] + weather(seconds, DRIZZLE)


def title():
    seconds = 40
    return looped("music_title", seconds) * gain(MUSIC_DB) + weather(seconds, DRIZZLE)


def menu():
    """Wandern durch fünf Einträge, dann Wählen, dreimal."""
    parts = []
    for _ in range(3):
        for _ in range(5):
            parts += [load("menu_move") * gain(-18), np.zeros((int(RATE * 0.35), 2))]
        parts += [load("menu_select") * gain(-10), np.zeros((RATE * 2, 2))]
    x = np.concatenate(parts)
    return x + looped("music_title", len(x) / RATE)[:len(x)] * gain(MUSIC_DB)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    seconds = 30
    save("1_ruhe", layers({"music_calm": (MUSIC_DB, 1.0)}, seconds, 0) + weather(seconds, DRIZZLE))
    save("2_dunkel", layers({"music_dark": (DARK_DB, 1.0)}, seconds, 0) + weather(seconds, DRIZZLE))
    save("3_verdacht", layers({"music_calm": (MUSIC_DB, 1.0), "music_suspect": (MUSIC_DB, 1.0)}, seconds, 12) + looped("heartbeat", seconds) * gain(HEART_DB) + weather(seconds, DRIZZLE))
    save("4_gefecht", layers({"music_dark": (DARK_DB, np.sin(0.7 * np.pi / 2)), "music_tension": (MUSIC_DB, 1.0)}, seconds, 24) + weather(seconds, SHOWER))
    save("5_alle_lagen_nacheinander", fades())
    save("6_regen_schauer_niesel_pause_schauer", rain_compare())
    save("7_boeen", gusts())
    save("8_stadt_alle_fernen_geraeusche", city())
    save("9_regenrinne_aus_der_naehe", near("gutter", -20 + 14 * np.log10(DRIZZLE)))
    save("10_klavier_hinter_der_scheibe", near("piano", -26))
    save("11_streichholz", match())
    save("12_fenster_gehen_aus_stadt_verstummt", hushed())
    save("13_strassenbahnlicht_stottert_und_geht_aus", tram_light())
    save("14_titelthema", title())
    save("15_menue_wandern_und_waehlen", menu())


if __name__ == "__main__":
    main()
