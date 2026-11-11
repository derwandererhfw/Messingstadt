"""Probe für die neue Einsatzmusik: nur die ruhige Lage, zum Vergleich mit der alten. Ändert nichts am Spiel.

Aufruf: python tools/art/musik_probe.py
Schreibt work/hoerproben/vergleich/Q_musik_ruhe_alt_dann_neu.wav und Q2_musik_ruhe_neu_ganze_schleife.wav.
Instrumente haben Obertöne bis etwa 2,5 kHz statt Sinus hinter einem Tiefpass, das Thema hat mehr Töne und eine Gegenstimme.
"""
import wave

import numpy as np

import generate_audio as g
from hoerproben import DRIZZLE, MUSIC_DB, RATE, ROOT, gain, limited, looped, weather

OUT = ROOT / "work" / "hoerproben" / "vergleich"
SECONDS = g.MUSIC_SECONDS
MR = g.MUSIC_RATE
CALM_DBA = -25.7  # wie die alte Datei, die Musik wird also nicht lauter

THEME = (
    (6, 69, 3.5), (9.5, 67, 0.5), (10, 65, 3), (13.5, 62, 2), (15.5, 60, 0.5), (16, 58, 1.5), (18, 62, 5),
    (30, 70, 3.5), (33.5, 72, 0.5), (34, 69, 3), (37.5, 64, 2), (39.5, 62, 0.5), (40, 61, 1.5), (42, 64, 5),
    (54, 69, 3.5), (57.5, 70, 0.5), (58, 72, 3), (61.5, 70, 2), (63.5, 67, 0.5), (64, 63, 1.5), (66, 67, 5),
    (78, 63, 3.5), (81.5, 65, 0.5), (82, 62, 3), (85.5, 61, 2), (87.5, 64, 0.5), (88, 61, 1.5), (90, 61, 2.5), (92.5, 62, 3),
)
COUNTER = ((49, 57, 4), (53, 53, 3), (61, 58, 4), (65, 55, 3), (73, 55, 4), (77, 51, 3), (85, 52, 4), (89, 49, 3))


def voice(note, seconds, attack, release, top, rolloff, rng, vibrato=0.0, odd=False, cents=0.0):
    """Ein gestrichener oder geblasener Ton: Obertöne bis top, Vibrato setzt verzögert ein."""
    t = np.arange(int(seconds * MR)) / MR
    f = g.midi(note) * 2 ** (cents / 1200)
    phase = 2 * np.pi * f * t + vibrato * np.minimum(t / 1.2, 1) * np.sin(2 * np.pi * 5.2 * t + rng.uniform(0, 6.28))
    out = np.zeros_like(t)
    k = 1
    while k * f < top:
        out += np.sin(k * phase + rng.uniform(0, 6.28)) / k ** rolloff
        k += 2 if odd else 1
    env = np.minimum(1, t / attack) * np.minimum(1, (seconds - t) / release)
    return out * np.sin(np.clip(env, 0, 1) * np.pi / 2) ** 2


def pluck(note, rng):
    """Gezupfter Kontrabass: hohe Teiltöne klingen schneller aus als der Grundton."""
    t = np.arange(int(2.5 * MR)) / MR
    f = g.midi(note)
    out = np.zeros_like(t)
    k = 1
    while k * f < 900:
        out += np.sin(2 * np.pi * k * f * t + rng.uniform(0, 6.28)) / k ** 1.3 * np.exp(-t * (0.9 + 0.5 * k))
        k += 1
    return out * np.minimum(t / 0.006, 1)


def a_level(x):
    return g.a_level(x, MR)


def calm():
    rng = g.own_rng("music_calm_neu")
    size = SECONDS * MR
    t = np.arange(size) / MR
    strings = np.zeros(size)
    for i, chord in enumerate(g.CHORDS):
        for note in chord:
            for cents in (-6, 0, 7):
                g.place(strings, i * g.CHORD_SECONDS, voice(note, g.CHORD_SECONDS + 5, 2.8, 4.5, 2500, 1.25, rng, 0.03, cents=cents))
    strings = g.spectrum_filter(strings, 1800)
    strings *= 0.8 + 0.2 * np.sin(2 * np.pi * 8 * t / SECONDS)
    bow = g.spectrum_filter(rng.standard_normal(size), 2500, 300) * np.abs(strings) / np.abs(strings).max()

    bass = np.zeros(size)
    for i, chord in enumerate(g.CHORDS):
        for offset, step, level in ((0, 0, 1.0), (6, 0, 0.7), (9, 7, 0.6)):
            g.place(bass, i * g.CHORD_SECONDS + offset, pluck(chord[0] + step, rng) * level)

    theme = np.zeros(size)
    for start, note, length in THEME:
        g.place(theme, start, voice(note - 12, length + 1.5, 0.18, 1.5, 2600, 1.1, rng, 0.05))
    theme = g.spectrum_filter(theme, 1500, 250)
    counter = np.zeros(size)
    for start, note, length in COUNTER:
        g.place(counter, start, voice(note, length + 1.2, 0.3, 1.2, 2200, 1.3, rng, 0.03, odd=True))
    counter = g.spectrum_filter(counter, 1400, 150)

    drone = np.sin(2 * np.pi * g.midi(26) * t) * (0.75 + 0.25 * np.sin(2 * np.pi * 4 * t / SECONDS))
    brush = g.spectrum_filter(rng.standard_normal(size), 3000, 800) * np.exp(-((t % 1.5) / 0.25)) * (0.5 + 0.5 * np.sin(2 * np.pi * 2 * t / SECONDS)) ** 2

    def unit(x):
        return x / np.abs(x).max()

    mid = (unit(g.hall(strings, 7.0, 0.45, rng)) + 0.02 * unit(bow) + 0.45 * unit(bass) + 0.5 * unit(g.hall(theme, 8.0, 0.6, rng))
           + 0.22 * unit(g.hall(counter, 8.0, 0.6, rng)) + 0.3 * unit(drone) + 0.04 * unit(brush))
    side = unit(g.hall(strings, 7.0, 1.0, rng)) * 0.4 + unit(g.hall(theme, 8.0, 1.0, rng)) * 0.3
    mid = mid * 10 ** ((CALM_DBA - a_level(mid)) / 20)
    side = side * np.sqrt(np.mean(mid ** 2) / np.mean(side ** 2))
    return np.stack((0.893 * mid + 0.45 * side, 0.893 * mid - 0.45 * side), axis=1)


def to_rate(x):
    at = np.arange(int(len(x) * RATE / MR)) * MR / RATE
    return np.stack([np.interp(at, np.arange(len(x)), x[:, c]) for c in (0, 1)], axis=1)


def save(name, x):
    x = limited(x)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())
    print(f"{name}: {len(x) / RATE:.0f} s")


def report(label, x):
    m = x[:, 0]
    power = np.abs(np.fft.rfft(m)) ** 2
    f = np.fft.rfftfreq(len(m), 1 / MR)
    share = lambda low, high: power[(f >= low) & (f < high)].sum() / power.sum() * 100
    print(f"{label}: im Spiel {a_level(m) + MUSIC_DB:.1f} dB(A), Spitze {20 * np.log10(np.abs(x).max()):.1f} dBFS, unter 120 Hz {share(0, 120):.0f} %, "
          f"120-500 {share(120, 500):.0f} %, 500-1000 {share(500, 1000):.1f} %, 1-2 kHz {share(1000, 2000):.2f} %, über 2 kHz {share(2000, 1e9):.3f} %, "
          f"Naht {np.abs(x[0] - x[-1]).max() / (np.median(np.abs(np.diff(x, axis=0))) + 1e-9):.1f}-fach")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    new = calm()
    with wave.open(str(g.OUT / "music_calm.wav")) as f:
        old = np.frombuffer(f.readframes(f.getnframes()), "<i2").astype(float).reshape(-1, 2) / 32768
    report("alt", old)
    report("neu", new)
    part = 44
    fresh = to_rate(new)
    bed = weather(part, DRIZZLE)
    before = looped("music_calm", part) * gain(MUSIC_DB) + bed
    after = fresh[:part * RATE] * gain(MUSIC_DB) + bed
    save("Q_musik_ruhe_alt_dann_neu", np.concatenate((before, np.zeros((int(1.5 * RATE), 2)), after)))
    save("Q2_musik_ruhe_neu_ganze_schleife", np.concatenate((fresh, fresh[:8 * RATE])) * gain(MUSIC_DB) + weather(SECONDS + 8, DRIZZLE))


if __name__ == "__main__":
    main()
