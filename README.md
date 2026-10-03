# YAWNS

**Yet Another Worthless Niche Slop**, v6769.001. A mapping-only editor client: the menus lead to song select and the editor, nothing else. It works fully offline.

## What's in it

- **Map overlay:** another map under the one you edit, with its rhythm on two timeline lanes and mismatched hitsounds marked.
- **Hitsounds tab:** Hitsound Studio's lanes inside the editor, with ghost notes from any difficulty.
- **Tools tab:** Hitsound Copier, Timing Copier, Property Transformer, Rhythm Guide, Pattern Gallery, Map Cleaner, Mapset Merger, Combo Colour Studio, Timing Helper.
- **Right-click a selection, Tools:** organise stream, save as pattern, align to axis, radial copy, complete sliders, Sliderator, tumours.
- **Mapping tools toolbox:** axis guide and Snapping Tools.
- **Verify:** extra checks (uneven streams, off-axis sliders, auto-fail, unused hitsound files, hitsounds that differ from the hitsound difficulty), and a fix button on the issues that can fix themselves.
- **Map backups** on every save.

Details, how it is built and tested, and the roadmap: [YAWNS-NOTES.md](YAWNS-NOTES.md).

## Running it

YAWNS needs osu!lazer installed and opens the same library (beatmaps, skins, settings). So:

- it must match your lazer version. Each YAWNS release says which lazer release it is built for, and it refuses to start if your lazer last ran a different one,
- close lazer before starting YAWNS, only one of the two may run at a time.

**Download** (Releases page): `YAWNS-<version>-x86_64.AppImage` for Linux, `YAWNS-<version>-win-x64.zip` for Windows (unzip, run `yawns.exe`).

**From source** (Linux, NixOS):

```sh
./run.sh          # builds on first run, then starts YAWNS
./run.sh --build  # rebuild
```

## Credits

- **osu!lazer** by ppy Pty Ltd is the base of YAWNS. Thanks to ppy and the lazer contributors.
- **Hitsound Studio** ([hitsound.vivlos.dev](https://hitsound.vivlos.dev)) is my own tool, ported into the Hitsounds tab.
- **Mapping Tools** by OliBomby ([github.com/OliBomby/Mapping_Tools](https://github.com/OliBomby/Mapping_Tools)) inspired most of the Tools tab. The tools are reimplemented for lazer's editor, not copied.

## Licence

The code is MIT-licensed, see [LICENCE](LICENCE). It is based on code by ppy Pty Ltd, whose copyright notice the licence requires to stay.

Game assets (skin, samples, textures) are not in this repo. They come from ppy's osu-resources and are licensed separately under CC BY-NC 4.0, so YAWNS is free and non-commercial. The release builds include them without the Torus and Venera fonts (those need a commercial licence), YAWNS uses Inter instead. The intro voice line is cut from osu!lazer's triangles intro (CC BY-NC 4.0, ppy Pty Ltd).

Bundled third-party libraries and their licences: [THIRD-PARTY.md](THIRD-PARTY.md).

YAWNS is not affiliated with or endorsed by ppy Pty Ltd.
