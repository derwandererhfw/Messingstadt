"""Erzeugt alle Geräusche des Spiels per Synthese, ohne fremde Aufnahmen.

Aufruf: python tools/art/generate_audio.py
Schreibt game/assets/audio/*.wav. Gleicher Zufall, gleiche Dateien.
Schleifen enden und beginnen gleich, damit beim Wiederholen kein Knacken entsteht.
Neue Geräusche holen ihren Zufall aus own_rng. So bleiben alle anderen Dateien bytegleich.
"""
import wave
import zlib
from pathlib import Path

import numpy as np

RATE = 32000
OUT = Path(__file__).resolve().parents[2] / "game" / "assets" / "audio"
RNG = np.random.default_rng(1927)


def time(seconds):
    return np.arange(int(seconds * RATE)) / RATE


def own_rng(name):
    """Eigener Zufall je Geräusch: Der gemeinsame hängt an der Reihenfolge, jede Einfügung änderte alles dahinter."""
    return np.random.default_rng(zlib.crc32(name.encode()))


def noise(seconds, rng=None):
    return (rng or RNG).standard_normal(int(seconds * RATE))


def lowpass(x, cutoff):
    """Einfacher Tiefpass, zweimal laufen lassen macht ihn steiler."""
    a = 1 - np.exp(-2 * np.pi * cutoff / RATE)
    y = np.empty_like(x)
    state = 0.0
    for _ in range(2):
        for i, v in enumerate(x):
            state += a * (v - state)
            y[i] = state
        x = y.copy()
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def decay(t, seconds):
    return np.exp(-t / seconds)


def fade(x, start=0.002, end=0.01):
    a, b = int(start * RATE), int(end * RATE)
    x = x.copy()
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def echo(x, delays, gain):
    """Straßenecho: leise Wiederholungen, die Wände zurückwerfen."""
    longest = int(max(delays) * RATE) + len(x)
    out = np.zeros(longest)
    out[:len(x)] += x
    for i, d in enumerate(delays):
        at = int(d * RATE)
        out[at:at + len(x)] += x * gain ** (i + 1)
    return out


def loopable(x, overlap=0.5):
    """Blendet das Ende in den Anfang, damit die Schleife nahtlos läuft."""
    n = int(overlap * RATE)
    ramp = np.linspace(0, 1, n)
    out = x[:-n].copy()
    out[:n] = x[:n] * ramp + x[-n:] * (1 - ramp)
    return out


# Diese Klänge sind neu gebaut (siehe "Überarbeitung" weiter unten). Die alten Funktionen laufen weiter,
# damit der gemeinsame Zufall für alle anderen Dateien gleich bleibt, schreiben diese Namen aber nicht mehr.
SUPERSEDED = {"hit", "fall", "shot_0", "shot_1", "shot_2"} | {f"step_{kind}_{i}" for kind in ("soft", "boot", "run") for i in range(4)}


# Diese Klänge stammen aus freien Aufnahmen (tools/art/import_recordings.py, Herkunft in docs/ton-herkunft.md).
# Der Generator schreibt sie nie, auch wenn die alten Funktionen sie noch berechnen.
RECORDED = {"dog", "door_open", "pages", "gasp_0", "gasp_1", "bandage", "shop_equip"}


def save(name, x, peak=0.9, replace=False):
    if name in RECORDED or (name in SUPERSEDED and not replace):
        return
    x = np.asarray(x, np.float64)
    top = np.abs(x).max()
    if top > 0:
        x = x / top * peak
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(str(OUT / f"{name}.wav"), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(data.tobytes())


def save_stereo(name, left, right, rate=RATE):
    data = (np.clip(np.stack((left, right), axis=1), -1, 1) * 32767).astype("<i2")
    with wave.open(str(OUT / f"{name}.wav"), "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(rate)
        f.writeframes(data.tobytes())


def widen(name, mid, side, rate=RATE, width=0.6):
    """Legt eine unabhängige Seitenspur um die Mitte. Jeder Kanal behält den Pegel der Mitte, der Regen wird also nicht lauter."""
    side = side * np.sqrt(np.mean(mid ** 2) / np.mean(side ** 2))
    keep = np.sqrt(1 - width ** 2)
    save_stereo(name, keep * mid + width * side, keep * mid - width * side, rate)


def peak_to(x, peak):
    return x / np.abs(x).max() * peak


def rain_bed(rng):
    t = 12.5
    body = highpass(lowpass(noise(t, rng), 6500), 900)
    swell = 1 + 0.15 * np.sin(2 * np.pi * time(t) * 0.17)
    x = body * swell
    # Einzelne Tropfen: weiche Tupfer aus Rauschen. Reine Töne klängen hier wie Metall.
    for _ in range(60):
        at = rng.integers(0, int((t - 0.1) * RATE))
        d = time(0.04)
        x[at:at + len(d)] += lowpass(noise(0.04, rng), 1800)[:len(d)] * decay(d, 0.009) * rng.uniform(0.5, 1.6)
    return peak_to(loopable(x), 0.6)


def rain():
    mid = rain_bed(RNG)
    widen("rain", mid, rain_bed(own_rng("rain")))


def wind_bed(rng):
    t = 16.5
    x = lowpass(noise(t, rng), 260)
    tt = time(t)
    x *= 0.65 + 0.35 * np.sin(2 * np.pi * tt * 0.11) * np.sin(2 * np.pi * tt * 0.07 + 1)
    # Fernes Grollen der Fabriken
    x += 0.5 * lowpass(noise(t, rng), 60)
    return peak_to(loopable(x), 0.7)


def wind():
    mid = wind_bed(RNG)
    widen("wind", mid, wind_bed(own_rng("wind")))


def wind_gust():
    """Böe: Rauschen in den Mitten, das langsam auf und ab zieht. Der tiefe Wind allein trüge auf kleinen Lautsprechern nicht."""
    rng = own_rng("wind_gust")
    t = 16.5
    tt = time(t)

    def bed():
        slow = highpass(lowpass(noise(t, rng), 900), 240)
        return peak_to(loopable(slow * (0.6 + 0.4 * np.sin(2 * np.pi * tt * 0.37 + rng.uniform(0, 6.28)))), 0.5)

    mid = bed()
    widen("wind_gust", mid, bed())


def hum(base, seconds, harmonics, wobble=0.0):
    tt = time(seconds)
    x = np.zeros_like(tt)
    for i, gain in enumerate(harmonics):
        x += gain * np.sin(2 * np.pi * base * (i + 1) * tt + i)
    return x * (1 + wobble * np.sin(2 * np.pi * 0.7 * tt))


def lamp():
    t = 4.5
    # Kaum hörbares, weiches Brummen ohne hohe Obertöne
    x = hum(100, t, (1, 0.2), 0.08) + 0.06 * lowpass(noise(t), 400)
    save("lamp", loopable(x), 0.5)


def neon():
    t = 4.5
    x = hum(120, t, (1, 0.35), 0.05)
    save("neon", loopable(x), 0.5)


def steam():
    t = 5.5
    x = highpass(noise(t), 2400)
    tt = time(t)
    x *= 0.55 + 0.45 * np.abs(np.sin(2 * np.pi * tt * 0.2)) ** 2
    save("steam", loopable(x), 0.5)


def fire():
    t = 6.5
    x = 0.6 * lowpass(noise(t), 220)
    for _ in range(120):
        at = RNG.integers(0, int((t - 0.05) * RATE))
        d = time(0.02)
        x[at:at + len(d)] += lowpass(noise(0.02), 1300)[:len(d)] * decay(d, 0.006) * RNG.uniform(0.2, 1.0)
    save("fire", loopable(x), 0.6)


def diesel():
    """Fluchtwagen im Leerlauf: dumpfes, unregelmäßiges Tuckern."""
    t = 4.5
    tt = time(t)
    x = np.zeros_like(tt)
    beat = 11.0
    # Gleichmäßiges Grollen statt einzelner Schläge, die klackern würden.
    x += (np.sin(2 * np.pi * 52 * tt) + 0.35 * np.sin(2 * np.pi * 104 * tt)) * (0.75 + 0.25 * np.sin(2 * np.pi * beat * tt))
    x += 0.3 * lowpass(noise(t), 240)
    save("diesel", loopable(x), 0.6)


def machine():
    """Maschinenhalle hinter den Fabrikmauern."""
    t = 6.5
    tt = time(t)
    x = hum(48, t, (1, 0.35), 0.1) + 0.8 * lowpass(noise(t), 150)
    x *= 0.85 + 0.15 * np.sin(2 * np.pi * tt * 2.0)
    save("machine", loopable(x), 0.6)


def television():
    """Gedämpftes Murmeln und Rauschen durch die Scheibe."""
    t = 6.5
    tt = time(t)
    x = 0.25 * highpass(lowpass(noise(t), 3000), 800)
    speech = lowpass(noise(t), 500) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * tt * 2.3) + np.sin(2 * np.pi * tt * 0.9)))
    x += highpass(speech, 200) * 2
    save("television", loopable(x), 0.5)


def steps():
    """Schritte auf nassem Pflaster: nur ein dumpfer Tritt. Hohe Anteile klängen bei vielen Wachen wie Metallklackern."""
    for name, pitch, peak in (("step_soft", 80, 0.6), ("step_boot", 62, 0.7), ("step_run", 90, 0.8)):
        for i in range(4):
            t = time(0.18)
            thump = np.sin(2 * np.pi * (pitch + i * 4) * t * (1 - t)) * decay(t, 0.04)
            body = lowpass(noise(0.18), 420) * decay(t, 0.035 + 0.006 * i)
            save(f"{name}_{i}", fade(thump + 1.1 * body, 0.006, 0.03), peak)


def shots():
    """Pistole in der Häuserschlucht: harter Knall, kurzer Bass, Echo von den Wänden."""
    for i, (color, boom) in enumerate(((5200, 150), (4200, 125), (6200, 170))):
        t = time(0.32)
        crack = lowpass(noise(0.32), color) * decay(t, 0.012)
        body = np.sin(2 * np.pi * boom * t * (1 - t * 0.8)) * decay(t, 0.05)
        tail = lowpass(noise(0.32), 900) * decay(t, 0.09) * 0.35
        dry = crack * 1.8 + body * 0.9 + tail
        wet = echo(dry, (0.09 + 0.01 * i, 0.19, 0.31, 0.5), 0.34)
        save(f"shot_{i}", fade(lowpass(wet, 9000), 0.0005, 0.05), 0.95)


def hits():
    t = time(0.14)
    save("hit", fade(np.sin(2 * np.pi * 120 * t * (1 - t)) * decay(t, 0.03) + 0.6 * lowpass(noise(0.14), 1400) * decay(t, 0.02)), 0.75)
    t = time(0.55)
    sweep = np.sin(2 * np.pi * np.cumsum(1500 - 1200 * t ** 0.5) / RATE) * decay(t, 0.07)
    tick = lowpass(noise(0.55), 3000) * decay(t, 0.012)
    save("ricochet", fade(sweep * 0.25 + tick), 0.35)
    t = time(0.5)
    save("fall", fade(np.sin(2 * np.pi * 78 * t * (1 - t * 0.5)) * decay(t, 0.09) + 0.5 * lowpass(noise(0.5), 700) * decay(t, 0.13)), 0.8)


def whistle():
    """Trillerpfeife der Wache: zwei Stöße, schrill mit rollender Kugel."""
    parts = []
    for length in (0.32, 0.62):
        t = time(length)
        trill = np.sin(2 * np.pi * 34 * t)
        tone = np.sin(2 * np.pi * np.cumsum(2850 + 170 * trill) / RATE) + 0.35 * np.sin(2 * np.pi * np.cumsum(3120 + 170 * trill) / RATE)
        parts.append(fade(tone * (0.9 + 0.1 * trill), 0.01, 0.03))
        parts.append(np.zeros(int(0.07 * RATE)))
    save("whistle", echo(np.concatenate(parts), (0.14, 0.3), 0.3), 0.55)


def bell():
    t = time(2.2)
    x = np.zeros_like(t)
    for at in (0.0, 0.42, 0.84, 1.26):
        tt = np.maximum(t - at, 0)
        strike = sum(g * np.sin(2 * np.pi * f * tt) for f, g in ((740, 1), (1120, 0.7), (1760, 0.5), (2480, 0.25)))
        x += strike * decay(tt, 0.28) * (t >= at)
    save("alarm_bell", fade(x), 0.5)


def small_things():
    t = time(0.05)
    save("click", fade(highpass(noise(0.05), 3000) * decay(t, 0.006)), 0.6)
    # Dietrich im Schloss: Schaben und einzelne Stifte, als Schleife
    x = 0.12 * highpass(noise(2.0), 3500)
    for _ in range(12):
        at = RNG.integers(0, int(1.9 * RATE))
        d = time(0.03)
        x[at:at + len(d)] += np.sin(2 * np.pi * RNG.uniform(1800, 3200) * d) * decay(d, 0.006) * RNG.uniform(0.5, 1.2)
    save("lockpick", loopable(x, 0.1), 0.45)
    t = time(0.55)
    creak = np.sin(2 * np.pi * np.cumsum(170 + 260 * t + 50 * np.sin(2 * np.pi * 24 * t)) / RATE) * np.minimum(t * 6, 1) * decay(t, 0.3)
    latch = highpass(noise(0.55), 1500) * decay(t, 0.012)
    save("door_open", fade(creak * 0.5 + latch), 0.6)
    t = time(0.5)
    paper = highpass(noise(0.5), 2200) * (0.5 + 0.5 * np.sin(2 * np.pi * 9 * t)) * decay(t, 0.2)
    save("pages", fade(paper), 0.4)
    t = time(0.6)
    coins = np.zeros_like(t)
    for at, f in ((0.0, 3900), (0.08, 4700), (0.17, 3400), (0.27, 5100)):
        tt = np.maximum(t - at, 0)
        coins += np.sin(2 * np.pi * f * tt) * decay(tt, 0.05) * (t >= at)
    save("coins", fade(coins), 0.5)
    t = time(0.75)
    flap = highpass(lowpass(noise(0.75), 3200), 500) * (0.5 + 0.5 * np.sin(2 * np.pi * 13 * t)) ** 2 * np.minimum(1, (0.75 - t) * 5)
    save("pigeons", fade(flap), 0.5)
    t = time(0.22)
    squeak = np.sin(2 * np.pi * np.cumsum(3000 + 1500 * np.sin(2 * np.pi * 8 * t)) / RATE) * np.sin(np.pi * t / 0.22) ** 2
    save("rat", fade(squeak), 0.2)


# ---------- Musik ----------

MUSIC_RATE = 22050
MUSIC_SECONDS = 96


def midi(note):
    return 440.0 * 2 ** ((note - 69) / 12)


def place(buffer, start, sound):
    """Legt einen Klang in die Schleife. Was über das Ende hinausragt, kommt am Anfang wieder heraus."""
    index = (int(start * MUSIC_RATE) + np.arange(len(sound))) % len(buffer)
    np.add.at(buffer, index, sound)


def tone(note, seconds, attack, release, partials, detune=0.0, rng=None):
    t = np.arange(int(seconds * MUSIC_RATE)) / MUSIC_RATE
    out = np.zeros_like(t)
    for h, gain in enumerate(partials, start=1):
        for cents in (-detune, detune) if detune else (0.0,):
            f = midi(note) * h * 2 ** (cents / 1200)
            out += gain * np.sin(2 * np.pi * f * t + (rng or RNG).uniform(0, 6.28))
    env = np.minimum(1, t / attack) * np.minimum(1, (seconds - t) / release)
    return out * np.sin(env * np.pi / 2) ** 2


def spectrum_filter(x, cutoff, low=0.0):
    """Filtert im Frequenzraum, das ist von Natur aus nahtlos für eine Schleife."""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / MUSIC_RATE)
    gain = 1 / (1 + (f / cutoff) ** 4)
    if low:
        gain *= 1 - 1 / (1 + (f / low) ** 4)
    return np.fft.irfft(spec * gain, len(x))


def hall(x, seconds, mix, rng=None):
    """Großer dunkler Raum: Faltung mit abklingendem Rauschen, ebenfalls nahtlos."""
    t = np.arange(len(x)) / MUSIC_RATE
    impulse = (rng or RNG).standard_normal(len(x)) * np.exp(-t / (seconds / 3))
    impulse = spectrum_filter(impulse, 1400)
    wet = np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(impulse), len(x))
    wet *= np.abs(x).max() / max(np.abs(wet).max(), 1e-9)
    return x * (1 - mix) + wet * mix


# Harmonien: eine dunkle Folge in d-Moll, je Akkord 12 Sekunden
CHORDS = (
    (38, 50, 57, 62, 65), (34, 50, 58, 62, 65), (31, 50, 58, 62, 67), (33, 52, 57, 64, 69),
    (38, 50, 57, 62, 65), (39, 51, 58, 63, 67), (36, 51, 55, 60, 67), (33, 49, 57, 64, 69),
)
CHORD_SECONDS = 12
THEME = (
    (6, 69), (10, 65), (13.5, 64), (18, 62), (30, 70), (34, 69), (37.5, 65), (42, 64),
    (54, 69), (58, 72), (61.5, 70), (66, 69), (78, 63), (82, 62), (85.5, 61), (90, 62),
)
PAD_PARTIALS = (1, 0.42, 0.2, 0.1, 0.05)
BASS_PATTERN = (0, 0, 12, 0, 7, 0, 12, 7)
PULSE_SECONDS = 0.5
DARK_PAD_CUTOFF = 380
DARK_PAD_GAIN = 0.95
TREMOLO_PER_LOOP = 384
SUSPECT_GAIN = 0.6
FIGHT_UPPER_PEAK = 0.3


def music():
    """Vier Lagen gleicher Länge und Harmonie: Ruhe, Dunkel, Verdacht, Gefecht. Das Spiel blendet sie gegeneinander."""
    rng = own_rng("music")
    size = MUSIC_SECONDS * MUSIC_RATE
    pad = np.zeros(size)
    for i, chord in enumerate(CHORDS):
        for note in chord:
            place(pad, i * CHORD_SECONDS, tone(note, CHORD_SECONDS + 5, 3.5, 4.5, PAD_PARTIALS, detune=7))
    pad = spectrum_filter(pad, 750)
    t = np.arange(size) / MUSIC_RATE
    # Langsames Atmen der Fläche: ganze Schwingungen je Durchlauf, damit die Schleife aufgeht.
    pad *= 0.8 + 0.2 * np.sin(2 * np.pi * 8 * t / MUSIC_SECONDS)

    drone = np.sin(2 * np.pi * midi(26) * t) * (0.75 + 0.25 * np.sin(2 * np.pi * 4 * t / MUSIC_SECONDS))
    drone = spectrum_filter(drone, 200)
    breath = spectrum_filter(RNG.standard_normal(size), 500, 120) * (0.5 + 0.5 * np.sin(2 * np.pi * 8 * t / MUSIC_SECONDS + 1)) ** 2

    theme = np.zeros(size)
    for start, note in THEME:
        # Weicher Ton mit wenigen Obertönen und langem Abklang, damit nichts nach Glocke oder Metall klingt.
        place(theme, start, tone(note, 4.5, 0.5, 3.5, (1, 0.28, 0.08), detune=3))
    theme = spectrum_filter(theme, 1300)

    pad_room = hall(pad, 7.0, 0.45)
    theme_room = hall(theme, 8.0, 0.7)
    calm = pad_room * 1.0 + drone * 0.5 + breath * 0.25 + theme_room * 0.42
    gain = 0.7 / np.abs(calm).max()
    # Die Seitenspur ist nur Raum: Die Summe beider Kanäle ergibt wieder die bisherige Musik, je Kanal gleich laut wie bisher.
    room = hall(pad, 7.0, 1.0, rng) * 0.4 + hall(theme, 8.0, 1.0, rng) * 0.3
    widen("music_calm", calm * gain, room, MUSIC_RATE, 0.45)

    # Dunkel: ohne Thema, die Fläche nur noch als Schatten.
    shadow = spectrum_filter(pad_room, DARK_PAD_CUTOFF)
    dark = shadow * DARK_PAD_GAIN + drone * 0.5 + breath * 0.25
    save_layer("music_dark", dark * gain, spectrum_filter(room, DARK_PAD_CUTOFF) * DARK_PAD_GAIN * gain)

    # Verdacht: zitternde Streicher in den Mitten über derselben Harmonie, je Takt ein Halbton Reibung.
    strings = np.zeros(size)
    for i, chord in enumerate(CHORDS):
        for note in chord[2:4]:
            place(strings, i * CHORD_SECONDS, tone(note, CHORD_SECONDS + 3, 1.5, 3.0, (1, 0.35, 0.1), detune=9, rng=rng))
        place(strings, i * CHORD_SECONDS, tone(chord[3] + 1, CHORD_SECONDS + 3, 4.0, 3.0, (1, 0.2), detune=6, rng=rng) * 0.3)
    strings *= 0.55 + 0.45 * np.sin(2 * np.pi * TREMOLO_PER_LOOP * t / MUSIC_SECONDS)
    strings = spectrum_filter(strings, 950, 130)
    suspect = hall(strings, 3.0, 0.3, rng)
    save_layer("music_suspect", suspect * SUSPECT_GAIN * gain, hall(strings, 3.0, 1.0, rng) * 0.4 * SUSPECT_GAIN * gain)

    tension = np.zeros(size)
    steps = int(MUSIC_SECONDS / PULSE_SECONDS)
    for step in range(steps):
        when = step * PULSE_SECONDS
        root = CHORDS[int(when // CHORD_SECONDS)][0]
        note = root + BASS_PATTERN[step % len(BASS_PATTERN)]
        place(tension, when, tone(note, 0.42, 0.012, 0.3, (1, 0.5, 0.25), detune=4) * (1.0 if step % 2 == 0 else 0.7))
    kick_t = np.arange(int(0.3 * MUSIC_RATE)) / MUSIC_RATE
    kick = np.sin(2 * np.pi * np.cumsum(46 + 70 * np.exp(-kick_t / 0.03)) / MUSIC_RATE) * np.exp(-kick_t / 0.1)
    for step in range(0, steps, 4):
        place(tension, step * PULSE_SECONDS, kick * 1.6)
    bass = hall(spectrum_filter(tension, 650), 2.5, 0.25)

    # Die Bassfigur zwei Oktaven höher, damit das Gefecht auch aus kleinen Lautsprechern kommt.
    upper = np.zeros(size)
    for step in range(steps):
        when = step * PULSE_SECONDS
        root = CHORDS[int(when // CHORD_SECONDS)][0]
        note = root + 24 + BASS_PATTERN[step % len(BASS_PATTERN)]
        place(upper, when, tone(note, 0.46, 0.02, 0.3, (1, 0.4, 0.12), detune=7, rng=rng) * (1.0 if step % 2 == 0 else 0.65))
    upper = spectrum_filter(upper, 850, 140)
    fight = peak_to(bass, 0.7) + peak_to(hall(upper, 2.5, 0.3, rng), FIGHT_UPPER_PEAK)
    save_layer("music_tension", fight, peak_to(hall(upper, 2.5, 1.0, rng), 0.12))


def save_layer(name, mid, side):
    """Die Lagen über Ruhe tragen nichts über 2 kHz: Die halbe Abtastrate reicht und spart die Hälfte der Datei."""
    save_stereo(name, (mid + side)[::2], (mid - side)[::2], MUSIC_RATE // 2)


def save_music(name, x, peak):
    x = x / np.abs(x).max() * peak
    data = (x * 32767).astype("<i2")
    with wave.open(str(OUT / f"{name}.wav"), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(MUSIC_RATE)
        f.writeframes(data.tobytes())


def thunder():
    """Fernes Grollen: mehrere Wellen tiefen Rauschens, die sich überlagern und lange ausrollen."""
    for i, (seconds, waves) in enumerate(((7.0, 5), (8.5, 7), (6.0, 4))):
        t = time(seconds)
        body = np.zeros_like(t)
        for k in range(waves):
            start = RNG.uniform(0, seconds * 0.45)
            env = np.maximum(t - start, 0)
            body += lowpass(noise(seconds), RNG.uniform(70, 170)) * np.minimum(env * 6, 1) * decay(env, RNG.uniform(0.7, 1.8)) * (t >= start)
        save(f"thunder_{i}", fade(body, 0.05, 0.4), 0.9)


def airship():
    """Motoren in der Höhe: tiefes Dröhnen mit langsamer Schwebung, ohne hohe Töne."""
    t = 8.5
    tt = time(t)
    x = np.sin(2 * np.pi * 41 * tt) + 0.7 * np.sin(2 * np.pi * 43.5 * tt) + 0.35 * np.sin(2 * np.pi * 82 * tt)
    x = x * (0.8 + 0.2 * np.sin(2 * np.pi * 7.5 * tt)) + 0.5 * lowpass(noise(t), 180)
    save("airship", loopable(x), 0.7)


def steam_burst():
    t = time(2.4)
    hiss = highpass(lowpass(noise(2.4), 7000), 1400) * np.minimum(t * 25, 1) * decay(t, 0.8)
    rumble = lowpass(noise(2.4), 200) * decay(t, 0.5)
    save("steam_burst", fade(hiss + 0.5 * rumble, 0.002, 0.2), 0.7)


def weld():
    """Elektrisches Knistern der Schweißnaht, weich gefiltert."""
    t = 3.5
    x = 0.15 * lowpass(noise(t), 2600)
    for _ in range(170):
        at = RNG.integers(0, int((t - 0.05) * RATE))
        d = time(0.015)
        x[at:at + len(d)] += lowpass(noise(0.015), 3800)[:len(d)] * decay(d, 0.004) * RNG.uniform(0.3, 1.6)
    save("weld", loopable(x, 0.2), 0.5)


def dog():
    """Bellen in der Ferne, dumpf durch die Häuser."""
    t = time(1.6)
    x = np.zeros_like(t)
    for at in (0.0, 0.32, 0.72):
        tt = np.maximum(t - at, 0)
        bark = np.sin(2 * np.pi * (380 - 900 * tt) * tt) * np.minimum(tt * 60, 1) * decay(tt, 0.05) * (t >= at)
        x += bark + 0.5 * lowpass(noise(1.6), 1100) * decay(tt, 0.05) * (t >= at)
    save("dog", fade(echo(lowpass(x, 1100), (0.2, 0.45), 0.3), 0.002, 0.2), 0.8)


def fresh_random():
    """Hundekampf und Laufbursche entstanden mit frischem Zufall. So bleiben sie bytegleich, wenn alles neu erzeugt wird."""
    global RNG
    RNG = np.random.default_rng(1927)


def dog_fight():
    """Biss, Fehlbiss, Tritt und Kette: alles kurz und aus der Nähe."""
    t = time(0.28)
    snarl = np.sin(2 * np.pi * (210 + 60 * np.sin(2 * np.pi * 38 * t)) * t) * decay(t, 0.09)
    crunch = lowpass(noise(0.28), 2600) * decay(t, 0.03)
    save("dog_bite", fade(snarl * 0.8 + crunch * 1.2 + 0.6 * lowpass(noise(0.28), 500) * decay(t, 0.08)), 0.8)
    t = time(0.12)
    click = lowpass(noise(0.12), 3800) * decay(t, 0.007)
    save("dog_snap", fade(click + 0.3 * np.sin(2 * np.pi * 330 * t) * decay(t, 0.02)), 0.6)
    t = time(0.2)
    thud = np.sin(2 * np.pi * 95 * t * (1 - t)) * decay(t, 0.04)
    yelp = np.sin(2 * np.pi * np.cumsum(1250 - 2400 * t) / RATE) * decay(np.maximum(t - 0.03, 0), 0.05) * (t > 0.03)
    save("kick", fade(thud + 0.35 * lowpass(noise(0.2), 1000) * decay(t, 0.02) + 0.3 * yelp), 0.7)
    t = time(0.22)
    x = np.zeros_like(t)
    for at, pitch in ((0.0, 3400), (0.07, 4100), (0.13, 3000)):
        tt = np.maximum(t - at, 0)
        x += sum(np.sin(2 * np.pi * pitch * r * tt) for r in (1.0, 1.47, 2.09)) * decay(tt, 0.012) * (t >= at)
    save("chain", fade(highpass(x, 1500)), 0.4)


def boy_whistle():
    """Der Laufbursche pfeift durch die Finger: steigend, dann fallend, zweimal."""
    t = time(0.34)
    glide = 1900 + 1400 * np.sin(np.pi * t / 0.34)
    tone = np.sin(2 * np.pi * np.cumsum(glide) / RATE) + 0.25 * lowpass(noise(0.34), 6000)
    gap = np.zeros(int(0.06 * RATE))
    save("boy_whistle", echo(np.concatenate((fade(tone, 0.015, 0.05), gap, fade(tone, 0.015, 0.05))), (0.12, 0.27), 0.25), 0.5)


def siren():
    """Werkssirene weit weg: langsam auf- und abschwellend, stark gedämpft."""
    t = time(9.0)
    pitch = 240 + 150 * np.sin(np.pi * np.minimum(t / 4.5, 1)) ** 2 + 90 * np.sin(2 * np.pi * t / 9)
    tone = np.sin(2 * np.pi * np.cumsum(pitch) / RATE) + 0.3 * np.sin(4 * np.pi * np.cumsum(pitch) / RATE)
    save("siren", fade(lowpass(tone, 700) * np.sin(np.pi * t / 9.0) ** 2, 0.1, 0.5), 0.8)


def factory_horn():
    """Schichthorn der Fabrik: ein tiefer, langer Ton."""
    t = time(4.5)
    tone = sum(g * np.sin(2 * np.pi * f * t) for f, g in ((110, 1), (138, 0.7), (165, 0.5)))
    save("factory_horn", fade(lowpass(tone, 500) * np.minimum(t * 2, 1) * np.minimum((4.5 - t) * 1.2, 1), 0.05, 0.3), 0.8)


def train():
    """Güterzug hinter den Fabriken: gleichmäßiges Rollen, in der Mitte am lautesten."""
    t = time(12.0)
    rumble = lowpass(noise(12.0), 180) * (0.75 + 0.25 * np.sin(2 * np.pi * 2.6 * t))
    save("train", fade(rumble * np.sin(np.pi * t / 12.0) ** 2, 0.1, 0.5), 0.8)


def radio():
    """Tanzmusik aus einem Volksempfänger: dünn, dumpf, durch die Scheibe."""
    t_total = 16.5
    tt = time(t_total)
    x = np.zeros_like(tt)
    beat = 0.5
    bass = (110, 147, 165, 147)
    chord = ((277, 330, 440), (294, 370, 440), (330, 415, 494), (294, 370, 440))
    for k in range(int(t_total / beat)):
        at = k * beat
        n = int(0.45 * RATE)
        start = int(at * RATE)
        d = time(0.45)
        bar = k // 4 % 4
        if k % 2 == 0:
            sound = np.sin(2 * np.pi * bass[bar] * d) * decay(d, 0.16)
        else:
            sound = sum(np.sin(2 * np.pi * f * d) for f in chord[bar]) * decay(d, 0.09) * 0.45
        end = min(len(x), start + n)
        x[start:end] += sound[:end - start]
    x = highpass(lowpass(x, 1500), 240) + 0.07 * highpass(lowpass(noise(t_total), 3000), 900)
    save("radio", loopable(x), 0.5)


def heartbeat():
    t_total = 6.5
    x = np.zeros(int(t_total * RATE))
    period = 0.75
    for k in range(int(t_total / period)):
        for offset, gain in ((0.0, 1.0), (0.17, 0.7)):
            at = int((k * period + offset) * RATE)
            d = time(0.14)
            # Der Grundton allein fehlt auf kleinen Lautsprechern, die Obertöne tragen ihn dort.
            beat = (np.sin(2 * np.pi * 52 * d) + 0.7 * np.sin(2 * np.pi * 156 * d) + 0.4 * np.sin(2 * np.pi * 260 * d) * decay(d, 0.03)) * decay(d, 0.04) * gain
            end = min(len(x), at + len(d))
            x[at:end] += beat[:end - at]
    save("heartbeat", loopable(x, 0.2), 0.85)


def typewriter():
    """Tastenanschlag: kurz und dumpf, kein helles Klicken."""
    t = time(0.05)
    save("typewriter", fade(lowpass(noise(0.05), 1500) * decay(t, 0.009) + np.sin(2 * np.pi * 170 * t) * decay(t, 0.012), 0.001, 0.008), 0.6)


def stingers():
    """Kurze, dunkle Abschlüsse statt Fanfaren: Blechakkord und Pauke."""
    t = time(3.0)
    swell = np.minimum(t * 3, 1) * decay(t, 1.0)
    win = sum(np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t) for f in (110, 165, 220, 277))
    save("win", fade(win * swell + lowpass(noise(3.0), 200) * decay(t, 0.3)), 0.6)
    lose = sum(np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t) for f in (73.4, 87.3, 110, 155.6))
    save("lose", fade(lose * swell + lowpass(noise(3.0), 140) * decay(t, 0.5)), 0.65)


def distant_shots():
    """Schüsse viele Straßen weiter: nur noch dumpfe Stöße mit langem Echo."""
    rng = own_rng("distant_shots")
    x = np.zeros(int(5.0 * RATE))
    for at in (0.3, 0.72, 0.95, 2.2, 3.1):
        d = time(0.25)
        pop = lowpass(noise(0.25, rng), 600) * decay(d, 0.03) + np.sin(2 * np.pi * 90 * d) * decay(d, 0.05)
        i = int(at * RATE)
        x[i:i + len(d)] += pop * rng.uniform(0.5, 1.0)
    save("distant_shots", fade(echo(x, (0.38, 0.85), 0.42), 0.002, 0.3), 0.7)


def tram():
    """Straßenbahn in der Ferne: Rollen, Schienenstöße und eine erstickte Klingel, nichts Helles."""
    rng = own_rng("tram")
    t = time(11.0)
    roll = lowpass(noise(11.0, rng), 240)
    joints = np.zeros_like(t)
    for at in np.arange(0.4, 10.6, 0.62):
        d = time(0.1)
        i = int(at * RATE)
        joints[i:i + len(d)] += np.sin(2 * np.pi * 95 * d) * decay(d, 0.03)
    ring = np.zeros_like(t)
    for at in (3.4, 3.72):
        tt = np.maximum(t - at, 0)
        ring += (np.sin(2 * np.pi * 620 * tt) + 0.4 * np.sin(2 * np.pi * 930 * tt)) * decay(tt, 0.1) * (t >= at)
    x = peak_to(roll, 1) + 0.4 * joints + 0.1 * lowpass(ring, 800)
    save("tram", fade(x * np.sin(np.pi * t / 11.0) ** 2, 0.1, 0.5), 0.8)


def foghorn():
    """Nebelhorn vom Hafen: ein Ton, mit Echo über dem Wasser."""
    t = time(3.6)
    tone_ = sum(g * np.sin(2 * np.pi * f * t) for f, g in ((92, 1), (184, 0.5), (276, 0.2)))
    env = np.minimum(t / 0.6, 1) * np.minimum((3.6 - t) / 0.9, 1)
    save("foghorn", fade(echo(lowpass(tone_ * env, 420), (1.2, 2.6), 0.32), 0.05, 0.6), 0.8)


def church_bell():
    """Kirche weit weg: langsame Schläge, stark gedämpft. Ohne die hohen Teiltöne klingt sie nicht nach Blech."""
    t = time(11.0)
    x = np.zeros_like(t)
    for at in (0.0, 2.8, 5.6):
        tt = np.maximum(t - at, 0)
        strike = sum(g * np.sin(2 * np.pi * f * tt) for f, g in ((165, 1), (196, 0.45), (330, 0.5), (415, 0.12)))
        x += strike * np.minimum(tt / 0.03, 1) * decay(tt, 1.5) * (t >= at)
    save("church_bell", fade(lowpass(x, 500), 0.02, 0.6), 0.7)


def shutter():
    """Ein Fensterladen schlägt im Wind: zweimal dumpf auf Holz."""
    rng = own_rng("shutter")
    x = np.zeros(int(1.0 * RATE))
    for at, g in ((0.0, 1.0), (0.24, 0.45)):
        d = time(0.14)
        i = int(at * RATE)
        x[i:i + len(d)] += (np.sin(2 * np.pi * 135 * d) * decay(d, 0.03) + 0.7 * lowpass(noise(0.14, rng), 800) * decay(d, 0.015)) * g
    save("shutter", fade(echo(x, (0.16, 0.34), 0.25), 0.001, 0.2), 0.7)


def gutter():
    """Wasser aus einer Regenrinne: Rinnsal und weiche Tropfen. Reine Töne klängen auch hier wie Metall."""
    rng = own_rng("gutter")
    t = 8.5
    tt = time(t)
    x = 0.35 * highpass(lowpass(noise(t, rng), 1500), 320) * (0.7 + 0.3 * np.sin(2 * np.pi * tt * 1.3))
    for _ in range(45):
        at = rng.integers(0, int((t - 0.1) * RATE))
        d = time(0.05)
        x[at:at + len(d)] += lowpass(noise(0.05, rng), 1200)[:len(d)] * decay(d, 0.012) * rng.uniform(0.6, 2.2)
    save("gutter", loopable(x), 0.5)


PIANO_CHORDS = ((50, 62, 65, 69), (46, 62, 65, 70), (43, 62, 67, 70), (45, 61, 64, 69))
PIANO_MELODY = ((0, 74), (2, 72), (3, 69), (8, 74), (10, 77), (11, 74), (16, 79), (18, 77), (19, 74), (24, 73), (26, 76), (28, 69))
PIANO_BEAT = 0.75


def piano():
    """Klavier in der Kneipe, hinter der Scheibe: langsam, leicht verstimmt, nur die Mitten kommen durchs Glas."""
    rng = own_rng("piano")
    size = int(32 * PIANO_BEAT * MUSIC_RATE)
    x = np.zeros(size)

    def strike(when, note, gain):
        s = tone(note, 2.2, 0.004, 0.6, (1, 0.4, 0.14), detune=6, rng=rng)
        d = np.arange(len(s)) / MUSIC_RATE
        place(x, when * PIANO_BEAT + rng.uniform(-0.012, 0.012), s * np.exp(-d / 0.6) * gain)

    for bar, chord in enumerate(PIANO_CHORDS):
        for beat in range(0, 8):
            at = bar * 8 + beat
            if beat % 2 == 0:
                strike(at, chord[0], 0.8)
            else:
                for note in chord[1:]:
                    strike(at, note, 0.28)
    for at, note in PIANO_MELODY:
        strike(at, note, 0.7)
    save_music("piano", spectrum_filter(x, 1000, 170), 0.5)


def tram_hum():
    """Licht in der stehenden Straßenbahn: Brummen der Drossel, kaum hörbar, nichts Helles."""
    rng = own_rng("tram_hum")
    t = 4.5
    x = hum(100, t, (1, 0.3, 0.12), 0.05) + 0.08 * lowpass(noise(t, rng), 500)
    save("tram_hum", loopable(x), 0.5)


def match():
    """Streichholz: kurzes Reiben, dann Aufflammen. Weich gefiltert, damit es nicht klackert."""
    rng = own_rng("match")
    t = time(0.9)
    rub = highpass(lowpass(noise(0.9, rng), 2800), 900) * np.exp(-((t - 0.07) / 0.045) ** 2)
    rub *= 0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 60 * t))
    flare = lowpass(noise(0.9, rng), 1600) * np.minimum(np.maximum(t - 0.1, 0) / 0.03, 1) * decay(np.maximum(t - 0.1, 0), 0.22)
    save("match", fade(rub + 0.7 * flare, 0.002, 0.15), 0.6)


TITLE_CHORDS = (
    (38, 50, 57, 62, 65), (34, 50, 58, 62, 65), (31, 50, 55, 62, 67),
    (39, 51, 58, 63, 67), (41, 50, 57, 62, 65), (33, 49, 57, 64, 67),
)
TITLE_CHORD_SECONDS = 12
TITLE_MELODY = (
    (3, 57, 6), (9, 58, 3), (13, 57, 8), (27, 55, 5), (33, 58, 4), (39, 63, 7),
    (47, 62, 4), (51, 60, 3), (55, 57, 6), (63, 61, 4), (67, 57, 5),
)
TITLE_DRUM_SECONDS = 6


def title_theme():
    """Titelthema: langsame Fläche in d-Moll, ein tiefes Cello darüber, ferne Pauke. Leise und ohne Helles."""
    rng = own_rng("title_theme")
    seconds = len(TITLE_CHORDS) * TITLE_CHORD_SECONDS
    size = seconds * MUSIC_RATE
    t = np.arange(size) / MUSIC_RATE
    pad = np.zeros(size)
    for i, chord in enumerate(TITLE_CHORDS):
        for note in chord:
            place(pad, i * TITLE_CHORD_SECONDS, tone(note, TITLE_CHORD_SECONDS + 6, 5.0, 5.5, PAD_PARTIALS, detune=8, rng=rng))
    pad = spectrum_filter(pad, 650)
    pad *= 0.8 + 0.2 * np.sin(2 * np.pi * 6 * t / seconds)
    cello = np.zeros(size)
    for start, note, length in TITLE_MELODY:
        place(cello, start, tone(note, length + 2.5, 0.9, 2.5, (1, 0.45, 0.2, 0.08), detune=4, rng=rng))
    # Langsames Vibrato über die Lautstärke, ganze Schwingungen je Durchlauf.
    cello = spectrum_filter(cello * (0.85 + 0.15 * np.sin(2 * np.pi * 360 * t / seconds)), 1100, 90)
    drum = np.zeros(size)
    hit_t = np.arange(int(1.2 * MUSIC_RATE)) / MUSIC_RATE
    hit = np.sin(2 * np.pi * np.cumsum(55 + 25 * np.exp(-hit_t / 0.05)) / MUSIC_RATE) * np.exp(-hit_t / 0.45)
    for k in range(seconds // TITLE_DRUM_SECONDS):
        place(drum, k * TITLE_DRUM_SECONDS, hit * (1.0 if k % 2 == 0 else 0.6))
    drum = spectrum_filter(drum, 260)
    air = spectrum_filter(rng.standard_normal(size), 450, 110) * (0.5 + 0.5 * np.sin(2 * np.pi * 3 * t / seconds)) ** 2
    mix = hall(pad, 9.0, 0.5, rng) + hall(cello, 8.0, 0.55, rng) * 0.55 + drum * 0.35 + air * 0.2
    room = hall(pad, 9.0, 1.0, rng) * 0.4 + hall(cello, 8.0, 1.0, rng) * 0.25
    widen("music_title", peak_to(mix, 0.7), room, MUSIC_RATE, 0.45)


def menu_sounds():
    """Menü: ein weiches Tippen beim Wandern, beim Wählen ein Stempel auf die Akte. Nichts Klickendes."""
    rng = own_rng("menu_sounds")
    t = time(0.09)
    tap = lowpass(noise(0.09, rng), 1200) * decay(t, 0.008) + 0.3 * np.sin(2 * np.pi * 300 * t) * decay(t, 0.012)
    save("menu_move", fade(tap, 0.001, 0.03), 0.4)
    t = time(0.45)
    thud = np.sin(2 * np.pi * 190 * t * (1 - t)) * decay(t, 0.04) + 0.9 * lowpass(noise(0.45, rng), 1100) * decay(t, 0.025)
    paper = highpass(lowpass(noise(0.45, rng), 3000), 1500) * decay(np.maximum(t - 0.02, 0), 0.06) * (t >= 0.02)
    save("menu_select", fade(thud + 0.45 * paper, 0.001, 0.1), 0.55)


def peil_band(rng, count, low, high):
    spec = np.fft.rfft(rng.standard_normal(count))
    f = np.fft.rfftfreq(count, 1 / RATE)
    out = np.fft.irfft(spec / (1 + (f / high) ** 6) * (1 - 1 / (1 + (f / low) ** 6)), count)
    return out / np.abs(out).max()


def peil_ping():
    """Peilgerät von 1927: ein Relais zieht an und fällt ab, dahinter summt kurz die Röhre. Dumpf, damit es über Minuten nicht nervt."""
    rng = own_rng("peil_ping")
    for i, (thunk, gap, hum_hz) in enumerate(((230, 0.055, 100), (205, 0.07, 100), (255, 0.045, 100))):
        t = time(0.42)
        x = np.zeros_like(t)
        for at, gain in ((0.0, 1.0), (gap, 0.45)):
            tt = np.maximum(t - at, 0)
            clack = lowpass(noise(0.42, rng), 1700) * decay(tt, 0.006) + 0.8 * np.sin(2 * np.pi * thunk * tt) * decay(tt, 0.014)
            x += clack * gain * (t >= at)
        swell = np.minimum(t / 0.03, 1) * decay(t, 0.11)
        x += 0.22 * (np.sin(2 * np.pi * hum_hz * t) + 0.5 * np.sin(2 * np.pi * 2 * hum_hz * t) + 0.2 * np.sin(2 * np.pi * 3 * hum_hz * t)) * swell
        # Mitten, damit das Gerät auch im Regen und auf kleinen Lautsprechern durchkommt: Anker schlägt an, die Röhre summt mit Obertönen.
        own = own_rng(f"peil_ping_{i}")
        x = 0.45 * x / np.abs(x).max()
        for at, gain in ((0.0, 1.0), (gap, 0.5)):
            tt = np.maximum(t - at, 0)
            x += gain * (1.3 * peil_band(own, len(t), 500, 1700) * decay(tt, 0.012) + 0.35 * np.sin(2 * np.pi * 3.1 * thunk * tt) * decay(tt, 0.02)) * (t >= at)
        x += 0.12 * (np.sin(2 * np.pi * 4 * hum_hz * t) + 0.7 * np.sin(2 * np.pi * 6 * hum_hz * t) + 0.4 * np.sin(2 * np.pi * 8 * hum_hz * t)) * swell
        save(f"peil_ping_{i}", fade(x, 0.001, 0.08), 0.42)


# ---------- Überarbeitung: Körper aus gefiltertem Rauschen statt nackter Sinustöne ----------

def band(x, low, high):
    """Bandpass im Frequenzraum, steil genug, dass oberhalb von high kaum etwas bleibt."""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    gain = 1 / (1 + (f / high) ** 6)
    if low:
        gain *= 1 - 1 / (1 + (f / low) ** 6)
    return np.fft.irfft(spec * gain, len(x))


def puff(rng, seconds, low, high, decay_s, attack=0.003, at=0.0):
    """Ein Stoß gefilterten Rauschens: weicher Einsatz, dann Abklingen. Ab der Stelle at."""
    t = time(seconds)
    tt = np.maximum(t - at, 0)
    return band(noise(seconds, rng), low, high) * np.minimum(tt / attack, 1) * decay(tt, decay_s) * (t >= at)


def street(dry, seconds, rng, mix, low=150, high=2400):
    """Häuserschlucht: wenige frühe Rückwürfe und ein rauschender Nachhall, gefaltet statt als feste Echos."""
    n = len(dry)
    t = np.arange(n) / RATE
    impulse = band(rng.standard_normal(n), low, high) * decay(t, seconds / 6.9) * (t > 0.03)
    for _ in range(5):
        impulse[int(rng.uniform(0.035, 0.22) * RATE)] += rng.uniform(0.15, 0.5) * np.abs(impulse).max() * 12
    wet = np.fft.irfft(np.fft.rfft(dry, 2 * n) * np.fft.rfft(impulse, 2 * n), 2 * n)[:n]
    return dry + mix * wet / np.abs(wet).max() * np.abs(dry).max()


def gunshot(rng, seconds, crack, body, thump, tail, drive):
    """Knall (hell, sehr kurz), Körper (Mitten), Druck (tief) und Nachhall der Straße. tanh macht den Schuss dicht."""
    t = time(seconds)
    low, high, length, gain = crack
    dry = band(noise(seconds, rng), low, high) * decay(t, length) * gain
    low, high, length, gain = body
    dry += band(noise(seconds, rng), low, high) * np.minimum(t / 0.002, 1) * decay(t, length) * gain
    start, end, length, gain = thump
    dry += np.sin(2 * np.pi * np.cumsum(end + (start - end) * decay(t, 0.02)) / RATE) * decay(t, length) * gain
    dry = np.tanh(dry / np.abs(dry).max() * drive)
    room, mix = tail
    return fade(street(dry, room, rng, mix), 0.0005, 0.15)


def new_shots():
    """Je Waffe ein eigener Bau: Pistole trocken und kurz, Schrot breit und lang, Maschinenpistole knapp, Gewehr mit langem Rollen."""
    rng = own_rng("new_shots")
    for i in range(3):
        k = 1 + 0.08 * (i - 1)
        save(f"shot_{i}", gunshot(rng, 1.3, (700, 5200 * k, 0.006, 1.0), (160, 1100 * k, 0.045, 1.0), (190, 70, 0.04, 0.35), (0.9, 0.3), 2.2), 0.9, replace=True)
        save(f"shotgun_{i}", gunshot(rng, 2.0, (400, 4200 * k, 0.012, 1.1), (100, 900 * k, 0.1, 1.3), (130, 50, 0.08, 0.4), (1.5, 0.42), 2.6), 0.6)
        save(f"rifle_{i}", gunshot(rng, 2.2, (900, 6000 * k, 0.005, 1.1), (140, 900 * k, 0.055, 0.9), (160, 60, 0.05, 0.3), (2.0, 0.5), 2.4), 0.95)
    for i in range(4):
        k = 1 + 0.06 * (i - 1.5)
        save(f"smg_{i}", gunshot(rng, 0.7, (800, 4600 * k, 0.005, 1.0), (170, 1000 * k, 0.028, 0.8), (210, 90, 0.03, 0.5), (0.5, 0.18), 2.0), 0.8)


STEP_PEAKS = {"soft": 0.2, "boot": 0.16, "run": 0.25}


def new_steps():
    """Tritt auf nassem Pflaster: dumpfer Absatz, Sohle in den Mitten, etwas Wasser. Alles Rauschen, nichts Tonales, nichts Helles."""
    rng = own_rng("new_steps")
    for i in range(4):
        v = rng.uniform(0.85, 1.15, 6)
        soft = 0.8 * puff(rng, 0.26, 60, 200 * v[0], 0.028) + 0.55 * puff(rng, 0.26, 300, 1300 * v[1], 0.03 * v[2], 0.005) + 0.12 * puff(rng, 0.26, 900, 2000, 0.06, 0.008, 0.012)
        save(f"step_soft_{i}", fade(soft, 0.004, 0.05), STEP_PEAKS["soft"], replace=True)
        boot = (1.0 * puff(rng, 0.3, 55, 180 * v[3], 0.035) + 0.7 * puff(rng, 0.3, 180, 700 * v[4], 0.02)
                + 0.4 * puff(rng, 0.3, 350, 1200, 0.03, 0.004, 0.035 * v[5]) + 0.08 * puff(rng, 0.3, 900, 2000, 0.06, 0.008, 0.045))
        save(f"step_boot_{i}", fade(boot, 0.004, 0.05), STEP_PEAKS["boot"], replace=True)
        run = 0.7 * puff(rng, 0.3, 70, 220 * v[0], 0.022) + 0.75 * puff(rng, 0.3, 350, 1500 * v[1], 0.025 * v[2], 0.004) + 0.3 * puff(rng, 0.3, 900, 2100, 0.08, 0.01, 0.015)
        save(f"step_run_{i}", fade(run, 0.004, 0.05), STEP_PEAKS["run"], replace=True)


def new_hits():
    """Kugel trifft, Körper fällt, Knüppel trifft: stumpfe Stöße aus Rauschen, je in mehreren Fassungen."""
    rng = own_rng("new_hits")
    for i in range(3):
        v = rng.uniform(0.85, 1.15, 3)
        hit = 0.9 * puff(rng, 0.3, 60, 190 * v[0], 0.05, 0.002) + 0.8 * puff(rng, 0.3, 220, 1500 * v[1], 0.022, 0.002) + 0.25 * puff(rng, 0.3, 500, 1800, 0.07 * v[2], 0.01, 0.02)
        save(f"hit_{i}", fade(hit, 0.001, 0.08), 0.45)
    for i in range(2):
        v = rng.uniform(0.85, 1.15, 3)
        fall = (1.0 * puff(rng, 0.9, 45, 170 * v[0], 0.09, 0.004) + 0.5 * puff(rng, 0.9, 200, 900 * v[1], 0.06, 0.006)
                + 0.55 * puff(rng, 0.9, 50, 220, 0.07, 0.004, 0.17 * v[2]) + 0.3 * puff(rng, 0.9, 250, 1100, 0.09, 0.01, 0.19 * v[2]))
        save(f"fall_{i}", fade(fall, 0.002, 0.2), 0.4)
        club = 0.9 * puff(rng, 0.35, 70, 240 * v[0], 0.045, 0.002) + 0.9 * puff(rng, 0.35, 350, 1700 * v[1], 0.014, 0.001) + 0.3 * puff(rng, 0.35, 200, 800, 0.08, 0.01, 0.02)
        save(f"club_hit_{i}", fade(club, 0.001, 0.1), 0.45)
    t = time(0.3)
    swing = band(noise(0.3, rng), 350, 1900) * np.sin(np.pi * np.minimum(t / 0.22, 1)) ** 3
    save("club_swing", fade(swing, 0.01, 0.05), 0.3)


def mechanism(rng, seconds, events):
    """Mechanik einer Waffe: Folge kurzer, gedämpfter Stöße. events: (Zeit, tief, hoch, Abklingen, Stärke)."""
    x = np.zeros(int(seconds * RATE))
    for at, low, high, length, gain in events:
        x += gain * puff(rng, seconds, low, high, length, 0.0015, at)
    return fade(x, 0.002, 0.1)


def reloads():
    """Nachladen je Waffe. Auf 2,6 kHz begrenzt, damit die Mechanik nicht klackert."""
    rng = own_rng("reloads")
    save("reload_pistole", mechanism(rng, 1.1, ((0.0, 500, 2200, 0.012, 0.6), (0.34, 250, 1500, 0.02, 1.0), (0.72, 600, 2600, 0.01, 0.7), (0.79, 300, 1800, 0.018, 0.9))), 0.4)
    save("reload_schrotflinte", mechanism(rng, 1.5, ((0.0, 300, 1600, 0.02, 0.7), (0.32, 300, 1600, 0.02, 0.7), (0.8, 350, 2000, 0.03, 0.9), (0.98, 200, 1400, 0.03, 1.0))), 0.45)
    save("reload_maschinenpistole", mechanism(rng, 1.3, ((0.0, 400, 2000, 0.015, 0.7), (0.42, 200, 1300, 0.025, 1.0), (0.85, 500, 2400, 0.012, 0.6), (0.93, 250, 1500, 0.02, 0.9))), 0.42)


def field_kit():
    """Rauchtopf (Aufschlag, dann langes Zischen) und Verband (Stoff reißt, wird gewickelt)."""
    rng = own_rng("field_kit")
    save("smoke_throw", fade(0.9 * puff(rng, 0.5, 90, 400, 0.04, 0.002) + 0.6 * puff(rng, 0.5, 400, 1600, 0.02, 0.002) + 0.4 * puff(rng, 0.5, 300, 1200, 0.03, 0.002, 0.16), 0.001, 0.15), 0.5)
    t = time(4.0)
    hiss = band(noise(4.0, rng), 700, 4200) * np.minimum(t / 0.25, 1) * decay(t, 1.3) * (0.85 + 0.15 * np.sin(2 * np.pi * 5 * t + np.sin(2 * np.pi * 0.7 * t)))
    save("smoke_hiss", fade(hiss + 0.25 * band(noise(4.0, rng), 150, 600) * decay(t, 0.8), 0.05, 0.8), 0.35)
    t = time(1.6)
    tear = band(noise(1.6, rng), 900, 3400) * np.exp(-((t - 0.22) / 0.12) ** 2) * (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 23 * t)))
    wrap = sum(band(noise(1.6, rng), 400, 1800) * np.exp(-((t - at) / 0.09) ** 2) for at in (0.7, 1.0, 1.3))
    save("bandage", fade(tear + 0.6 * wrap, 0.02, 0.2), 0.3)


# ---------- Runde 2: Signale entschärft, Regen mit Aufschlag, Tiefes unterscheidbar, Brummen und Hörner mit Obertönen ----------
# Diese Funktionen laufen nach den alten und überschreiben deren Dateien. Die alten bleiben wegen des gemeinsamen Zufalls stehen.

def a_level(x, rate=RATE):
    """Mittlerer Pegel in dB(A), als Näherung ans Gehör."""
    power = np.abs(np.fft.rfft(x)) ** 2
    f2 = np.fft.rfftfreq(len(x), 1 / rate) ** 2
    weight = (12194 ** 2 * f2 ** 2) / ((f2 + 20.6 ** 2) * np.sqrt((f2 + 107.7 ** 2) * (f2 + 737.9 ** 2)) * (f2 + 12194 ** 2) + 1e-30) * 10 ** 0.1
    return 20 * np.log10(np.sqrt((power * weight ** 2).sum() * 2) / len(x) + 1e-12)


def leveled(x, dba):
    """Stellt den Klang auf einen gehörten Pegel statt auf eine Spitze. So bleibt die Mischung beim Umbau, wo sie war."""
    x = x * 10 ** ((dba - a_level(x)) / 20)
    top = np.abs(x).max()
    return x / top * 0.95 if top > 0.95 else x


def save_level(name, x, dba):
    if name in RECORDED:
        return
    data = (leveled(np.asarray(x, np.float64), dba) * 32767).astype("<i2")
    with wave.open(str(OUT / f"{name}.wav"), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(data.tobytes())


def rich(freq, t, top, rolloff, rng, wobble=0.0):
    """Ton mit Obertönen bis top, je Teilton leiser. freq darf eine Kurve sein. Klingt nach Horn oder Trafo statt nach Prüfton."""
    phase = 2 * np.pi * np.cumsum(np.broadcast_to(freq, t.shape)) / RATE
    drift = wobble * np.sin(2 * np.pi * 5.3 * t + rng.uniform(0, 6.28))
    out = np.zeros_like(t)
    k = 1
    while k * np.max(freq) < top:
        out += np.sin(k * (phase + drift) + rng.uniform(0, 6.28)) / k ** rolloff
        k += 1
    return out


def ring(t, modes, at=0.0):
    """Angeschlagenes Metall oder Glas: gedämpfte Eigentöne, die nicht harmonisch liegen."""
    tt = np.maximum(t - at, 0)
    return sum(g * np.sin(2 * np.pi * f * tt) * decay(tt, d) for f, g, d in modes) * (t >= at)


def clinks(rng, seconds, count, span, base, low=600, high=4000):
    """Kleine Metallteile schlagen aneinander: Münzen, Kette, Schlüssel."""
    t = time(seconds)
    x = np.zeros_like(t)
    for _ in range(count):
        at = rng.uniform(0, span)
        k = rng.uniform(0.85, 1.2)
        x += rng.uniform(0.4, 1.0) * (ring(t, ((base * k, 1, 0.03), (base * 1.47 * k, 0.6, 0.022), (base * 1.93 * k, 0.3, 0.015)), at) + 0.8 * puff(rng, seconds, low, high * 0.8, 0.004, 0.0008, at))
    return band(x, low, high)


def soft_signals():
    """Pfeifen, Glocke, Münzen, Kette, Klicken, Dietrich: tiefer, mit Luft und Raum, nichts über 4 kHz."""
    rng = own_rng("soft_signals")
    parts = []
    for length in (0.28, 0.5):
        t = time(length)
        trill = np.sin(2 * np.pi * 32 * t)
        tone_ = np.sin(2 * np.pi * np.cumsum(2150 + 120 * trill) / RATE) + 0.5 * np.sin(2 * np.pi * np.cumsum(2380 + 120 * trill) / RATE)
        blast = (tone_ * (0.6 + 0.4 * trill) + 0.5 * band(noise(length, rng), 1400, 3600)) * np.minimum(t / 0.02, 1)
        parts += [fade(blast, 0.01, 0.04), np.zeros(int(0.08 * RATE))]
    blasts = np.concatenate(parts + [np.zeros(int(0.7 * RATE))])
    save_level("whistle", fade(street(band(blasts, 900, 3400), 0.8, rng, 0.3), 0.005, 0.2), -25.0)

    t = time(0.36)
    glide = 1500 + 800 * np.sin(np.pi * t / 0.36)
    call = (np.sin(2 * np.pi * np.cumsum(glide) / RATE) + 0.45 * band(noise(0.36, rng), 1200, 3200)) * np.sin(np.pi * t / 0.36) ** 0.5
    calls = np.concatenate((fade(call, 0.02, 0.05), np.zeros(int(0.07 * RATE)), fade(call, 0.02, 0.05), np.zeros(int(0.6 * RATE))))
    save_level("boy_whistle", fade(street(band(calls, 800, 3200), 0.7, rng, 0.3), 0.005, 0.2), -25.0)

    # Rasselglocke: ein Hammer schlägt dreizehnmal je Sekunde auf eine Schale.
    t = time(3.0)
    hits_ = np.zeros_like(t)
    for at in np.arange(0.0, 2.0, 1 / 13):
        hits_[int(at * RATE)] = rng.uniform(0.7, 1.0)
    h = time(0.7)
    strike = ring(h, ((640, 1, 0.25), (1280, 0.6, 0.18), (1766, 0.35, 0.1), (2176, 0.15, 0.06))) + 0.3 * puff(rng, 0.7, 500, 2500, 0.004, 0.0008)
    bell_ = np.fft.irfft(np.fft.rfft(hits_, 2 * len(t)) * np.fft.rfft(strike, 2 * len(t)), 2 * len(t))[:len(t)]
    save_level("alarm_bell", fade(street(band(bell_, 300, 3200), 1.2, rng, 0.35), 0.002, 0.4), -30.0)

    save_level("coins", fade(clinks(rng, 0.7, 5, 0.4, 2100, 500, 4200), 0.001, 0.1), -30.0)
    save_level("chain", fade(clinks(rng, 0.3, 4, 0.16, 1500, 600, 3800), 0.001, 0.05), -34.0)
    save_level("click", fade(puff(rng, 0.08, 500, 2600, 0.006, 0.0008) + 0.5 * puff(rng, 0.08, 150, 500, 0.012, 0.001), 0.0005, 0.02), -34.0)

    # Dietrich: leises Schaben, ab und zu fällt ein Stift. Sechs Sekunden, damit man die Schleife nicht erkennt.
    t = time(6.5)
    scrape = band(noise(6.5, rng), 700, 2600) * (0.3 + 0.7 * np.clip(np.sin(2 * np.pi * 0.9 * t) * np.sin(2 * np.pi * 0.37 * t + 1) + 0.3, 0, 1))
    pins = np.zeros_like(t)
    for _ in range(16):
        d = time(0.06)
        tick = (band(rng.standard_normal(len(d)), 600, 2400) * decay(d, rng.uniform(0.004, 0.01)) + 0.4 * band(rng.standard_normal(len(d)), 200, 600) * decay(d, 0.015)) * rng.uniform(0.4, 1.0)
        at = rng.integers(int(0.3 * RATE), int(6.0 * RATE))
        pins[at:at + len(d)] += fade(tick, 0.0008, 0.01)
    save_level("lockpick", loopable(0.25 * scrape / np.abs(scrape).max() + pins / np.abs(pins).max()), -32.0)


RAIN_DROPS = 420
RAIN_DROP_SHARE = 0.35
RAIN_DBA = -18.5


def rain_surface(rng):
    """Regen mit Aufschlag: dunkleres Rauschen als Grund, darauf einzelne Tropfen auf Pflaster, in Pfützen und auf Blech."""
    t = 12.5
    n = int(t * RATE)
    spec = np.fft.rfft(band(rng.standard_normal(n), 450, 5200))
    f = np.fft.rfftfreq(n, 1 / RATE)
    bed = np.fft.irfft(spec / np.sqrt(np.maximum(f, 200) / 1000), n) * (1 + 0.15 * np.sin(2 * np.pi * time(t) * 0.17))
    d = time(0.07)
    kinds = (
        lambda: band(rng.standard_normal(len(d)), 900, 3200) * decay(d, 0.006),
        lambda: band(rng.standard_normal(len(d)), 400, 1400) * decay(d, 0.014),
        lambda: band(rng.standard_normal(len(d)), 1200, 3600) * decay(d, 0.009),
    )
    drops = np.zeros(n)
    for _ in range(RAIN_DROPS):
        drop = fade(kinds[rng.choice(3, p=(0.55, 0.25, 0.2))](), 0.0008, 0.01)
        at = rng.integers(0, n - len(d))
        drops[at:at + len(d)] += drop / np.abs(drop).max() * rng.uniform(0.2, 1.0) ** 2
    # Die Tropfen standen dem Nutzer zu weit vorn. Sie liegen jetzt deutlich unter dem Grundrauschen.
    x = bed / np.sqrt(np.mean(bed ** 2)) + RAIN_DROP_SHARE * drops / np.sqrt(np.mean(drops ** 2))
    return loopable(x)


def new_rain():
    rng = own_rng("new_rain")
    widen("rain", leveled(rain_surface(rng), RAIN_DBA), rain_surface(rng))


def rails():
    """Güterzug und Straßenbahn: Der Zug stampft und schlägt über Schienenstöße, die Bahn singt mit ihrem Motor und klingelt."""
    rng = own_rng("rails")
    t = time(12.0)
    x = band(noise(12.0, rng), 40, 260) * (0.75 + 0.25 * np.sin(2 * np.pi * 2.6 * t))
    x /= np.abs(x).max()
    clack = np.zeros_like(t)
    for at in np.arange(0.5, 11.3, 0.62):
        for offset, g in ((0.0, 1.0), (0.11, 0.7)):
            d = time(0.12)
            i = int((at + offset) * RATE)
            clack[i:i + len(d)] += band(rng.standard_normal(len(d)), 120, 600) * decay(d, 0.02) * g
    chuff = band(noise(12.0, rng), 250, 1100) * (0.5 + 0.5 * np.sin(2 * np.pi * 2.8 * t)) ** 3
    x += 0.5 * clack / np.abs(clack).max() + 0.3 * chuff / np.abs(chuff).max()
    save_level("train", fade(x * np.sin(np.pi * t / 12.0) ** 2, 0.1, 0.5), -32.5)

    t = time(11.0)
    x = 0.8 * band(noise(11.0, rng), 50, 300)
    x /= np.abs(x).max()
    whine = rich(140 + 70 * np.sin(np.pi * t / 11.0) ** 2, t, 1100, 1.5, rng, 0.3)
    joints = np.zeros_like(t)
    for at in np.arange(0.4, 10.4, 1.1):
        for offset, g in ((0.0, 1.0), (0.16, 0.8)):
            d = time(0.12)
            i = int((at + offset) * RATE)
            joints[i:i + len(d)] += band(rng.standard_normal(len(d)), 90, 500) * decay(d, 0.025) * g
    ding = ring(t, ((700, 1, 0.35), (1450, 0.5, 0.2), (1900, 0.2, 0.1)), 3.4) + ring(t, ((700, 1, 0.35), (1450, 0.5, 0.2), (1900, 0.2, 0.1)), 3.75)
    x += 0.2 * whine / np.abs(whine).max() + 0.45 * joints / np.abs(joints).max() + 0.08 * ding / np.abs(ding).max()
    save_level("tram", fade(x * np.sin(np.pi * t / 11.0) ** 2, 0.1, 0.5), -27.9)


def new_hums():
    """Lampen, Motoren, Maschinen: Obertöne, etwas Rauschen und ein eigener Rhythmus statt eines nackten Sinus."""
    rng = own_rng("new_hums")
    t = time(4.5)
    buzz = band(noise(4.5, rng), 200, 1200) * (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 50 * t)))
    save_level("lamp", loopable(rich(100, t, 1500, 1.3, rng, 0.2) + 0.15 * buzz / np.abs(buzz).max()), -29.4)
    save_level("tram_hum", loopable(rich(100, t, 900, 1.6, rng, 0.2) + 0.35 * rich(50, t, 400, 1.2, rng, 0.4) + 0.1 * buzz / np.abs(buzz).max()), -28.9)
    flicker = np.clip(1.2 + np.sin(2 * np.pi * 0.8 * t) * np.sin(2 * np.pi * 3.1 * t + 2), 0.2, 1)
    save_level("neon", loopable((rich(100, t, 2500, 1.0, rng, 0.3) + 0.3 * buzz / np.abs(buzz).max()) * flicker), -26.3)

    t = time(6.5)
    thumps = np.zeros_like(t)
    for at in np.arange(0.0, 6.4, 0.5):
        d = time(0.25)
        i = int(at * RATE)
        thumps[i:i + len(d)] += fade(band(rng.standard_normal(len(d)), 45, 160) * decay(d, 0.06), 0.004, 0.05)[:len(t) - i]
    clatter = band(noise(6.5, rng), 120, 700) * (0.6 + 0.4 * np.sin(2 * np.pi * 4 * t))
    save_level("machine", loopable(thumps / np.abs(thumps).max() + 0.4 * rich(48, t, 600, 1.4, rng, 0.2) / 2 + 0.4 * clatter / np.abs(clatter).max()), -39.2)

    t = time(8.5)
    props = band(noise(8.5, rng), 60, 400) * (0.5 + 0.5 * np.sin(2 * np.pi * 11 * t)) ** 2
    save_level("airship", loopable(rich(41, t, 500, 1.2, rng, 0.3) + 0.7 * rich(43.5, t, 500, 1.2, rng, 0.3) + 1.2 * props / np.abs(props).max()), -41.3)

    # Diesel im Leerlauf: Zündungen, die nicht ganz gleichmäßig kommen.
    t = time(4.5)
    fire_ = np.zeros_like(t)
    at = 0.0
    while at < 4.4:
        fire_[int(at * RATE)] = rng.uniform(0.6, 1.0)
        at += (1 / 11) * rng.uniform(0.9, 1.1)
    d = time(0.15)
    pulse = fade(band(rng.standard_normal(len(d)), 50, 260) * decay(d, 0.03), 0.003, 0.03)
    idle = np.fft.irfft(np.fft.rfft(fire_, 2 * len(t)) * np.fft.rfft(pulse, 2 * len(t)), 2 * len(t))[:len(t)]
    rattle = band(noise(4.5, rng), 100, 500)
    save_level("diesel", loopable(idle / np.abs(idle).max() + 0.25 * rattle / np.abs(rattle).max()), -36.6)


def new_signals():
    """Hörner, Glocke, Schlussakkorde: Obertöne wie bei Blech und Bronze, dazu der Raum der Stadt."""
    rng = own_rng("new_signals")
    t = time(5.5)
    env = np.minimum(t / 0.35, 1) * np.clip((4.2 - t) / 0.6, 0, 1)
    horn = (rich(110, t, 2200, 1.1, rng, 0.25) + 0.8 * rich(138.6, t, 2200, 1.1, rng, 0.25)) * env
    breath = band(noise(5.5, rng), 300, 1800) * env
    save_level("factory_horn", fade(street(band(horn + 0.25 * breath / np.abs(breath).max(), 60, 2200), 1.6, rng, 0.35), 0.05, 0.3), -26.0)

    t = time(6.2)
    env = np.minimum(t / 0.5, 1) * np.clip((3.2 - t) / 0.8, 0, 1)
    save_level("foghorn", fade(street(band(rich(92, t, 1200, 1.0, rng, 0.2) * env, 50, 1300), 2.5, rng, 0.45, 80, 1200), 0.05, 0.6), -26.6)

    # Kirchenglocke: Unterton, Prime, Terz, Quinte und Oktave klingen verschieden lang aus.
    t = time(11.0)
    x = np.zeros_like(t)
    partials = ((98, 0.5, 4.0), (196, 1, 2.5), (235, 0.6, 1.5), (294, 0.35, 1.2), (392, 0.5, 0.9), (523, 0.2, 0.5), (588, 0.15, 0.4))
    for at in (0.0, 2.8, 5.6):
        x += ring(t, partials, at) + 0.4 * puff(rng, 11.0, 150, 900, 0.01, 0.002, at) * np.abs(ring(t, partials, at)).max()
    save_level("church_bell", fade(band(x, 60, 1300), 0.02, 0.6), -27.6)

    t = time(3.4)
    swell = np.minimum(t / 0.25, 1) * decay(t, 1.0)
    drum = puff(rng, 3.4, 50, 160, 0.3, 0.004)
    for name, chord, top, dba in (("win", (110, 165, 220, 277), 1600, -29.0), ("lose", (73.4, 87.3, 110, 155.6), 1100, -31.0)):
        brass = sum(rich(f, t, top, 1.2, rng, 0.2) for f in chord) * swell
        save_level(name, fade(street(brass / np.abs(brass).max() + 0.6 * drum / np.abs(drum).max(), 0.9, rng, 0.25), 0.01, 0.4), dba)


def for_others():
    """Klänge, die andere Teile des Spiels abrufen: Passanten, Waffenkammer, Stadtplan, Startbildschirm, Einsatz 2."""
    rng = own_rng("for_others")
    # Erschrecken: scharfes Einatmen. Ohne Stimme, deshalb nur ein Luftholen.
    for i in range(2):
        t = time(0.45)
        air = band(noise(0.45, rng), 600, 1200) + 0.6 * band(noise(0.45, rng), 1900 + 200 * i, 2900 + 200 * i)
        save_level(f"gasp_{i}", fade(air * np.minimum(t / (0.1 + 0.03 * i), 1) ** 2 * decay(np.maximum(t - 0.14, 0), 0.05), 0.01, 0.1), -30.0)

    drawer = 0.9 * puff(rng, 0.9, 80, 400, 0.05, 0.003, 0.5) + 0.5 * puff(rng, 0.9, 300, 1200, 0.02, 0.002, 0.5)
    buy = clinks(rng, 0.9, 5, 0.35, 2000, 500, 4000)
    save_level("shop_buy", fade(buy / np.abs(buy).max() + drawer / np.abs(drawer).max(), 0.001, 0.15), -30.0)
    save_level("shop_sell", fade(clinks(rng, 0.8, 8, 0.55, 2200, 500, 4000), 0.001, 0.1), -31.0)
    t = time(0.6)
    leather = band(noise(0.6, rng), 500, 2200) * np.exp(-((t - 0.13) / 0.08) ** 2)
    latch = puff(rng, 0.6, 400, 2200, 0.008, 0.001, 0.3) + 0.8 * puff(rng, 0.6, 250, 1500, 0.014, 0.001, 0.38)
    save_level("shop_equip", fade(leather / np.abs(leather).max() + 0.9 * latch / np.abs(latch).max(), 0.01, 0.1), -30.0)

    t = time(0.18)
    save_level("map_select", fade(puff(rng, 0.18, 300, 1800, 0.012, 0.001) + 0.3 * band(noise(0.18, rng), 900, 2800) * np.exp(-((t - 0.06) / 0.03) ** 2), 0.001, 0.05), -33.0)
    t = time(0.6)
    slide = band(noise(0.6, rng), 800, 3000) * np.exp(-((t - 0.14) / 0.09) ** 2)
    save_level("map_accept", fade(slide / np.abs(slide).max() + 0.9 * puff(rng, 0.6, 90, 500, 0.04, 0.002, 0.3) / 0.3, 0.01, 0.15), -30.0)
    stamp = (1.0 * puff(rng, 0.7, 50, 200, 0.07, 0.002) + 0.8 * puff(rng, 0.7, 250, 1100, 0.02, 0.001) + 0.3 * puff(rng, 0.7, 600, 2400, 0.04, 0.004, 0.03)
             + 0.25 * puff(rng, 0.7, 300, 1500, 0.012, 0.001, 0.38))
    save_level("stamp", fade(stamp, 0.001, 0.15), -27.0)

    # Schichtwechsel: viele Stiefel durcheinander, als Schleife.
    n = int(8.5 * RATE)
    crowd = np.zeros(n)
    d = time(0.3)
    for _ in range(75):
        step = fade(band(rng.standard_normal(len(d)), 55, 180) * decay(d, 0.035) + 0.6 * band(rng.standard_normal(len(d)), 200, 900) * decay(d, 0.025), 0.004, 0.05)
        at = rng.integers(0, n - len(d))
        crowd[at:at + len(d)] += step * rng.uniform(0.3, 1.0)
    save_level("crowd_steps", loopable(crowd), -36.0)

    t = time(0.6)
    spark = np.zeros_like(t)
    for _ in range(7):
        spark += rng.uniform(0.4, 1.0) * puff(rng, 0.6, 1200, 4200, rng.uniform(0.003, 0.01), 0.0005, rng.uniform(0, 0.3))
    arc = rich(100, t, 1800, 0.9, rng) * np.minimum(t / 0.01, 1) * decay(t, 0.12)
    save_level("tram_spark", fade(band(spark / np.abs(spark).max() + 0.3 * arc / np.abs(arc).max(), 300, 4500), 0.001, 0.15), -30.0)

    # Zellentür: Angel ächzt, dann schlägt Eisen auf Eisen. Der Zellengang hallt.
    t = time(2.4)
    groan = band(noise(2.4, rng), 90, 420) * (0.5 + 0.5 * np.sin(2 * np.pi * 17 * t)) * np.exp(-((t - 0.45) / 0.3) ** 2)
    clang = ring(t, ((180, 1, 0.5), (410, 0.7, 0.35), (730, 0.4, 0.2), (1180, 0.2, 0.12)), 1.0) + 1.5 * puff(rng, 2.4, 60, 300, 0.08, 0.002, 1.0)
    door = groan / np.abs(groan).max() * 0.5 + clang / np.abs(clang).max()
    save_level("cell_door", fade(street(band(door, 50, 2200), 1.1, rng, 0.3, 120, 1800), 0.01, 0.4), -28.0)
    save_level("keys", fade(clinks(rng, 0.9, 9, 0.7, 1700, 600, 4000), 0.001, 0.1), -32.0)


# ---------- Neue Musik: Instrumente mit Obertönen, mehr Bewegung, Einsätze für den Lagenwechsel ----------
# Der Nutzer hat die Probe der ruhigen Lage freigegeben. Länge, Tonart und Takt bleiben, damit die Lagen synchron laufen.

NEW_THEME = (
    (6, 69, 3.5), (9.5, 67, 0.5), (10, 65, 3), (13.5, 62, 2), (15.5, 60, 0.5), (16, 58, 1.5), (18, 62, 5),
    (30, 70, 3.5), (33.5, 72, 0.5), (34, 69, 3), (37.5, 64, 2), (39.5, 62, 0.5), (40, 61, 1.5), (42, 64, 5),
    (54, 69, 3.5), (57.5, 70, 0.5), (58, 72, 3), (61.5, 70, 2), (63.5, 67, 0.5), (64, 63, 1.5), (66, 67, 5),
    (78, 63, 3.5), (81.5, 65, 0.5), (82, 62, 3), (85.5, 61, 2), (87.5, 64, 0.5), (88, 61, 1.5), (90, 61, 2.5), (92.5, 62, 3),
)
NEW_COUNTER = ((49, 57, 4), (53, 53, 3), (61, 58, 4), (65, 55, 3), (73, 55, 4), (77, 51, 3), (85, 52, 4), (89, 49, 3))
MUSIC_LEVELS = {"music_calm": -26.2, "music_dark": -29.6, "music_suspect": -32.4, "music_tension": -26.9, "music_title": -26.1}
STINGER_DBA = -24.0


def bowed(note, seconds, attack, release, rng, top=2500, rolloff=1.25, vibrato=0.0, odd=False, cents=0.0):
    """Gestrichener oder geblasener Ton: Obertöne bis top, Vibrato setzt verzögert ein."""
    t = np.arange(int(seconds * MUSIC_RATE)) / MUSIC_RATE
    f = midi(note) * 2 ** (cents / 1200)
    phase = 2 * np.pi * f * t + vibrato * np.minimum(t / 1.2, 1) * np.sin(2 * np.pi * 5.2 * t + rng.uniform(0, 6.28))
    out = np.zeros_like(t)
    k = 1
    while k * f < top and k <= 16:
        out += np.sin(k * phase + rng.uniform(0, 6.28)) / k ** rolloff
        k += 2 if odd else 1
    env = np.minimum(1, t / attack) * np.minimum(1, (seconds - t) / release)
    return out * np.sin(np.clip(env, 0, 1) * np.pi / 2) ** 2


def plucked(note, rng):
    """Gezupfter Kontrabass: hohe Teiltöne klingen schneller aus als der Grundton."""
    t = np.arange(int(2.5 * MUSIC_RATE)) / MUSIC_RATE
    f = midi(note)
    out = np.zeros_like(t)
    k = 1
    while k * f < 900:
        out += np.sin(2 * np.pi * k * f * t + rng.uniform(0, 6.28)) / k ** 1.3 * np.exp(-t * (0.9 + 0.5 * k))
        k += 1
    return out * np.minimum(t / 0.006, 1)


def timpani(seconds=1.4, low=52):
    t = np.arange(int(seconds * MUSIC_RATE)) / MUSIC_RATE
    return np.sin(2 * np.pi * np.cumsum(low + 28 * np.exp(-t / 0.04)) / MUSIC_RATE) * np.exp(-t / 0.4) + 0.4 * np.sin(2 * np.pi * 1.5 * low * t) * np.exp(-t / 0.25)


def unit(x):
    return x / np.abs(x).max()


def layer_out(name, mid, side, full=False):
    """Stellt die Mitte auf den Pegel der alten Datei und legt den Raum als Seitenspur daneben."""
    mid = mid * 10 ** ((MUSIC_LEVELS[name] - a_level(mid, MUSIC_RATE)) / 20)
    side = side * 0.45 * np.sqrt(np.mean(mid ** 2) / np.mean(side ** 2))
    if full:
        save_stereo(name, 0.893 * mid + side, 0.893 * mid - side, MUSIC_RATE)
    else:
        save_layer(name, 0.893 * mid, side)


def string_pad(rng, chords, seconds_each, attack, top=2500):
    size = len(chords) * seconds_each * MUSIC_RATE
    pad = np.zeros(size)
    for i, chord in enumerate(chords):
        for note in chord:
            for cents in (-6, 0, 7):
                place(pad, i * seconds_each, bowed(note, seconds_each + 5, attack, 4.5, rng, top, vibrato=0.03, cents=cents))
    return pad


def new_music():
    rng = own_rng("new_music")
    size = MUSIC_SECONDS * MUSIC_RATE
    t = np.arange(size) / MUSIC_RATE
    strings = spectrum_filter(string_pad(rng, CHORDS, CHORD_SECONDS, 2.8), 1800) * (0.8 + 0.2 * np.sin(2 * np.pi * 8 * t / MUSIC_SECONDS))
    bow = spectrum_filter(rng.standard_normal(size), 2500, 300) * np.abs(strings) / np.abs(strings).max()
    bass = np.zeros(size)
    for i, chord in enumerate(CHORDS):
        for offset, step, level in ((0, 0, 1.0), (6, 0, 0.7), (9, 7, 0.6)):
            place(bass, i * CHORD_SECONDS + offset, plucked(chord[0] + step, rng) * level)
    theme = np.zeros(size)
    for start, note, length in NEW_THEME:
        place(theme, start, bowed(note - 12, length + 1.5, 0.18, 1.5, rng, 2600, 1.1, 0.05))
    theme = spectrum_filter(theme, 1500, 250)
    counter = np.zeros(size)
    for start, note, length in NEW_COUNTER:
        place(counter, start, bowed(note, length + 1.2, 0.3, 1.2, rng, 2200, 1.3, 0.03, odd=True))
    counter = spectrum_filter(counter, 1400, 150)
    drone = np.sin(2 * np.pi * midi(26) * t) * (0.75 + 0.25 * np.sin(2 * np.pi * 4 * t / MUSIC_SECONDS))
    brush = spectrum_filter(rng.standard_normal(size), 3000, 800) * np.exp(-((t % 1.5) / 0.25)) * (0.5 + 0.5 * np.sin(2 * np.pi * 2 * t / MUSIC_SECONDS)) ** 2
    strings_room = unit(hall(strings, 7.0, 0.45, rng))
    strings_side = unit(hall(strings, 7.0, 1.0, rng))
    calm = strings_room + 0.02 * unit(bow) + 0.45 * unit(bass) + 0.5 * unit(hall(theme, 8.0, 0.6, rng)) + 0.22 * unit(hall(counter, 8.0, 0.6, rng)) + 0.3 * unit(drone) + 0.04 * unit(brush)
    layer_out("music_calm", calm, strings_side * 0.4 + unit(hall(theme, 8.0, 1.0, rng)) * 0.3, full=True)

    # Dunkel: die Streicher nur als Schatten, darunter ein liegender Celloton je Akkord und der Bass auf der Eins.
    pedal = np.zeros(size)
    low_bass = np.zeros(size)
    for i, chord in enumerate(CHORDS):
        place(pedal, i * CHORD_SECONDS, bowed(chord[0] + 12, CHORD_SECONDS + 4, 3.0, 4.0, rng, 700, 1.4, 0.02))
        place(low_bass, i * CHORD_SECONDS, plucked(chord[0], rng))
    dark = 0.9 * unit(spectrum_filter(strings_room, 480)) + 0.5 * unit(hall(pedal, 6.0, 0.4, rng)) + 0.3 * unit(low_bass) + 0.4 * unit(drone)
    layer_out("music_dark", dark, unit(spectrum_filter(strings_side, 480)))

    # Verdacht: zitternde Streicher über derselben Harmonie mit einem Halbton Reibung, dazu ein leiser Paukenwirbel am Akkordanfang.
    tremolo = np.zeros(size)
    roll = np.zeros(size)
    for i, chord in enumerate(CHORDS):
        for note in chord[2:4]:
            place(tremolo, i * CHORD_SECONDS, bowed(note, CHORD_SECONDS + 3, 1.5, 3.0, rng, 2200, 1.2, cents=5))
        place(tremolo, i * CHORD_SECONDS, bowed(chord[3] + 1, CHORD_SECONDS + 3, 4.0, 3.0, rng, 1800, 1.4) * 0.3)
        for k in range(10):
            place(roll, i * CHORD_SECONDS + k * 0.14, timpani(0.6, 48) * (0.3 + 0.07 * k))
    tremolo = spectrum_filter(tremolo * (0.55 + 0.45 * np.sin(2 * np.pi * TREMOLO_PER_LOOP * t / MUSIC_SECONDS)), 1700, 130)
    suspect = unit(hall(tremolo, 3.0, 0.3, rng)) + 0.25 * unit(spectrum_filter(roll, 300))
    layer_out("music_suspect", suspect, unit(hall(tremolo, 3.0, 1.0, rng)))

    # Gefecht: gestoßene tiefe Streicher auf der Bassfigur, dieselbe Figur zwei Oktaven höher, Blech am Akkordanfang, Pauke und Besen.
    low = np.zeros(size)
    upper = np.zeros(size)
    brass = np.zeros(size)
    drums = np.zeros(size)
    steps_ = int(MUSIC_SECONDS / PULSE_SECONDS)
    swish_t = np.arange(int(0.2 * MUSIC_RATE)) / MUSIC_RATE
    for step in range(steps_):
        when = step * PULSE_SECONDS
        note = CHORDS[int(when // CHORD_SECONDS)][0] + BASS_PATTERN[step % len(BASS_PATTERN)]
        accent = 1.0 if step % 2 == 0 else 0.7
        place(low, when, bowed(note + 12, 0.42, 0.015, 0.25, rng, 900, 1.0) * accent)
        place(upper, when, bowed(note + 24, 0.44, 0.02, 0.28, rng, 2400, 1.2) * accent)
        if step % 4 == 0:
            place(drums, when, timpani(0.5, 46) * 1.4)
        elif step % 2 == 1:
            place(drums, when, spectrum_filter(rng.standard_normal(len(swish_t)), 1800, 500) * np.exp(-swish_t / 0.04) * 0.25)
    for i, chord in enumerate(CHORDS):
        for offset in (0, 6):
            for note in chord[1:4]:
                place(brass, i * CHORD_SECONDS + offset, bowed(note, 2.2, 0.06, 1.6, rng, 1800, 1.0))
    fight = unit(hall(spectrum_filter(low, 900), 2.5, 0.25, rng)) + 0.45 * unit(hall(spectrum_filter(upper, 1800, 140), 2.5, 0.3, rng)) + 0.35 * unit(hall(spectrum_filter(brass, 1500), 3.0, 0.4, rng)) + 0.7 * unit(spectrum_filter(drums, 1900))
    layer_out("music_tension", fight, unit(hall(spectrum_filter(upper, 1800, 140), 2.5, 1.0, rng)))

    # Titel: dieselben Instrumente, langsamer und weiter.
    seconds = len(TITLE_CHORDS) * TITLE_CHORD_SECONDS
    size = seconds * MUSIC_RATE
    t = np.arange(size) / MUSIC_RATE
    pad = spectrum_filter(string_pad(rng, TITLE_CHORDS, TITLE_CHORD_SECONDS, 4.5), 1700) * (0.8 + 0.2 * np.sin(2 * np.pi * 6 * t / seconds))
    cello = np.zeros(size)
    for start, note, length in TITLE_MELODY:
        place(cello, start, bowed(note, length + 2.5, 0.5, 2.5, rng, 2400, 1.15, 0.06))
    cello = spectrum_filter(cello, 1500, 90)
    drum = np.zeros(size)
    pizz = np.zeros(size)
    for k in range(seconds // TITLE_DRUM_SECONDS):
        place(drum, k * TITLE_DRUM_SECONDS, timpani() * (1.0 if k % 2 == 0 else 0.6))
    for i, chord in enumerate(TITLE_CHORDS):
        place(pizz, i * TITLE_CHORD_SECONDS, plucked(chord[0], rng))
        place(pizz, i * TITLE_CHORD_SECONDS + 9, plucked(chord[0] + 7, rng) * 0.6)
    title = unit(hall(pad, 9.0, 0.5, rng)) + 0.6 * unit(hall(cello, 8.0, 0.55, rng)) + 0.3 * unit(spectrum_filter(drum, 300)) + 0.35 * unit(pizz)
    layer_out("music_title", title, unit(hall(pad, 9.0, 1.0, rng)) * 0.4 + unit(hall(cello, 8.0, 1.0, rng)) * 0.25, full=True)

    # Einsätze für den Lagenwechsel. Ohne Terz, damit sie über jedem Akkord der Folge stehen können.
    n = int(3.5 * MUSIC_RATE)
    rise = np.zeros(n)
    for k in range(6):
        place(rise, k * 0.11, timpani(0.5, 46) * (0.4 + 0.12 * k))
    place(rise, 0.7, timpani(1.6, 46) * 1.6)
    swell = np.zeros(n)
    for note in (38, 45, 50):
        swell[:int(2.6 * MUSIC_RATE)] += bowed(note, 2.6, 0.7, 1.6, rng, 1500, 1.1)
    stinger = unit(spectrum_filter(rise, 400)) + 0.6 * unit(spectrum_filter(swell, 1500))
    save_stinger("music_in_fight", stinger)
    n = int(5.0 * MUSIC_RATE)
    settle = np.zeros(n)
    for note, level in ((38, 1.0), (50, 0.6), (57, 0.4)):
        settle[:int(4.6 * MUSIC_RATE)] += bowed(note, 4.6, 0.9, 3.4, rng, 1300, 1.3, 0.03) * level
    settle[:int(2.5 * MUSIC_RATE)] += 1.5 * plucked(38, rng)
    save_stinger("music_to_calm", spectrum_filter(settle, 1300))


def save_stinger(name, x):
    x = x * 10 ** ((STINGER_DBA - a_level(x, MUSIC_RATE)) / 20)
    # Paukenspitzen weich abfangen, statt den ganzen Einsatz leiser zu machen.
    x = np.tanh(x / 0.5) * 0.5
    with wave.open(str(OUT / f"{name}.wav"), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(MUSIC_RATE)
        f.writeframes((x * 32767).astype("<i2").tobytes())


def quieter_hiss():
    """
    Der Nutzer hörte im Einsatz ein zu lautes Geräusch und hielt es für das Schweißen. Gemessen lag der Dampfstoß der Kessel
    11 dB über der Musik und war zur Hälfte heller als 4 kHz. Beide sind jetzt dunkler, setzen weich ein und liegen 9 und 8 dB tiefer.
    """
    rng = own_rng("quieter_hiss")
    t = time(2.4)
    hiss = band(noise(2.4, rng), 600, 3600) * np.minimum(t / 0.12, 1) * decay(t, 0.8)
    rumble = band(noise(2.4, rng), 60, 220) * decay(t, 0.5)
    save_level("steam_burst", fade(hiss / np.abs(hiss).max() + 0.5 * rumble / np.abs(rumble).max(), 0.02, 0.3), -30.8)
    n = int(3.5 * RATE)
    x = 0.15 * band(rng.standard_normal(n), 300, 2200)
    d = time(0.02)
    for _ in range(170):
        crackle = fade(band(rng.standard_normal(len(d)), 500, 2800) * decay(d, 0.004), 0.0008, 0.004)
        at = rng.integers(0, n - len(d))
        x[at:at + len(d)] += crackle / np.abs(crackle).max() * rng.uniform(0.3, 1.6) * 0.25
    save_level("weld", loopable(x, 0.2), -35.8)


def telephone():
    """Feldtelefon der Wache: Kurbel, kurzes Klingeln mit gedämpfter Stimme, tote Leitung, gekappter Draht. Alles leise und dunkel."""
    rng = own_rng("telephone")
    # Kurbelinduktor: Hörer ab, dann ratternde Umdrehungen.
    t = time(2.0)
    lift = puff(rng, 2.0, 300, 1800, 0.012, 0.001) + 0.7 * puff(rng, 2.0, 200, 900, 0.02, 0.001, 0.09)
    turns = band(noise(2.0, rng), 300, 1900) * (0.5 + 0.5 * np.sin(2 * np.pi * 7.5 * t)) ** 4 * np.clip((t - 0.3) / 0.1, 0, 1) * np.clip((1.85 - t) / 0.15, 0, 1)
    whirr = band(rich(58, t, 700, 1.3, rng, 0.3), 150, 700) * np.clip((t - 0.3) / 0.2, 0, 1) * np.clip((1.85 - t) / 0.2, 0, 1)
    save_level("phone_dial", fade(lift / np.abs(lift).max() + 0.8 * turns / np.abs(turns).max() + 0.25 * whirr / np.abs(whirr).max(), 0.002, 0.1), -32.0)

    # Die Kaserne meldet sich: ein kurzer Wecker, dann eine Stimme durch die Muschel. Keine Worte, nur Silben.
    t = time(2.6)
    strikes = np.zeros_like(t)
    for at in np.arange(0.0, 0.7, 1 / 22):
        strikes[int(at * RATE)] = rng.uniform(0.7, 1.0)
    h = time(0.4)
    gong = ring(h, ((1150, 1, 0.12), (1730, 0.6, 0.09), (2300, 0.25, 0.05)))
    bell_ = np.fft.irfft(np.fft.rfft(strikes, 2 * len(t)) * np.fft.rfft(gong, 2 * len(t)), 2 * len(t))[:len(t)]
    syllables = np.zeros_like(t)
    at = 1.0
    while at < 2.35:
        length = rng.uniform(0.09, 0.2)
        syllables += np.exp(-((t - at - length / 2) / (length / 2.5)) ** 2) * rng.uniform(0.6, 1.0)
        at += length + rng.uniform(0.02, 0.09)
    pitch = 118 + 14 * np.sin(2 * np.pi * 1.3 * t) + 8 * np.sin(2 * np.pi * 3.1 * t + 1)
    voice_ = band(rich(pitch, t, 1600, 0.9, rng, 0.2), 320, 1300) * syllables
    save_level("phone_called", fade(band(bell_, 600, 3000) / np.abs(bell_).max() * 0.7 + voice_ / np.abs(voice_).max(), 0.002, 0.15), -32.0)

    # Tote Leitung: Er drückt dreimal die Gabel, nichts kommt zurück.
    taps = sum(puff(rng, 1.1, 350, 2000, 0.01, 0.001, at) + 0.6 * puff(rng, 1.1, 150, 600, 0.02, 0.001, at + 0.05) for at in (0.0, 0.28, 0.52))
    save_level("phone_dead", fade(taps, 0.001, 0.2), -33.0)

    # Draht gekappt: die Zange beißt zu, der gespannte Draht schnalzt weg.
    t = time(0.5)
    snip = puff(rng, 0.5, 700, 3000, 0.006, 0.0008) + 0.6 * puff(rng, 0.5, 300, 1200, 0.015, 0.001, 0.02)
    twang = ring(t, ((410, 1, 0.12), (830, 0.5, 0.08), (1270, 0.25, 0.05)), 0.03)
    save_level("wire_cut", fade(snip / np.abs(snip).max() + 0.35 * twang / np.abs(twang).max(), 0.0008, 0.1), -32.0)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for make in (rain, wind, lamp, neon, steam, fire, diesel, machine, television, steps, shots, hits, whistle, bell, small_things, stingers, music, thunder, airship, steam_burst, weld, dog, siren, factory_horn, train, radio, heartbeat, typewriter, fresh_random, dog_fight, boy_whistle, wind_gust, distant_shots, tram, foghorn, church_bell, shutter, gutter, piano, tram_hum, match, title_theme, menu_sounds, peil_ping, new_shots, new_steps, new_hits, reloads, field_kit, soft_signals, new_rain, rails, new_hums, new_signals, for_others, new_music, quieter_hiss, telephone):
        make()
    files = list(OUT.glob("*.wav"))
    print(f"{len(files)} Geräusche, {sum(f.stat().st_size for f in files) // 1024} KiB")


if __name__ == "__main__":
    main()
