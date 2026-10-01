using System;
using System.IO;
using System.Security;

namespace LetSmall
{
    internal static class ErrorMessages
    {
        internal static string For(Exception error)
        {
            if (error is FileNotFoundException || error is DirectoryNotFoundException)
                return "The file or folder no longer exists.";
            if (error is UnauthorizedAccessException || error is SecurityException)
                return "Access denied. Check file and folder permissions.";
            if (error is PathTooLongException)
                return "The file path is too long. Choose a shorter path.";
            if (error is InvalidDataException || error is FormatException ||
                error is NotSupportedException)
                return "The image is damaged or its format is not supported.";
            if (error is OutOfMemoryException || error is OverflowException)
                return "The image is too large to process. Try a smaller image.";
            if (error is DllNotFoundException || error is BadImageFormatException ||
                error is EntryPointNotFoundException)
                return "An image component is missing or incompatible. Reinstall letsmall.";
            if (error is IOException)
            {
                var code = error.HResult & 0xffff;
                if (code == 112 || code == 39)
                    return "Not enough disk space. Free some space and try again.";
                if (code == 32 || code == 33)
                    return "The file is in use. Close it in other apps and try again.";
                return "Could not read or save the file. Check the folder and try again.";
            }
            return "The operation could not be completed. Please try again.";
        }
    }
}
