// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: Bezier curve maths for the slider editor.
    /// </summary>
    public static class BezierTools
    {
        /// <summary>
        /// Degree elevation: the same Bezier curve described with one more control point (n+1 points become n+2).
        /// The ends stay put and each point in between is Q_i = i/(n+1) * P_(i-1) + (1 - i/(n+1)) * P_i, where n is the old degree.
        /// </summary>
        public static Vector2[] ElevateDegree(IReadOnlyList<Vector2> points)
        {
            if (points.Count < 2)
                throw new ArgumentException("A Bezier curve needs at least two control points.", nameof(points));

            int n = points.Count - 1;
            var elevated = new Vector2[n + 2];

            elevated[0] = points[0];
            elevated[n + 1] = points[n];

            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)(n + 1);
                elevated[i] = t * points[i - 1] + (1 - t) * points[i];
            }

            return elevated;
        }

        /// <summary>
        /// The point on the curve at <paramref name="t"/> (0 to 1), by de Casteljau's algorithm.
        /// </summary>
        public static Vector2 Evaluate(IReadOnlyList<Vector2> points, float t)
        {
            var work = new Vector2[points.Count];

            for (int i = 0; i < work.Length; i++)
                work[i] = points[i];

            for (int level = work.Length - 1; level > 0; level--)
            {
                for (int i = 0; i < level; i++)
                    work[i] = work[i] + (work[i + 1] - work[i]) * t;
            }

            return work[0];
        }

        /// <summary>
        /// Bernstein basis polynomial B(i, n)(t): how much control point i of a degree n Bezier curve counts at t.
        /// </summary>
        public static float Bernstein(int n, int i, float t)
        {
            double binomial = 1;

            for (int k = 1; k <= i; k++)
                binomial = binomial * (n - k + 1) / k;

            return (float)(binomial * Math.Pow(t, i) * Math.Pow(1 - t, n - i));
        }

        /// <summary>
        /// The parameter of the point on the curve nearest <paramref name="target"/>, and how far that point is, found by sampling.
        /// </summary>
        public static (float T, float Distance) ClosestT(IReadOnlyList<Vector2> points, Vector2 target, int steps = 200)
        {
            float bestT = 0, bestDistance = float.MaxValue;

            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float distance = Vector2.Distance(Evaluate(points, t), target);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestT = t;
                }
            }

            return (bestT, bestDistance);
        }

        /// <summary>
        /// How far to move each control point so the curve point at <paramref name="t"/> moves by exactly <paramref name="delta"/>,
        /// each point in proportion to its Bernstein weight there (direct curve editing). The first point stays put.
        /// </summary>
        public static Vector2[] DragOffsets(int pointCount, float t, Vector2 delta)
        {
            int n = pointCount - 1;
            var offsets = new Vector2[pointCount];
            float sumOfSquares = 0;

            for (int i = 1; i <= n; i++)
                sumOfSquares += Bernstein(n, i, t) * Bernstein(n, i, t);

            if (sumOfSquares < 1e-9f)
                return offsets;

            for (int i = 1; i <= n; i++)
                offsets[i] = delta * (Bernstein(n, i, t) / sumOfSquares);

            return offsets;
        }
    }
}
