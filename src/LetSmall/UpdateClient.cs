using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LetSmall
{
    [DataContract]
    internal sealed class UpdateConfig
    {
        [DataMember(Name = "origin")] public string Origin { get; set; }
        [DataMember(Name = "publicKey")] public string PublicKey { get; set; }
    }

    [DataContract]
    internal sealed class UpdateEnvelope
    {
        [DataMember(Name = "payload")] public string Payload { get; set; }
        [DataMember(Name = "signature")] public string Signature { get; set; }
    }

    [DataContract]
    internal sealed class UpdateRelease
    {
        [DataMember(Name = "schema")] public int Schema { get; set; }
        [DataMember(Name = "product")] public string Product { get; set; }
        [DataMember(Name = "platform")] public string Platform { get; set; }
        [DataMember(Name = "version")] public string Version { get; set; }
        [DataMember(Name = "publishedAt")] public string PublishedAt { get; set; }
        [DataMember(Name = "notes")] public string Notes { get; set; }
        [DataMember(Name = "url")] public string Url { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "sha256")] public string Sha256 { get; set; }
    }

    internal sealed class UpdateClient : IDisposable
    {
        internal const long MaxPackageSize = 30 * 1024 * 1024;
        internal static string CurrentVersion
        {
            get { return typeof(UpdateClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion; }
        }

        private readonly UpdateConfig config;
        private readonly HttpClient http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private CancellationTokenSource downloadCancellation;
        private Task check;
        private string package;
        private bool disposed;
        internal UpdateRelease Release { get; private set; }
        internal bool Checking { get; private set; }
        internal bool Downloading { get; private set; }
        internal bool Ready { get { return package != null; } }
        internal int Progress { get; private set; }
        internal string Error { get; private set; }
        internal bool Available { get { return Release != null && ParseVersion(Release.Version) > ParseVersion(CurrentVersion); } }
        internal event Action Changed;

        internal UpdateClient()
        {
            http.Timeout = Timeout.InfiniteTimeSpan;
            using (var stream = typeof(UpdateClient).Assembly.GetManifestResourceStream("LetSmall.UpdateConfig.json"))
            {
                if (stream != null)
                    try { config = ReadJson<UpdateConfig>(stream); }
                    catch (SerializationException) { }
                    catch (System.Xml.XmlException) { }
            }
        }

        internal static Version ParseVersion(string value)
        {
            if (value == null || !Regex.IsMatch(value, @"\A(0|[1-9][0-9]{0,3})\.(0|[1-9][0-9]{0,3})\.(0|[1-9][0-9]{0,3})\z"))
                throw new InvalidDataException("Invalid product version.");
            return new Version(value);
        }

        internal static T ReadJson<T>(Stream stream)
        {
            return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        internal static UpdateRelease VerifyRelease(byte[] bytes, UpdateConfig trust)
        {
            UpdateEnvelope envelope;
            using (var stream = new MemoryStream(bytes)) envelope = ReadJson<UpdateEnvelope>(stream);
            if (envelope == null || envelope.Payload == null || envelope.Signature == null)
                throw new InvalidDataException("Missing update signature.");
            var payload = Convert.FromBase64String(envelope.Payload);
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(trust.PublicKey);
                if (rsa.KeySize < 2048 || !rsa.VerifyData(payload, CryptoConfig.MapNameToOID("SHA256"),
                    Convert.FromBase64String(envelope.Signature))) throw new InvalidDataException("Untrusted update.");
            }
            UpdateRelease release;
            using (var stream = new MemoryStream(payload)) release = ReadJson<UpdateRelease>(stream);
            if (release == null) throw new InvalidDataException("Missing release information.");
            DateTimeOffset published;
            if (release.Schema != 1 || release.Product != "letsmall" || release.Platform != "win-x64" ||
                release.Size <= 0 || release.Size >= MaxPackageSize || release.Notes == null || release.Notes.Length > 16000 ||
                !DateTimeOffset.TryParse(release.PublishedAt, out published) ||
                release.Sha256 == null || !Regex.IsMatch(release.Sha256, @"\A[a-f0-9]{64}\z"))
                throw new InvalidDataException("Invalid update information.");
            ParseVersion(release.Version);
            var uri = TrustedUri(release.Url, trust);
            if (uri.AbsolutePath != "/downloads/letsmall-" + release.Version + "-win-x64.exe" || uri.Query.Length != 0)
                throw new InvalidDataException("Invalid package address.");
            return release;
        }

        private static Uri TrustedUri(string value, UpdateConfig trust)
        {
            Uri origin, uri;
            if (trust == null || !Uri.TryCreate(trust.Origin, UriKind.Absolute, out origin) ||
                origin.Scheme != "https" || origin.GetLeftPart(UriPartial.Authority) != trust.Origin ||
                !Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != "https" ||
                uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
                uri.GetLeftPart(UriPartial.Authority) != trust.Origin)
                throw new InvalidDataException("Updates are not configured or the address is untrusted.");
            return uri;
        }

        internal Task CheckAsync()
        {
            if (check != null && !check.IsCompleted) return check;
            if (Downloading || Ready || disposed) return Task.CompletedTask;
            check = CheckCoreAsync();
            return check;
        }

        private async Task CheckCoreAsync()
        {
            Checking = true;
            Error = null;
            Release = null;
            Notify();
            try
            {
                var uri = TrustedUri(config == null ? null : config.Origin + "/updates/latest.json", config);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    using (var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token))
                    {
                        response.EnsureSuccessStatusCode();
                        using (timeout.Token.Register(() => response.Dispose()))
                        using (var input = await response.Content.ReadAsStreamAsync())
                        using (var output = new MemoryStream())
                        {
                            await CopyBoundedAsync(input, output, 65536, timeout.Token, null);
                            Release = VerifyRelease(output.ToArray(), config);
                        }
                    }
                }
            }
            catch (Exception ex) when (Recoverable(ex)) { Error = "Could not check for updates. Close and reopen this window to try again."; }
            finally { Checking = false; Notify(); }
        }

        internal async Task DownloadAsync()
        {
            if (!Available || Downloading || Ready || disposed) return;
            Downloading = true;
            Error = null;
            Progress = 0;
            var release = Release;
            var directory = Path.Combine(Path.GetTempPath(), "letsmall-update-" + Guid.NewGuid().ToString("N"));
            var partial = Path.Combine(directory, "setup.partial");
            downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            downloadCancellation.CancelAfter(TimeSpan.FromMinutes(10));
            Notify();
            try
            {
                Directory.CreateDirectory(directory);
                using (var response = await http.GetAsync(TrustedUri(release.Url, config),
                    HttpCompletionOption.ResponseHeadersRead, downloadCancellation.Token))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength != release.Size)
                        throw new InvalidDataException("Unexpected download length.");
                    using (downloadCancellation.Token.Register(() => response.Dispose()))
                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                        await CopyBoundedAsync(input, output, release.Size, downloadCancellation.Token,
                            count => { Progress = (int)(count * 100 / release.Size); Notify(); });
                }
                await Task.Run(() => VerifyPackage(partial, release));
                var ready = Path.Combine(directory, "letsmall-setup.exe");
                File.Move(partial, ready);
                package = ready;
            }
            catch (Exception ex) when (Recoverable(ex)) { Error = "Download cancelled or failed verification. You can try again."; }
            finally
            {
                Downloading = false;
                downloadCancellation.Dispose();
                downloadCancellation = null;
                if (!Ready) TryDeleteDirectory(directory);
                Notify();
            }
        }

        private static async Task CopyBoundedAsync(Stream input, Stream output, long limit,
            CancellationToken token, Action<long> progress)
        {
            var buffer = new byte[81920];
            long total = 0;
            int count;
            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, token)) != 0)
            {
                total += count;
                if (total > limit) throw new InvalidDataException("Response too large.");
                await output.WriteAsync(buffer, 0, count, token);
                if (progress != null) progress(total);
            }
        }

        internal static void VerifyPackage(string path, UpdateRelease release)
        {
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                if (input.Length != release.Size ||
                    BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant() != release.Sha256)
                    throw new InvalidDataException("Package verification failed.");
            }
        }

        internal void StartInstaller()
        {
            if (!Ready || disposed) throw new InvalidOperationException("The update is not ready.");
            VerifyPackage(package, Release);
            var directory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
            if (directory.IndexOf('"') >= 0) throw new InvalidDataException("Invalid installation path.");
            var process = Process.Start(new ProcessStartInfo(package,
                "/UPDATEPID=" + Process.GetCurrentProcess().Id + " /D=" + directory) { UseShellExecute = true });
            if (process == null) throw new IOException("Could not start installer.");
            process.Dispose();
            // The installer runs from this directory after the app exits.
            package = null;
        }

        internal void CancelDownload() { if (downloadCancellation != null) downloadCancellation.Cancel(); }
        private void Notify() { if (!disposed && Changed != null) Changed(); }
        private static bool Recoverable(Exception ex)
        {
            return ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is HttpRequestException ||
                ex is OperationCanceledException || ex is SerializationException || ex is CryptographicException ||
                ex is FormatException || ex is ArgumentException || ex is System.Xml.XmlException || ex is InvalidOperationException;
        }
        private static void TryDeleteDirectory(string directory)
        {
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        public void Dispose()
        {
            disposed = true;
            lifetime.Cancel();
            CancelDownload();
            http.Dispose();
            if (package != null) TryDeleteDirectory(Path.GetDirectoryName(package));
        }
    }
}
