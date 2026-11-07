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


def save(name, x, peak=0.9):
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


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for make in (rain, wind, lamp, neon, steam, fire, diesel, machine, television, steps, shots, hits, whistle, bell, small_things, stingers, music, thunder, airship, steam_burst, weld, dog, siren, factory_horn, train, radio, heartbeat, typewriter, fresh_random, dog_fight, boy_whistle, wind_gust, distant_shots, tram, foghorn, church_bell, shutter, gutter, piano, tram_hum, match, title_theme, menu_sounds):
        make()
    files = list(OUT.glob("*.wav"))
    print(f"{len(files)} Geräusche, {sum(f.stat().st_size for f in files) // 1024} KiB")


if __name__ == "__main__":
    main()
