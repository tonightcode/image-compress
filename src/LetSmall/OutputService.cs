using System;
using System.IO;
using System.Threading;

namespace LetSmall
{
    internal static class OutputService
    {
        internal static string Compress(ImageJob job, string selectedFormat,
            AppSettings settings, CancellationToken cancellation)
        {
            var format = selectedFormat == "Original"
                ? ImageCodec.FormatFromPath(job.SourcePath) : selectedFormat;
            var folder = string.IsNullOrWhiteSpace(settings.OutputFolder)
                ? Path.GetDirectoryName(job.SourcePath) : settings.OutputFolder;
            Directory.CreateDirectory(folder);

            var stem = Path.GetFileNameWithoutExtension(job.SourcePath) + settings.Suffix;
            var extension = ImageCodec.ExtensionFor(format);
            var temp = Path.Combine(folder, ".letsmall-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                cancellation.ThrowIfCancellationRequested();
                ImageCodec.Encode(ImageCodec.Decode(job.SourcePath), format, temp);
                cancellation.ThrowIfCancellationRequested();
                for (var number = 1; ; number++)
                {
                    var suffix = number == 1 ? "" : "_" + number;
                    var output = Path.Combine(folder, stem + suffix + extension);
                    if (string.Equals(output, job.SourcePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    try
                    {
                        File.Move(temp, output);
                        return output;
                    }
                    catch (IOException)
                    {
                        if (!File.Exists(output))
                        {
                            throw;
                        }
                    }
                }
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }
    }
}
