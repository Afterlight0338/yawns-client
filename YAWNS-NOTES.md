# YAWNS (Yet Another Worthless Niche Slop)

A mapping-only osu!standard client, forked from osu!lazer `2026.921.0-lazer` (branch `mapping`).
It shares the library in `~/.local/share/osu` with the installed lazer, so it must stay on the same release tag and never run at the same time.
Every change to upstream code is marked with a `YAWNS:` comment.

## Run, build, test
- Play: `./run.sh` (add `--build` after code changes). Refuses to start while lazer runs or when the versions differ.
- Build: `nix-shell -p dotnet-sdk_10 --run "dotnet build osu.Desktop.slnf -c Debug"`
- Tests run headless inside `osu-fhs-runner` (native libs). Run long suites against a copy of the repo, not the live checkout.
- After merging a new upstream tag, clear conflicts on deleted files with
  `git status --porcelain | awk '/^(DU|UD)/{print $2}' | xargs -r git rm -q`

## v6769.001 (2026-10-03)
- Private, personal use. Fully offline: no online API (a logged out stand-in), every endpoint points at an `.invalid` host, no news banner, no online beatmap lookups, no Discord presence, no self-updates; login, wiki, online settings and beatmap submission are hidden.
- Version: `OsuGameBase.YAWNS_VERSION` (shown in the window title and settings). The assembly version stays 0.0.0 on purpose: a real one would switch on lazer's release-only crash reports, update checks and writing the version into the game.ini it shares with lazer.
- The executable is `yawns`; the editor title and messages that name the app say YAWNS. Format and game words (.osu, osu!stable, the osu! mode, built-in skin names) stay; skin names live in the database lazer shares.
- The lazer release YAWNS is built from is `OsuGameBase.LAZER_VERSION`; update it after merging a newer release. Both `run.sh` and the app itself (`OsuGameDesktop.SetHost`, before the database opens) refuse to start when lazer's game.ini `Version` differs, so prebuilt releases are protected too. Debug builds read `game.dev.ini` in `osu-development` instead.
- v6769.001 status: the relaunch check for the online.db fix (`LocalCachedBeatmapMetadataSource.FetchCache`) was skipped on purpose, the client starts in guest mode and that is offline enough. osu.Game.Tests (same filter as "Test status" below) on 2026-10-03: 1201 pass, 14 fail, 8 skipped. The 14 are exactly the known ones (3 headless-only, 11 TestSceneBeatmapEditorNavigation). The osu! editor suite was not rerun.
- How to run tests: the FHS runner only has .NET 8, so build and test from inside a .NET 10 nix-shell:
  `nix-shell -p dotnet-sdk_10 --run "dotnet build osu.Game.Tests -c Debug -v q; osu-fhs-runner -c 'dotnet test osu.Game.Tests --no-build -c Debug --filter \"...\"'"`
  Filter for the regression run: `FullyQualifiedName~osu.Game.Tests.Visual.Editing|FullyQualifiedName~osu.Game.Tests.Editing|FullyQualifiedName~Beatmaps.Formats|FullyQualifiedName~osu.Game.Tests.Database|FullyQualifiedName~osu.Game.Tests.Visual.Navigation.TestSceneMappingClient|FullyQualifiedName~TestSceneBeatmapEditorNavigation`

## Features
- **Mapping-only client:** menus lead to edit song select and the editor only; editor test-play stays. taiko/catch/mania, mobile, macOS and the tournament client are removed.
- **Map overlay:** another map under the edited one (opacity, offset, overlay just a pattern), two rhythm lanes on the timeline with unmatched hitsounds in red. Compose screen, left toolbox, "overlay".
- **Tools tab** (top right, next to verify): Hitsound Copier, Timing Copier (both from the overlay map), Property Transformer, Rhythm Guide, Pattern Gallery.
- **Right-click a selection, Tools:** Organise stream, Save as pattern, Align to axis.
- **Stream organiser:** Clean curve (wobble filtered out without shrinking, turns evened out), Arc, Straight; spacing fitted between the ends or from distance snap; even, accelerate or decelerate. One undo step.
- **Pattern gallery:** `.osupattern` files (plain .osu inside) in `~/.local/share/osu/patterns`, preview cards, insert at the current time fitted to the map's BPM.
- **Axis guide** (right toolbox, "mapping tools"): detects the map's slider tilt, draws the four base axes through the selection, says how far off axis it is.
- **Hitsounds tab** (next to tools): Hitsound Studio's lanes, native. Works on a hitsound difficulty (circles in the middle; a gameplay difficulty is read-only, with a button to create one). Click a lane to add or remove a hit, Ctrl+drag paints, right-drag erases, Shift+drag selects, Delete, Ctrl+C/X/V at the playhead, Alt+scroll zooms. Lanes have mute, solo and a volume for new hits. The overlay map shows as ghost notes. "Import hitsounds from..." reads another difficulty, "Copy to difficulties..." runs the Hitsound Copier and saves them. Undo works for everything.
- **Slider Completionator** (right-click, Tools, "Complete sliders..."): sets selected sliders to a number of beats or to end at the playhead, and a length, then works out the velocity (or keeps a velocity and works out the length). Can scale the control points.
- **Radial copy** (right-click, Tools, or the right toolbox): the 360/n rule, n-1 rotated copies around the playfield or selection centre, a set number of beats apart.
- **Map backups:** every save also writes `~/.local/share/osu/backups/<set>/<difficulty> <time>.osu`, newest 30 per difficulty kept. File, "Open backups folder".
- **Verify checks:** uneven streams (warning, points at Organise stream) and straight sliders off the map's axes (negligible, shown when negligible issues are shown).

## Status (2026-10-02 unattended run)
- [x] Phase 8: rename to YAWNS, logo as window icon and main menu cookie (`osu.Game/Resources/YAWNS/menu-logo.png`, `osu.Desktop/lazer.ico`)
- [x] Phase 5: Hitsound Studio tab (port of Hitsound Studio, hitsound.vivlos.dev): lanes, editing, playback through the beatmap skin, import, copy to difficulties. Not ported: waveform transient highlighting, .osz export (lazer exports natively).
- [x] Phase 7: radial copy, map backups, verify checks (spacing and overlap checks already existed upstream, so only the new ones were added)
- [x] Property Transformer (Mapping Tools port; slider velocity, hitsound volume and index per object since lazer stores them there; storyboard and video times not touched)
- [x] Slider Completionator (Mapping Tools port)
- [x] Rhythm Guide (Mapping Tools port)
- [x] Verify fix buttons (IHasFix on an issue template): Resnap (unsnapped objects), Unmute (muted clickable objects), Organise (uneven streams), Align (off-axis sliders), Delete (new unused hitsound files check), Copy (new check: hitsounds differing from the set's hitsound difficulty).
- [x] Map Cleaner (Mapping Tools port, the parts lazer lacks: resnap objects and slider ends to 1/16 and 1/12, resnap bookmarks, remove muting, mute unclickable, delete unused hitsound files; green lines are written clean on save anyway)
- [x] Mapset Merger (brings a difficulty of another set in the library into this one, custom indices moved and files copied)
- [x] Combo Colour Studio (colour points, bursts, read back from a map; points saved per difficulty in combo-colour-studio/)
- [x] AutoFail Detector (Verify check, osu!stable 2B object loading, without mods and with Hard Rock; no fix: Mapping Tools' fix uses negative-length spinners)
- [x] Timing Helper (Tools tab; auto beat counts need timing that is already close, or set beats between markers)
- [x] Snapping Tools (mapping tools group toggle: virtual points, lines and circles of the 8 objects nearest the current time, used by placement and dragging)
- [x] Sliderator (selected sliders, easing curves instead of Mapping Tools' hand-drawn graph; no repeats)
- [x] Tumour Generator (one layer: triangle, square, circle, parabola; keeps duration by raising slider velocity)
- Not ported: Hitsound Preview Helper (the Hitsounds tab covers that workflow), Slider Picturator (relies on osu!stable's slider texture downscaling, nothing to see in lazer). Slider Merger, Metadata Manager and resnapping already exist in lazer.

## Test status at the end of the run
- osu! editor tests (osu.Game.Rulesets.Osu.Tests, Editor): 284/284 pass.
- osu.Game.Tests (editing, formats, database, YAWNS client and editor navigation): 1173 pass, 14 fail, all known and unrelated to YAWNS changes:
  3 always fail in this headless setup (TestErrorNotifications, TestHandleCurrentScreenChanges, TestUrlDecodingOfArgs), and
  11 TestSceneBeatmapEditorNavigation tests expect upstream's play song select, which YAWNS replaces with edit song select by design.
- Intro (2026-10-02): the triangles intro is the only one (Circles/Welcome deleted, no Intro sequence setting). Its track is "welcome to" + a metal pipe clang (2026-10-03, from Pixabay: pipe trimmed to 2.04s from 1.32s, -6.1 dB to match the old jingle loudness, 0.5s fade out) + silence, the main menu stays silent. Expected failures from that: TestPauseDuringIntro (expects menu music) and TestImportantNotificationDoesntInterruptSetup (headless runs game time faster than audio, so the 8s intro fallback posts the audio warning; upstream used the Circles intro in game tests to avoid this). Also TestVelocityToolbox (osu editor tests): it edits whatever map the game has loaded, now the 8 s intro, and seeks past its end. Rebuild: ffmpeg concat of the original voice 0-0.84s and the 2.04s clang, padded to 8s, mp3 224k. The game finds the imported intro by BeatmapHash = sha256 of the .osu, so a new audio file also needs a changed .osu (e.g. the Tags line) and the new hash in IntroTriangles.cs, or the old import keeps playing.

## Still to try in the real client (not testable headless)
How things look and feel: tools tab cards and panels, stream organiser on real streams, Hitsounds tab (playback through your skin, scrolling and zoom), the YAWNS logo on the main menu.
