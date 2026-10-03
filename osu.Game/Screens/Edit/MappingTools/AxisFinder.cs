// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: finds a map's axis tilt, the easy version of "axis" from mapping guides.
    /// Mappers tilt straight sliders a few degrees off the playfield's X/Y axes. With one tilt θ that gives four base axes:
    /// θ, -θ, 90+θ and 90-θ (mirroring with ctrl+H/J and rotating by 90 stays on them).
    /// A slider's axis is the line from its head to its tail; between circles the axis is implied by the line joining them.
    /// </summary>
    public static class AxisFinder
    {
        /// <summary>
        /// Used when a map has too few straight sliders to tell.
        /// </summary>
        public const double DEFAULT_TILT = 10;

        /// <summary>
        /// Sliders bending further than this (relative to their head to tail distance) are not straight enough to show an axis.
        /// </summary>
        public const double MAX_BEND = 0.1;

        /// <summary>
        /// The tilt (0 to 45 degrees) most straight sliders in <paramref name="hitObjects"/> share, or null with fewer than 3 straight sliders.
        /// </summary>
        public static double? DetectTilt(IEnumerable<HitObject> hitObjects)
        {
            double[] tilts = hitObjects.OfType<IHasPath>()
                                       .Where(IsStraight)
                                       .Select(s => TiltOf(AngleOf(s.Path.PositionAt(0), s.Path.PositionAt(1))))
                                       .ToArray();

            if (tilts.Length < 3)
                return null;

            // Most common tilt in whole degrees, counting neighbours too so 9.6 and 10.4 agree.
            int[] counts = new int[46];
            foreach (double t in tilts)
                counts[(int)Math.Round(t)]++;

            int best = Enumerable.Range(0, 46).MaxBy(d => (d > 0 ? counts[d - 1] : 0) + 2 * counts[d] + (d < 45 ? counts[d + 1] : 0));

            // Refine to the average of the tilts near the peak.
            return tilts.Where(t => Math.Abs(t - best) <= 1.5).Average();
        }

        /// <summary>
        /// The four base axis angles in degrees for a tilt.
        /// </summary>
        public static double[] BaseAxes(double tilt) => new[] { tilt, -tilt, 90 + tilt, 90 - tilt };

        /// <summary>
        /// Direction of the line from <paramref name="from"/> to <paramref name="to"/>, in degrees (screen coordinates, y down).
        /// </summary>
        public static double AngleOf(Vector2 from, Vector2 to) => MathHelper.RadiansToDegrees(Math.Atan2(to.Y - from.Y, to.X - from.X));

        /// <summary>
        /// How far a line at <paramref name="angle"/> is tilted from the nearest playfield axis, 0 to 45 degrees.
        /// </summary>
        public static double TiltOf(double angle)
        {
            double x = mod(angle, 90);
            return Math.Min(x, 90 - x);
        }

        /// <summary>
        /// Signed degrees to rotate a line at <paramref name="angle"/> by to land on the nearest base axis.
        /// </summary>
        public static double RotationToNearestAxis(double angle, double tilt)
        {
            // Lines have no direction, so compare modulo 180, and the base axes repeat every 90 within that.
            double best = double.MaxValue;

            foreach (double axis in BaseAxes(tilt))
            {
                double delta = mod(axis - angle + 45, 90) - 45;

                if (Math.Abs(delta) < Math.Abs(best))
                    best = delta;
            }

            return best;
        }

        /// <summary>
        /// Whether a slider is straight enough to show an axis (bends less than <see cref="MAX_BEND"/> of its head to tail distance).
        /// </summary>
        public static bool IsStraight(IHasPath slider)
        {
            var path = new List<Vector2>();
            slider.Path.GetPathToProgress(path, 0, 1);

            Vector2 head = path[0];
            Vector2 tail = path[^1];
            float length = Vector2.Distance(head, tail);

            if (length < 10)
                return false;

            Vector2 normal = (tail - head).PerpendicularLeft / length;
            return path.Max(p => Math.Abs(Vector2.Dot(p - head, normal))) <= MAX_BEND * length;
        }

        private static double mod(double value, double m) => (value % m + m) % m;
    }
}
