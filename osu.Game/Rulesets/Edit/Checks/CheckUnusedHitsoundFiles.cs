// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;

namespace osu.Game.Rulesets.Edit.Checks
{
    /// <summary>
    /// YAWNS: hitsound files in the set that no difficulty plays (Mapping Tools' Map Cleaner "remove unused samples").
    /// </summary>
    public class CheckUnusedHitsoundFiles : ICheck
    {
        public CheckMetadata Metadata => new CheckMetadata(CheckCategory.Files, "Unused hitsound files", CheckScope.BeatmapSet);

        public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
        {
            new IssueTemplateUnusedHitsound(this),
        };

        public IEnumerable<Issue> Run(BeatmapVerifierContext context)
        {
            var set = context.CurrentDifficulty.Playable.BeatmapInfo.BeatmapSet;

            if (set == null)
                return Enumerable.Empty<Issue>();

            return MapCleaner.UnusedHitsoundFiles(set, context.AllDifficulties.Select(d => d.Playable), context.CurrentDifficulty.Working.Storyboard)
                             .Select(file => new IssueTemplateUnusedHitsound(this).Create(file));
        }

        public class IssueTemplateUnusedHitsound : IssueTemplate, IHasFix
        {
            public IssueTemplateUnusedHitsound(ICheck check)
                : base(check, IssueType.Warning, "\"{0}\" is not played by any difficulty.")
            {
            }

            public Issue Create(string filename) => new Issue(this, filename);

            public string FixText => "Delete";

            public void Fix(Issue issue, EditorBeatmap beatmap, BeatmapManager beatmaps)
            {
                var set = beatmap.BeatmapInfo.BeatmapSet;
                var file = set?.Files.FirstOrDefault(f => f.Filename == (string)issue.Arguments[0]);

                if (set != null && file != null)
                    beatmaps.DeleteFile(set, file);
            }
        }
    }
}
