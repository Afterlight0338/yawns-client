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

## v6769.003 (2026-10-07)
- **Any lazer version with the same database structure:** the game.ini version check is gone (app and run.sh). `RealmAccess.RefuseSchemaChanges` (on in Release builds) makes YAWNS refuse to start, with a message box, when lazer's client.realm has a newer or older schema version or does not open. It never migrates the library and never takes lazer's "back up and start fresh" path, so the file is left untouched. `LAZER_VERSION` is now only informational. Test: `SharedLibrarySchemaTest`.
- **Stream organiser, Follow volume:** a fourth speed mode. Each gap's spacing is scaled by the volume at its two objects (`VolumeGapScale`: loudest gap = Strength times the quietest, medium volume = plain spacing), on top of the rhythm (gap duration). "Volume from": song loudness (`SongLoudness`: mean waveform amplitude from each object to the next, in dB, blurred over neighbours) or hitsound volume (each circle's sample volume). Tests in StreamOrganiserTest and TestSceneStreamOrganiser.
- **Select-all lag:** benchmark `TestSceneSelectAllPerf` (osu.Game.Rulesets.Osu.Tests/Editor, `[Explicit]`, run it by name): 2000 objects, prints ms per frame with nothing selected, all selected (first frames and steady) and deselected. With everything selected: YAWNS before 94 to 99 ms, upstream 2026.921.0 69 to 71 ms, YAWNS now 75 to 78 ms (headless Release, a few runs each, about 5 ms noise).
  - Cause 1: every slider blueprint carried 48 hidden boxes for the Ctrl curve preview (48k extra drawables on a whole-map selection). Now created on first use.
  - Cause 2: the true slider end ring re-evaluated the path every frame for every selected slider. Now only when position, path version, duration or radius change.
  - What is left is upstream: select all keeps a drawable and a blueprint alive for every object (`EditorBlueprintContainer.SelectAll`, `Playfield.KeepAllAlive`). Not touched, to keep ppy merges easy.
  - Not a problem (checked with the profiler): no objects come alive or die while the selection holds, and the YAWNS selection handlers run once per select-all (AddRange fires one event).
- **Overlay skin:** Map panel of the overlay, "Overlay skin" dropdown ("Same as editor", the built-in skins, your skins; random skin left out). A chosen skin gets its own skin chain (`ReferenceBeatmapLayer.OverlaySkinSource`: the skin plus the same fallbacks as `SkinManager.AllSources`), so neither the editor skin nor the edited map's beatmap skin leaks in. Kept across difficulty switches (`ReferenceState.Skin`), reset when the editor is reopened. Tutorial text updated.
- Tests after these changes: osu! editor suite 376 pass, 1 known failure (TestVelocityToolbox); TestSceneEditorReferenceBeatmap (new TestOverlaySkin), TestSceneEditorReferenceDifficultySwitch (skin kept), TestSceneDifficultySwitching and TestSceneTutorial pass.

## Profiling
- dotnet-trace is not in nixpkgs' SDK: `dotnet tool install dotnet-trace --tool-path <dir>`, and run it with `DOTNET_ROOT` set to the nix SDK (`dirname $(readlink -f $(which dotnet))`).
- Start the test in the background inside `osu-fhs-runner`, find the `testhost` pid, `dotnet-trace collect -p <pid> --duration 00:00:00:40 --format Speedscope`. Sum inclusive time per method over the window you care about (the test steps are plain methods in the stacks, use them as time markers). Map setup in the test dominates a whole-run profile, so always cut a window.
- A/B against upstream: `git worktree add <dir> 2026.921.0-lazer`, copy the benchmark test in, build and run the same way.

## v6769.002 (2026-10-03)
- New editor tools, all described in the Tutorial tab (top right, after hitsounds): radial copy v2, angled flip (Ctrl+Shift+T), radial guide, perfect it, quick rotate and live rotate, scale options for sliders, randomise sliders, axis drag, increase Bezier degree, Alt+drag curve editing, curve point preview, true slider end ring, SV hotkeys, SV equaliser, stream wiggle, distance snap edge clamp, continue distance snap, divisor BPM hover.
- Built for lazer 2026.921.0-lazer, same as 001. osu.Game.Rulesets.Osu.Tests editor suite on 2026-10-03: 376 pass, 1 known failure (TestVelocityToolbox). osu.Game.Tests: only the known failures.

## v6769.001 (2026-10-03)
- Private, personal use. Fully offline: no online API (a logged out stand-in), every endpoint points at an `.invalid` host, no news banner, no online beatmap lookups, no Discord presence, no self-updates; login, wiki, online settings and beatmap submission are hidden.
- Version: `OsuGameBase.YAWNS_VERSION` (shown in the window title and settings). The assembly version stays 0.0.0 on purpose: a real one would switch on lazer's release-only crash reports, update checks and writing the version into the game.ini it shares with lazer.
- The executable is `yawns`; the editor title and messages that name the app say YAWNS. Format and game words (.osu, osu!stable, the osu! mode, built-in skin names) stay; skin names live in the database lazer shares.
- The lazer release YAWNS is built from is `OsuGameBase.LAZER_VERSION` (informational since 003). Any lazer version works as long as the realm schema version is the same: `RealmAccess.RefuseSchemaChanges` (Release builds) refuses to start otherwise and never migrates or replaces lazer's client.realm. Merge a newer lazer release when its schema_version changes. Debug builds read `game.dev.ini` in `osu-development` instead.
- v6769.001 status: the relaunch check for the online.db fix (`LocalCachedBeatmapMetadataSource.FetchCache`) was skipped on purpose, the client starts in guest mode and that is offline enough. osu.Game.Tests (same filter as "Test status" below) on 2026-10-03: 1201 pass, 14 fail, 8 skipped. The 14 are exactly the known ones (3 headless-only, 11 TestSceneBeatmapEditorNavigation). The osu! editor suite was not rerun.
- How to run tests: the FHS runner only has .NET 8, so build and test from inside a .NET 10 nix-shell:
  `nix-shell -p dotnet-sdk_10 --run "dotnet build osu.Game.Tests -c Debug -v q; osu-fhs-runner -c 'dotnet test osu.Game.Tests --no-build -c Debug --filter \"...\"'"`
  Filter for the regression run: `FullyQualifiedName~osu.Game.Tests.Visual.Editing|FullyQualifiedName~osu.Game.Tests.Editing|FullyQualifiedName~Beatmaps.Formats|FullyQualifiedName~osu.Game.Tests.Database|FullyQualifiedName~osu.Game.Tests.Visual.Navigation.TestSceneMappingClient|FullyQualifiedName~TestSceneBeatmapEditorNavigation`

## Features
- **Mapping-only client:** menus lead to edit song select and the editor only; editor test-play stays. taiko/catch/mania, mobile, macOS and the tournament client are removed.
- **Tutorial tab** (top right, after hitsounds): every YAWNS feature explained with what it does, how to use it, where to find it and a diagram (Tutorial/ in MappingTools: DiagramCanvas draws in playfield coordinates, TutorialTopics holds the text, TutorialDiagrams the pictures). Add a topic there whenever a feature is added. A test checks every topic has its parts and that diagrams stay on the canvas.
- **Map overlay:** another map under the edited one (opacity, offset, its own skin, overlay just a pattern), two rhythm lanes on the timeline with unmatched hitsounds in red. Compose screen, left toolbox, "overlay".
- **Tools tab** (top right, next to verify): Hitsound Copier, Timing Copier (both from the overlay map), Property Transformer, Rhythm Guide, Pattern Gallery.
- **Right-click a selection, Tools:** Organise stream, Save as pattern, Align to axis.
- **Stream organiser:** Clean curve (wobble filtered out without shrinking, turns evened out), Arc, Straight; spacing fitted between the ends or from distance snap; even, accelerate, decelerate or follow volume (song loudness or hitsound volume). One undo step.
- **Wiggle** (stream organiser): every other object moves 0 to 40 px to alternating sides of the stream line, for wiggle streams.
- **Pattern gallery:** `.osupattern` files (plain .osu inside) in `~/.local/share/osu/patterns`, preview cards, insert at the current time fitted to the map's BPM.
- **Axis guide** (right toolbox, "mapping tools"): detects the map's slider tilt, draws the four base axes through the selection, says how far off axis it is.
- **Hitsounds tab** (next to tools): Hitsound Studio's lanes, native. Works on a hitsound difficulty (circles in the middle; a gameplay difficulty is read-only, with a button to create one). Click a lane to add or remove a hit, Ctrl+drag paints, right-drag erases, Shift+drag selects, Delete, Ctrl+C/X/V at the playhead, Alt+scroll zooms. Lanes have mute, solo and a volume for new hits. The overlay map shows as ghost notes. "Import hitsounds from..." reads another difficulty, "Copy to difficulties..." runs the Hitsound Copier and saves them. Undo works for everything.
- **Slider Completionator** (right-click, Tools, "Complete sliders..."): sets selected sliders to a number of beats or to end at the playhead, and a length, then works out the velocity (or keeps a velocity and works out the length). Can scale the control points.
- **Radial copy** (right-click, Tools, or the right toolbox): the 360/n rule, n-1 rotated copies around the playfield or selection centre, a set number of beats apart.
- **Angled flip** (right-click, Tools, or the right toolbox, or Ctrl+Shift+T): mirrors the selection across a yellow line at any angle (clockwise or anticlockwise, through the playfield, selection or guide centre). It only moves the objects while you adjust it (nothing is added); "Keep original" adds a mirrored copy a set number of beats later instead. One undo step.
- **Radial guide** (right toolbox, "Radial guide"): spokes (the full circle in n steps, with a first-spoke angle) and rings around a centre you drag on the playfield; placing and dragging snap to crossings, spokes and rings. Radial copy and angled flip can turn around its centre ("Radial guide centre").
- **Perfect it** (right-click, Tools, or the right toolbox): moves roughly placed objects onto an exact regular polygon or star ({n/k}, part of one if fewer objects), n-fold rotational symmetry (groups like radial copy makes) or a mirror pair (second half mirrors the first). Least squares, live preview, Keep or Cancel. Only positions move (a slider moves whole, its shape stays).
- **Increase Bezier degree** (select a control point of a Bezier slider, press I, or right-click it): adds one control point and keeps the exact curve and length (de Casteljau degree elevation). A perfect-curve segment is converted to Bezier first, which changes its shape; other curve types are left alone.
- **Direct curve editing** (select a Bezier slider, Alt+left-drag on its curve; middle click stays lazer's quick delete): the point under the cursor follows it, each control point moving in proportion to its Bernstein weight there (head stays). **Ctrl over a selected single-segment Bezier slider** draws a yellow preview of the curve if a point were added at the cursor; **Ctrl+Shift+click** adds the point at the nearer end of the slider instead of along it. Snapping a placed object to a perfect curve's centre is in Snapping Tools (turn it on in mapping tools), not always on, because an always-on centre steals grid placement.
- **True slider end** (a selected slider): a white ring marks the legacy last tick (36 ms before the end, or half the slider if shorter), where osu!stable-style scoring ends. **Hover the beat divisor** to see its BPM at the current time (170 BPM at 1/6 is 255). **Distance snap** now goes to the nearest place on the playfield edge instead of switching off when the snapped spot is off screen. **Continue distance snap** (editor key, unbound by default): sets the distance spacing from the selected object (or the last at or before the current time) and the one before it.
- **Quick rotate** (right toolbox, "Quick rotate"): Ctrl+Alt+. and Ctrl+Alt+, rotate the selection clockwise and anticlockwise by a step you set (default 60, decimals fine); Ctrl+Shift+Scroll rotates live, 5 degrees a notch, with Alt added 1 degree (untested in the real client: a headless test cannot drive Shift+wheel; Alt+Scroll alone is the timeline zoom, so it is not used; all rebindable). The rotate and scale origin now default to the selection centre (lazer already remembers your last choice).
- **Scale options** (scale popover, "Sliders in a group"): when scaling several objects, keep slider shapes (lazer, heads only), scale shapes with the layout, or scale shapes and adjust SV so durations stay. The scale range is wider (negative flips, within the playfield).
- **Randomise sliders** (right toolbox): turn each selected slider around its head by a random angle (up to a limit you set), or jitter its anchors by up to some pixels, lengths kept. With no sliders selected it works on every slider in the map. A slider that would not fit on the playfield after a few tries is left as it was. One undo step.
- **Axis drag** (hold Ctrl+Alt while dragging objects): the drag stays on the nearest of the map's base axes (see Axis guide; Alt also toggles distance snap while held).
- **SV equaliser** (right toolbox): sliders keep the speed they would have at a reference BPM (SV2 = SV1 x BPM1 / BPM2), for the selected sliders or every slider. One-shot: using it twice corrects twice. Velocities outside 0.1 to 10 are left alone.
- **SV hotkeys** (selected sliders): [ and ] lower or raise slider velocity by 0.1 (Ctrl 0.25, Alt 0.01), \ copies the velocity of the slider before the selection. In lazer the velocity belongs to the slider, so this edits the sliders instead of inserting green lines. One undo step.
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
How things look and feel: the overlay in another skin, select-all on a big map, tools tab cards and panels, stream organiser on real streams, Hitsounds tab (playback through your skin, scrolling and zoom), the YAWNS logo on the main menu.
