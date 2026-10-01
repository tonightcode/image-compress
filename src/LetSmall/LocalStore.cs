using System;
using System.IO;
using System.Xml.Serialization;

namespace LetSmall
{
    public sealed class AppSettings
    {
        public string OutputFolder { get; set; }
        public string Suffix { get; set; }

        public AppSettings()
        {
            OutputFolder = "";
            Suffix = "_compressed";
        }
    }

    public sealed class QuotaState
    {
        public DateTime LastDay { get; set; }
        public int Used { get; set; }
    }

    internal static class LocalStore
    {
        internal static string DirectoryPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "letsmall");
            }
        }

        internal static T Load<T>(string name) where T : new()
        {
            var path = Path.Combine(DirectoryPath, name);
            if (!File.Exists(path))
            {
                return new T();
            }
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    return (T)new XmlSerializer(typeof(T)).Deserialize(stream);
                }
            }
            catch (InvalidOperationException)
            {
                return new T();
            }
            catch (IOException)
            {
                return new T();
            }
        }

        internal static void Save<T>(string name, T value)
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, name);
            var temp = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write))
                {
                    new XmlSerializer(typeof(T)).Serialize(stream, value);
                }
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
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

    internal sealed class QuotaCounter
    {
        private readonly QuotaState state;

        internal QuotaCounter()
        {
            state = LocalStore.Load<QuotaState>("quota.xml");
        }

        internal int Remaining
        {
            get
            {
                RefreshDay();
                return Math.Max(0, 3 - state.Used);
            }
        }

        internal void RecordSuccess()
        {
            RefreshDay();
            state.Used++;
            LocalStore.Save("quota.xml", state);
        }

        private void RefreshDay()
        {
            var today = DateTime.Today;
            if (state.LastDay.Date >= today)
            {
                return;
            }
            state.LastDay = today;
            state.Used = 0;
            LocalStore.Save("quota.xml", state);
        }
    }
}
