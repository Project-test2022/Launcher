namespace Launcher.Model
{
    /// <summary>
    /// マニフェストモデル
    /// </summary>
    internal sealed class Manifest
    {
        public string Version { get; set; } = "0.0.0";
        public string BaseFrom { get; set; } = "0.0.0";
        public string? PatchUrl { get; set; }
        public bool Mandatory { get; set; } = false;
    }
}
