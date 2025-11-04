using System.Reflection.Metadata.Ecma335;

namespace Launcher.Model
{
    /// <summary>
    /// マニフェストモデル
    /// </summary>
    internal sealed class Manifest
    {
        public string Version { get; set; } = "0.0.0";
        public string BaseFrom { get; set; } = "0.0.0";
        public List<PatchArchiveEntry>? PatchArchives { get; set; }
        public List<AddArchiveEntry>? AddFiles { get; set; }
        public List<string>? RemoveFiles { get; set; }
        public bool Mandatory { get; set; } = false;

        /// <summary>
        /// パッチが存在しない場合に true を返します
        /// </summary>
        public bool IsEmpty()
        {
            return PatchArchives == null
                || PatchArchives.Count == 0
                || string.IsNullOrWhiteSpace(PatchArchives[0].ArchiveName);
        }

        /// <summary>
        /// 最初のパッチURLを取得します
        /// </summary>
        public string? GetPatchUrl()
        {
            return PatchArchives?.FirstOrDefault()?.Url;
        }
    }
}
