// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: maths shared by the symmetry tools (radial copy, mirror copy, radial guide, perfect it).
    /// Angles are in degrees and match <see cref="Utils.GeometryUtils.RotatePointAroundOrigin"/>: positive turns clockwise on the playfield (y points down).
    /// </summary>
    public static class Symmetry
    {
        /// <summary>
        /// A linear map of the plane (2x2 matrix): rotations, reflections, scales and their combinations.
        /// </summary>
        public readonly record struct Linear(float M11, float M12, float M21, float M22)
        {
            public static readonly Linear IDENTITY = new Linear(1, 0, 0, 1);

            public static Linear Rotation(double degrees)
            {
                float c = (float)Math.Cos(double.DegreesToRadians(degrees));
                float s = (float)Math.Sin(double.DegreesToRadians(degrees));
                return new Linear(c, -s, s, c);
            }

            /// <summary>
            /// Reflection across a line through the origin at <paramref name="axisDegrees"/>.
            /// </summary>
            public static Linear Reflection(double axisDegrees)
            {
                float c = (float)Math.Cos(double.DegreesToRadians(2 * axisDegrees));
                float s = (float)Math.Sin(double.DegreesToRadians(2 * axisDegrees));
                return new Linear(c, s, s, -c);
            }

            public static Linear Scale(float factor) => new Linear(factor, 0, 0, factor);

            public Vector2 Apply(Vector2 v) => new Vector2(M11 * v.X + M12 * v.Y, M21 * v.X + M22 * v.Y);

            /// <summary>
            /// This map followed by <paramref name="next"/>.
            /// </summary>
            public Linear Then(Linear next) => new Linear(
                next.M11 * M11 + next.M12 * M21, next.M11 * M12 + next.M12 * M22,
                next.M21 * M11 + next.M22 * M21, next.M21 * M12 + next.M22 * M22);

            /// <summary>
            /// How much lengths change (1 for rotations and reflections).
            /// </summary>
            public float LengthFactor => MathF.Sqrt(MathF.Abs(M11 * M22 - M12 * M21));

            /// <summary>
            /// Applies the map around <paramref name="centre"/> to a position.
            /// </summary>
            public Vector2 ApplyAround(Vector2 centre, Vector2 position) => centre + Apply(position - centre);
        }

        public static int GreatestCommonDivisor(int a, int b) => b == 0 ? Math.Abs(a) : GreatestCommonDivisor(b, a % b);
    }
}
