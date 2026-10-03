// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: port of Mapping Tools' Tumour Generator 2 (one layer): tumours along a slider's path, each built from its own few anchors
    /// as Mapping Tools' reconstruction hints do (triangle and square: red anchors, circle: a perfect curve, parabola: a bezier).
    /// The path between tumours stays the slider's own curve, simplified to the fewest points within half a pixel.
    /// </summary>
    public class TumourGenerator
    {
        public enum Shape
        {
            Triangle,
            Square,
            Circle,
            Parabola,
        }

        public enum Sides
        {
            Left,
            Right,

            [Description("Alternating, left first")]
            AlternatingLeft,

            [Description("Alternating, right first")]
            AlternatingRight,

            Random,
        }

        public readonly Bindable<Shape> Template = new Bindable<Shape>();
        public readonly Bindable<Sides> Side = new Bindable<Sides>(Sides.AlternatingLeft);

        /// <summary>
        /// Length of a tumour along the path, in pixels.
        /// </summary>
        public readonly BindableDouble Length = new BindableDouble(15) { MinValue = 2, MaxValue = 200, Precision = 1 };

        /// <summary>
        /// How far a tumour sticks out, in pixels.
        /// </summary>
        public readonly BindableDouble Height = new BindableDouble(15) { MinValue = 1, MaxValue = 100, Precision = 1 };

        /// <summary>
        /// From the start of one tumour to the next, in pixels.
        /// </summary>
        public readonly BindableDouble Distance = new BindableDouble(40) { MinValue = 2, MaxValue = 400, Precision = 1 };

        /// <summary>
        /// Where the tumours start and end along the slider, 0 to 1.
        /// </summary>
        public readonly BindableDouble Start = new BindableDouble { MinValue = 0, MaxValue = 1, Precision = 0.01 };

        public readonly BindableDouble End = new BindableDouble(1) { MinValue = 0, MaxValue = 1, Precision = 0.01 };

        /// <summary>
        /// At most this many tumours (0: as many as fit).
        /// </summary>
        public readonly BindableInt Count = new BindableInt { MinValue = 0, MaxValue = 200 };

        public int Seed;

        /// <param name="path">The slider's path as a polyline.</param>
        /// <returns>Control points (position, and a type where a new segment starts) in the same space as <paramref name="path"/>.</returns>
        public List<(Vector2 Position, PathType? Type)> Generate(IReadOnlyList<Vector2> path)
        {
            var cumulative = new List<double> { 0 };
            for (int i = 1; i < path.Count; i++)
                cumulative.Add(cumulative[^1] + (path[i] - path[i - 1]).Length);

            double total = cumulative[^1];
            var result = new List<(Vector2, PathType?)>();

            if (path.Count < 2 || total < 1)
                return result;

            var random = new Random(Seed);
            bool side = Side.Value == Sides.AlternatingRight; // flipped before the first tumour
            double end = Math.Min(total, End.Value * total);
            double d = Start.Value * total;
            double lastEnd = 0;
            int placed = 0;

            while (d + Length.Value <= end + 1e-6 && (Count.Value == 0 || placed < Count.Value))
            {
                side = Side.Value switch
                {
                    Sides.Left => false,
                    Sides.Right => true,
                    Sides.Random => random.NextDouble() < 0.5,
                    _ => !side,
                };

                double from = d, to = d + Length.Value;

                // The plain path up to the tumour, then the tumour.
                addPlain(result, path, cumulative, lastEnd, from);
                addTumour(result, positionAt(path, cumulative, from), positionAt(path, cumulative, to), side);

                lastEnd = to;
                placed++;
                d += Distance.Value;
            }

            addPlain(result, path, cumulative, lastEnd, total);
            return result;
        }

        private void addTumour(List<(Vector2, PathType?)> result, Vector2 start, Vector2 end, bool otherSide)
        {
            var x = end - start;
            if (x.LengthSquared < 1e-6f)
                return;

            // Mapping Tools' templates point to -Y, the left of the direction of travel; the other side mirrors that.
            var y = new Vector2(-x.Y, x.X).Normalized() * (otherSide ? 1 : -1);
            float length = x.Length, h = (float)Height.Value;
            Vector2 at(float along, float up) => start + x.Normalized() * along + y * up;

            switch (Template.Value)
            {
                case Shape.Triangle:
                    add(result, start, PathType.LINEAR);
                    add(result, at(length / 2, h), PathType.LINEAR);
                    break;

                case Shape.Square:
                    add(result, start, PathType.LINEAR);
                    add(result, at(0, h), PathType.LINEAR);
                    add(result, at(length, h), PathType.LINEAR);
                    break;

                case Shape.Circle:
                    add(result, start, PathType.PERFECT_CURVE);
                    result.Add((at(length / 2, h), null));
                    break;

                case Shape.Parabola:
                    add(result, start, PathType.BEZIER);
                    result.Add((at(length / 2, 2 * h), null));
                    break;
            }

            add(result, end, PathType.LINEAR);
        }

        // The path between two distances, as few linear points as stay within half a pixel of it.
        private static void addPlain(List<(Vector2, PathType?)> result, IReadOnlyList<Vector2> path, List<double> cumulative, double from, double to)
        {
            if (to - from < 0.5 && result.Count > 0)
                return;

            var points = new List<Vector2> { positionAt(path, cumulative, from) };
            for (int i = 0; i < path.Count; i++)
            {
                if (cumulative[i] > from && cumulative[i] < to)
                    points.Add(path[i]);
            }

            points.Add(positionAt(path, cumulative, to));

            var simplified = simplify(points, 0.5f);
            add(result, simplified[0], PathType.LINEAR);
            foreach (var p in simplified.Skip(1))
                result.Add((p, null));
        }

        // A point where the last one is the same position just takes over the new segment type.
        private static void add(List<(Vector2 Position, PathType? Type)> result, Vector2 position, PathType type)
        {
            if (result.Count > 0 && Vector2.DistanceSquared(result[^1].Position, position) < 1e-4f)
                result[^1] = (result[^1].Position, type);
            else
                result.Add((position, type));
        }

        private static Vector2 positionAt(IReadOnlyList<Vector2> path, List<double> cumulative, double distance)
        {
            int i = cumulative.BinarySearch(distance);
            if (i >= 0)
                return path[i];

            i = Math.Clamp(~i, 1, path.Count - 1);
            double segment = cumulative[i] - cumulative[i - 1];
            return segment <= 0 ? path[i] : Vector2.Lerp(path[i - 1], path[i], (float)((distance - cumulative[i - 1]) / segment));
        }

        // Douglas-Peucker.
        private static List<Vector2> simplify(List<Vector2> points, float tolerance)
        {
            if (points.Count < 3)
                return points;

            Vector2 a = points[0], b = points[^1];
            int farthest = 0;
            float farthestDistance = 0;

            for (int i = 1; i < points.Count - 1; i++)
            {
                float distance = distanceToSegment(points[i], a, b);

                if (distance > farthestDistance)
                {
                    farthestDistance = distance;
                    farthest = i;
                }
            }

            if (farthestDistance <= tolerance)
                return new List<Vector2> { a, b };

            var left = simplify(points.Take(farthest + 1).ToList(), tolerance);
            var right = simplify(points.Skip(farthest).ToList(), tolerance);
            return left.Concat(right.Skip(1)).ToList();
        }

        private static float distanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.LengthSquared < 1e-9f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared, 0, 1);
            return (a + ab * t - p).Length;
        }
    }
}
