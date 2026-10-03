// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: the 360/n rule from mapping guides: copies of the selection rotated by 360/n around a centre, each a set number of beats after the last
    /// (kick sliders rotated by 120 degrees, a pentagon of circles by 72, and so on).
    /// Also star steps (k/n of a turn per copy: a pentagram is 5/2), part circles, spirals (a scale per copy) and every other copy mirrored.
    /// </summary>
    public class RadialCopy
    {
        /// <summary>
        /// How many divisions make the full circle. 3 means steps of 120 degrees.
        /// </summary>
        public readonly BindableInt Count = new BindableInt(3) { MinValue = 2, MaxValue = 18 };

        /// <summary>
        /// How many divisions each copy turns further. 1 walks round the circle, 2 with 5 divisions draws a pentagram.
        /// </summary>
        public readonly BindableInt Step = new BindableInt(1) { MinValue = 1, MaxValue = 17 };

        /// <summary>
        /// How many copies to add. Fewer than a full turn gives part of a circle, more keeps going (useful with a scale).
        /// </summary>
        public readonly BindableInt Copies = new BindableInt(2) { MinValue = 1, MaxValue = 36 };

        public readonly BindableBool Anticlockwise = new BindableBool();

        /// <summary>
        /// Size of each copy relative to the one before. Below 1 spirals inwards, above 1 outwards.
        /// </summary>
        public readonly BindableFloat ScalePerCopy = new BindableFloat(1) { MinValue = 0.5f, MaxValue = 1.5f, Precision = 0.01f };

        /// <summary>
        /// Every other copy is mirrored across its own spoke (the line from the centre through it).
        /// </summary>
        public readonly BindableBool AlternateMirror = new BindableBool();

        /// <summary>
        /// Time from one copy to the next, in beats.
        /// </summary>
        public readonly BindableDouble Beats = new BindableDouble(1) { MinValue = 0, MaxValue = 8, Precision = 0.25 };

        /// <summary>
        /// Copies needed to come back to the start: n / gcd(n, k).
        /// </summary>
        public int FullTurn => Count.Value / Symmetry.GreatestCommonDivisor(Count.Value, Step.Value);

        /// <summary>
        /// The star polygon name, {n/k}.
        /// </summary>
        public string Name => Step.Value == 1 ? $"{Count.Value} around" : $"{{{Count.Value}/{Step.Value}}}";

        /// <summary>
        /// New objects: <see cref="Copies"/> transformed copies of <paramref name="selection"/>.
        /// </summary>
        /// <param name="selection">The objects to copy.</param>
        /// <param name="centre">What the copies turn around.</param>
        /// <param name="beatLength">Milliseconds per beat at the selection.</param>
        public List<OsuHitObject> Create(IReadOnlyCollection<OsuHitObject> selection, Vector2 centre, double beatLength)
        {
            var copies = new List<OsuHitObject>();

            if (selection.Count == 0)
                return copies;

            // The selection's own spoke, for the alternate mirror.
            Vector2 centroid = selection.Aggregate(Vector2.Zero, (sum, h) => sum + h.Position) / selection.Count;
            double spoke = centroid == centre ? 0 : AxisFinder.AngleOf(centre, centroid);
            double turn = 360.0 * Step.Value / Count.Value * (Anticlockwise.Value ? -1 : 1);

            for (int j = 1; j <= Copies.Value; j++)
            {
                var map = AlternateMirror.Value && j % 2 == 1 ? Symmetry.Linear.Reflection(spoke) : Symmetry.Linear.IDENTITY;
                map = map.Then(Symmetry.Linear.Scale(MathF.Pow(ScalePerCopy.Value, j))).Then(Symmetry.Linear.Rotation(turn * j));

                copies.AddRange(Transform(selection, map, centre, j * Beats.Value * beatLength));
            }

            return copies;
        }

        /// <summary>
        /// Independent copies of <paramref name="objects"/> with <paramref name="map"/> applied around <paramref name="centre"/>, <paramref name="timeOffset"/> later.
        /// Slider bodies are transformed too, and a scaled slider gets its velocity scaled the same so its duration stays.
        /// </summary>
        public static IEnumerable<OsuHitObject> Transform(IEnumerable<OsuHitObject> objects, Symmetry.Linear map, Vector2 centre, double timeOffset)
        {
            float lengthFactor = map.LengthFactor;

            // A serialisation round trip gives independent copies, same as the editor's own copy and paste.
            foreach (var copy in new ClipboardContent { HitObjects = objects.Cast<Rulesets.Objects.HitObject>().ToList() }.Serialize().Deserialize<ClipboardContent>().HitObjects.Cast<OsuHitObject>())
            {
                copy.StartTime += timeOffset;
                copy.Position = map.ApplyAround(centre, copy.Position);

                if (copy is Slider slider)
                {
                    // Control points are relative to the head, so they only take the linear part.
                    foreach (var point in slider.Path.ControlPoints)
                        point.Position = map.Apply(point.Position);

                    if (Math.Abs(lengthFactor - 1) > 1e-4)
                    {
                        if (slider.Path.ExpectedDistance.Value is double expected)
                            slider.Path.ExpectedDistance.Value = expected * lengthFactor;

                        slider.SliderVelocityMultiplier = Math.Clamp(slider.SliderVelocityMultiplier * lengthFactor, 0.1, 10);
                    }
                }

                yield return copy;
            }
        }
    }
}
