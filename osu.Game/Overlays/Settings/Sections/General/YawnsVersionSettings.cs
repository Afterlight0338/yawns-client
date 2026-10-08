// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Online.Multiplayer;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Updater;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// YAWNS: whether to look for a new YAWNS release when the game starts, and a button to look now.
    /// </summary>
    public partial class YawnsVersionSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "YAWNS version";

        [BackgroundDependencyLoader]
        private void load(YawnsVersionChecker? checker)
        {
            if (checker == null)
                return;

            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = "Check for new versions on start",
                HintText = $"Asks GitHub once per start and shows a notification with a link when there is a newer release. Nothing is downloaded. You have {OsuGameBase.YAWNS_VERSION}.",
                Current = checker.CheckOnStart,
            })
            {
                Keywords = new[] { @"update", @"version", @"release" },
            });

            Add(new SettingsButtonV2
            {
                Text = "Check now",
                Action = () => checker.Check(true).FireAndForget(),
            });
        }
    }
}
