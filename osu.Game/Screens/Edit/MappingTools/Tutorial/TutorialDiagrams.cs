// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;
using osuTK.Graphics;
using static osu.Game.Screens.Edit.MappingTools.Tutorial.DiagramCanvas;

namespace osu.Game.Screens.Edit.MappingTools.Tutorial
{
    /// <summary>
    /// YAWNS: the diagrams of the tutorial tab, one per feature. Blue is what you have, green what you get, yellow a guide, red a rough or unwanted thing.
    /// </summary>
    public static class TutorialDiagrams
    {
        private static readonly Vector2 centre = new Vector2(256, 192);

        private static Vector2[] shift(IEnumerable<Vector2> points, Vector2 offset, float scale = 1) => points.Select(p => p * scale + offset).ToArray();

        public static void RadialCopy(DiagramCanvas c)
        {
            c.Ring(centre, 120, FAINT);

            for (int i = 0; i < 3; i++)
                c.Line(centre, Polar(centre, 120, -90 + i * 120), FAINT, 1.5f);

            c.Dot(centre, GUIDE, 12);
            c.Label("centre", centre + new Vector2(0, 20), 14, GUIDE);

            c.Circle(Polar(centre, 120, -90), ORIGINAL, 24, "1");
            c.Circle(Polar(centre, 120, 30), RESULT, 24, "2");
            c.Circle(Polar(centre, 120, 150), RESULT, 24, "3");

            c.Label("original", Polar(centre, 120, -90) + new Vector2(60, -4), 14, ORIGINAL);
            c.Label("120°", Polar(centre, 48, -30), 18, GUIDE);
            c.Label("360 / 3", Polar(centre, 48, 90), 18, GUIDE);
            c.Label("copies turn by 360 / divisions", new Vector2(256, 362), 16);
        }

        public static void AngledFlip(DiagramCanvas c)
        {
            Vector2 a = Polar(centre, 280, 45), b = Polar(centre, 280, 225);
            c.Line(a, b, GUIDE, 3);
            c.Label("mirror line 45°", new Vector2(420, 330), 15, GUIDE);

            var originals = new[] { new Vector2(120, 90), new Vector2(210, 60), new Vector2(240, 150) };

            for (int i = 0; i < originals.Length; i++)
            {
                Vector2 flipped = Mirror(originals[i], centre, 45);
                c.Dashed(originals[i], flipped, FAINT, 2);
                c.Circle(flipped, RESULT, 22, (i + 1).ToString());
                c.Circle(originals[i], ORIGINAL, 22, (i + 1).ToString());
            }

            c.Label("before", new Vector2(80, 130), 15, ORIGINAL);
            c.Label("after (same objects, moved)", new Vector2(400, 150), 15, RESULT);
        }

        public static void RadialGuide(DiagramCanvas c)
        {
            for (int i = 0; i < 6; i++)
                c.Line(centre, Polar(centre, 230, i * 60), new Color4(70, 220, 240, 110), 1.5f);

            for (int r = 1; r <= 3; r++)
                c.Ring(centre, r * 60, new Color4(70, 220, 240, 110), 1.5f);

            c.Dot(centre, AXIS, 16);
            c.Label("drag me", centre + new Vector2(0, 24), 14, AXIS);

            Vector2 snap = Polar(centre, 120, 60);
            c.Circle(snap, RESULT, 20, "1");
            c.Label("snaps to the crossing", snap + new Vector2(0, 38), 14, RESULT);

            Vector2 cursor = snap + new Vector2(26, -22);
            c.Arrow(cursor + new Vector2(40, -30), cursor, TEXT, 2);
            c.Label("6 spokes, 3 rings", new Vector2(400, 360), 15);
        }

        private static readonly Vector2[] rough_hexagon =
        {
            new Vector2(8, -6), new Vector2(-10, 4), new Vector2(7, 11), new Vector2(-6, -8), new Vector2(11, 5), new Vector2(-9, 7),
        };

        public static void PerfectPolygon(DiagramCanvas c)
        {
            Vector2 left = new Vector2(130, 192), right = new Vector2(382, 192);

            for (int i = 0; i < 6; i++)
            {
                Vector2 perfect = Polar(right, 90, i * 60 - 90);
                c.Circle(Polar(left, 90, i * 60 - 90) + rough_hexagon[i], ROUGH, 18, (i + 1).ToString());
                c.Circle(perfect, RESULT, 18, (i + 1).ToString());
            }

            for (int i = 0; i < 6; i++)
                c.Line(Polar(right, 90, i * 60 - 90), Polar(right, 90, (i + 1) * 60 - 90), new Color4(90, 230, 140, 120), 2);

            c.Arrow(new Vector2(215, 192), new Vector2(255, 192), TEXT, 3);
            c.Label("placed by hand", new Vector2(130, 320), 15, ROUGH);
            c.Label("exact hexagon", new Vector2(382, 320), 15, RESULT);
        }

        public static void PerfectRotational(DiagramCanvas c)
        {
            // Three groups of two objects around a centre: rough on the left half of each pair, the exact pattern on top.
            c.Dot(centre, GUIDE, 12);

            var jitter = new[] { new Vector2(10, -8), new Vector2(-12, 6), new Vector2(7, 10), new Vector2(-9, -9), new Vector2(11, 5), new Vector2(-6, -11) };

            for (int g = 0; g < 3; g++)
            {
                Vector2 inner = Polar(centre, 60, -90 + g * 120 + 10), outer = Polar(centre, 130, -90 + g * 120 + 35);
                c.Circle(inner + jitter[g * 2], ROUGH, 15, null, 0.7f);
                c.Circle(outer + jitter[g * 2 + 1], ROUGH, 15, null, 0.7f);
                c.Circle(inner, RESULT, 15, (g * 2 + 1).ToString());
                c.Circle(outer, RESULT, 15, (g * 2 + 2).ToString());
                c.Line(centre, Polar(centre, 175, -90 + g * 120), FAINT, 1.5f);
            }

            c.Label("3 folds: each group is the first turned 120°", new Vector2(256, 366), 15);
            c.Label("red: placed by hand", new Vector2(90, 24), 14, ROUGH);
            c.Label("green: exact", new Vector2(90, 46), 14, RESULT);
        }

        public static void PerfectMirror(DiagramCanvas c)
        {
            c.Dashed(new Vector2(256, 20), new Vector2(256, 364), GUIDE, 3, 10);
            c.Label("fitted mirror line", new Vector2(256, 372), 14, GUIDE);

            var left = new[] { new Vector2(150, 90), new Vector2(110, 190), new Vector2(170, 290) };
            var roughRight = new[] { new Vector2(372, 96), new Vector2(414, 184), new Vector2(344, 300) };

            for (int i = 0; i < 3; i++)
            {
                Vector2 exact = new Vector2(512 - left[i].X, left[i].Y);
                c.Circle(left[i], ORIGINAL, 20, (i + 1).ToString());
                c.Circle(roughRight[i], ROUGH, 20, (i + 4).ToString(), 0.7f);
                c.Circle(exact, RESULT, 20, (i + 4).ToString());
                c.Arrow(roughRight[i] + (exact - roughRight[i]).Normalized() * 24, exact - (exact - roughRight[i]).Normalized() * 24, TEXT, 2);
            }

            c.Label("first half", new Vector2(130, 24), 15, ORIGINAL);
            c.Label("second half becomes its mirror", new Vector2(392, 24), 15, RESULT);
        }

        public static void QuickRotate(DiagramCanvas c)
        {
            Vector2 pivot = new Vector2(256, 200);
            var group = new[] { new Vector2(150, 120), new Vector2(190, 70), new Vector2(240, 100) };

            c.Dot(pivot, GUIDE, 12);
            c.Label("selection centre", pivot + new Vector2(0, 22), 14, GUIDE);

            for (int i = 0; i < group.Length; i++)
            {
                Vector2 turned = pivot + Symmetry.Linear.Rotation(60).Apply(group[i] - pivot);
                c.Circle(turned, RESULT, 20, (i + 1).ToString());
                c.Circle(group[i], ORIGINAL, 20, (i + 1).ToString(), 0.7f);
            }

            var arc = Enumerable.Range(0, 13).Select(i => Polar(pivot, 120, 230 + i * 5)).ToArray();
            c.Polyline(arc, GUIDE, 3);
            c.Arrow(arc[^2], arc[^1], GUIDE, 3);
            c.Label("60°", Polar(pivot, 142, 262), 18, GUIDE);
            c.Label("Ctrl+Alt+.  clockwise", new Vector2(380, 320), 15);
            c.Label("Ctrl+Alt+,  anticlockwise", new Vector2(380, 345), 15);
        }

        public static void ScaleOptions(DiagramCanvas c)
        {
            c.Line(new Vector2(256, 10), new Vector2(256, 374), FAINT, 2);
            c.Label("keep shapes", new Vector2(128, 24), 16, ORIGINAL);
            c.Label("scale shapes", new Vector2(384, 24), 16, RESULT);

            for (int side = 0; side < 2; side++)
            {
                Vector2 origin = new Vector2(30 + side * 256, 340);
                var circle = new Vector2(40, -60);
                var originalPoints = new[] { origin + new Vector2(45, -110), origin + new Vector2(85, -150), origin + new Vector2(105, -185), origin + new Vector2(140, -165) };

                c.Slider(originalPoints.Skip(1).Prepend(originalPoints[0]).ToArray(), ORIGINAL, 10, 0.4f);
                c.Circle(origin + circle, ORIGINAL, 12, null, 0.4f);

                // The scaled group (x1.5 from the corner): heads always move, the body only scales in the right panel.
                var heads = originalPoints.Select(p => origin + (p - origin) * 1.5f).ToArray();
                var path = side == 0 ? originalPoints.Select(p => p - originalPoints[0] + heads[0]).ToArray() : heads;
                c.Slider(path, RESULT, side == 0 ? 10 : 14);
                c.Circle(origin + circle * 1.5f, RESULT, 16);
            }

            c.Label("heads move, bodies keep size", new Vector2(128, 362), 13);
            c.Label("bodies grow, SV keeps timing", new Vector2(384, 362), 13);
        }

        public static void RandomiseSliders(DiagramCanvas c)
        {
            var angles = new[] { -25, 40, -10, 70, -55 };

            for (int i = 0; i < 5; i++)
            {
                Vector2 head = new Vector2(70 + i * 90, 90);
                c.Slider(new[] { head, head + new Vector2(0, 60) }, ORIGINAL, 8, 0.35f);
                Vector2 end = head + Symmetry.Linear.Rotation(angles[i]).Apply(new Vector2(0, 60));
                c.Slider(new[] { head, end }, RESULT, 8);
            }

            c.Label("each slider turns around its head by a random angle", new Vector2(256, 190), 15);

            var curve = new[] { new Vector2(70, 330), new Vector2(150, 250), new Vector2(230, 350), new Vector2(300, 270) };
            var jittered = new[] { curve[0], new Vector2(160, 266), new Vector2(214, 330), curve[3] + new Vector2(10, 14) };
            c.Curve(curve, FAINT, 3);
            c.Curve(jittered, RESULT, 4);
            c.Label("or jitter the anchors", new Vector2(410, 300), 15);
        }

        public static void AxisGuide(DiagramCanvas c)
        {
            foreach (double angle in new[] { 10.0, -10.0, 100.0, 80.0 })
                c.Line(Polar(centre, 260, angle), Polar(centre, 260, angle + 180), new Color4(70, 220, 240, 120), 1.5f);

            c.Label("the map's four base axes (tilt 10°)", new Vector2(256, 366), 15, AXIS);

            Vector2 a = Polar(centre, 170, 10), b = Polar(centre, 170, 190);
            c.Circle(a, ORIGINAL, 20, "1");
            c.Circle(b, ORIGINAL, 20, "2");
            c.Label("on axis", centre + new Vector2(0, -26), 15, RESULT);
        }

        public static void AxisDrag(DiagramCanvas c)
        {
            foreach (double angle in new[] { 10.0, -10.0 })
                c.Line(Polar(centre, 280, angle), Polar(centre, 280, angle + 180), new Color4(70, 220, 240, 110), 1.5f);

            Vector2 start = new Vector2(110, 215), mouse = new Vector2(400, 255);
            Vector2 axisEnd = start + Symmetry.Linear.Rotation(10).Apply(new Vector2(1, 0)) * Vector2.Dot(mouse - start, new Vector2((float)Math.Cos(10 * Math.PI / 180), (float)Math.Sin(10 * Math.PI / 180)));

            c.Dashed(start, mouse, ROUGH, 2);
            c.Dot(mouse, ROUGH, 12);
            c.Label("your hand", mouse + new Vector2(0, 22), 14, ROUGH);
            c.Circle(start, ORIGINAL, 20, "1", 0.6f);
            c.Circle(axisEnd, RESULT, 20, "1");
            c.Label("Ctrl+Alt: stays on the axis", new Vector2(256, 350), 16, RESULT);
        }

        private static readonly Vector2[] cubic = { new Vector2(0, 0), new Vector2(40, -130), new Vector2(140, -130), new Vector2(170, 0) };

        public static void BezierDegree(DiagramCanvas c)
        {
            var left = shift(cubic, new Vector2(40, 270));
            var elevated = BezierTools.ElevateDegree(cubic);
            var right = shift(elevated, new Vector2(300, 270));

            foreach (var (points, tint) in new[] { (left, ORIGINAL), (right, RESULT) })
            {
                c.Curve(points, tint, 5);
                c.Polyline(points, FAINT, 1.5f);

                foreach (var p in points)
                    c.Dot(p, Color4.White, 11);
            }

            c.Label("4 control points", new Vector2(125, 330), 15, ORIGINAL);
            c.Label("5 control points, same curve", new Vector2(385, 330), 15, RESULT);
            c.Arrow(new Vector2(230, 200), new Vector2(285, 200), TEXT, 3);
            c.Label("I", new Vector2(257, 178), 20, GUIDE);
        }

        public static void DirectCurve(DiagramCanvas c)
        {
            var curve = shift(new[] { new Vector2(0, 0), new Vector2(60, -190), new Vector2(240, -190), new Vector2(300, 0) }, new Vector2(110, 300));
            float t = 0.5f;
            Vector2 before = BezierTools.Evaluate(curve, t), after = before + new Vector2(30, 70);
            var offsets = BezierTools.DragOffsets(curve.Length, t, after - before);
            var moved = curve.Select((p, i) => p + offsets[i]).ToArray();

            c.Curve(curve, FAINT, 4);
            c.Curve(moved, RESULT, 5);

            foreach (var p in moved)
                c.Dot(p, Color4.White, 10);

            c.Dot(before, ORIGINAL, 12);
            c.Arrow(before, after - (after - before).Normalized() * 8, GUIDE, 3);
            c.Label("Alt + drag here", before + new Vector2(0, -22), 15, GUIDE);
            c.Label("the curve follows your cursor; the head stays put", new Vector2(256, 366), 15);
        }

        public static void PointPreview(DiagramCanvas c)
        {
            var curve = shift(new[] { new Vector2(0, 0), new Vector2(50, -170), new Vector2(210, -170), new Vector2(270, 0) }, new Vector2(120, 290));
            Vector2 mouse = new Vector2(256, 110);
            var inserted = new[] { curve[0], curve[1], mouse, curve[2], curve[3] };

            c.Curve(curve, ORIGINAL, 5);
            c.Curve(inserted, GUIDE, 4);
            c.Dot(mouse, GUIDE, 14);
            c.Label("Ctrl held: yellow shows the curve with a point here", new Vector2(256, 28), 15, GUIDE);
            c.Label("Ctrl+click adds it, Ctrl+Shift+click adds it at the nearer end instead", new Vector2(256, 366), 14);
        }

        public static void TrueEnd(DiagramCanvas c)
        {
            var path = shift(new[] { new Vector2(0, 0), new Vector2(120, -120), new Vector2(240, 60), new Vector2(330, -40) }, new Vector2(80, 230));
            c.Slider(path, ORIGINAL, 26);

            Vector2 end = path[^1];
            Vector2 tick = BezierTools.Evaluate(path, 0.93f);
            c.Ring(tick, 26, Color4.White, 3);
            c.Label("true end: 36 ms before the visual end", tick + new Vector2(-30, -48), 15, Color4.White);
            c.Label("visual end", end + new Vector2(10, 44), 14, ORIGINAL);
        }

        public static void Completionator(DiagramCanvas c)
        {
            var path = shift(new[] { new Vector2(0, 0), new Vector2(110, -90), new Vector2(230, 40) }, new Vector2(60, 190));
            c.Slider(path, ORIGINAL, 20);
            c.Slider(shift(new[] { new Vector2(0, 0), new Vector2(150, -150), new Vector2(330, 60) }, new Vector2(60, 330)), RESULT, 20);

            c.Label("before: 230 px, any length of time", new Vector2(256, 36), 15, ORIGINAL);
            c.Label("after: length and velocity worked out so it fills", new Vector2(256, 250), 15, RESULT);

            // A tiny timeline: the playhead and where the slider ends.
            c.Line(new Vector2(40, 356), new Vector2(472, 356), FAINT, 2);
            c.Line(new Vector2(390, 346), new Vector2(390, 366), GUIDE, 3);
            c.Label("playhead", new Vector2(390, 336), 12, GUIDE);
        }

        public static void Sliderator(DiagramCanvas c)
        {
            var curve = shift(new[] { new Vector2(0, 0), new Vector2(110, -150), new Vector2(260, -150), new Vector2(380, 0) }, new Vector2(60, 150));
            c.Curve(curve, FAINT, 3);

            for (int i = 0; i <= 10; i++)
            {
                float eased = (float)Math.Pow(i / 10.0, 2);
                c.Dot(BezierTools.Evaluate(curve, eased), ORIGINAL, 10);
            }

            c.Label("ease in: the ball starts slow", new Vector2(256, 24), 15, ORIGINAL);

            var low = shift(curve, new Vector2(0, 170));

            c.Curve(low, FAINT, 3);

            for (int i = 0; i <= 10; i++)
            {
                float eased = 1 - (float)Math.Pow(1 - i / 10.0, 2);
                c.Dot(BezierTools.Evaluate(low, eased), RESULT, 10);
            }

            c.Label("ease out: the ball ends slow (same moments in time)", new Vector2(256, 200), 15, RESULT);
        }

        public static void Tumours(DiagramCanvas c)
        {
            Vector2 a = new Vector2(40, 90), b = new Vector2(470, 90);
            c.Line(a, b, FAINT, 3);

            var triangle = new List<Vector2> { a };

            for (int i = 0; i < 4; i++)
            {
                float x = 70 + i * 100;
                triangle.Add(new Vector2(x, 90));
                triangle.Add(new Vector2(x + 30, 40));
                triangle.Add(new Vector2(x + 60, 90));
            }

            c.Polyline(triangle, RESULT, 4);
            c.Label("triangle", new Vector2(256, 130), 14, RESULT);

            var square = new List<Vector2> { new Vector2(40, 210) };

            for (int i = 0; i < 4; i++)
            {
                float x = 70 + i * 100;
                square.AddRange(new[] { new Vector2(x, 210), new Vector2(x, 165), new Vector2(x + 60, 165), new Vector2(x + 60, 210) });
            }

            c.Line(new Vector2(40, 210), new Vector2(470, 210), FAINT, 3);
            c.Polyline(square, RESULT, 4);
            c.Label("square", new Vector2(256, 250), 14, RESULT);

            var circle = new List<Vector2> { new Vector2(40, 330) };

            for (int i = 0; i < 4; i++)
            {
                for (int s = 0; s <= 12; s++)
                    circle.Add(Polar(new Vector2(100 + i * 100, 330), 30, 180 + s * 15));
            }

            c.Line(new Vector2(40, 330), new Vector2(470, 330), FAINT, 3);
            c.Polyline(circle, RESULT, 4);
            c.Label("circle (and parabola)", new Vector2(256, 366), 14, RESULT);
        }

        public static void StreamOrganiser(DiagramCanvas c)
        {
            var wobbly = new[] { new Vector2(60, 280), new Vector2(105, 270), new Vector2(170, 220), new Vector2(200, 195), new Vector2(262, 170), new Vector2(300, 118), new Vector2(380, 108), new Vector2(430, 70) };

            for (int i = 0; i < wobbly.Length; i++)
                c.Circle(wobbly[i], ROUGH, 15, null, 0.8f);

            c.Label("hand placed: wobbly, uneven gaps", new Vector2(150, 330), 15, ROUGH);

            var clean = Enumerable.Range(0, 8).Select(i => Polar(new Vector2(60, 40), 360, 15 + i * 6.2)).ToArray();

            foreach (var p in clean)
                c.Circle(p, RESULT, 15);

            c.Label("organised: clean curve, even spacing", new Vector2(320, 150), 15, RESULT);
        }

        public static void Wiggle(DiagramCanvas c)
        {
            var straight = Enumerable.Range(0, 9).Select(i => new Vector2(50 + i * 52, 110)).ToArray();

            foreach (var p in straight)
                c.Circle(p, ORIGINAL, 14, null, 0.6f);

            c.Label("a straight stream", new Vector2(256, 60), 15, ORIGINAL);

            var wiggled = new Vector2[straight.Length];

            for (int i = 0; i < straight.Length; i++)
                wiggled[i] = i == 0 ? straight[i] + new Vector2(0, 150) : straight[i] + new Vector2(0, 150 + (i % 2 == 1 ? 28 : -28));

            c.Polyline(wiggled, new Color4(90, 230, 140, 120), 2);

            foreach (var p in wiggled)
                c.Circle(p, RESULT, 14);

            c.Label("wiggle: every other object swaps sides of the line", new Vector2(256, 280), 15, RESULT);
            c.Label("a pixel amount you choose", new Vector2(256, 304), 13);
        }

        public static void SvHotkeys(DiagramCanvas c)
        {
            var rows = new[] { ("1.0x", 400f), ("1.1x", 364f), ("1.35x", 296f) };

            for (int i = 0; i < rows.Length; i++)
            {
                float y = 100 + i * 80;
                c.Slider(new[] { new Vector2(40, y), new Vector2(240, y) }, i == 0 ? ORIGINAL : RESULT, 14);
                c.Box(new Vector2(280, y - 14), new Vector2(rows[i].Item2 * 0.55f, 28), rows[i].Item1, i == 0 ? ORIGINAL : RESULT, 15);
            }

            c.Label("same length, faster slider = shorter in time", new Vector2(256, 30), 15);
            c.Label("]  +0.1      [  -0.1      Ctrl: 0.25      Alt: 0.01", new Vector2(256, 330), 14, GUIDE);
            c.Label("\\  copies the slider before the selection", new Vector2(256, 354), 14, GUIDE);
        }

        public static void SvEqualiser(DiagramCanvas c)
        {
            c.Box(new Vector2(30, 24), new Vector2(210, 30), "120 BPM", ORIGINAL, 15);
            c.Box(new Vector2(250, 24), new Vector2(232, 30), "240 BPM", ROUGH, 15);

            c.Label("same SV 1.0x everywhere:", new Vector2(130, 90), 14);
            c.Box(new Vector2(30, 106), new Vector2(180, 24), "slider", ORIGINAL, 13);
            c.Box(new Vector2(260, 106), new Vector2(90, 24), "twice as fast", ROUGH, 13);

            c.Arrow(new Vector2(256, 160), new Vector2(256, 200), TEXT, 3);

            c.Label("equalised (SV2 = SV1 x BPM1 / BPM2 = 0.5x):", new Vector2(200, 226), 14, RESULT);
            c.Box(new Vector2(30, 244), new Vector2(180, 24), "slider", ORIGINAL, 13);
            c.Box(new Vector2(260, 244), new Vector2(180, 24), "same speed", RESULT, 13);

            c.Label("keeps the on-screen speed across BPM changes", new Vector2(256, 340), 15);
        }

        public static void SnappingTools(DiagramCanvas c)
        {
            Vector2 a = new Vector2(120, 270), b = new Vector2(250, 130), d = new Vector2(380, 250);
            c.Line(Polar(a, 400, -47), Polar(a, -200, -47), new Color4(70, 220, 240, 100), 1.5f);
            c.Line(Polar(b, 400, 42), Polar(b, -200, 42), new Color4(70, 220, 240, 100), 1.5f);
            c.Ring(a, Vector2.Distance(a, b), new Color4(255, 160, 60, 110), 1.5f);
            c.Ring(b, Vector2.Distance(a, b), new Color4(255, 160, 60, 110), 1.5f);

            c.Circle(a, ORIGINAL, 20, "1");
            c.Circle(b, ORIGINAL, 20, "2");
            c.Circle(d, RESULT, 20, "3");
            c.Label("lines, circles and points between objects", new Vector2(256, 28), 15, AXIS);
            c.Label("a new circle snaps to their crossings", new Vector2(256, 366), 15, RESULT);
        }

        public static void DistanceEdge(DiagramCanvas c)
        {
            c.Frame(new Vector2(6, 6), new Vector2(506, 378), new Color4(255, 255, 255, 120), 2);
            Vector2 reference = new Vector2(380, 190);
            c.Circle(reference, ORIGINAL, 22, "1");
            c.Ring(reference, 130, new Color4(255, 160, 60, 140), 2);

            c.Dot(new Vector2(510, 190), ROUGH, 14);
            c.Label("the ring is off the playfield here", new Vector2(380, 300), 14, ROUGH);
            c.Circle(new Vector2(506, 190), RESULT, 20, "2");
            c.Arrow(new Vector2(470, 240), new Vector2(500, 214), TEXT, 2);
            c.Label("now it goes to the nearest edge instead of not snapping", new Vector2(256, 358), 14, RESULT);
        }

        public static void ContinueDistance(DiagramCanvas c)
        {
            Vector2 a = new Vector2(100, 200), b = new Vector2(190, 160), n = new Vector2(280, 120);
            c.Circle(a, ORIGINAL, 20, "1");
            c.Circle(b, ORIGINAL, 20, "2");
            c.Circle(n, RESULT, 20, "3");
            c.Arrow(a + (b - a).Normalized() * 24, b - (b - a).Normalized() * 24, GUIDE, 2);
            c.Arrow(b + (n - b).Normalized() * 24, n - (n - b).Normalized() * 24, GUIDE, 2);
            c.Label("same distance", new Vector2(190, 110), 15, GUIDE);
            c.Label("a key you bind: sets the distance spacing from the last two objects", new Vector2(256, 340), 14);
        }

        public static void OverlayLanes(DiagramCanvas c)
        {
            c.Label("overlay map", new Vector2(70, 100), 15, AXIS);
            c.Label("your map", new Vector2(62, 200), 15, ORIGINAL);
            c.Line(new Vector2(130, 100), new Vector2(490, 100), FAINT, 2);
            c.Line(new Vector2(130, 200), new Vector2(490, 200), FAINT, 2);

            for (int i = 0; i < 9; i++)
            {
                float x = 150 + i * 40;
                c.Dot(new Vector2(x, 100), AXIS, 12);

                if (i != 4 && i != 7)
                    c.Dot(new Vector2(x, 200), ORIGINAL, 12);
                else
                    c.Dot(new Vector2(x, 200), ROUGH, 12);
            }

            c.Label("red: a hitsound in the overlay with no match in yours", new Vector2(256, 260), 15, ROUGH);
            c.Label("select a range of the overlay to copy, insert it or overlay it as a pattern", new Vector2(256, 300), 13);
        }

        /// <summary>
        /// A "from, to" picture for the tools that act on a whole difficulty.
        /// </summary>
        public static Action<DiagramCanvas> Flow(string from, string to, string note, Color4? toColour = null) => c =>
        {
            c.Box(new Vector2(30, 120), new Vector2(180, 90), from, AXIS, 16);
            c.Arrow(new Vector2(215, 165), new Vector2(295, 165), TEXT, 4);
            c.Box(new Vector2(300, 120), new Vector2(182, 90), to, toColour ?? RESULT, 16);
            c.Label(note, new Vector2(256, 290), 15);
        };

        public static void PropertyTransformer(DiagramCanvas c)
        {
            c.Label("new = old x multiplier + add", new Vector2(256, 60), 22, GUIDE);
            c.Box(new Vector2(30, 120), new Vector2(140, 50), "BPM 120", ORIGINAL);
            c.Arrow(new Vector2(176, 145), new Vector2(222, 145), TEXT, 3);
            c.Box(new Vector2(230, 120), new Vector2(130, 50), "x 1.5  + 0", GUIDE);
            c.Arrow(new Vector2(366, 145), new Vector2(412, 145), TEXT, 3);
            c.Box(new Vector2(418, 120), new Vector2(70, 50), "180", RESULT);

            c.Label("slider velocity, volume, indices, times of objects, bookmarks, breaks...", new Vector2(256, 230), 14);
            c.Label("one undo step", new Vector2(256, 262), 14, RESULT);
        }

        public static void ColourStudio(DiagramCanvas c)
        {
            c.Line(new Vector2(30, 170), new Vector2(482, 170), FAINT, 3);
            var colours = new[] { ROUGH, new Color4(255, 190, 70, 255), RESULT, ORIGINAL };

            for (int i = 0; i < 4; i++)
                c.Circle(new Vector2(60 + i * 40, 170), colours[i], 14);

            c.Line(new Vector2(250, 140), new Vector2(250, 200), GUIDE, 3);
            c.Label("colour point", new Vector2(250, 128), 13, GUIDE);

            for (int i = 0; i < 3; i++)
                c.Circle(new Vector2(290 + i * 50, 170), colours[(i + 1) % 4], 14);

            c.Circle(new Vector2(450, 170), Color4.White, 14);
            c.Label("burst", new Vector2(450, 140), 13, Color4.White);
            c.Label("from a colour point on, combos cycle through its colours", new Vector2(256, 260), 14);
        }

        public static void TimingHelper(DiagramCanvas c)
        {
            c.Line(new Vector2(30, 150), new Vector2(482, 150), FAINT, 3);

            for (int i = 0; i < 9; i++)
                c.Line(new Vector2(40 + i * 50, 140), new Vector2(40 + i * 50, 160), FAINT, 2);

            var heard = new[] { 43f, 96f, 142f, 205f, 243f, 292f };

            foreach (float x in heard)
                c.Dot(new Vector2(x, 150), ROUGH, 12);

            c.Label("markers you put on the sounds", new Vector2(256, 100), 14, ROUGH);
            c.Arrow(new Vector2(256, 190), new Vector2(256, 230), TEXT, 3);

            for (int i = 0; i < 6; i++)
                c.Dot(new Vector2(40 + i * 50, 290), RESULT, 12);

            c.Label("BPM changes and red lines make every marker snapped", new Vector2(256, 330), 14, RESULT);
        }

        public static void HitsoundLanes(DiagramCanvas c)
        {
            var names = new[] { "normal", "whistle", "finish", "clap" };

            for (int i = 0; i < names.Length; i++)
            {
                float y = 60 + i * 60;
                c.Label(names[i], new Vector2(60, y), 14, TEXT);
                c.Line(new Vector2(110, y), new Vector2(490, y), FAINT, 2);

                for (int k = 0; k < 8; k++)
                {
                    if ((k + i * 3) % 3 == 0)
                        c.Dot(new Vector2(130 + k * 48, y), i % 2 == 0 ? ORIGINAL : RESULT, 14);
                }
            }

            c.Label("click a lane to add or remove a hit, Ctrl+drag paints, right-drag erases", new Vector2(256, 320), 14);
        }

        public static void Backups(DiagramCanvas c)
        {
            c.Box(new Vector2(30, 120), new Vector2(120, 60), "Ctrl+S", GUIDE);
            c.Arrow(new Vector2(155, 150), new Vector2(200, 150), TEXT, 3);
            c.Box(new Vector2(205, 120), new Vector2(120, 60), "difficulty.osu", ORIGINAL, 14);
            c.Arrow(new Vector2(250, 190), new Vector2(250, 240), TEXT, 3);
            c.Box(new Vector2(140, 245), new Vector2(240, 60), "backups/<set>/<diff> <time>.osu", RESULT, 13);
            c.Label("newest 30 per difficulty are kept", new Vector2(256, 345), 14);
        }

        public static void Verify(DiagramCanvas c)
        {
            var items = new[]
            {
                ("uneven stream", "Organise", ROUGH), ("off-axis straight slider", "Align", ROUGH), ("unsnapped object", "Resnap", ROUGH),
                ("muted clickable object", "Unmute", ROUGH), ("AutoFail risk (2B)", "no fix", ROUGH),
            };

            for (int i = 0; i < items.Length; i++)
            {
                float y = 40 + i * 62;
                c.Box(new Vector2(30, y), new Vector2(300, 46), items[i].Item1, items[i].Item3, 15);
                c.Box(new Vector2(350, y), new Vector2(130, 46), items[i].Item2, items[i].Item2 == "no fix" ? FAINT : RESULT, 15);
            }
        }
    }
}
