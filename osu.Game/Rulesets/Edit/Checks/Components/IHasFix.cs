// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Edit.Checks.Components
{
    /// <summary>
    /// YAWNS: an issue template whose issues the verify screen can fix with one click.
    /// </summary>
    public interface IHasFix
    {
        /// <summary>
        /// The fix button's text, like "Resnap".
        /// </summary>
        string FixText { get; }

        /// <summary>
        /// Fixes the issue. Changes to <paramref name="beatmap"/> become one undo step.
        /// Changes to the beatmap set's files (through <paramref name="beatmaps"/>) are permanent.
        /// </summary>
        void Fix(Issue issue, EditorBeatmap beatmap, BeatmapManager beatmaps);
    }
}
