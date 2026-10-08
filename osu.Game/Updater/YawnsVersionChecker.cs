// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Game.Online.Multiplayer;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Updater
{
    /// <summary>
    /// YAWNS: the only thing YAWNS does online. Once per start (Release builds), it asks GitHub for the latest yawns-client release
    /// and posts a notification that opens the release page when it is newer than <see cref="OsuGameBase.YAWNS_VERSION"/>.
    /// It never downloads or installs anything, and stays silent when offline.
    /// </summary>
    public partial class YawnsVersionChecker : Component
    {
        public const string RELEASES_PAGE = "https://github.com/Afterlight0338/yawns-client/releases/latest";

        private const string latest_release_api = "https://api.github.com/repos/Afterlight0338/yawns-client/releases/latest";

        private const string settings_file = "yawns-settings.json";

        /// <summary>
        /// Whether to check when the game starts (kept in YAWNS' own settings file, not the game.ini it shares with lazer).
        /// </summary>
        public readonly BindableBool CheckOnStart = new BindableBool(true);

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            try
            {
                if (storage.Exists(settings_file))
                {
                    using var reader = new StreamReader(storage.GetStream(settings_file));
                    var settings = JsonConvert.DeserializeObject<Dictionary<string, bool>>(reader.ReadToEnd());

                    if (settings != null && settings.TryGetValue("checkForNewVersions", out bool check))
                        CheckOnStart.Value = check;
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Could not read {settings_file}: {e.Message}");
            }

            CheckOnStart.BindValueChanged(_ => saveSettings());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (CheckOnStart.Value && !DebugUtils.IsDebugBuild)
                Check(false).FireAndForget();
        }

        private void saveSettings()
        {
            try
            {
                using var stream = storage.CreateFileSafely(settings_file);
                using var writer = new StreamWriter(stream);
                writer.Write(JsonConvert.SerializeObject(new Dictionary<string, bool> { ["checkForNewVersions"] = CheckOnStart.Value }));
            }
            catch (Exception e)
            {
                Logger.Log($"Could not save {settings_file}: {e.Message}");
            }
        }

        /// <summary>
        /// Asks GitHub for the latest release.
        /// </summary>
        /// <param name="manual">Also tell the user when they are up to date or the check failed (the settings button).</param>
        public async Task Check(bool manual)
        {
            string? latest = null;

            try
            {
                var request = new OsuJsonWebRequest<GitHubRelease>(latest_release_api);
                await request.PerformAsync().ConfigureAwait(false);
                latest = request.ResponseObject?.TagName;
            }
            catch (Exception e)
            {
                Logger.Log($"Could not check for a new YAWNS version: {e.Message}");
            }

            Schedule(() =>
            {
                if (IsNewer(latest, OsuGameBase.YAWNS_VERSION))
                {
                    notifications?.Post(new SimpleNotification
                    {
                        Text = $"YAWNS {latest!.TrimStart('v')} is out (you have {OsuGameBase.YAWNS_VERSION}). Click to open the release page.",
                        Icon = FontAwesome.Solid.Download,
                        Activated = () =>
                        {
                            game?.OpenUrlExternally(RELEASES_PAGE);
                            return true;
                        },
                    });
                }
                else if (manual)
                {
                    notifications?.Post(new SimpleNotification
                    {
                        Text = latest == null ? "Could not reach GitHub to check for a new YAWNS version." : $"YAWNS {OsuGameBase.YAWNS_VERSION} is the latest version.",
                    });
                }
            });
        }

        /// <summary>
        /// Whether a release tag (v6769.004) is a newer version than <paramref name="current"/> (6769.003). Unreadable tags are never newer.
        /// </summary>
        public static bool IsNewer(string? tag, string current)
        {
            if (parse(tag) is not int[] latest || parse(current) is not int[] mine)
                return false;

            for (int i = 0; i < Math.Max(latest.Length, mine.Length); i++)
            {
                int a = i < latest.Length ? latest[i] : 0;
                int b = i < mine.Length ? mine[i] : 0;

                if (a != b)
                    return a > b;
            }

            return false;

            static int[]? parse(string? version)
            {
                if (string.IsNullOrWhiteSpace(version))
                    return null;

                string[] parts = version.Trim().TrimStart('v', 'V').Split('.');
                var numbers = parts.Select(p => int.TryParse(p, out int n) ? n : (int?)null).ToArray();

                return numbers.All(n => n != null) ? numbers.Select(n => n!.Value).ToArray() : null;
            }
        }
    }
}
