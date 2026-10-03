// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: port of Mapping Tools' Sliderator: sliders whose ball changes speed. The slider's path doubles back on itself in tiny
    /// hidden zig-zags ("dendrites", at most 12 px, inside the slider body) wherever the ball should be slower than the slider velocity,
    /// so along the visible path it follows a chosen position function. Points sit on whole pixels as the .osu format stores them.
    /// </summary>
    /// <remarks>
    /// The path between turn points ("axons") is fitted with quadratic beziers split where the path turns more than 45 degrees,
    /// a smaller version of Mapping Tools' path generator.
    /// </remarks>
    public class Sliderator
    {
        /// <summary>
        /// Position along the visible path in pixels, for a time in milliseconds from the slider's start.
        /// </summary>
        public Func<double, double> PositionFunction = _ => 0;

        /// <summary>
        /// The slider's duration in milliseconds.
        /// </summary>
        public double MaxT;

        /// <summary>
        /// The new slider velocity in pixels per millisecond. At least the fastest the position function moves.
        /// </summary>
        public double Velocity;

        public double MinDendriteLength = 1;

        private const double max_dendrite = 12;

        private readonly List<Vector2> path = new List<Vector2>();
        private readonly List<Vector2> diff = new List<Vector2>();
        private readonly List<double> diffL = new List<double>();
        private readonly List<double> pathL = new List<double>();

        private List<LatticePoint> lattice = new List<LatticePoint>();
        private List<Neuron> neurons = new List<Neuron>();

        public double MaxS => MaxT * Velocity;

        /// <summary>
        /// The fastest the position function moves, in pixels per millisecond (sampled).
        /// </summary>
        public static double MaxSpeed(Func<double, double> position, double duration, int samples = 2000)
        {
            double max = 0;

            for (int i = 0; i < samples; i++)
            {
                double t = duration * i / samples;
                max = Math.Max(max, Math.Abs(position(t + duration / samples) - position(t)) / (duration / samples));
            }

            return max;
        }

        /// <param name="pathPoints">The visible path, as a polyline in playfield space.</param>
        public void SetPath(IReadOnlyList<Vector2> pathPoints)
        {
            path.Clear();
            diff.Clear();
            diffL.Clear();
            pathL.Clear();

            path.Add(pathPoints[0]);
            pathL.Add(0);
            double sum = 0;

            foreach (var p in pathPoints.Skip(1))
            {
                var d = p - path[^1];
                double l = d.Length;

                if (l < 1e-9)
                    continue;

                path.Add(p);
                diff.Add(d);
                diffL.Add(l);
                sum += l;
                pathL.Add(sum);
            }

            if (sum < 1e-9)
                throw new InvalidOperationException("Zero length path.");

            // Repeat the last segment so these line up with the path points.
            diff.Add(diff[^1]);
            diffL.Add(diffL[^1]);
        }

        /// <summary>
        /// The new path's anchors in playfield space. Two equal anchors in a row are a red anchor (a new segment), as in the .osu format.
        /// </summary>
        public List<Vector2> Sliderate()
        {
            lattice = latticePoints();
            generateNeurons();
            generateAxons();
            generateDendrites();
            return anchors();
        }

        private Vector2 positionAt(double x)
        {
            int n = pathL.BinarySearch(x);
            if (n < 0) n = ~n - 1;
            n = Math.Clamp(n, 0, diff.Count - 1);
            return path[n] + diff[n] / (float)diffL[n] * (float)(x - pathL[n]);
        }

        private record LatticePoint(Vector2 Pos, double PathPosition, double Error, int SegmentIndex, double SegmentProgress);

        private class Neuron
        {
            public readonly LatticePoint Nucleus;
            public readonly double Time;
            public readonly List<Vector2> Dendrites = new List<Vector2>();
            public List<Vector2> Axon = new List<Vector2>();
            public Neuron? Terminal;
            public double WantedLength;
            public double DendriteLength;
            public double AxonLength;

            public Neuron(LatticePoint nucleus, double time)
            {
                Nucleus = nucleus;
                Time = time;
            }
        }

        // Whole pixel points within 0.35 px of the path, in path order.
        private List<LatticePoint> latticePoints(double tolerance = 0.35)
        {
            var result = new List<LatticePoint>();

            for (int n = 0; n < diff.Count - 1; n++)
            {
                double l = diffL[n];
                Vector2 a = path[n], d = diff[n];
                double l2 = d.LengthSquared;
                int ax = Math.Abs(d.X) < Math.Abs(d.Y) ? 1 : 0;
                int dr = Math.Sign(d[ax]);
                int from = (int)Math.Round(a[ax]), to = (int)Math.Round(a[ax] + d[ax]) + dr;

                for (int i = from; i != to; i += dr)
                {
                    double s = (i - a[ax]) / d[ax];
                    double r = a[1 - ax] + s * d[1 - ax];
                    int j = (int)Math.Round(r);
                    var k = ax == 1 ? new Vector2(j, i) : new Vector2(i, j);
                    double t = Math.Clamp(s + (j - r) * d[1 - ax] / l2, 0, 1);
                    var p = a + (float)t * d;
                    double x = pathL[n] + t * l;
                    double e = (k - p).Length;

                    if (e > tolerance)
                        continue;
                    if (Math.Abs(t - 1) < 1e-9 && n + 1 < diff.Count)
                        continue;

                    var point = new LatticePoint(k, x, e, n, t);

                    if (result.Count > 0 && k == result[^1].Pos)
                    {
                        if (e <= result[^1].Error)
                            result[^1] = point;
                    }
                    else
                        result.Add(point);
                }
            }

            if (result.Count == 0)
                result.Add(new LatticePoint(new Vector2(MathF.Round(path[0].X), MathF.Round(path[0].Y)), 0, 0, 0, 0));

            return result;
        }

        private LatticePoint nearestLatticePoint(double pathPosition)
        {
            int l = 0, r = lattice.Count - 1;

            while (r - l > 1)
            {
                int i = (l + r) / 2;
                if (lattice[i].PathPosition > pathPosition) r = i;
                else l = i;
            }

            return Math.Abs(pathPosition - lattice[l].PathPosition) < Math.Abs(pathPosition - lattice[r].PathPosition) ? lattice[l] : lattice[r];
        }

        private double speedAt(double time, double epsilon = 0.01) => (PositionFunction(time + epsilon) - PositionFunction(time)) / epsilon;

        private void generateNeurons()
        {
            const double max_overshot = 32;
            const double epsilon = 0.01;
            const double delta_t = 0.02;

            neurons = new List<Neuron>();
            double actualLength = 0, nucleusTime = 0, nucleusWantedLength = 0;
            int lastDirection = 1;
            var current = new Neuron(lattice[0], 0);

            for (double t = 0; t <= MaxT; t += delta_t)
            {
                double time = Math.Min(t, MaxT);
                double wantedLength = PositionFunction(time);
                double speed = (PositionFunction(time + epsilon) - wantedLength) / epsilon;
                int direction = Math.Sign(speed);
                double velocity = Math.Abs(speed);
                var nearest = nearestLatticePoint(wantedLength);

                // A turn-around of the position function starts a new neuron.
                if (direction * lastDirection < 0 || (direction == 0 && lastDirection != 0))
                {
                    var next = new Neuron(nearest, time);
                    current.Terminal = next;
                    current.WantedLength += actualLength;
                    neurons.Add(current);
                    current = next;
                    nucleusWantedLength = wantedLength;
                    nucleusTime = time - delta_t;
                }

                actualLength = (time - nucleusTime) * Velocity;

                // So does the length error growing too large.
                double lengthError = Math.Abs(Math.Abs(wantedLength - nucleusWantedLength) - actualLength);

                if (lengthError > Math.Max(MinDendriteLength, velocity * max_overshot) || (nearest.Error < 0.05 && lengthError > Math.Max(MinDendriteLength, velocity * MinDendriteLength)))
                {
                    if ((nearest.Pos - current.Nucleus.Pos).LengthSquared > 0.1)
                    {
                        var next = new Neuron(nearest, time);
                        current.Terminal = next;
                        current.WantedLength += actualLength;
                        neurons.Add(current);
                        current = next;
                    }
                    else
                        current.WantedLength += actualLength;

                    nucleusWantedLength = wantedLength;
                    nucleusTime = time;
                }

                lastDirection = direction;
            }

            current.WantedLength += actualLength;
            var last = new Neuron(nearestLatticePoint(PositionFunction(MaxT)), MaxT);
            current.Terminal = last;
            neurons.Add(current);
            neurons.Add(last);

            // Scale to exactly the expected length.
            double total = neurons.Sum(n => n.WantedLength);
            if (total > 0)
            {
                foreach (var n in neurons)
                    n.WantedLength *= MaxS / total;
            }
        }

        private void generateAxons()
        {
            foreach (var neuron in neurons.Where(n => n.Terminal != null))
            {
                var axon = fitPath(neuron.Nucleus.PathPosition, neuron.Terminal!.Nucleus.PathPosition);
                axon[0] = neuron.Nucleus.Pos;
                axon[^1] = neuron.Terminal.Nucleus.Pos;
                neuron.Axon = axon;
                neuron.AxonLength = curveLength(axon);
                neuron.DendriteLength = neuron.WantedLength - neuron.AxonLength;
            }
        }

        // Anchors (with repeated points as red anchors) for the path between two path positions: quadratic beziers between turns of more than 45 degrees.
        private List<Vector2> fitPath(double from, double to)
        {
            int direction = Math.Sign(to - from);
            var splits = new List<double> { from };

            if (direction != 0)
            {
                double turned = 0;
                Vector2? lastDirection = null;
                int first = pathL.BinarySearch(Math.Min(from, to)) is int f && f < 0 ? ~f : f + 1;

                for (int i = first; i < pathL.Count && pathL[i] < Math.Max(from, to); i++)
                {
                    var d = diff[Math.Min(i, diff.Count - 1)].Normalized();

                    if (lastDirection is Vector2 ld)
                        turned += Math.Abs(Math.Atan2(ld.X * d.Y - ld.Y * d.X, Vector2.Dot(ld, d)));

                    lastDirection = d;

                    if (turned > Math.PI / 4)
                    {
                        splits.Add(pathL[i]);
                        turned = 0;
                    }
                }

                if (direction < 0)
                    splits = splits.Take(1).Concat(splits.Skip(1).Reverse()).ToList();
            }

            splits.Add(to);

            var anchors = new List<Vector2>();

            for (int i = 0; i + 1 < splits.Count; i++)
            {
                Vector2 p1 = positionAt(splits[i]), p2 = positionAt(splits[i + 1]);

                // After the first piece this repeats the last point, which makes it a red anchor.
                anchors.Add(p1);

                // Mapping Tools' "double middle": the control point that makes the quadratic pass through the path's middle.
                var average = (p1 + p2) / 2;
                var middle = positionAt((splits[i] + splits[i + 1]) / 2);
                if (Vector2.DistanceSquared(average, middle) >= 0.1f)
                    anchors.Add(average + (middle - average) * 2);

                anchors.Add(p2);
            }

            return anchors;
        }

        // Repeated points split the anchors into bezier pieces.
        private static double curveLength(List<Vector2> anchors)
        {
            double length = 0;
            var piece = new List<Vector2> { anchors[0] };

            for (int i = 1; i < anchors.Count; i++)
            {
                if (anchors[i] == anchors[i - 1])
                {
                    length += bezierLength(piece);
                    piece = new List<Vector2> { anchors[i] };
                }
                else
                    piece.Add(anchors[i]);
            }

            return length + bezierLength(piece);
        }

        private static double bezierLength(List<Vector2> points, int steps = 64)
        {
            double length = 0;
            var last = points[0];

            for (int s = 1; s <= steps; s++)
            {
                var p = bezierAt(points, (float)s / steps);
                length += (p - last).Length;
                last = p;
            }

            return length;
        }

        private static Vector2 bezierAt(List<Vector2> points, float t)
        {
            var work = points.ToArray();

            for (int k = work.Length - 1; k > 0; k--)
            {
                for (int i = 0; i < k; i++)
                    work[i] = work[i] + (work[i + 1] - work[i]) * t;
            }

            return work[0];
        }

        private Vector2 nearbyDirection(int index)
        {
            for (int i = 0; i < 10; i++)
            {
                var d = diff[Math.Clamp(index + i, 0, diff.Count - 1)];
                if (d.LengthSquared > 1e-12)
                    return d;
            }

            return Vector2.UnitX;
        }

        private void generateDendrites()
        {
            double leftovers = 0;

            foreach (var neuron in neurons.Where(n => n.Terminal != null))
            {
                var terminal = neuron.Terminal!;
                int dir = Math.Sign(terminal.Nucleus.PathPosition - neuron.Nucleus.PathPosition);
                dir = dir == 0 ? 1 : dir;
                var dir1 = dir * nearbyDirection(neuron.Nucleus.SegmentIndex).Normalized();
                var dir2 = -dir * nearbyDirection(terminal.Nucleus.SegmentIndex).Normalized();

                double toAdd = neuron.DendriteLength + leftovers;

                // Split the dendrites so the ball passes the middle when the position function does.
                double width = terminal.Time - neuron.Time;
                double axonWidth = neuron.AxonLength / Velocity;
                double middleTime = binarySearch(neuron.Time, terminal.Time, d => PositionFunction(d) <= (neuron.Nucleus.PathPosition + terminal.Nucleus.PathPosition) / 2);
                double leftPortion = Math.Abs(width - axonWidth) < 1e-9 ? 0.5 : Math.Clamp((2 * (middleTime - neuron.Time) - axonWidth) / (2 * (width - axonWidth)), 0, 1);

                double left = toAdd * leftPortion;
                double right = toAdd * (1 - leftPortion);

                double speedLeft = speedAt(neuron.Time + left / Velocity / 2);
                double speedRight = speedAt(terminal.Time - right / Velocity / 2);

                right += addDendrites(neuron, left, dir1, MinDendriteLength, 4 * Math.Pow(speedLeft * 2, 2));
                leftovers = addDendrites(terminal, right, dir2, MinDendriteLength, 4 * Math.Pow(speedRight * 2, 2));
            }
        }

        private static double binarySearch(double low, double high, Func<double, bool> isLow, double precision = 0.01)
        {
            if (high < low)
                (low, high) = (high, low);

            while (high - low > precision)
            {
                double mid = (low + high) / 2;
                if (isLow(mid)) low = mid;
                else high = mid;
            }

            return (low + high) / 2;
        }

        private static double addDendrites(Neuron neuron, double length, Vector2 dir, double minLength, double maxLength)
        {
            while (length > minLength)
            {
                double size = Math.Clamp(Math.Floor(length), Math.Max(minLength, 1), Math.Max(Math.Max(minLength, 1), Math.Min(maxLength, max_dendrite)));
                var dendrite = round(dir * (float)size);

                // Longer than 12 px would show outside the body.
                while (dendrite.Length > max_dendrite)
                {
                    size -= 0.5;
                    dendrite = round(dir * (float)size);
                }

                if (dendrite.Length < 1)
                    dendrite = Vector2.UnitX;

                neuron.Dendrites.Add(dendrite);
                length -= dendrite.Length;
            }

            return length;
        }

        private static Vector2 round(Vector2 v) => new Vector2(MathF.Round(v.X), MathF.Round(v.Y));

        private List<Vector2> anchors()
        {
            var result = new List<Vector2>();

            for (int index = 0; index < neurons.Count; index++)
            {
                var neuron = neurons[index];
                result.Add(neuron.Nucleus.Pos);
                if (index != 0)
                    result.Add(neuron.Nucleus.Pos);

                foreach (var d in neuron.Dendrites)
                {
                    result.Add(neuron.Nucleus.Pos + d);
                    result.Add(neuron.Nucleus.Pos);
                    result.Add(neuron.Nucleus.Pos);
                }

                if (index != neurons.Count - 1)
                    result.AddRange(neuron.Axon.Skip(1).SkipLast(1));
            }

            result.RemoveAt(result.Count - 1);
            return result;
        }
    }
}
