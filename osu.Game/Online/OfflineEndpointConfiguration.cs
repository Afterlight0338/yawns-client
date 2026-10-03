// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online
{
    /// <summary>
    /// YAWNS: works offline. Every endpoint points at a ".invalid" host, which never resolves, so nothing can reach an online service.
    /// </summary>
    public class OfflineEndpointConfiguration : EndpointConfiguration
    {
        public OfflineEndpointConfiguration()
        {
            WebsiteUrl = APIUrl = SpectatorUrl = MultiplayerUrl = MetadataUrl = @"https://offline.invalid";
            BeatmapSubmissionServiceUrl = null; // hides "Submit beatmap" in the editor
            APIClientSecret = APIClientID = string.Empty;
        }
    }
}
