// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Screens.Edit.Reference;

namespace osu.Game.Tests.Database
{
    /// <summary>
    /// YAWNS: library search used by the reference beatmap picker.
    /// </summary>
    [TestFixture]
    public class ReferencePickerSearchTest : RealmTest
    {
        [Test]
        public void TestSearch()
        {
            RunTestWithRealm((realmAccess, _) =>
            {
                var realm = realmAccess.Realm;

                var osu = CreateRuleset();
                var mania = new RulesetInfo("mania", "osu!mania", string.Empty, 3) { Available = true };

                BeatmapSetInfo editingSet = null!;

                realm.Write(() =>
                {
                    editingSet = realm.Add(CreateBeatmapSet(osu)); // Kuba Oms - My Love
                    realm.Add(withMetadata(CreateBeatmapSet(osu), "Soleily", "Renatus"));
                    realm.Add(withMetadata(CreateBeatmapSet(mania), "Soleily", "Renatus"));

                    var deleted = withMetadata(CreateBeatmapSet(osu), "Soleily", "Renatus (deleted)");
                    deleted.DeletePending = true;
                    realm.Add(deleted);
                });

                var editing = editingSet.Beatmaps.Single(b => b.DifficultyName == "Easy");

                Assert.That(ReferencePickerPopover.Search(realm, editing, string.Empty).Select(b => b.DifficultyName),
                    Is.EquivalentTo(new[] { "Easy", "Normal", "Hard", "Insane" }), "empty search lists the difficulties of the edited set");

                var renatus = ReferencePickerPopover.Search(realm, editing, "soleily REN").ToArray();
                Assert.That(renatus, Has.Length.EqualTo(4), "all terms must match, case-insensitively; other rulesets and deleted sets are excluded");
                Assert.That(renatus.All(b => b.Metadata.Title == "Renatus" && b.Ruleset.ShortName == "osu"));

                Assert.That(ReferencePickerPopover.Search(realm, editing, "renatus insane").Select(b => b.DifficultyName),
                    Is.EqualTo(new[] { "Insane" }), "difficulty names are searchable");

                Assert.That(ReferencePickerPopover.Search(realm, editing, "love easy").Select(b => b.DifficultyName), Is.EqualTo(new[] { "Easy" }),
                    "the difficulty being edited can be overlaid too, for comparing its own patterns");
                Assert.That(ReferencePickerPopover.Search(realm, editing, "no such beatmap"), Is.Empty);
            });
        }

        private static BeatmapSetInfo withMetadata(BeatmapSetInfo set, string artist, string title)
        {
            var metadata = new BeatmapMetadata { Artist = artist, Title = title };

            foreach (var b in set.Beatmaps)
                b.Metadata = metadata;

            return set;
        }
    }
}
