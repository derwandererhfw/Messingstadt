"""Erzeugt alle Geräusche des Spiels per Synthese, ohne fremde Aufnahmen.

Aufruf: python tools/art/generate_audio.py
Schreibt game/assets/audio/*.wav. Gleicher Zufall, gleiche Dateien.
Schleifen enden und beginnen gleich, damit beim Wiederholen kein Knacken entsteht.
"""
import wave
from pathlib import Path

import numpy as np

RATE = 32000
OUT = Path(__file__).resolve().parents[2] / "game" / "assets" / "audio"
RNG = np.random.default_rng(1927)


def time(seconds):
    return np.arange(int(seconds * RATE)) / RATE


def noise(seconds):
    return RNG.standard_normal(int(seconds * RATE))


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


def rain():
    t = 12.5
    body = highpass(lowpass(noise(t), 6500), 900)
    swell = 1 + 0.15 * np.sin(2 * np.pi * time(t) * 0.17)
    x = body * swell
    # Einzelne Tropfen: weiche Tupfer aus Rauschen. Reine Töne klängen hier wie Metall.
    for _ in range(60):
        at = RNG.integers(0, int((t - 0.1) * RATE))
        d = time(0.04)
        x[at:at + len(d)] += lowpass(noise(0.04), 1800)[:len(d)] * decay(d, 0.009) * RNG.uniform(0.5, 1.6)
    save("rain", loopable(x), 0.6)


def wind():
    t = 16.5
    x = lowpass(noise(t), 260)
    tt = time(t)
    x *= 0.65 + 0.35 * np.sin(2 * np.pi * tt * 0.11) * np.sin(2 * np.pi * tt * 0.07 + 1)
    # Fernes Grollen der Fabriken
    x += 0.5 * lowpass(noise(t), 60)
    save("wind", loopable(x), 0.7)


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
    for _ in range(14):
        at = RNG.integers(0, int((t - 0.1) * RATE))
        d = time(0.015)
        x[at:at + len(d)] += lowpass(noise(0.015), 2500)[:len(d)] * decay(d, 0.004) * 1.5
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
        x[at:at + len(d)] += highpass(noise(0.02), 1500)[:len(d)] * decay(d, 0.005) * RNG.uniform(0.3, 1.8)
    save("fire", loopable(x), 0.6)


def diesel():
    """Fluchtwagen im Leerlauf: dumpfes, unregelmäßiges Tuckern."""
    t = 4.5
    tt = time(t)
    x = np.zeros_like(tt)
    beat = 11.0
    for k in range(int(t * beat)):
        at = max(0, int((k / beat + RNG.uniform(-0.003, 0.003)) * RATE))
        if at >= len(x):
            continue
        d = time(0.09)
        pulse = (np.sin(2 * np.pi * 55 * d) + 0.5 * np.sin(2 * np.pi * 110 * d)) * decay(d, 0.03)
        end = min(len(x), at + len(d))
        x[at:end] += pulse[:end - at] * (1 if k % 2 else 0.7)
    x += 0.15 * lowpass(noise(t), 500)
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
    """Schritte auf nassem Pflaster: dumpfer Absatz, dazu ein leises Knirschen."""
    for voice, (name, pitch, weight) in enumerate((("step_soft", 92, 0.8), ("step_boot", 72, 1.0), ("step_run", 110, 1.0))):
        for i in range(4):
            t = time(0.16)
            thump = np.sin(2 * np.pi * (pitch + i * 5) * t * (1 - t)) * decay(t, 0.03)
            scrape = highpass(lowpass(noise(0.16), 4500), 1200) * decay(t, 0.018 + 0.006 * i)
            wet = highpass(noise(0.16), 2800) * np.exp(-((t - 0.05) / 0.02) ** 2) * 0.25
            save(f"{name}_{i}", fade(weight * thump + 0.4 * scrape + wet), 0.85)


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


def tone(note, seconds, attack, release, partials, detune=0.0):
    t = np.arange(int(seconds * MUSIC_RATE)) / MUSIC_RATE
    out = np.zeros_like(t)
    for h, gain in enumerate(partials, start=1):
        for cents in (-detune, detune) if detune else (0.0,):
            f = midi(note) * h * 2 ** (cents / 1200)
            out += gain * np.sin(2 * np.pi * f * t + RNG.uniform(0, 6.28))
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


def hall(x, seconds, mix):
    """Großer dunkler Raum: Faltung mit abklingendem Rauschen, ebenfalls nahtlos."""
    t = np.arange(len(x)) / MUSIC_RATE
    impulse = RNG.standard_normal(len(x)) * np.exp(-t / (seconds / 3))
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


def music():
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

    calm = hall(pad, 7.0, 0.45) * 1.0 + drone * 0.5 + breath * 0.25 + hall(theme, 8.0, 0.7) * 0.42
    save_music("music_calm", calm, 0.7)

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
    tension = hall(spectrum_filter(tension, 650), 2.5, 0.25)
    save_music("music_tension", tension, 0.7)


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


def stingers():
    """Kurze, dunkle Abschlüsse statt Fanfaren: Blechakkord und Pauke."""
    t = time(3.0)
    swell = np.minimum(t * 3, 1) * decay(t, 1.0)
    win = sum(np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t) for f in (110, 165, 220, 277))
    save("win", fade(win * swell + lowpass(noise(3.0), 200) * decay(t, 0.3)), 0.6)
    lose = sum(np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t) for f in (73.4, 87.3, 110, 155.6))
    save("lose", fade(lose * swell + lowpass(noise(3.0), 140) * decay(t, 0.5)), 0.65)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for make in (rain, wind, lamp, neon, steam, fire, diesel, machine, television, steps, shots, hits, whistle, bell, small_things, stingers, music, thunder, airship, steam_burst, weld):
        make()
    files = list(OUT.glob("*.wav"))
    print(f"{len(files)} Geräusche, {sum(f.stat().st_size for f in files) // 1024} KiB")


if __name__ == "__main__":
    main()
