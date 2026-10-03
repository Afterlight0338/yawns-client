// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: settings for <see cref="RadialCopy"/>, with the copies previewed on the playfield.
    /// </summary>
    public partial class RadialCopyPopover : CopyPreviewPopover
    {
        private readonly RadialCopy radialCopy;
        private readonly SymmetryCentre centre;

        // Bound copies, so this popover's callbacks go away with it.
        private readonly BindableInt count = new BindableInt();
        private readonly BindableInt step = new BindableInt();
        private readonly BindableInt copies = new BindableInt();
        private readonly BindableBool anticlockwise = new BindableBool();
        private readonly BindableFloat scale = new BindableFloat();
        private readonly BindableBool alternateMirror = new BindableBool();
        private readonly BindableDouble beats = new BindableDouble();
        private readonly Bindable<SymmetryCentre.Mode> around = new Bindable<SymmetryCentre.Mode>();
        private readonly Bindable<Vector2> guide = new Bindable<Vector2>();

        public RadialCopyPopover(RadialCopy radialCopy, SymmetryCentre centre)
        {
            this.radialCopy = radialCopy;
            this.centre = centre;
        }

        protected override IEnumerable<Drawable> CreateSettings() => new Drawable[]
        {
            new FormSliderBar<int>
            {
                Caption = "Divisions",
                HintText = "The full circle in this many steps: 3 is 120 degrees, 4 is 90, 5 is 72, 6 is 60...",
                Current = radialCopy.Count,
                TabbableContentContainer = this,
            },
            new FormSliderBar<int>
            {
                Caption = "Star step",
                HintText = "How many divisions each copy jumps. 1 walks round the circle; 2 with 5 divisions draws a pentagram, 3 with 7 a sharp star.",
                Current = radialCopy.Step,
                TabbableContentContainer = this,
            },
            new FormSliderBar<int>
            {
                Caption = "Copies",
                HintText = "Resets to one full turn when the divisions or step change. Fewer gives part of a circle, more keeps going (with a scale: a spiral).",
                Current = radialCopy.Copies,
                TabbableContentContainer = this,
            },
            new FormSliderBar<float>
            {
                Caption = "Scale per copy",
                HintText = "Each copy this much the size of the one before, around the centre. Below 1 spirals inwards, above 1 outwards. Sliders keep their duration.",
                Current = radialCopy.ScalePerCopy,
                TabbableContentContainer = this,
            },
            new FormCheckBox { Caption = "Anticlockwise", Current = radialCopy.Anticlockwise },
            new FormCheckBox
            {
                Caption = "Mirror every other copy",
                HintText = "Across its own spoke, for zigzag and back-and-forth patterns.",
                Current = radialCopy.AlternateMirror,
            },
            new FormEnumDropdown<SymmetryCentre.Mode>
            {
                Caption = "Rotate around",
                Current = centre.Around,
            },
            new FormSliderBar<double>
            {
                Caption = "Beats between copies",
                Current = radialCopy.Beats,
                TabbableContentContainer = this,
            },
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            count.BindTo(radialCopy.Count);
            step.BindTo(radialCopy.Step);
            copies.BindTo(radialCopy.Copies);
            anticlockwise.BindTo(radialCopy.Anticlockwise);
            scale.BindTo(radialCopy.ScalePerCopy);
            alternateMirror.BindTo(radialCopy.AlternateMirror);
            beats.BindTo(radialCopy.Beats);
            around.BindTo(centre.Around);
            guide.BindTo(centre.Guide);

            step.MaxValue = count.Value - 1;
            count.BindValueChanged(c =>
            {
                step.MaxValue = c.NewValue - 1;
                copies.Value = radialCopy.FullTurn - 1;
                UpdatePreview();
            });
            step.BindValueChanged(_ =>
            {
                copies.Value = radialCopy.FullTurn - 1;
                UpdatePreview();
            });
            copies.BindValueChanged(_ => UpdatePreview());
            anticlockwise.BindValueChanged(_ => UpdatePreview());
            scale.BindValueChanged(_ => UpdatePreview());
            alternateMirror.BindValueChanged(_ => UpdatePreview());
            beats.BindValueChanged(_ => UpdatePreview());
            around.BindValueChanged(_ => UpdatePreview());
            guide.BindValueChanged(_ => UpdatePreview());
        }

        protected override List<OsuHitObject> CreateCopies()
        {
            double beatLength = EditorBeatmap.ControlPointInfo.TimingPointAt(Original.Min(h => h.StartTime)).BeatLength;
            return radialCopy.Create(Original, centre.Resolve(Original), beatLength);
        }

        protected override string Describe()
        {
            int gcd = Symmetry.GreatestCommonDivisor(radialCopy.Count.Value, radialCopy.Step.Value);
            return gcd == 1 ? radialCopy.Name : $"{radialCopy.Name}: back at the start after {radialCopy.FullTurn}";
        }
    }
}
