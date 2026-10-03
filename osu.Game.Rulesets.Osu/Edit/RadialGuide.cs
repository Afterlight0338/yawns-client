// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: a radial guide: spokes (the full circle in n steps) and rings around a centre that can be dragged, with snapping to them.
    /// The centre lives in <see cref="SymmetryCentre.Guide"/> so radial copy and the other symmetry tools can turn around it.
    /// </summary>
    public class RadialGuide
    {
        public readonly BindableBool Enabled = new BindableBool();

        /// <summary>
        /// How many spokes: the full circle in this many steps. 6 is every 60 degrees.
        /// </summary>
        public readonly BindableInt Spokes = new BindableInt(6) { MinValue = 2, MaxValue = 36 };

        /// <summary>
        /// Where the first spoke points, in degrees clockwise from the right.
        /// </summary>
        public readonly BindableFloat Offset = new BindableFloat(0) { MinValue = 0, MaxValue = 360, Precision = 0.5f };

        public readonly BindableInt Rings = new BindableInt(4) { MinValue = 0, MaxValue = 8 };

        /// <summary>
        /// Distance between rings in playfield pixels.
        /// </summary>
        public readonly BindableFloat RingSpacing = new BindableFloat(60) { MinValue = 10, MaxValue = 200, Precision = 1 };

        public readonly SymmetryCentre Centre;

        public RadialGuide(SymmetryCentre centre)
        {
            Centre = centre;
        }

        /// <summary>
        /// The direction of spoke <paramref name="index"/> as a unit vector (y points down, so positive angles turn clockwise).
        /// </summary>
        public Vector2 SpokeDirection(int index)
        {
            double angle = double.DegreesToRadians(Offset.Value + 360.0 * index / Spokes.Value);
            return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        /// <summary>
        /// Where spokes cross rings, and the centre.
        /// </summary>
        public IEnumerable<Vector2> Points()
        {
            Vector2 centre = Centre.Guide.Value;

            yield return centre;

            for (int s = 0; s < Spokes.Value; s++)
            {
                for (int r = 1; r <= Rings.Value; r++)
                    yield return centre + SpokeDirection(s) * (r * RingSpacing.Value);
            }
        }

        /// <summary>
        /// The snapped position: a crossing or the centre within <paramref name="radius"/>, otherwise the nearest place on a spoke or ring within it.
        /// </summary>
        public Vector2? Snap(Vector2 position, float radius)
        {
            if (!Enabled.Value)
                return null;

            Vector2 centre = Centre.Guide.Value;

            var point = Points().Select(p => (Vector2?)p).MinBy(p => Vector2.DistanceSquared(p!.Value, position));

            if (point is Vector2 nearestPoint && Vector2.Distance(nearestPoint, position) <= radius)
                return nearestPoint;

            var candidates = new List<Vector2>();

            // A spoke is a ray: it only exists on its own side of the centre.
            for (int s = 0; s < Spokes.Value; s++)
            {
                Vector2 direction = SpokeDirection(s);
                candidates.Add(centre + direction * Math.Max(0, Vector2.Dot(position - centre, direction)));
            }

            Vector2 offset = position - centre;

            if (offset.LengthSquared > 1e-6f)
            {
                for (int r = 1; r <= Rings.Value; r++)
                    candidates.Add(centre + offset.Normalized() * (r * RingSpacing.Value));
            }

            var nearest = candidates.Select(p => (Vector2?)p).MinBy(p => Vector2.DistanceSquared(p!.Value, position));
            return nearest is Vector2 n && Vector2.Distance(n, position) <= radius ? n : null;
        }
    }
}
