// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Primitives;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: port of Mapping Tools' Snapping Tools: virtual points, lines and circles that are geometrically relevant to nearby objects,
    /// for the cursor to snap to. The generators are Mapping Tools' default ones; to keep the numbers sane they work on the few objects
    /// nearest to the current time, and intersections only use the main lines and the circles between neighbouring objects.
    /// </summary>
    public static class SnappingTools
    {
        /// <param name="Head">Where the object starts.</param>
        /// <param name="Anchors">Slider control points after the head, in playfield space (empty for circles).</param>
        /// <param name="End">Where a slider ends (null for circles).</param>
        /// <param name="Linear">A straight slider (its line is relevant).</param>
        /// <param name="PerfectCircle">The three points of a perfect-circle slider.</param>
        public record SnapSource(Vector2 Head, Vector2[] Anchors, Vector2? End = null, bool Linear = false, Vector2[]? PerfectCircle = null);

        public readonly record struct Line(Vector2 Point, Vector2 Direction)
        {
            public Vector2 Project(Vector2 p)
            {
                var d = Direction.Normalized();
                return Point + d * Vector2.Dot(p - Point, d);
            }
        }

        public readonly record struct Circle(Vector2 Centre, float Radius)
        {
            public Vector2 Project(Vector2 p)
            {
                var offset = p - Centre;
                return offset.LengthSquared < 1e-6f ? Centre + new Vector2(Radius, 0) : Centre + offset.Normalized() * Radius;
            }
        }

        public class Geometry
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public readonly List<Vector2> Intersections = new List<Vector2>();
            public readonly List<Line> Lines = new List<Line>();
            public readonly List<Circle> Circles = new List<Circle>();
        }

        /// <param name="objects">In time order.</param>
        public static Geometry Generate(IReadOnlyList<SnapSource> objects)
        {
            var g = new Geometry();

            // Points on circles and slider heads, slider ends (last anchors).
            var basePoints = objects.Select(o => o.Head).Concat(objects.Where(o => o.End != null).Select(o => o.End!.Value)).Distinct().ToList();

            g.Points.AddRange(basePoints);
            g.Points.AddRange(objects.SelectMany(o => o.Anchors));

            var mainLines = new List<Line>();
            var neighbourCircles = new List<Circle>();

            foreach (var o in objects)
            {
                if (o.Linear && o.End is Vector2 end && (end - o.Head).LengthSquared > 1)
                    mainLines.Add(new Line(o.Head, end - o.Head));

                if (o.PerfectCircle is { Length: 3 } p && circleThrough(p[0], p[1], p[2]) is Circle c)
                {
                    g.Circles.Add(c);
                    g.Points.Add(c.Centre);
                }
            }

            for (int i = 0; i < basePoints.Count; i++)
            {
                for (int j = i + 1; j < basePoints.Count; j++)
                {
                    Vector2 a = basePoints[i], b = basePoints[j];
                    Vector2 diff = b - a;

                    if (diff.LengthSquared < 1)
                        continue;

                    // Average of two points.
                    g.Points.Add((a + b) / 2);

                    // Squares (types I and II) and equilateral triangles (types I and II).
                    var r1 = rotate(diff, MathF.PI * 3 / 4) / MathF.Sqrt(2);
                    var r2 = rotate(diff, MathF.PI / 2);
                    var r3 = rotate(diff, MathF.PI * 2 / 3);
                    var r4 = rotate(diff, MathF.PI * 5 / 6) / MathF.Sqrt(3);
                    g.Points.AddRange(new[] { a - r1, b + r1, a - r2, a + r2, b - r2, b + r2, a - r3, b + r3, a - r4, b + r4 });

                    // Lines by two points and the bisector of two points.
                    mainLines.Add(new Line(a, diff));
                    mainLines.Add(new Line((a + b) / 2, new Vector2(-diff.Y, diff.X)));

                    // Circles by two points.
                    float radius = diff.Length;
                    g.Circles.Add(new Circle(a, radius));
                    g.Circles.Add(new Circle(b, radius));

                    if (j == i + 1)
                    {
                        neighbourCircles.Add(new Circle(a, radius));
                        neighbourCircles.Add(new Circle(b, radius));
                    }
                }
            }

            // Average of three points.
            for (int i = 0; i < basePoints.Count; i++)
            for (int j = i + 1; j < basePoints.Count; j++)
            for (int k = j + 1; k < basePoints.Count; k++)
                g.Points.Add((basePoints[i] + basePoints[j] + basePoints[k]) / 3);

            // Angle bisectors where consecutive objects turn.
            for (int i = 1; i + 1 < objects.Count; i++)
            {
                Vector2 a = objects[i - 1].Head, b = objects[i].Head, c = objects[i + 1].Head;

                if ((a - b).LengthSquared < 1 || (c - b).LengthSquared < 1)
                    continue;

                var d1 = (a - b).Normalized();
                var d2 = (c - b).Normalized();
                mainLines.Add(new Line(b, d1 + d2));
                mainLines.Add(new Line(b, d1 - d2));
            }

            mainLines.RemoveAll(l => l.Direction.LengthSquared < 1e-6f);
            g.Lines.AddRange(mainLines);

            // Parallel and perpendicular lines through each point, to each straight slider and each line between neighbouring objects.
            var guideLines = mainLines.Where(l => objects.Any(o => o.Linear && o.Head == l.Point)).ToList();
            for (int i = 0; i + 1 < objects.Count; i++)
            {
                if ((objects[i + 1].Head - objects[i].Head).LengthSquared > 1)
                    guideLines.Add(new Line(objects[i].Head, objects[i + 1].Head - objects[i].Head));
            }

            foreach (var p in basePoints)
            {
                foreach (var l in guideLines)
                {
                    g.Lines.Add(new Line(p, l.Direction));
                    g.Lines.Add(new Line(p, new Vector2(-l.Direction.Y, l.Direction.X)));
                }
            }

            // Reflections of the points across the main lines.
            foreach (var l in mainLines)
            {
                foreach (var p in basePoints)
                    g.Points.Add(2 * l.Project(p) - p);
            }

            // Intersections.
            for (int i = 0; i < mainLines.Count; i++)
            {
                for (int j = i + 1; j < mainLines.Count; j++)
                {
                    if (intersect(mainLines[i], mainLines[j]) is Vector2 x)
                        g.Intersections.Add(x);
                }

                foreach (var c in neighbourCircles)
                    g.Intersections.AddRange(intersect(mainLines[i], c));
            }

            for (int i = 0; i < neighbourCircles.Count; i++)
            {
                for (int j = i + 1; j < neighbourCircles.Count; j++)
                    g.Intersections.AddRange(intersect(neighbourCircles[i], neighbourCircles[j]));
            }

            // Only what is on (or near) the playfield matters.
            var bounds = new RectangleF(-64, -64, 512 + 128, 384 + 128);
            g.Points.RemoveAll(p => !bounds.Contains(p));
            g.Intersections.RemoveAll(p => !bounds.Contains(p));

            return g;
        }

        /// <summary>
        /// The snapped position: the nearest point (or intersection) within <paramref name="radius"/>, otherwise the nearest place on a line or circle within it.
        /// </summary>
        public static Vector2? Snap(Geometry g, Vector2 position, float radius)
        {
            var point = g.Points.Concat(g.Intersections).Select(p => (Vector2?)p).MinBy(p => Vector2.DistanceSquared(p!.Value, position));

            if (point is Vector2 nearestPoint && Vector2.Distance(nearestPoint, position) <= radius)
                return nearestPoint;

            var onShape = g.Lines.Select(l => l.Project(position)).Concat(g.Circles.Select(c => c.Project(position)))
                           .Select(p => (Vector2?)p).MinBy(p => Vector2.DistanceSquared(p!.Value, position));

            return onShape is Vector2 nearest && Vector2.Distance(nearest, position) <= radius ? nearest : null;
        }

        private static Vector2 rotate(Vector2 v, float angle)
        {
            float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
            return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }

        private static Vector2? intersect(Line a, Line b)
        {
            float cross = a.Direction.X * b.Direction.Y - a.Direction.Y * b.Direction.X;

            if (MathF.Abs(cross) < 1e-6f)
                return null;

            var diff = b.Point - a.Point;
            float t = (diff.X * b.Direction.Y - diff.Y * b.Direction.X) / cross;
            return a.Point + a.Direction * t;
        }

        private static IEnumerable<Vector2> intersect(Line line, Circle circle)
        {
            var foot = line.Project(circle.Centre);
            float distanceSquared = (foot - circle.Centre).LengthSquared;
            float r2 = circle.Radius * circle.Radius;

            if (distanceSquared > r2)
                yield break;

            var d = line.Direction.Normalized() * MathF.Sqrt(r2 - distanceSquared);
            yield return foot + d;
            yield return foot - d;
        }

        private static IEnumerable<Vector2> intersect(Circle a, Circle b)
        {
            var diff = b.Centre - a.Centre;
            float d = diff.Length;

            if (d < 1e-4f || d > a.Radius + b.Radius || d < MathF.Abs(a.Radius - b.Radius))
                yield break;

            float along = (a.Radius * a.Radius - b.Radius * b.Radius + d * d) / (2 * d);
            float h = MathF.Sqrt(MathF.Max(0, a.Radius * a.Radius - along * along));
            var mid = a.Centre + diff * (along / d);
            var perpendicular = new Vector2(-diff.Y, diff.X) / d * h;

            yield return mid + perpendicular;
            yield return mid - perpendicular;
        }

        private static Circle? circleThrough(Vector2 a, Vector2 b, Vector2 c)
        {
            float d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));

            if (MathF.Abs(d) < 1e-4f)
                return null;

            float aa = a.LengthSquared, bb = b.LengthSquared, cc = c.LengthSquared;
            var centre = new Vector2((aa * (b.Y - c.Y) + bb * (c.Y - a.Y) + cc * (a.Y - b.Y)) / d, (aa * (c.X - b.X) + bb * (a.X - c.X) + cc * (b.X - a.X)) / d);
            return new Circle(centre, (a - centre).Length);
        }
    }
}
