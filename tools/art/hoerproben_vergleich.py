"""Vorher und nachher zum Abhören: erst der alte Klang, nach einer Pause der neue, mit den Pegeln des Spiels.

Aufruf: python tools/art/hoerproben_vergleich.py
Schreibt work/hoerproben/vergleich/*.wav (nicht im Repository). Die alten Klänge kommen aus dem Commit ALT.
Ohne Ortsklang, Hall und Limiter des Spiels.
"""
import io
import subprocess
import wave
from pathlib import Path

import numpy as np

from hoerproben import AUDIO, DRIZZLE, MUSIC_DB, RATE, ROOT, SHOWER, gain, limited, looped, weather

ALT = "6479d44"
ALT2 = "2e6684e"
OUT = ROOT / "work" / "hoerproben" / "vergleich"
PAUSE = 1.5


def read(data):
    with wave.open(data) as f:
        rate, channels = f.getframerate(), f.getnchannels()
        x = np.frombuffer(f.readframes(f.getnframes()), "<i2").astype(float) / 32768
    x = x.reshape(-1, channels)
    if channels == 1:
        x = np.repeat(x, 2, axis=1)
    if rate != RATE:
        at = np.arange(int(len(x) * RATE / rate)) * rate / RATE
        x = np.stack([np.interp(at, np.arange(len(x)), x[:, c]) for c in (0, 1)], axis=1)
    return x


def new(name):
    return read(str(AUDIO / f"{name}.wav"))


def old(name, commit=ALT):
    raw = subprocess.run(["git", "show", f"{commit}:game/assets/audio/{name}.wav"], capture_output=True, check=True, cwd=ROOT).stdout
    return read(io.BytesIO(raw))


def pitched(x, pitch):
    """Wie PitchScale im Spiel: schneller oder langsamer abgespielt."""
    at = np.arange(int(len(x) / pitch)) * pitch
    return np.stack([np.interp(at, np.arange(len(x)), x[:, c]) for c in (0, 1)], axis=1)


def old_weather(seconds, strength):
    x = np.concatenate([old("rain", ALT2)] * (int(seconds / 12) + 1))[:int(seconds * RATE)]
    return x * gain(-27 + 16 * np.log10(strength)) + looped("wind", seconds) * gain(-30)


def sequence(events, bed_rain=DRIZZLE, bed=weather):
    """events: (Zeit, Klang, Lautstärke in dB). Darunter Regen und Wind."""
    seconds = max(at + len(x) / RATE for at, x, _ in events) + 0.5
    out = bed(seconds, bed_rain)
    for at, x, db in events:
        i = int(at * RATE)
        out[i:i + len(x)] += x[:len(out) - i] * gain(db)
    return out


def both(before, after):
    return np.concatenate((before, np.zeros((int(PAUSE * RATE), 2)), after))


def save(name, x):
    x = limited(x)
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(str(OUT / f"{name}.wav"), "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(data.tobytes())
    print(f"{name}: {len(x) / RATE:.0f} s, Spitze {20 * np.log10(np.abs(x).max() + 1e-9):.1f} dBFS")


def steps(load):
    events = []
    at = 0.5
    for kind, db, pace in (("soft", -19, 0.5), ("boot", -22, 0.55), ("run", -13, 0.3)):
        for i in range(8):
            events.append((at, load(f"step_{kind}_{i % 4}"), db))
            at += pace
        at += 1.0
    return sequence(events)


def shots(sounds, db, pace, count=4):
    return sequence([(0.5 + i * pace, sounds[i % len(sounds)], db) for i in range(count)])


def fight(load_shot, hit, fall):
    """Zwölf Sekunden Gefecht: Musik, Schauer, Pistolen, Schrot, eine Salve, Treffer, einer fällt."""
    events = [(1.0, load_shot("pistole", 0), 0), (1.9, load_shot("pistole", 1), 0), (2.0, hit(0), -3), (3.2, load_shot("schrot", 0), 0),
              (4.4, load_shot("pistole", 2), 0), (5.5, load_shot("gewehr", 0), 0), (5.62, hit(1), -3), (6.0, fall(0), -1)]
    events += [(7.5 + i * 0.11, load_shot("mp", i), 0) for i in range(7)]
    events += [(9.3, load_shot("schrot", 1), 0), (9.42, hit(2), -3), (9.9, fall(1), -1)]
    out = sequence(events, SHOWER)
    return out + looped("music_tension", len(out) / RATE)[:len(out)] * gain(MUSIC_DB)


def old_shot(kind, i):
    x = old(f"shot_{i % 3}")
    pitch, db = {"pistole": (1.0, 0), "schrot": (0.62, 2), "gewehr": (0.72, 2), "mp": (1.18, 0)}[kind]
    return pitched(x, pitch) * gain(db)


def new_shot(kind, i):
    name, count = {"pistole": ("shot", 3), "schrot": ("shotgun", 3), "gewehr": ("rifle", 3), "mp": ("smg", 4)}[kind]
    return new(f"{name}_{i % count}")


def round_two():
    """Runde 2: Signale, Regen, Tiefes, Brummen, Hörner. Alt aus ALT2, neu aus dem Ordner."""
    def before(name):
        return old(name, ALT2)

    def lined(load, items, gap=0.6, bed=weather):
        events, at = [], 0.5
        for name, db in items:
            x = load(name)
            events.append((at, x, db))
            at += len(x) / RATE + gap
        return sequence(events, DRIZZLE, bed)

    def loops(load, items, seconds=5):
        events, at = [], 0.5
        for name, db in items:
            x = load(name)
            x = np.concatenate([x] * (int(seconds * RATE / len(x)) + 1))[:int(seconds * RATE)]
            events.append((at, x, db))
            at += seconds + 0.6
        return sequence(events)

    signals = (("whistle", -3), ("boy_whistle", -5), ("alarm_bell", 0), ("coins", -4), ("chain", -17), ("chain", -17), ("click", -3))
    save("I_pfeifen_glocke_muenzen_kette_klick", both(lined(before, signals), lined(new, signals)))
    save("J_dietrich_12_sekunden", both(loops(before, (("lockpick", -8),), 12), loops(new, (("lockpick", -8),), 12)))
    seconds = 12
    rain = [np.concatenate([w(seconds, s) for s in (SHOWER, DRIZZLE)]) for w in (old_weather, weather)]
    save("K_regen_schauer_dann_niesel", both(rain[0], rain[1]))
    far = (("train", -27), ("tram", -29), ("thunder_0", -18))
    save("L_gueterzug_strassenbahn_donner", both(lined(before, far), lined(new, far)))
    # Brummen im Spiel ist sehr leise und nur aus der Nähe zu hören. Hier 12 dB lauter als im Spiel, sonst hört man nichts.
    hums = (("lamp", -26), ("neon", -22), ("tram_hum", -18), ("machine", -14), ("airship", 1), ("diesel", -8))
    save("M_brummen_lampe_neon_bahn_maschine_luftschiff_diesel_12dB_lauter", both(loops(before, hums), loops(new, hums)))
    horns = (("factory_horn", -29), ("foghorn", -29), ("church_bell", -30), ("win", -2), ("lose", -2))
    save("N_schichthorn_nebelhorn_kirche_sieg_niederlage", both(lined(before, horns), lined(new, horns)))
    fresh = (("gasp_0", -8), ("gasp_1", -8), ("shop_buy", -8), ("shop_equip", -8), ("shop_sell", -8), ("map_select", -10), ("map_accept", -8), ("stamp", -6),
             ("tram_spark", -10), ("cell_door", -6), ("keys", -8))
    save("O_neu_erschrecken_waffenkammer_stadtplan_stempel_funken_zellentuer_schluessel", lined(new, fresh, 0.9))
    save("P_neu_arbeiterschritte_schleife", loops(new, (("crowd_steps", -10),), 16))


def verdict():
    """Nach dem ersten Urteil: Regen in Stufen und Peilgerät in Stufen, jeweils über der ruhigen Musik. Ansage durch Pausen getrennt."""
    seconds = 12
    music = looped("music_calm", seconds) * gain(MUSIC_DB)
    wind = looped("wind", seconds) * gain(-30)
    gap = np.zeros((int(PAUSE * RATE), 2))

    def rain(x, db):
        return np.concatenate([x] * 2)[:seconds * RATE] * gain(db)

    before = old("rain", "0a084f6")
    parts = []
    for db in (-27, -31, -35):
        parts += [music + wind + rain(before, db), gap]
    save("R1_regen_bisher_dann_4dB_leiser_dann_8dB_leiser", np.concatenate(parts[:-1]))
    parts = []
    for db in (-27, -31, -35):
        parts += [music + wind + rain(new("rain"), db), gap]
    save("R2_regen_tropfen_zurueckgenommen_gleiche_drei_stufen", np.concatenate(parts[:-1]))

    def pings(load, db, bed_rain):
        out = music + weather(seconds, bed_rain)
        for k, at in enumerate((1.0, 3.6, 6.4, 9.2)):
            x = load(f"peil_ping_{k % 3}")
            i = int(at * RATE)
            out[i:i + len(x)] += x * gain(db)
        return out

    for label, bed_rain in (("S1_peilton_im_niesel", DRIZZLE), ("S2_peilton_im_schauer", SHOWER)):
        parts = [pings(lambda n: old(n, "0a084f6"), -22, bed_rain), gap]
        for db in (-16, -12, -8):
            parts += [pings(new, db, bed_rain), gap]
        save(label + "_bisher_dann_neu_in_drei_stufen", np.concatenate(parts[:-1]))


ALT_MUSIK = "ec7a0ec"
DARK_DB = MUSIC_DB - 2


def music_round():
    """Jede Lage alt gegen neu, dann der Wechsel Ruhe, Gefecht, Ruhe mit den neuen Einsätzen."""
    seconds = 30

    def cut(x, start=0):
        x = np.concatenate([x, x])
        return x[int(start * RATE):int((start + seconds) * RATE)]

    def mix(load, parts, start=0):
        out = weather(seconds, DRIZZLE)
        for name, db in parts:
            out = out + cut(load(name), start) * gain(db)
        return out

    def before(name):
        return old(name, ALT_MUSIK)

    fight_dark = DARK_DB + 20 * np.log10(np.sin(0.7 * np.pi / 2))
    for label, parts, start in (("T1_musik_ruhe", (("music_calm", MUSIC_DB),), 0), ("T2_musik_dunkel", (("music_dark", DARK_DB),), 0),
                                ("T3_musik_verdacht", (("music_calm", MUSIC_DB), ("music_suspect", MUSIC_DB)), 12),
                                ("T4_musik_gefecht", (("music_dark", fight_dark), ("music_tension", MUSIC_DB)), 24), ("T5_titelthema", (("music_title", MUSIC_DB),), 0)):
        save(label + "_alt_dann_neu", both(mix(before, parts, start), mix(new, parts, start)))

    # Wechsel: 12 s Ruhe, Alarm, 20 s Gefecht, Entwarnung, 16 s Ruhe. Überblendung wie im Spiel, Einsatz auf dem nächsten Schlag.
    total = 48
    t = np.arange(total * RATE) / RATE
    want = ((t >= 12.2) & (t < 32.3)).astype(float)
    level = np.empty_like(want)
    value = 0.0
    for i, w in enumerate(want):
        value += np.clip(w - value, -0.35 / RATE, 0.35 / RATE)
        level[i] = value

    def run(name):
        x = new(name)
        return np.concatenate([x] * (total * RATE // len(x) + 1))[:total * RATE]

    out = weather(total, DRIZZLE)
    out += run("music_calm") * gain(MUSIC_DB) * np.sin((1 - level) * np.pi / 2)[:, None]
    out += run("music_dark") * gain(DARK_DB) * np.sin(0.7 * level * np.pi / 2)[:, None]
    out += run("music_tension") * gain(MUSIC_DB) * np.sin(level * np.pi / 2)[:, None]
    for at, name in ((12.5, "music_in_fight"), (32.5, "music_to_calm")):
        x = new(name) * gain(MUSIC_DB + 3)
        i = int(at * RATE)
        out[i:i + len(x)] += x[:len(out) - i]
    save("T6_wechsel_ruhe_gefecht_ruhe_mit_einsaetzen", out)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    music_round()
    verdict()
    round_two()
    save("A_schritte_leise_stiefel_rennen", both(steps(old), steps(new)))
    for label, kind, pace, count in (("B_pistole", "pistole", 1.4, 4), ("C_schrotflinte", "schrot", 2.2, 3), ("D_maschinenpistole_salve", "mp", 0.11, 8), ("E_gewehr", "gewehr", 2.4, 3)):
        save(label, both(shots([old_shot(kind, i) for i in range(4)], 0, pace, count), shots([new_shot(kind, i) for i in range(4)], 0, pace, count)))
    before = sequence([(0.5, old("hit"), -3), (1.3, old("hit"), -3), (2.1, old("hit"), -3), (3.2, old("fall"), -1), (4.4, old("fall"), -1)])
    after = sequence([(0.5, new("hit_0"), -3), (1.3, new("hit_1"), -3), (2.1, new("hit_2"), -3), (3.2, new("fall_0"), -1), (4.4, new("fall_1"), -1)])
    save("F_treffer_und_fallen", both(before, after))
    save("G_gefecht_mit_musik_und_schauer", both(fight(old_shot, lambda i: old("hit"), lambda i: old("fall")), fight(new_shot, lambda i: new(f"hit_{i}"), lambda i: new(f"fall_{i}"))))
    # Neu, ohne Vorher: Nachladen dreimal, Totschläger, Rauchtopf, Verband.
    kit = [(0.5, new("reload_pistole"), -12), (2.2, new("reload_schrotflinte"), -12), (4.3, new("reload_maschinenpistole"), -12),
           (6.4, new("club_swing"), -12), (6.6, new("club_hit_0"), -5), (7.8, new("club_swing"), -12), (8.0, new("club_hit_1"), -5),
           (9.5, new("smoke_throw"), -6), (9.5, new("smoke_hiss"), -10), (14.0, new("bandage"), -12)]
    save("H_neu_nachladen_totschlaeger_rauchtopf_verband", sequence(kit))


if __name__ == "__main__":
    main()
