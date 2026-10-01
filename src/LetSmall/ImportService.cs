using System;
using System.Collections.Generic;
using System.IO;

namespace LetSmall
{
    internal static class ImportService
    {
        internal static List<string> FindImages(IEnumerable<string> inputs,
            IEnumerable<string> existingPaths)
        {
            var known = new HashSet<string>(existingPaths, StringComparer.OrdinalIgnoreCase);
            var found = new List<string>();
            var folders = new Stack<string>();
            foreach (var input in inputs)
            {
                if (File.Exists(input))
                {
                    AddFile(input, known, found);
                }
                else if (Directory.Exists(input))
                {
                    folders.Push(input);
                }
            }

            while (folders.Count > 0)
            {
                var folder = folders.Pop();
                try
                {
                    foreach (var file in Directory.EnumerateFiles(folder))
                    {
                        AddFile(file, known, found);
                    }
                    foreach (var child in Directory.EnumerateDirectories(folder))
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                        {
                            folders.Push(child);
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            return found;
        }

        private static void AddFile(string path, HashSet<string> known, List<string> found)
        {
            if (ImageCodec.FormatFromPath(path) == null)
            {
                return;
            }
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (known.Add(fullPath))
                {
                    found.Add(fullPath);
                }
            }
            catch (ArgumentException) { }
            catch (PathTooLongException) { }
        }
    }
}
