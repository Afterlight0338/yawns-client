// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vector2 = osuTK.Vector2;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: "perfect it": moves roughly placed objects onto the exact shape they were going for. All fits are least squares in closed form
    /// (points are complex numbers, so a rotation is a multiplication), and the turning direction with the smaller error wins.
    /// </summary>
    public static class PerfectIt
    {
        /// <param name="Moved">How far the points moved on average (root mean square), in playfield pixels: how rough the original was.</param>
        public readonly record struct Result(Vector2[] Positions, double Moved);

        /// <summary>
        /// Fits a regular polygon {n/k}: point i goes to the vertex i*k steps round (a pentagram is n = 5, k = 2). With fewer points than n it is part of the polygon.
        /// Centre, radius and starting angle are free.
        /// </summary>
        public static Result FitPolygon(IReadOnlyList<Vector2> points, int n, int k)
        {
            if (points.Count < 3 || n < 3 || k < 1)
                return unchanged(points);

            Result? best = null;

            foreach (int direction in new[] { 1, -1 })
            {
                var a = Enumerable.Range(0, points.Count).Select(i => Complex.FromPolarCoordinates(1, direction * 2 * Math.PI * k * i / n)).ToArray();
                var z = points.Select(toComplex).ToArray();

                Complex zMean = mean(z), aMean = mean(a);
                double denominator = a.Sum(x => (x - aMean).Magnitude * (x - aMean).Magnitude);

                if (denominator < 1e-9)
                    continue;

                Complex w = z.Zip(a).Aggregate(Complex.Zero, (sum, p) => sum + (p.First - zMean) * Complex.Conjugate(p.Second - aMean)) / denominator;
                Complex centre = zMean - w * aMean;

                var fitted = a.Select(x => fromComplex(centre + w * x)).ToArray();
                var result = new Result(fitted, rms(points, fitted));

                if (best == null || result.Moved < best.Value.Moved)
                    best = result;
            }

            return best ?? unchanged(points);
        }

        /// <summary>
        /// Fits n-fold rotational symmetry: the points are <paramref name="folds"/> consecutive groups, each the first group turned by 360/folds more
        /// (what radial copy makes). The first group becomes the average of all groups turned back, and the centre is where the groups balance.
        /// </summary>
        public static Result FitRotational(IReadOnlyList<Vector2> points, int folds)
        {
            if (folds < 2 || points.Count < folds || points.Count % folds != 0)
                return unchanged(points);

            int m = points.Count / folds;
            var z = points.Select(toComplex).ToArray();
            Complex centre = mean(z);

            Result? best = null;

            foreach (int direction in new[] { 1, -1 })
            {
                var turn = Enumerable.Range(0, folds).Select(g => Complex.FromPolarCoordinates(1, direction * 2 * Math.PI * g / folds)).ToArray();

                var offsets = Enumerable.Range(0, m)
                                        .Select(p => Enumerable.Range(0, folds).Aggregate(Complex.Zero, (sum, g) => sum + (z[g * m + p] - centre) * Complex.Conjugate(turn[g])) / folds)
                                        .ToArray();

                var fitted = Enumerable.Range(0, points.Count).Select(i => fromComplex(centre + offsets[i % m] * turn[i / m])).ToArray();
                var result = new Result(fitted, rms(points, fitted));

                if (best == null || result.Moved < best.Value.Moved)
                    best = result;
            }

            return best!.Value;
        }

        /// <summary>
        /// Fits a mirror pair: the second half of the points is the first half reflected across a line. Point i pairs with point i + half.
        /// The line is perpendicular to the pairs' average direction and through their average midpoint.
        /// </summary>
        public static Result FitMirror(IReadOnlyList<Vector2> points)
        {
            if (points.Count < 2 || points.Count % 2 != 0)
                return unchanged(points);

            int m = points.Count / 2;
            Vector2 sumDirection = Vector2.Zero;

            for (int i = 0; i < m; i++)
            {
                Vector2 d = points[i + m] - points[i];

                if (d.LengthSquared > 1e-6f)
                    sumDirection += d.Normalized();
            }

            if (sumDirection.LengthSquared < 1e-6f)
                return unchanged(points);

            Vector2 normal = sumDirection.Normalized();
            float offset = Enumerable.Range(0, m).Average(i => Vector2.Dot((points[i] + points[i + m]) / 2, normal));

            Vector2 reflect(Vector2 p) => p - 2 * (Vector2.Dot(p, normal) - offset) * normal;

            var fitted = new Vector2[points.Count];

            for (int i = 0; i < m; i++)
            {
                Vector2 average = (points[i] + reflect(points[i + m])) / 2;
                fitted[i] = average;
                fitted[i + m] = reflect(average);
            }

            return new Result(fitted, rms(points, fitted));
        }

        private static Result unchanged(IReadOnlyList<Vector2> points) => new Result(points.ToArray(), 0);

        private static Complex toComplex(Vector2 v) => new Complex(v.X, v.Y);

        private static Vector2 fromComplex(Complex c) => new Vector2((float)c.Real, (float)c.Imaginary);

        private static Complex mean(IReadOnlyCollection<Complex> values) => values.Aggregate(Complex.Zero, (sum, v) => sum + v) / values.Count;

        /// <summary>
        /// Root mean square distance between matching points.
        /// </summary>
        private static double rms(IReadOnlyList<Vector2> from, IReadOnlyList<Vector2> to) =>
            Math.Sqrt(Enumerable.Range(0, from.Count).Average(i => (double)Vector2.DistanceSquared(from[i], to[i])));
    }
}
