using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace LetSmall
{
    internal static class LicenseStore
    {
        private const string FileName = "license.xml";

        internal static bool IsUnlocked()
        {
            try
            {
                var path = Path.Combine(LocalStore.DirectoryPath, FileName);
                if (!File.Exists(path))
                    return false;
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 16384
                };
                using (var reader = XmlReader.Create(path, settings))
                {
                    var envelope = (LicenseEnvelope)new XmlSerializer(
                        typeof(LicenseEnvelope)).Deserialize(reader);
                    return LicenseVerifier.Verify(envelope, PublicKey(), DeviceId());
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (SecurityException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (XmlException) { return false; }
        }

        internal static bool Save(LicenseEnvelope envelope)
        {
            if (!LicenseVerifier.Verify(envelope, PublicKey(), DeviceId()))
                return false;
            LocalStore.Save(FileName, envelope);
            return true;
        }

        internal static string DeviceId()
        {
            try
            {
                using (var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                    RegistryView.Registry64))
                using (var key = registry.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var machine = key == null ? null : key.GetValue("MachineGuid") as string;
                    if (string.IsNullOrWhiteSpace(machine) || identity.User == null)
                        return null;
                    var input = "letsmall-device-v1\n" + machine.Trim().ToLowerInvariant() +
                        "\n" + identity.User.Value;
                    using (var hash = SHA256.Create())
                    {
                        return BitConverter.ToString(hash.ComputeHash(
                            Encoding.UTF8.GetBytes(input))).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch (SecurityException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (IOException) { return null; }
        }

        private static string PublicKey()
        {
            using (var stream = typeof(LicenseStore).Assembly.GetManifestResourceStream(
                "LetSmall.LicensePublicKey.xml"))
            {
                if (stream == null)
                    return null;
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }
    }
}
