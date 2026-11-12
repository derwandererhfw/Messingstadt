# Herkunft der Klänge aus Aufnahmen

Fast aller Ton entsteht per Synthese in `tools/art/generate_audio.py`. Die folgenden Spielklänge stammen aus freien Aufnahmen.
Alle Quellen stehen unter Creative Commons 0 (https://creativecommons.org/publicdomain/zero/1.0/): keine Namensnennung nötig, keine Einschränkung für Spiele.
Die Lizenz wurde am 12.11.2026 auf jeder Quellseite gelesen, unmittelbar vor dem Laden.

Die Rohaufnahmen liegen nicht im Repository. `tools/art/import_recordings.py <Ordner>` schneidet, filtert und pegelt sie zu den Spielklängen, `tools/art/recordings.json` hält die Prüfsummen von Quelle und Ergebnis fest. Der Generator überschreibt diese Klänge nicht (`RECORDED` in `generate_audio.py`), `check_audio.py` prüft sie gegen die Prüfsummen.

Die Freesound-Dateien sind die Vorschaufassungen der Seite (MP3). Deshalb sind alle Klänge bei 3 bis 3,8 kHz tiefpassgefiltert.

| Spielklang | Wofür | Quelle: Titel | Urheber | Adresse | Geladene Datei | SHA-256 der geladenen Datei |
|---|---|---|---|---|---|---|
| dog_0 bis dog_3 | Wachhund, ferner Hund in der Stadt | Dog bark | Weak_Hero | https://freesound.org/people/Weak_Hero/sounds/612857/ | freesound_612857_13194336-hq.mp3 | 4cd88bcabb8f… |
| door_open_0, door_open_1 | Tür geht auf | Door Creak | stib | https://freesound.org/people/stib/sounds/346267/ | freesound_346267_470543-hq.mp3 | c28360ce5b55… |
| door_open_2 | Tür geht auf | RPG Audio, doorOpen_2.ogg | Kenney Vleugels (Kenney.nl) | https://www.kenney.nl/assets/rpg-audio | kenney_rpg-audio.zip | 6dbeaf8544da… |
| gasp_0, gasp_1 | Passant erschrickt (gasp_1 ist dieselbe Aufnahme, 12 % höher) | Whimper_Gasp_1_Female | Drkvixn91 | https://freesound.org/people/Drkvixn91/sounds/318078/ | freesound_318078_5057106-hq.mp3 | a4e7c8d41a9c… |
| pages, pages_0 bis pages_2 | Blättern im Kontobuch (pages ist gleich pages_0, eine Sequenz nennt den Namen) | RPG Audio, bookFlip1 bis 3.ogg | Kenney Vleugels (Kenney.nl) | https://www.kenney.nl/assets/rpg-audio | kenney_rpg-audio.zip | 6dbeaf8544da… |
| bandage | Verband anlegen | RPG Audio, cloth1 bis 4.ogg | Kenney Vleugels (Kenney.nl) | https://www.kenney.nl/assets/rpg-audio | kenney_rpg-audio.zip | 6dbeaf8544da… |
| shop_equip | Ausrüsten in der Waffenkammer | RPG Audio, handleSmallLeather.ogg und dropLeather.ogg | Kenney Vleugels (Kenney.nl) | https://www.kenney.nl/assets/rpg-audio | kenney_rpg-audio.zip | 6dbeaf8544da… |

Die vollen Prüfsummen stehen in `tools/art/recordings.json`. Der Lizenztext des Kenney-Pakets liegt in `docs/lizenzen/kenney-rpg-audio-License.txt`.

## Geladen, aber nicht verwendet

- Dog Barks.wav (UnderlinedDesigns) und Dog Barking (nick121087): höher und kürzer als die Beller von Weak_Hero.
- Wooden door creaking (TheBuilder15), Creaking door (loudernoises), wood door old open close slow creak steps enter.flac (kyles): heller oder mit mehr Grundrauschen als Door Creak von stib.
- Impact Sounds (Kenney): Die Schritte auf Beton haben nichts Helles, fielen aber an der Grenze für Tonales durch. Schritte bleiben deshalb aus dem Generator.
- Münzen aus RPG Audio: liegen fast ganz über 4 kHz und wären nach dem Filtern kaum noch da.

## Weiter synthetisch, obwohl schwach

Die Zellentür (keine Aufnahme einer Eisentür auf der Liste), Stimmen und Gemurmel (gibt es noch nicht), Schüsse (Quellen noch nicht belegt).
