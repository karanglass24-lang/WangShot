using System;

namespace Greenshot.Helpers
{
    /// <summary>
    /// 왕샷: the name, version and places of the app. Change Version for a new release (and the version in installer\wangshot.iss).
    /// </summary>
    public static class WangShotInfo
    {
        public const string Name = "왕샷";

        public const string Version = "1.1.0";

        public static Version CurrentVersion => System.Version.Parse(Version);

        public const string Repository = "karanglass24-lang/WangShot";

        public static Uri LatestReleaseApi => new Uri("https://api.github.com/repos/" + Repository + "/releases/latest");

        public static Uri ReleasesPage => new Uri("https://github.com/" + Repository + "/releases/latest");
    }
}
