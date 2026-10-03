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
    /// YAWNS: random variation for sliders: each turned around its head by a random angle, or its shape jittered.
    /// A result that would leave the playfield is retried a few times, then the slider is left as it was.
    /// </summary>
    public static class SliderRandomiser
    {
        public const int ATTEMPTS = 8;

        /// <summary>
        /// Control point positions (relative to the head) of the path turned by <paramref name="degrees"/> around the head.
        /// </summary>
        public static Vector2[] Rotated(IReadOnlyList<Vector2> points, double degrees)
        {
            var map = Symmetry.Linear.Rotation(degrees);
            return points.Select(map.Apply).ToArray();
        }

        /// <summary>
        /// Control point positions with each one but the first moved by up to <paramref name="pixels"/> in a random direction.
        /// </summary>
        public static Vector2[] Jittered(IReadOnlyList<Vector2> points, double pixels, Random random)
        {
            var result = points.ToArray();

            for (int i = 1; i < result.Length; i++)
            {
                double angle = random.NextDouble() * 2 * Math.PI;
                double distance = Math.Sqrt(random.NextDouble()) * pixels;
                result[i] += new Vector2((float)(Math.Cos(angle) * distance), (float)(Math.Sin(angle) * distance));
            }

            return result;
        }

        /// <summary>
        /// Turns each slider by a random angle within +-<paramref name="maxDegrees"/> around its head. Returns how many changed.
        /// </summary>
        public static int RotateRandomly<T>(IEnumerable<T> sliders, double maxDegrees, Random random, Action<T> changed)
            where T : HitObject, IHasPath, IHasPosition =>
            apply(sliders, changed, original => Rotated(original, (random.NextDouble() * 2 - 1) * maxDegrees));

        /// <summary>
        /// Jitters each slider's shape by up to <paramref name="pixels"/>. Returns how many changed.
        /// </summary>
        public static int JitterShapes<T>(IEnumerable<T> sliders, double pixels, Random random, Action<T> changed)
            where T : HitObject, IHasPath, IHasPosition =>
            apply(sliders, changed, original => Jittered(original, pixels, random));

        private static int apply<T>(IEnumerable<T> sliders, Action<T> changed, Func<Vector2[], Vector2[]> candidate)
            where T : HitObject, IHasPath, IHasPosition
        {
            int count = 0;

            foreach (var slider in sliders)
            {
                var points = slider.Path.ControlPoints;
                var original = points.Select(p => p.Position).ToArray();
                bool done = false;

                for (int attempt = 0; attempt < ATTEMPTS && !done; attempt++)
                {
                    var next = candidate(original);

                    for (int i = 0; i < next.Length; i++)
                        points[i].Position = next[i];

                    done = slider.Path.Distance > 1
                           && next.All(p => inPlayfield(slider.Position + p))
                           && inPlayfield(slider.Position + slider.Path.PositionAt(1));
                }

                if (done)
                {
                    changed(slider);
                    count++;
                }
                else
                {
                    for (int i = 0; i < original.Length; i++)
                        points[i].Position = original[i];
                }
            }

            return count;
        }

        private static bool inPlayfield(Vector2 p) => p.X >= 0 && p.X <= 512 && p.Y >= 0 && p.Y <= 384;
    }
}
