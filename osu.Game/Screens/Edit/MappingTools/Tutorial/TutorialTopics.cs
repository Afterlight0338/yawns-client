// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.MappingTools.Tutorial
{
    /// <summary>
    /// YAWNS: what the tutorial tab explains.
    /// </summary>
    /// <param name="Category">The heading it sits under in the list.</param>
    /// <param name="Name">The feature's name.</param>
    /// <param name="What">What it does and why you would want it.</param>
    /// <param name="Steps">How to use it, one line each.</param>
    /// <param name="Where">Where to find it and its hotkeys.</param>
    /// <param name="Draw">Draws the diagram.</param>
    public record TutorialTopic(string Category, string Name, string What, string[] Steps, string Where, Action<DiagramCanvas> Draw);

    public static class TutorialTopics
    {
        private const string compose_menu = "Compose screen: right-click a selection, Tools. Also a button in the right toolbox, \"mapping tools\" group.";

        public static readonly TutorialTopic[] ALL =
        {
            // Symmetry
            new TutorialTopic("Symmetry", "Radial copy",
                "Makes rotated copies of a pattern around a centre, the way mappers build circles, stars and spirals out of one kick or one slider. The 360/n rule: with n divisions each copy turns 360/n degrees (3 divisions is 120°, 5 is 72°).",
                new[]
                {
                    "Select the objects you want to copy.",
                    "Open Radial copy. The copies appear live on the playfield.",
                    "Divisions: how many steps make a full circle. Star step 2 with 5 divisions draws a pentagram.",
                    "Copies: fewer gives part of a circle, more keeps going. Scale per copy below or above 1 makes a spiral.",
                    "\"Mirror every other copy\" makes zigzags. \"Rotate around\" picks the centre: the playfield, the selection or the radial guide.",
                    "Keep (or close) adds them as one undo step. Cancel removes them.",
                },
                compose_menu + " Choose \"Radial copy (360/n, stars, spirals)...\".", TutorialDiagrams.RadialCopy),

            new TutorialTopic("Symmetry", "Angled flip",
                "Mirrors the selected objects across a line at any angle, like a mirror held at that angle. 90° flips left to right, 0° top to bottom, 45° swaps x and y. The same objects are moved, nothing new is added (unless you tick Keep original).",
                new[]
                {
                    "Select the objects, then press Ctrl+Shift+T (or use the menu).",
                    "Drag the line angle. The yellow line shows the mirror, the objects follow it live.",
                    "Anticlockwise reverses which way the angle is measured. \"Line goes through\" picks the playfield centre, the selection centre or the radial guide.",
                    "Tick Keep original to add a mirrored copy a set number of beats later instead of flipping.",
                    "Keep (or close) applies it as one undo step. Cancel puts everything back.",
                },
                "Ctrl+Shift+T in the editor (rebindable). Or right-click a selection, Tools, \"Angled flip...\", or the right toolbox, mapping tools group.", TutorialDiagrams.AngledFlip),

            new TutorialTopic("Symmetry", "Radial guide",
                "A drawing aid: spokes and rings around a centre you can drag. Placing and dragging objects snaps to the crossings, the spokes and the rings, so symmetrical patterns are exact without maths. Radial copy and Angled flip can also use its centre.",
                new[]
                {
                    "Click \"Radial guide\" in the mapping tools group. The guide appears.",
                    "Spokes: the full circle in this many steps (6 is every 60°). First spoke angle turns them.",
                    "Rings and ring spacing set the circles.",
                    "Drag the dot in the middle to move the centre. It does not select or move other objects.",
                    "Place or drag objects near a crossing, spoke or ring and they snap.",
                },
                "Right toolbox, mapping tools, \"Radial guide\". Toggle Show guide in its popover.", TutorialDiagrams.RadialGuide),

            new TutorialTopic("Symmetry", "Perfect it: polygon",
                "Fixes objects you have already placed by hand so a shape is exact. It moves them, it does not add any (the polygon generator makes new objects instead). Your objects, in time order, become the corners of a regular polygon or star.",
                new[]
                {
                    "Place the objects roughly where the corners should be, in time order.",
                    "Select them and open Perfect it, mode \"Regular polygon or star\".",
                    "Divisions: how many corners the full shape has (the number you selected is used by default). Fewer objects fit part of the shape.",
                    "Star step: 1 goes round, 2 with 5 divisions is a pentagram.",
                    "The popover says how far the objects moved. Keep applies it, Cancel puts them back.",
                },
                compose_menu + " Choose \"Perfect it (polygon, symmetry, mirror)...\".", TutorialDiagrams.PerfectPolygon),

            new TutorialTopic("Symmetry", "Perfect it: rotational",
                "Cleans up a radial pattern. The objects are split into equal groups, and every group becomes the first group turned by 360 / folds degrees around the centre where the groups balance.",
                new[]
                {
                    "Select all the objects of the pattern, in order: group one first, then group two, and so on.",
                    "Open Perfect it, mode \"Rotational symmetry\".",
                    "Folds: how many groups. The object count must divide evenly by it, or the popover tells you.",
                },
                compose_menu, TutorialDiagrams.PerfectRotational),

            new TutorialTopic("Symmetry", "Perfect it: mirror pair",
                "Makes the second half of the selection the exact mirror image of the first half. The mirror line is fitted from the pairs (point 1 with point N/2+1, and so on).",
                new[]
                {
                    "Select an even number of objects: the first half, then its roughly mirrored second half.",
                    "Open Perfect it, mode \"Mirror pair\".",
                    "Both halves are averaged onto one exact mirror line.",
                },
                compose_menu, TutorialDiagrams.PerfectMirror),

            // Transform
            new TutorialTopic("Transform", "Quick rotate",
                "Rotate the selection by a fixed step with one key press, no dialog. Rotate and scale now remember your last origin choice and default to the selection centre.",
                new[]
                {
                    "Select objects. Press Ctrl+Alt+. to turn clockwise, Ctrl+Alt+, to turn anticlockwise.",
                    "The step is 60° by default. Change it in the Quick rotate popover (decimals are fine).",
                    "Ctrl+Shift+Scroll rotates live: 5° per notch, add Alt for 1° per notch.",
                },
                "Right toolbox, \"Quick rotate\" (step and buttons). Hotkeys can be rebound in the editor key settings.", TutorialDiagrams.QuickRotate),

            new TutorialTopic("Transform", "Scale options",
                "When you scale a group that contains sliders, you choose what happens to the sliders. Lazer only moves their heads; now they can scale with the layout, and optionally keep their duration by adjusting velocity.",
                new[]
                {
                    "Select circles and sliders and open the Scale popover (Ctrl+E by default).",
                    "\"Sliders in a group\": Keep shapes (heads only), Scale shapes, or Scale shapes and adjust SV (duration stays).",
                    "X-axis and Y-axis boxes scale in one direction only. The range is wider, and negative values flip within the playfield.",
                },
                "Compose screen, the Scale popover (right toolbox, transform).", TutorialDiagrams.ScaleOptions),

            new TutorialTopic("Transform", "Randomise sliders",
                "Adds natural variation: turn each slider by a random angle around its head, or jitter its anchors. Lengths stay. Anything that would not fit on the playfield after a few tries is left alone.",
                new[]
                {
                    "Select the sliders, or select none to affect every slider in the map.",
                    "Open \"Randomise sliders\". Set how far to rotate or how many pixels to jitter.",
                    "Press the button. Press it again for a new roll. Ctrl+Z undoes one press.",
                },
                "Right toolbox, mapping tools, \"Randomise sliders\".", TutorialDiagrams.RandomiseSliders),

            new TutorialTopic("Transform", "Axis guide",
                "Mappers tilt straight sliders a few degrees off the playfield's axes. Axis guide finds your map's tilt and draws its four base axes through the selection, so you can see whether patterns are on axis. It says how far off a line is, and Align to axis rotates the selection onto the nearest axis.",
                new[]
                {
                    "Turn on \"Axis guide\" in the mapping tools group.",
                    "Select objects. The four axes appear. Green is on axis, orange is off axis.",
                    "Right-click, Tools, \"Align to axis\" rotates the selection onto the nearest base axis.",
                },
                "Right toolbox, mapping tools, \"Axis guide\". Right-click, Tools, \"Align to axis\".", TutorialDiagrams.AxisGuide),

            new TutorialTopic("Transform", "Axis drag",
                "Hold Ctrl+Alt while dragging objects and the drag stays on the nearest of the map's four base axes, so placement keeps the tilt your sliders have.",
                new[]
                {
                    "Start dragging a selection.",
                    "Hold Ctrl+Alt. The object slides only along the nearest axis (see Axis guide for what they are).",
                    "Alt also toggles distance snap while held, as in lazer.",
                },
                "Compose screen, while dragging objects.", TutorialDiagrams.AxisDrag),

            // Sliders
            new TutorialTopic("Sliders", "Increase Bezier degree",
                "Adds one control point to a Bezier slider without changing its curve or length. More control points means more handles to shape it with later, the usual way to add detail to a curve you are happy with.",
                new[]
                {
                    "Select the slider and click one of its control points.",
                    "Press I (or right-click the point, \"Increase Bezier degree\").",
                    "The curve looks identical. There is now one more point, selected.",
                    "A perfect-curve segment is first turned into a Bezier, which does change its shape.",
                },
                "Compose screen, with a Bezier slider's control point selected: I.", TutorialDiagrams.BezierDegree),

            new TutorialTopic("Sliders", "Direct curve editing",
                "Drag the curve itself instead of one anchor. The point of the curve under your cursor follows the mouse, and every control point moves in proportion to how much it influences that point. Good for gently bending a curve.",
                new[]
                {
                    "Select a single Bezier slider.",
                    "Hold Alt and press the left button on the curve, then drag.",
                    "The head stays in place. Release to finish, Ctrl+Z undoes it.",
                    "Middle click is lazer's quick delete (it removes the anchor under the cursor), so it is not used here.",
                },
                "Compose screen: Alt + left-drag on the curve of the selected Bezier slider.", TutorialDiagrams.DirectCurve),

            new TutorialTopic("Sliders", "Point preview and insert",
                "Shows what adding an anchor would do to the curve before you add it, so you do not have to add, undo and try again.",
                new[]
                {
                    "Select a single Bezier slider.",
                    "Hold Ctrl and move the cursor. The yellow curve is the slider with a point at the cursor.",
                    "Ctrl+click adds the point. Ctrl+Shift+click adds it at whichever end of the slider is nearer instead of along it.",
                },
                "Compose screen: hold Ctrl over a selected single-segment Bezier slider.", TutorialDiagrams.PointPreview),

            new TutorialTopic("Sliders", "True slider end",
                "A selected slider shows a white ring where its scoring ends: the legacy last tick, 36 ms before the visual end (or half the slider if it is shorter). Useful for placing the next object and judging how close a slider really ends.",
                new[]
                {
                    "Select a slider.",
                    "The ring is drawn automatically.",
                },
                "Compose screen, any selected slider.", TutorialDiagrams.TrueEnd),

            new TutorialTopic("Sliders", "Slider Completionator",
                "Sets selected sliders to a length of time (a number of beats) or to end exactly at the playhead, and works out the length and velocity for you, or keeps a velocity and works out the length. Can scale the control points too.",
                new[]
                {
                    "Select sliders. Move the playhead if you want them to end there.",
                    "Open \"Complete sliders...\" and pick beats or end at playhead, and which of length or velocity is fixed.",
                    "Apply. It is one undo step.",
                },
                "Right-click a selection, Tools, \"Complete sliders...\", or the right toolbox.", TutorialDiagrams.Completionator),

            new TutorialTopic("Sliders", "Sliderator",
                "Variable-speed sliders: the slider ball can start slow and speed up, or the other way round, using easing curves. The shape stays, the speed over time changes.",
                new[]
                {
                    "Select sliders (no repeats).",
                    "Open \"Sliderate (variable speed)...\" and choose the easing (\"In\" starts slow, \"Out\" ends slow).",
                    "Apply.",
                },
                "Right-click a selection, Tools, \"Sliderate (variable speed)...\".", TutorialDiagrams.Sliderator),

            new TutorialTopic("Sliders", "Tumour Generator",
                "Grows bumps along a slider: triangles, squares, circles or parabolas. The duration stays the same by raising slider velocity to cover the longer path.",
                new[]
                {
                    "Select sliders.",
                    "Open Tumours and pick the shape, side, size and spacing, and which part of the slider.",
                    "Press the button.",
                },
                "Right toolbox, mapping tools, \"Tumours\".", TutorialDiagrams.Tumours),

            // Streams and SV
            new TutorialTopic("Streams and SV", "Stream organiser",
                "Cleans up a hand-placed stream of circles. They keep their times and the first position, and are moved onto a clean curve with even, accelerating, decelerating or volume-following spacing.",
                new[]
                {
                    "Select three or more circles of a stream.",
                    "Open \"Organise stream...\". Shape: clean curve (your overall shape without the wobble), arc or straight line.",
                    "Spacing: fit between the ends, or follow distance snap. Speed: even, accelerate or decelerate, with a strength.",
                    "Speed \"Follow volume\": louder parts get wider spacing and quieter parts tighter. \"Volume from\" picks the song's loudness at each object or the hitsound volume you set (green lines). Strength is how much wider the loudest gap is than the quietest.",
                    "Keep (or close) applies it as one undo step.",
                },
                "Right-click a selection, Tools, \"Organise stream...\".", TutorialDiagrams.StreamOrganiser),

            new TutorialTopic("Streams and SV", "Wiggle",
                "A stream option: every other object moves a set number of pixels to alternating sides of the stream line, for wiggle streams.",
                new[]
                {
                    "Open the stream organiser.",
                    "Raise \"Wiggle (px)\" above 0. 0 is off.",
                },
                "In the stream organiser popover.", TutorialDiagrams.Wiggle),

            new TutorialTopic("Streams and SV", "SV hotkeys",
                "Change slider velocity from the keyboard. In lazer the velocity belongs to each slider, so these edit the selected sliders (there are no green lines to insert).",
                new[]
                {
                    "Select sliders.",
                    "] raises and [ lowers velocity by 0.1. Hold Ctrl for 0.25 steps or Alt for 0.01 steps.",
                    "\\ copies the velocity of the slider before the selection.",
                },
                "Compose screen with sliders selected.", TutorialDiagrams.SvHotkeys),

            new TutorialTopic("Streams and SV", "SV equaliser",
                "After a BPM change the same velocity multiplier is a different speed on screen. The equaliser sets slider velocities so they keep the speed they would have at a reference BPM: SV2 = SV1 x BPM1 / BPM2. Useful for lowdiffs with tempo changes.",
                new[]
                {
                    "Select sliders, or select none to include every slider.",
                    "Open \"SV equaliser\". The reference BPM starts as the BPM at the first slider.",
                    "Press the button once. It changes velocities each time it is used, so do not repeat it. Ctrl+Z undoes it.",
                    "Sliders that would need a velocity outside 0.1 to 10 are left alone and counted.",
                },
                "Right toolbox, mapping tools, \"SV equaliser\".", TutorialDiagrams.SvEqualiser),

            // Snapping
            new TutorialTopic("Snapping", "Snapping Tools",
                "Virtual guides drawn from the objects near the current time: lines through pairs, circles around points, midpoints and crossings. New and dragged objects snap to them, which makes geometric patterns (equilateral triangles, squares, bisectors) exact. Also snaps to the centres of perfect-curve sliders.",
                new[]
                {
                    "Turn on \"Snapping tools\" in the mapping tools group.",
                    "Move the playhead near the objects you are building from. The guides follow it.",
                    "Place or drag an object close to a crossing or point. It snaps.",
                },
                "Right toolbox, mapping tools, \"Snapping tools\".", TutorialDiagrams.SnappingTools),

            new TutorialTopic("Snapping", "Distance snap at the edge",
                "When a distance-snapped object would land off the playfield, it now goes to the nearest place on the playfield edge instead of silently not snapping at all.",
                new[]
                {
                    "Turn distance snap on and place objects near the edge of the playfield.",
                    "A spot that would be off screen is clamped to the edge.",
                },
                "Automatic whenever distance snap is on.", TutorialDiagrams.DistanceEdge),

            new TutorialTopic("Snapping", "Continue distance snap",
                "Sets the distance spacing so it continues the gap between the last two objects, so you can carry a spacing you used earlier forward without adjusting the slider.",
                new[]
                {
                    "Bind the key in the editor key settings (\"Continue the distance between the last two objects\"). It is unbound by default.",
                    "Select an object (or put the playhead after it) and press the key.",
                    "The spacing multiplier is set from that object and the one before it, and distance snap turns on.",
                },
                "Editor key settings, unbound by default.", TutorialDiagrams.ContinueDistance),

            // Overlay
            new TutorialTopic("Overlay", "Map overlay and rhythm lanes",
                "Draws another map under the one you are editing, with an opacity and an offset, and puts two rhythm lanes on the timeline: the overlay's and yours. Unmatched hitsounds are marked red. You can copy, insert or overlay just a pattern from a selected range.",
                new[]
                {
                    "Compose screen, the \"overlay\" section of the left toolbox: pick a map.",
                    "Set the opacity and offset to line it up. Pick an overlay skin in the Map panel so the overlay looks different from your objects.",
                    "On the timeline's two lanes, drag to select a range of the overlay, then copy, insert, or overlay the pattern at the current time.",
                    "The Hitsound Copier and Timing Copier in the Tools tab work from the overlay map.",
                },
                "Compose screen, left toolbox, \"overlay\".", TutorialDiagrams.OverlayLanes),

            // Tools tab
            new TutorialTopic("Tools tab", "Hitsound Copier",
                "Copies hitsounds from the overlay map (or from the overlaid pattern) onto this difficulty.",
                new[] { "Pick an overlay map first.", "Open the Tools tab, Hitsound Copier.", "Choose what to copy and press the button." },
                "Tools tab (top right), Hitsound Copier.", TutorialDiagrams.Flow("overlay map hitsounds", "this difficulty", "matched by time, one undo step")),

            new TutorialTopic("Tools tab", "Timing Copier",
                "Replaces this difficulty's timing with the overlay map's, optionally moving or snapping objects to it.",
                new[] { "Pick an overlay map first.", "Open Timing Copier.", "Choose whether objects move with the timing or get snapped, then apply." },
                "Tools tab, Timing Copier.", TutorialDiagrams.Flow("overlay map timing", "this difficulty", "red and green lines copied over")),

            new TutorialTopic("Tools tab", "Property Transformer",
                "Multiplies and adds to timing, slider velocity, hitsound volume and index, and the times of objects, bookmarks and breaks, all at once, for example to make a whole difficulty 1.5 times faster.",
                new[] { "Open Property Transformer.", "Set a multiplier and an add for each property you want to change.", "Apply. It is one undo step." },
                "Tools tab, Property Transformer.", TutorialDiagrams.PropertyTransformer),

            new TutorialTopic("Tools tab", "Rhythm Guide",
                "Adds circles in the middle of the playfield at the rhythm of other difficulties (or only their hitsounds), snapped to 1/16 or 1/12, wherever this difficulty has nothing yet. A fast way to get a rhythm skeleton for a new difficulty.",
                new[] { "Open Rhythm Guide.", "Pick the difficulties to follow and the snapping.", "Apply, then place the circles where you want them." },
                "Tools tab, Rhythm Guide.", TutorialDiagrams.Flow("rhythm of other difficulties", "circles in the middle", "only where yours is empty")),

            new TutorialTopic("Tools tab", "Map Cleaner",
                "Resnaps objects and bookmarks, removes or adds muting, and deletes hitsound files nobody plays. Lazer already writes clean green lines on save, so that part of Mapping Tools' cleaner is not needed.",
                new[] { "Open Map Cleaner.", "Tick what to clean.", "Apply. It is one undo step." },
                "Tools tab, Map Cleaner.", TutorialDiagrams.Flow("messy difficulty", "clean difficulty", "snapped, tidy hitsound files", DiagramCanvas.RESULT)),

            new TutorialTopic("Tools tab", "Mapset Merger",
                "Brings a difficulty of another set in your library (a guest difficulty, say) into this set. Its custom hitsound indices move past the ones this set uses and the files come along. It takes this set's metadata, audio and background.",
                new[] { "Open Mapset Merger.", "Pick the difficulty from your library.", "Merge." },
                "Tools tab, Mapset Merger.", TutorialDiagrams.Flow("difficulty from another set", "this set", "hitsound files move along")),

            new TutorialTopic("Tools tab", "Combo Colour Studio",
                "Colour haxing: colour points work like timing points for combo colours. From a point on, combos cycle through its colours; a burst point colours just the one short combo it sits on. It can also read the points back from a map.",
                new[] { "Open Combo Colour Studio.", "Add colour points at times and give each its colours.", "Apply. Points are saved per difficulty." },
                "Tools tab, Combo Colour Studio.", TutorialDiagrams.ColourStudio),

            new TutorialTopic("Tools tab", "Timing Helper",
                "Put markers (objects, bookmarks) exactly on the sounds, then it changes BPMs and adds red lines so every marker is snapped, preferring round BPMs.",
                new[] { "Place a marker on each sound you hear.", "Open Timing Helper and run it. Auto beat counts need timing that is already close, or set beats between markers.", "Check the result. It is one undo step." },
                "Tools tab, Timing Helper.", TutorialDiagrams.TimingHelper),

            new TutorialTopic("Tools tab", "Pattern Gallery",
                "Saved patterns, ready to insert at the current time. They fit this map's BPM by beats. Each is a .osupattern file (plain .osu inside) in ~/.local/share/osu/patterns that you can share.",
                new[] { "Select objects and right-click, Tools, \"Save as pattern...\".", "Open Pattern Gallery in the Tools tab.", "Click a card to insert it at the current time." },
                "Tools tab, Pattern Gallery. Save with right-click, Tools, \"Save as pattern...\".", TutorialDiagrams.Flow("selection", "pattern card", "insert anywhere, fitted to the BPM", DiagramCanvas.AXIS)),

            // Other
            new TutorialTopic("Other", "Hitsounds tab",
                "Hitsound Studio inside the editor: lanes of hitsounds on a timeline, each lane one sample (sample set, addition, custom index, volume). Lanes and notes are saved as a project next to the difficulty; Export writes them into the hitsound difficulty the way Mapping Tools does: one circle per moment, with new custom indices and mixed samples where lanes overlap, so nothing is lost. The Copier puts them on the other difficulties.",
                new[]
                {
                    "Open the Hitsounds tab on a hitsound difficulty (a gameplay difficulty is read-only, with a button to create a hitsound one).",
                    "Click places a note, dragging on empty space selects, dragging a note moves the selection (also to another lane), Ctrl+drag paints, right-click deletes, right-drag erases.",
                    "W, E, R put a whistle, finish or clap on the selected moments. C and V copy and paste at the playhead, X or Delete deletes, 1 to 6 change the snap, G hides the ghost notes, Ctrl+Z undoes.",
                    "Pick a difficulty in the lane rack to see it as ghost notes (in Hitsounds mode its hitsounds become the lanes). Drop an audio file on a lane to use it as that lane's sample.",
                    "Export (also done when you save), then Copier, tick the difficulties, Copy and save.",
                },
                "Hitsounds tab (top right).", TutorialDiagrams.HitsoundLanes),

            new TutorialTopic("Other", "New version check",
                "The only thing YAWNS does online: when the game starts it asks GitHub for the latest YAWNS release and shows a notification with a link when there is a newer one. Nothing is downloaded or installed.",
                new[] { "Nothing to do: it runs on start.", "Settings, General, YAWNS version: turn it off or check now." },
                "Settings, General, YAWNS version.", TutorialDiagrams.Flow("game starts", "GitHub: latest release", "notification with a link if newer")),

            new TutorialTopic("Other", "Map backups",
                "Every save also writes a copy of the difficulty to a backups folder, newest 30 per difficulty kept, so a bad edit or a crash never costs more than the last save.",
                new[] { "Nothing to do: it happens on every save.", "File menu, \"Open backups folder\" to find them." },
                "~/.local/share/osu/backups/<set>/", TutorialDiagrams.Backups),

            new TutorialTopic("Other", "Verify checks",
                "New checks in the Verify tab, most with a fix button: uneven streams (Organise), straight sliders off the map's axes (Align), unsnapped objects (Resnap), muted clickable objects (Unmute), unused hitsound files (Delete), hitsounds differing from the hitsound difficulty (Copy), and an AutoFail detector for osu!stable 2B loading.",
                new[] { "Open the Verify tab.", "Click an issue to jump to it. Where there is a fix button, press it." },
                "Verify tab.", TutorialDiagrams.Verify),
        };
    }
}
