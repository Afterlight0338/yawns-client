// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Edit;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: the 360/n rule from mapping guides: copies of the selection rotated by 360/n around a centre, each a set number of beats after the last
    /// (kick sliders rotated by 120 degrees, a pentagon of circles by 72, and so on).
    /// </summary>
    public class RadialCopy
    {
        public enum Centre
        {
            [Description("Playfield centre")]
            Playfield,

            [Description("Selection centre")]
            Selection,
        }

        /// <summary>
        /// How many copies make the full circle, the selection included. 3 means rotated by 120 and 240 degrees.
        /// </summary>
        public readonly BindableInt Count = new BindableInt(3) { MinValue = 2, MaxValue = 18 };

        public readonly Bindable<Centre> Around = new Bindable<Centre>();

        /// <summary>
        /// Time from one copy to the next, in beats.
        /// </summary>
        public readonly BindableDouble Beats = new BindableDouble(1) { MinValue = 0, MaxValue = 8, Precision = 0.25 };

        /// <summary>
        /// New objects: <see cref="Count"/> - 1 rotated copies of <paramref name="selection"/>.
        /// </summary>
        /// <param name="selection">The objects to copy.</param>
        /// <param name="beatLength">Milliseconds per beat at the selection.</param>
        public List<OsuHitObject> Create(IReadOnlyCollection<OsuHitObject> selection, double beatLength)
        {
            if (selection.Count == 0)
                return new List<OsuHitObject>();

            Vector2 centre = Around.Value == Centre.Playfield
                ? OsuPlayfield.BASE_SIZE / 2
                : (new Vector2(selection.Min(h => h.Position.X), selection.Min(h => h.Position.Y))
                   + new Vector2(selection.Max(h => h.Position.X), selection.Max(h => h.Position.Y))) / 2;

            var copies = new List<OsuHitObject>();

            for (int k = 1; k < Count.Value; k++)
            {
                float angle = 360f * k / Count.Value;
                double offset = k * Beats.Value * beatLength;

                // A serialisation round trip gives independent copies, same as the editor's own copy and paste.
                foreach (var copy in new ClipboardContent { HitObjects = selection.Cast<Rulesets.Objects.HitObject>().ToList() }.Serialize().Deserialize<ClipboardContent>().HitObjects.Cast<OsuHitObject>())
                {
                    copy.StartTime += offset;
                    copy.Position = GeometryUtils.RotatePointAroundOrigin(copy.Position, centre, angle);

                    if (copy is Slider slider)
                    {
                        foreach (var point in slider.Path.ControlPoints)
                            point.Position = GeometryUtils.RotatePointAroundOrigin(point.Position, Vector2.Zero, angle);
                    }

                    copies.Add(copy);
                }
            }

            return copies;
        }
    }
}
