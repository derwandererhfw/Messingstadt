"""Baut Spielklänge aus freien Aufnahmen (alle CC0, siehe docs/ton-herkunft.md).

Aufruf: python tools/art/import_recordings.py <Ordner mit den Rohaufnahmen>
Der Ordner liegt außerhalb des Repositorys und enthält die Dateien so, wie sie geladen wurden (01 bis 10).
Schneidet, filtert dunkel (die Freesound-Dateien sind MP3-Vorschauen), stellt jeden Klang auf den Pegel des Klangs,
den er ersetzt, und schreibt game/assets/audio/*.wav sowie tools/art/recordings.json mit den Prüfsummen.
Der Generator überspringt diese Namen (generate_audio.RECORDED), check_audio.py prüft sie gegen recordings.json.
"""
import hashlib
import io
import json
import subprocess
import sys
import wave
import zipfile
from pathlib import Path

import numpy as np

import generate_audio as g

ROOT = Path(__file__).resolve().parents[2]
RATE = g.RATE
# Pegel der ersetzten Klänge kommen aus diesem Stand, dem letzten, den der Nutzer abgenommen hat.
REFERENCE = "e94a35d"
QUIETER_DB = 1.0

DOG = "04/freesound_612857_13194336-hq.mp3"
DOOR = "07/freesound_346267_470543-hq.mp3"
RPG = "09/kenney_rpg-audio.zip:Audio/"

# Name im Spiel: (Teile, Klang für den Pegel, Tiefpass, Hochpass, Straßenhall, Tonhöhe)
# Ein Teil ist (Quelle, Anfang, Ende, Pause danach). Ende None heißt bis zum Schluss.
SOUNDS = {
    # Wachhund: die tiefsten, härtesten Beller der Aufnahme, mit etwas Straße dahinter.
    "dog_0": ([(DOG, 6.70, 7.85, 0)], "dog", 3200, 150, 0.2, 1.0),
    "dog_1": ([(DOG, 9.95, 10.36, 0)], "dog", 3200, 150, 0.2, 1.0),
    "dog_2": ([(DOG, 1.14, 2.46, 0)], "dog", 3200, 150, 0.2, 1.0),
    "dog_3": ([(DOG, 30.98, 32.32, 0)], "dog", 3200, 150, 0.2, 1.0),
    "door_open_0": ([(DOOR, 14.97, 16.36, 0)], "door_open", 3500, 120, 0.0, 1.0),
    "door_open_1": ([(DOOR, 13.30, 14.85, 0)], "door_open", 3500, 120, 0.0, 1.0),
    "door_open_2": ([(RPG + "doorOpen_2.ogg", 0, None, 0)], "door_open", 3500, 120, 0.0, 1.0),
    "gasp_0": ([("01/freesound_318078_5057106-hq.mp3", 0.40, 0.95, 0)], "gasp_0", 3600, 200, 0.0, 1.0),
    "gasp_1": ([("01/freesound_318078_5057106-hq.mp3", 0.40, 0.95, 0)], "gasp_1", 3600, 200, 0.0, 1.12),
    # "pages" ohne Nummer bleibt bestehen, weil eine Sequenz den Klang beim Namen nennt und ein Test die Datei verlangt.
    "pages": ([(RPG + "bookFlip1.ogg", 0, None, 0)], "pages", 3800, 300, 0.0, 1.0),
    "pages_0": ([(RPG + "bookFlip1.ogg", 0, None, 0)], "pages", 3800, 300, 0.0, 1.0),
    "pages_1": ([(RPG + "bookFlip2.ogg", 0, None, 0)], "pages", 3800, 300, 0.0, 1.0),
    "pages_2": ([(RPG + "bookFlip3.ogg", 0, None, 0)], "pages", 3800, 300, 0.0, 1.0),
    "bandage": ([(RPG + "cloth4.ogg", 0, None, 0.08), (RPG + "cloth1.ogg", 0, None, 0.1), (RPG + "cloth2.ogg", 0, None, 0.06), (RPG + "cloth3.ogg", 0, None, 0)], "bandage", 3000, 150, 0.0, 1.0),
    "shop_equip": ([(RPG + "handleSmallLeather.ogg", 0, None, 0.05), (RPG + "dropLeather.ogg", 0, None, 0)], "shop_equip", 3200, 100, 0.0, 1.0),
}
# Schritte bleiben aus dem Generator. Die Aufnahmen auf Beton (Impact Sounds) haben nichts Helles, fielen aber
# an der Grenze für Tonales durch (49 bis 62 % statt höchstens 40 %), und Schritte dürfen nie wieder klackern.


def source_bytes(raw, source):
    if ":" in source:
        archive, member = source.split(":", 1)
        with zipfile.ZipFile(raw / archive) as f:
            return f.read(member), raw / archive
    return (raw / source).read_bytes(), raw / source


def decode(data):
    """Über ffmpeg nach mono in der Rate des Spiels."""
    out = subprocess.run(["ffmpeg", "-v", "error", "-i", "pipe:0", "-ac", "1", "-ar", str(RATE), "-f", "s16le", "pipe:1"], input=data, capture_output=True, check=True).stdout
    return np.frombuffer(out, "<i2").astype(float) / 32768


def energy(x):
    """Gehörte Energie des ganzen Klangs: Pegel in dB(A) plus Dauer. So zählt ein längerer Klang nicht als leiser."""
    return g.a_level(x) + 10 * np.log10(len(x) / RATE)


def reference(name):
    raw = subprocess.run(["git", "show", f"{REFERENCE}:game/assets/audio/{name}.wav"], capture_output=True, check=True, cwd=ROOT).stdout
    with wave.open(io.BytesIO(raw)) as f:
        return np.frombuffer(f.readframes(f.getnframes()), "<i2").astype(float) / 32768


def build(raw, name):
    parts, level_like, lowpass, highpass, room, pitch = SOUNDS[name]
    pieces, sources = [], {}
    for source, start, end, pause in parts:
        data, path = source_bytes(raw, source)
        sources[source] = hashlib.sha256(path.read_bytes()).hexdigest()
        x = decode(data)
        x = x[int(start * RATE):None if end is None else int(end * RATE)]
        pieces += [g.fade(x, 0.004, 0.03), np.zeros(int(pause * RATE))]
    x = np.concatenate(pieces)
    if pitch != 1.0:
        at = np.arange(int(len(x) / pitch)) * pitch
        x = np.interp(at, np.arange(len(x)), x)
    x = g.band(x, highpass, lowpass)
    if room:
        x = np.concatenate((x, np.zeros(int(0.6 * RATE))))
        x = g.street(x, 0.8, g.own_rng(name), room)
    x = g.fade(x, 0.003, min(0.08, len(x) / RATE / 4))
    x = x * 10 ** ((energy(reference(level_like)) - QUIETER_DB - energy(x)) / 20)
    top = np.abs(x).max()
    return (x / top * 0.9 if top > 0.9 else x), sources


def main():
    raw = Path(sys.argv[1])
    record = {}
    for name in sorted(SOUNDS):
        x, sources = build(raw, name)
        path = g.OUT / f"{name}.wav"
        with wave.open(str(path), "wb") as f:
            f.setnchannels(1)
            f.setsampwidth(2)
            f.setframerate(RATE)
            f.writeframes((x * 32767).astype("<i2").tobytes())
        record[name] = {"sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "quellen": sources}
        print(f"{name}: {len(x) / RATE:.2f} s, {g.a_level(x):.1f} dB(A), Spitze {20 * np.log10(np.abs(x).max()):.1f} dBFS")
    (Path(__file__).parent / "recordings.json").write_text(json.dumps(record, indent=1, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
