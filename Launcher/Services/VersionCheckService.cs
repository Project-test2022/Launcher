using Launcher.Model;
using System.IO;

namespace Launcher.Services
{
    /// <summary>
    /// 現在のバージョンとマニフェストの比較を行うサービス。
    /// </summary>
    public sealed class VersionCheckService
    {
        public async Task<VersionInfo> CheckAsync(Manifest manifest, string versionFilePath)
        {
            ArgumentNullException.ThrowIfNull(manifest);

            string currentVersion = "0.0.0";

            if (File.Exists(versionFilePath))
            {
                currentVersion = await File.ReadAllTextAsync(versionFilePath);
                currentVersion = currentVersion.Trim();
            }

            bool updateRequired = manifest.Version != currentVersion;

            return new VersionInfo(currentVersion, manifest.Version, updateRequired);
        }
    }
}
