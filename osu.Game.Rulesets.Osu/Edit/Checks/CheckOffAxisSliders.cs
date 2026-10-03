// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.Checks
{
    /// <summary>
    /// YAWNS: straight sliders off the map's own axes (from the axis guide's tilt detection).
    /// Negligible on purpose: breaking axis is a valid way to emphasise, so these are only hints, hidden unless shown.
    /// </summary>
    public class CheckOffAxisSliders : ICheck
    {
        /// <summary>
        /// Degrees off the nearest base axis before a slider is mentioned.
        /// </summary>
        public const double TOLERANCE = 8;

        public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Sliders off the map's axis");

        public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
        {
            new IssueTemplateOffAxis(this)
        };

        public IEnumerable<Issue> Run(BeatmapVerifierContext context)
        {
            var sliders = context.CurrentDifficulty.Playable.HitObjects.OfType<Slider>().ToList();

            if (AxisFinder.DetectTilt(sliders) is not double tilt)
                yield break;

            foreach (var slider in sliders.Where(AxisFinder.IsStraight))
            {
                double off = Math.Abs(AxisFinder.RotationToNearestAxis(AxisFinder.AngleOf(slider.Position, slider.EndPosition), tilt));

                if (off > TOLERANCE)
                    yield return new IssueTemplateOffAxis(this).Create(slider, off, tilt);
            }
        }

        public class IssueTemplateOffAxis : IssueTemplate, IHasFix
        {
            public IssueTemplateOffAxis(ICheck check)
                : base(check, IssueType.Negligible, "Straight slider {0:0}° off the map's axes (tilt {1:0}°). Fine if it breaks axis on purpose.")
            {
            }

            public Issue Create(Slider slider, double off, double tilt) => new Issue(slider, this, off, tilt);

            public string FixText => "Align";

            // Turns the slider around its head onto the nearest base axis.
            public void Fix(Issue issue, EditorBeatmap beatmap, BeatmapManager beatmaps)
            {
                var slider = (Slider)issue.HitObjects.Single();
                double tilt = (double)issue.Arguments[1];
                float angle = MathHelper.DegreesToRadians((float)AxisFinder.RotationToNearestAxis(AxisFinder.AngleOf(slider.Position, slider.EndPosition), tilt));
                float cos = MathF.Cos(angle), sin = MathF.Sin(angle);

                foreach (var point in slider.Path.ControlPoints)
                    point.Position = new Vector2(point.Position.X * cos - point.Position.Y * sin, point.Position.X * sin + point.Position.Y * cos);

                beatmap.Update(slider);
            }
        }
    }
}
