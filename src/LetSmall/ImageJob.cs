using System.IO;

namespace LetSmall
{
    internal sealed class ImageJob
    {
        internal string SourcePath { get; private set; }
        internal long SourceBytes { get; private set; }
        internal string Status { get; set; }
        internal string OutputPath { get; set; }
        internal long OutputBytes { get; set; }

        internal ImageJob(string sourcePath)
        {
            SourcePath = sourcePath;
            SourceBytes = new FileInfo(sourcePath).Length;
            Status = "Ready";
        }
    }
}
