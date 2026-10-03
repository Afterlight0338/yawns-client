// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: flips the selection across a line at any angle through a centre (in place), or keeps the original and adds the mirrored copy.
    /// </summary>
    public class AngledFlip
    {
        /// <summary>
        /// The mirror line's angle in degrees. 0 is horizontal (a vertical flip), 90 is vertical (a horizontal flip).
        /// </summary>
        public readonly BindableFloat Angle = new BindableFloat(45) { MinValue = 0, MaxValue = 360, Precision = 0.5f };

        public readonly BindableBool Anticlockwise = new BindableBool();

        /// <summary>
        /// Keep the selection and add the mirrored copy instead of flipping in place.
        /// </summary>
        public readonly BindableBool KeepOriginal = new BindableBool();

        /// <summary>
        /// Time from the original to the mirrored copy in beats, only used when <see cref="KeepOriginal"/> is set.
        /// </summary>
        public readonly BindableDouble Beats = new BindableDouble(1) { MinValue = 0, MaxValue = 8, Precision = 0.25 };

        /// <summary>
        /// Where the mirror line is drawn while the popover is open, null when it is closed.
        /// </summary>
        public readonly Bindable<Vector2?> GuideCentre = new Bindable<Vector2?>();

        public double LineAngle => Angle.Value * (Anticlockwise.Value ? -1 : 1);

        public List<OsuHitObject> Create(IReadOnlyCollection<OsuHitObject> selection, Vector2 centre, double beatLength) =>
            new List<OsuHitObject>(RadialCopy.Transform(selection, Symmetry.Linear.Reflection(LineAngle), centre, KeepOriginal.Value ? Beats.Value * beatLength : 0));
    }
}
