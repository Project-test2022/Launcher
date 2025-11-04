namespace Launcher.Model
{
    public sealed class VersionInfo
    {
        public string CurrentVersion { get; }
        public string LatestVersion { get; }
        public bool UpdateRequired { get; }

        public VersionInfo(string current, string latest, bool updateRequired)
        {
            CurrentVersion = current;
            LatestVersion = latest;
            UpdateRequired = updateRequired;
        }
    }
}
