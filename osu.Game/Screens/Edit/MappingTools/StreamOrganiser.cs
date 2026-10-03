// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: cleans up a hand-placed stream. The objects keep their times and the first position;
    /// they are moved onto a clean curve (the hand-drawn shape with its wobble filtered out, an arc or a straight line)
    /// with visually even (center to center), accelerating or decelerating spacing, fitted between the ends or taken from distance snap.
    /// For the clean curve, how sharply the stream turns is then evened out from object to object, keeping the ends where they were placed.
    /// </summary>
    public class StreamOrganiser
    {
        public enum SpeedMode
        {
            [Description("Even")]
            Even,

            [Description("Accelerate")]
            Accelerate,

            [Description("Decelerate")]
            Decelerate,
        }

        public enum ShapeMode
        {
            [Description("Clean curve")]
            Clean,

            [Description("Arc")]
            Arc,

            [Description("Straight line")]
            Straight,
        }

        public enum SpacingSource
        {
            [Description("Fit between first and last")]
            FitEnds,

            [Description("Distance snap")]
            DistanceSnap,
        }

        public readonly Bindable<SpeedMode> Speed = new Bindable<SpeedMode>();

        public readonly Bindable<ShapeMode> Shape = new Bindable<ShapeMode>();

        public readonly Bindable<SpacingSource> Spacing = new Bindable<SpacingSource>();

        /// <summary>
        /// How much bigger the widest gap is than the tightest when accelerating or decelerating.
        /// </summary>
        public readonly BindableDouble Strength = new BindableDouble(2)
        {
            MinValue = 1,
            MaxValue = 8,
            Precision = 0.1,
        };

        /// <summary>
        /// YAWNS: alternate objects sit this many pixels to either side of the stream line (0 is off), for wiggle streams.
        /// </summary>
        public readonly BindableDouble Wiggle = new BindableDouble(0)
        {
            MinValue = 0,
            MaxValue = 40,
            Precision = 0.5,
        };

        /// <summary>
        /// For <see cref="ShapeMode.Clean"/>: 0 evens out the turning the most (bends get rounder), 1 stays closest to the placed shape.
        /// Wobble between neighbouring objects is removed either way.
        /// </summary>
        public readonly BindableDouble FollowShape = new BindableDouble(0.5)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.05,
        };

        /// <summary>
        /// YAWNS: wiggle: every other object (from the second) is moved <paramref name="pixels"/> to alternating sides of the stream's direction there,
        /// the first object stays on the line. 0 changes nothing.
        /// </summary>
        public static Vector2[] Wiggled(Vector2[] points, double pixels)
        {
            if (pixels <= 0 || points.Length < 3)
                return points;

            var result = (Vector2[])points.Clone();

            for (int i = 1; i < points.Length; i++)
            {
                Vector2 direction = points[Math.Min(i + 1, points.Length - 1)] - points[i - 1];

                if (direction.LengthSquared < 1e-6f)
                    continue;

                direction.Normalize();
                float side = i % 2 == 1 ? 1 : -1;
                result[i] = points[i] + new Vector2(-direction.Y, direction.X) * side * (float)pixels;
            }

            return result;
        }

        /// <param name="positions">Object positions, in time order.</param>
        /// <param name="times">Object start times, same order.</param>
        /// <param name="distanceSnap">For <see cref="SpacingSource.DistanceSnap"/>: the distance snapped spacing for a gap (start time, duration).</param>
        public Vector2[] Organise(IReadOnlyList<Vector2> positions, IReadOnlyList<double> times, Func<double, double, double>? distanceSnap = null)
        {
            double ratio = Speed.Value switch
            {
                SpeedMode.Accelerate => Strength.Value,
                SpeedMode.Decelerate => 1 / Strength.Value,
                _ => 1,
            };

            return Wiggled(Organise(positions, times, ratio, FollowShape.Value, Shape.Value, Spacing.Value == SpacingSource.DistanceSnap ? distanceSnap : null), Wiggle.Value);
        }

        /// <param name="positions">Object positions, in time order.</param>
        /// <param name="times">Object start times, same order.</param>
        /// <param name="ratio">Spacing of the last gap relative to the first, per millisecond. 1 is even.</param>
        /// <param name="followShape">0 to 1, see <see cref="FollowShape"/>.</param>
        /// <param name="shape">The curve the objects are put on.</param>
        /// <param name="distanceSnap">
        /// When given, gap i is <c>distanceSnap(start, duration)</c> scaled by the speed ramp, and the last object moves (past the curve's end if needed).
        /// When null, the gaps are scaled so the last object lands on the last hand-placed position.
        /// </param>
        public static Vector2[] Organise(IReadOnlyList<Vector2> positions, IReadOnlyList<double> times, double ratio, double followShape,
                                         ShapeMode shape = ShapeMode.Clean, Func<double, double, double>? distanceSnap = null)
        {
            int n = positions.Count;

            if (n != times.Count)
                throw new ArgumentException("Need one time per position.");

            if (n < 3)
                return positions.ToArray();

            // Gap i is scaled geometrically from 1 to ratio along the stream.
            double[] gaps = new double[n - 1];

            for (int i = 0; i < n - 1; i++)
            {
                double duration = Math.Max(times[i + 1] - times[i], 0);
                double ramp = Math.Pow(ratio, (double)i / (n - 2));
                gaps[i] = (distanceSnap?.Invoke(times[i], duration) ?? duration) * ramp;
            }

            if (gaps.Sum() <= 0)
                return positions.ToArray();

            var walker = new CurveWalker(shape switch
            {
                ShapeMode.Arc => fitArc(positions),
                ShapeMode.Straight => new List<Vector2> { positions[0], positions[n - 1] },
                _ => smoothCurve(positions),
            });

            Vector2[] result;

            if (distanceSnap != null)
                result = walker.Walk(gaps, out _);
            else
            {

                // Scale the gaps so the walk ends exactly at the curve's end. A center to center gap covers at least as much curve as its length,
                // so the scale is at most length / sum, and the distance covered grows with it: bisect.
                double low = 0, high = walker.Length / gaps.Sum();

                for (int i = 0; i < 60; i++)
                {
                    double mid = (low + high) / 2;
                    walker.Walk(gaps.Select(g => g * mid).ToArray(), out double reached);

                    if (reached < walker.Length)
                        low = mid;
                    else
                        high = mid;
                }

                result = walker.Walk(gaps.Select(g => g * high).ToArray(), out _);
                result[n - 1] = positions[n - 1];
            }

            // The smoothed curve has no wobble left, but how sharply it turns can still vary from object to object: even that out too.
            if (shape == ShapeMode.Clean)
                result = evenTurns(result, 1.0 - 0.6 * followShape, distanceSnap == null ? positions[n - 1] : null);

            return result;
        }

        private const int curve_samples = 400;

        /// <summary>
        /// The clean curve: the hand-drawn path (straight between objects), resampled every half gap and smoothed with Taubin's
        /// non-shrinking filter. Each pass pulls every point toward its neighbours and then pushes it slightly back the other way:
        /// wobble from object to object is damped strongly, while bends spanning several objects (a hook, an S) keep their size and place.
        /// Ends stay put.
        /// </summary>
        private static List<Vector2> smoothCurve(IReadOnlyList<Vector2> points)
        {
            double length = 0;
            for (int i = 1; i < points.Count; i++)
                length += Vector2.Distance(points[i - 1], points[i]);

            var path = new CurveWalker(points.ToList());
            int samples = Math.Max(2, (int)Math.Round(2.0 * (points.Count - 1)));
            var curve = Enumerable.Range(0, samples + 1).Select(i => path.PositionAlong(length * i / samples)).ToArray();

            for (int pass = 0; pass < 40; pass++)
            {
                laplacianStep(curve, 0.33f);
                laplacianStep(curve, -0.34f);
            }

            // Smooth corners between the samples (no overshoot: they are evenly spaced now).
            return new SliderPath(PathType.CATMULL, curve.Select(p => p - curve[0]).ToArray()).CalculatedPath.Select(p => p + curve[0]).ToList();
        }

        /// <summary>
        /// Evens out how sharply the stream turns from object to object: the turn at each object is blurred over its neighbours,
        /// and the stream is redrawn from the first object with the same gaps.
        /// For <paramref name="target"/>, a smooth (constant plus linear) correction to the turns is solved so the last object lands on it.
        /// A linear correction adds no unevenness, and unlike scaling the whole stream it keeps the gaps and the size of the shape.
        /// </summary>
        private static Vector2[] evenTurns(Vector2[] stream, double sigma, Vector2? target)
        {
            int n = stream.Length;
            double[] turns = new double[n - 2];

            for (int i = 1; i < n - 1; i++)
            {
                Vector2 a = stream[i] - stream[i - 1];
                Vector2 b = stream[i + 1] - stream[i];
                turns[i - 1] = Math.Atan2(a.X * b.Y - a.Y * b.X, Vector2.Dot(a, b));
            }

            double[] smoothed = gaussian(turns, sigma);
            float[] gaps = stream.Zip(stream.Skip(1), Vector2.Distance).ToArray();
            double heading = Math.Atan2(stream[1].Y - stream[0].Y, stream[1].X - stream[0].X);

            Vector2[] draw(double constant, double slope)
            {
                var result = new Vector2[n];
                result[0] = stream[0];
                double h = heading;

                for (int i = 0; i < n - 1; i++)
                {
                    result[i + 1] = result[i] + gaps[i] * new Vector2((float)Math.Cos(h), (float)Math.Sin(h));

                    if (i < n - 2)
                        h += smoothed[i] + constant + slope * (i - (n - 3) / 2.0);
                }

                return result;
            }

            if (target is not Vector2 end)
                return draw(0, 0);

            // Newton's method on (constant, slope) for the last object's position.
            double c = 0, s = 0;

            for (int iteration = 0; iteration < 20; iteration++)
            {
                Vector2 miss = draw(c, s)[^1] - end;

                if (miss.Length < 0.01f)
                    return withEnd(draw(c, s), end);

                const double h = 1e-4;
                Vector2 dc = (draw(c + h, s)[^1] - draw(c, s)[^1]) / (float)h;
                Vector2 ds = (draw(c, s + h)[^1] - draw(c, s)[^1]) / (float)h;

                double det = dc.X * ds.Y - dc.Y * ds.X;

                if (Math.Abs(det) < 1e-9)
                    break;

                c -= (ds.Y * miss.X - ds.X * miss.Y) / det;
                s -= (-dc.Y * miss.X + dc.X * miss.Y) / det;
            }

            // Did not converge (unusual shapes such as near-loops): keep the original even stream.
            return stream;
        }

        private static Vector2[] withEnd(Vector2[] stream, Vector2 end)
        {
            stream[^1] = end;
            return stream;
        }

        /// <summary>
        /// Gaussian blur over the list (by index), renormalised at the ends.
        /// </summary>
        private static double[] gaussian(double[] values, double sigma)
        {
            int radius = (int)Math.Ceiling(sigma * 3);
            double[] result = new double[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                double sum = 0, weights = 0;

                for (int j = Math.Max(0, i - radius); j <= Math.Min(values.Length - 1, i + radius); j++)
                {
                    double w = Math.Exp(-(i - j) * (i - j) / (2 * sigma * sigma));
                    sum += w * values[j];
                    weights += w;
                }

                result[i] = sum / weights;
            }

            return result;
        }

        private static void laplacianStep(Vector2[] curve, float factor)
        {
            var previous = (Vector2[])curve.Clone();

            for (int i = 1; i < curve.Length - 1; i++)
                curve[i] = previous[i] + factor * ((previous[i - 1] + previous[i + 1]) / 2 - previous[i]);
        }

        /// <summary>
        /// The arc of the circle through the first and last point that best fits all points (least squares on the distance to the circle).
        /// Falls back to a straight line when the points are in line.
        /// </summary>
        private static List<Vector2> fitArc(IReadOnlyList<Vector2> points)
        {
            Vector2 first = points[0];
            Vector2 last = points[^1];
            float chord = Vector2.Distance(first, last);

            if (chord < 1)
                return new List<Vector2> { first, last };

            // Centres of circles through both ends lie on the perpendicular bisector: search along it.
            Vector2 middle = (first + last) / 2;
            Vector2 normal = (last - first).PerpendicularLeft / chord;

            double cost(double s)
            {
                Vector2 centre = middle + normal * (float)s;
                float radius = Vector2.Distance(first, centre);
                return points.Sum(p => Math.Pow(Vector2.Distance(p, centre) - radius, 2));
            }

            double range = chord * 20;
            double step = range / 1000;
            double best = Enumerable.Range(-1000, 2001).Select(i => i * step).MinBy(cost);

            // Refine around the best grid point.
            double lo = best - step, hi = best + step;

            for (int i = 0; i < 60; i++)
            {
                double a = lo + (hi - lo) / 3, b = hi - (hi - lo) / 3;
                if (cost(a) < cost(b))
                    hi = b;
                else
                    lo = a;
            }

            best = (lo + hi) / 2;

            // A centre at the edge of the search means a nearly infinite circle.
            if (Math.Abs(best) > range * 0.99)
                return new List<Vector2> { first, last };

            Vector2 c = middle + normal * (float)best;
            double r = Vector2.Distance(first, c);

            // Follow the points around the circle so the arc goes the way they do (possibly more than half way round).
            // The circle passes through the last point, so this ends exactly on it.
            double start = Math.Atan2(first.Y - c.Y, first.X - c.X);
            double end = start;

            for (int i = 1; i < points.Count; i++)
            {
                double next = Math.Atan2(points[i].Y - c.Y, points[i].X - c.X);
                end += Math.IEEERemainder(next - end, 2 * Math.PI);
            }

            return Enumerable.Range(0, curve_samples + 1).Select(i =>
            {
                double a = start + (end - start) * i / curve_samples;
                return c + new Vector2((float)(r * Math.Cos(a)), (float)(r * Math.Sin(a)));
            }).ToList();
        }

        /// <summary>
        /// Places objects along a polyline so each is a given straight-line distance from the previous one, continuing straight on past the end.
        /// </summary>
        private class CurveWalker
        {
            private readonly List<Vector2> points;
            private readonly double[] cumulative;

            public double Length => cumulative[^1];

            public CurveWalker(List<Vector2> points)
            {
                // Drop zero-length segments.
                this.points = points.Where((p, i) => i == 0 || Vector2.DistanceSquared(p, points[i - 1]) > 1e-8).ToList();

                if (this.points.Count == 1)
                    this.points.Add(this.points[0] + Vector2.UnitX * 1e-3f);

                cumulative = new double[this.points.Count];
                for (int i = 1; i < this.points.Count; i++)
                    cumulative[i] = cumulative[i - 1] + Vector2.Distance(this.points[i - 1], this.points[i]);
            }

            /// <summary>
            /// The point <paramref name="distance"/> along the polyline (clamped to its ends).
            /// </summary>
            public Vector2 PositionAlong(double distance)
            {
                int i = Array.FindLastIndex(cumulative, c => c <= distance);

                if (i < 0)
                    return points[0];
                if (i >= points.Count - 1)
                    return points[^1];

                double t = (distance - cumulative[i]) / (cumulative[i + 1] - cumulative[i]);
                return Vector2.Lerp(points[i], points[i + 1], (float)t);
            }

            /// <param name="gaps">Straight-line distance between consecutive objects.</param>
            /// <param name="reached">How far along the curve the last object is (past <see cref="Length"/> when it ran off the end).</param>
            public Vector2[] Walk(double[] gaps, out double reached)
            {
                var result = new Vector2[gaps.Length + 1];
                result[0] = points[0];

                int segment = 0;
                double along = 0; // 0..1 within the segment

                for (int i = 0; i < gaps.Length; i++)
                {
                    Vector2 from = result[i];
                    double d = gaps[i];
                    bool found = false;

                    // The first point further along the curve which is d away from the previous object.
                    for (; segment < points.Count - 1; segment++, along = 0)
                    {
                        double u = crossing(points[segment], points[segment + 1], from, d, along);

                        if (u >= 0)
                        {
                            along = u;
                            result[i + 1] = Vector2.Lerp(points[segment], points[segment + 1], (float)u);
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        // Ran off the end: continue straight on.
                        segment = points.Count - 2;
                        Vector2 end = points[^1];
                        Vector2 direction = (end - points[^2]).Normalized();
                        double t = rayCrossing(end, direction, from, d);
                        result[i + 1] = end + direction * (float)t;
                        along = 1 + t / Vector2.Distance(points[^2], end);
                    }
                }

                double segmentLength = cumulative[segment + 1] - cumulative[segment];
                reached = cumulative[segment] + along * segmentLength;
                return result;
            }

            /// <summary>
            /// The smallest u in [minU, 1] where a + u (b - a) is <paramref name="d"/> from <paramref name="centre"/>, or -1.
            /// </summary>
            private static double crossing(Vector2 a, Vector2 b, Vector2 centre, double d, double minU)
            {
                Vector2 ab = b - a;
                Vector2 ac = a - centre;

                double qa = Vector2.Dot(ab, ab);
                double qb = 2 * Vector2.Dot(ab, ac);
                double qc = Vector2.Dot(ac, ac) - d * d;
                double disc = qb * qb - 4 * qa * qc;

                if (qa <= 0 || disc < 0)
                    return -1;

                double sq = Math.Sqrt(disc);

                // Strictly after the previous object on its own segment; from the very start on later ones.
                // The small tolerances keep a crossing exactly on the joint between two segments from being missed by both.
                double lowest = minU > 0 ? minU + 1e-9 : -1e-6;

                foreach (double u in new[] { (-qb - sq) / (2 * qa), (-qb + sq) / (2 * qa) })
                {
                    if (u >= lowest && u <= 1 + 1e-6)
                        return Math.Clamp(u, 0, 1);
                }

                return -1;
            }

            /// <summary>
            /// The t >= 0 where origin + t direction is <paramref name="d"/> from <paramref name="centre"/> (furthest solution).
            /// </summary>
            private static double rayCrossing(Vector2 origin, Vector2 direction, Vector2 centre, double d)
            {
                Vector2 oc = origin - centre;
                double qb = 2 * Vector2.Dot(direction, oc);
                double qc = Vector2.Dot(oc, oc) - d * d;
                double disc = Math.Max(0, qb * qb - 4 * qc);
                return Math.Max(0, (-qb + Math.Sqrt(disc)) / 2);
            }
        }
    }
}
