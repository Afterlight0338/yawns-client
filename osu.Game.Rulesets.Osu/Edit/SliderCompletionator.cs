// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: port of the Slider Completionator from Mapping Tools (https://github.com/OliBomby/Mapping_Tools).
    /// Sets the duration (in beats, or up to the playhead) and length of selected sliders and works out the slider velocity,
    /// or keeps a given velocity and works out the length. A span lasts length * beat length / (100 * slider multiplier * velocity).
    /// </summary>
    public class SliderCompletionator
    {
        public enum FreeVariable
        {
            [Description("Work out the velocity")]
            Velocity,

            [Description("Work out the length")]
            Length,
        }

        public enum DurationSource
        {
            [Description("Keep the duration")]
            Keep,

            [Description("A number of beats")]
            Beats,

            [Description("End at the playhead")]
            Playhead,
        }

        public readonly Bindable<FreeVariable> Free = new Bindable<FreeVariable>();

        public readonly Bindable<DurationSource> Duration = new Bindable<DurationSource>(DurationSource.Beats);

        /// <summary>
        /// Duration of one span (head to tail, or between repeats), in beats.
        /// </summary>
        public readonly BindableDouble Beats = new BindableDouble(1) { MinValue = 0.125, MaxValue = 32, Precision = 0.125 };

        /// <summary>
        /// Length relative to the full length of the slider's control points. 1 uses the whole path as drawn.
        /// </summary>
        public readonly BindableDouble Length = new BindableDouble(1) { MinValue = 0.05, MaxValue = 4, Precision = 0.05 };

        /// <summary>
        /// The velocity to keep when working out the length.
        /// </summary>
        public readonly BindableDouble Velocity = new BindableDouble(1) { MinValue = 0.1, MaxValue = 10, Precision = 0.05 };

        /// <summary>
        /// Scale the control points so the path as drawn is the new length, instead of cutting or extending it.
        /// </summary>
        public readonly BindableBool MoveAnchors = new BindableBool();

        /// <returns>How many sliders changed, and how many of them could not reach the duration because the velocity hit 0.1x or 10x.</returns>
        public (int Completed, int Clamped) Apply(IEnumerable<Slider> sliders, EditorBeatmap beatmap, double playhead)
        {
            int completed = 0, clamped = 0;

            beatmap.BeginChange();

            foreach (var slider in sliders.ToList())
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(slider.StartTime).BeatLength;
                double multiplier = beatmap.Difficulty.SliderMultiplier;

                double span = Duration.Value switch
                {
                    DurationSource.Beats => Beats.Value * beatLength,
                    DurationSource.Playhead => (playhead - slider.StartTime) / slider.SpanCount(),
                    _ => slider.SpanDuration,
                };

                if (span <= 0)
                    continue;

                double fullLength = slider.Path.CalculatedDistance;
                double length = Length.Value * fullLength;
                double velocity;

                if (Free.Value == FreeVariable.Velocity)
                {
                    velocity = length * beatLength / (100 * multiplier * span);

                    if (velocity < 0.1 || velocity > 10)
                    {
                        velocity = Math.Clamp(velocity, 0.1, 10);
                        // Keep the duration: the length gives way.
                        length = 100 * multiplier * velocity * span / beatLength;
                        clamped++;
                    }
                }
                else
                {
                    velocity = Velocity.Value;
                    length = 100 * multiplier * velocity * span / beatLength;
                }

                slider.SliderVelocityMultiplier = velocity;

                if (MoveAnchors.Value && fullLength > 0)
                {
                    float scale = (float)(length / fullLength);

                    foreach (var point in slider.Path.ControlPoints)
                        point.Position *= scale;

                    slider.Path.ExpectedDistance.Value = null;
                }
                else
                    slider.Path.ExpectedDistance.Value = length;

                beatmap.Update(slider);
                completed++;
            }

            beatmap.EndChange();
            return (completed, clamped);
        }
    }
}
