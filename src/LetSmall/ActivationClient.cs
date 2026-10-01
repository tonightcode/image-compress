using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace LetSmall
{
    [DataContract]
    internal sealed class ServiceConfig
    {
        [DataMember(Name = "checkoutUrl")]
        public string CheckoutUrl { get; set; }

        [DataMember(Name = "activationUrl")]
        public string ActivationUrl { get; set; }

        internal static ServiceConfig Load()
        {
            using (var stream = typeof(ServiceConfig).Assembly.GetManifestResourceStream(
                "LetSmall.ServiceConfig.json"))
            {
                if (stream == null)
                    return null;
                try
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    using (var content = new MemoryStream(Encoding.UTF8.GetBytes(
                        reader.ReadToEnd())))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(ServiceConfig));
                        var config = (ServiceConfig)serializer.ReadObject(content);
                        return config != null && IsHttps(config.CheckoutUrl) &&
                            IsHttps(config.ActivationUrl) ? config : null;
                    }
                }
                catch (SerializationException) { return null; }
                catch (XmlException) { return null; }
            }
        }

        private static bool IsHttps(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 &&
                uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
        }
    }

    [DataContract]
    internal sealed class ActivationRequest
    {
        [DataMember(Name = "token")]
        public string Token { get; set; }

        [DataMember(Name = "deviceId")]
        public string DeviceId { get; set; }
    }

    internal sealed class ActivationException : Exception
    {
        internal ActivationException(string message) : base(message) { }
    }

    internal static class ActivationClient
    {
        internal static bool IsActivationArgument(string value)
        {
            return value != null && value.StartsWith("letsmall:",
                StringComparison.OrdinalIgnoreCase);
        }

        internal static string ParseToken(string link)
        {
            Uri uri;
            if (!Uri.TryCreate(link, UriKind.Absolute, out uri) ||
                uri.Scheme != "letsmall" || uri.Host != "activate" ||
                uri.AbsolutePath != "/" || uri.Fragment.Length != 0)
                return null;

            var query = HttpUtility.ParseQueryString(uri.Query);
            if (query.AllKeys.Length != 1 || query.AllKeys[0] != "token")
                return null;
            var values = query.GetValues("token");
            var token = values != null && values.Length == 1 ? values[0] : null;
            return token != null && Regex.IsMatch(token, @"\A[A-Za-z0-9_-]{16,256}\z")
                ? token : null;
        }

        internal static bool OpenCheckout()
        {
            var config = ServiceConfig.Load();
            if (config == null)
                return false;
            try
            {
                Process.Start(new ProcessStartInfo(config.CheckoutUrl)
                {
                    UseShellExecute = true
                });
                return true;
            }
            catch (Win32Exception) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        internal static Task RedeemAsync(string link)
        {
            var token = ParseToken(link);
            if (token == null)
                throw new ActivationException("This activation link is invalid.");
            var config = ServiceConfig.Load();
            if (config == null)
                throw new ActivationException("Activation is not configured yet.");
            var deviceId = LicenseStore.DeviceId();
            if (deviceId == null)
                throw new ActivationException("This computer could not be identified.");

            return Task.Run(() => Redeem(config.ActivationUrl, token, deviceId));
        }

        private static void Redeem(string endpoint, string token, string deviceId)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.AllowAutoRedirect = false;
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                using (var body = new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(ActivationRequest)).WriteObject(body,
                        new ActivationRequest { Token = token, DeviceId = deviceId });
                    request.ContentLength = body.Length;
                    using (var output = request.GetRequestStream())
                        output.Write(body.GetBuffer(), 0, (int)body.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new ActivationException("The activation service rejected this link.");
                    var envelope = ReadLicense(response.GetResponseStream());
                    try
                    {
                        if (!LicenseStore.Save(envelope))
                            throw new ActivationException("The license could not be verified.");
                    }
                    catch (IOException)
                    {
                        throw new ActivationException("The license could not be saved.");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        throw new ActivationException("The license could not be saved.");
                    }
                }
            }
            catch (WebException error)
            {
                using (error.Response)
                {
                    if (error.Response is HttpWebResponse response &&
                        (int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                        throw new ActivationException(
                            "This activation link was rejected. It may be expired, used, or unpaid.");
                }
                throw new ActivationException("Could not contact the activation service.");
            }
        }

        private static LicenseEnvelope ReadLicense(Stream stream)
        {
            using (var content = new MemoryStream())
            {
                var buffer = new byte[4096];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (content.Length + count > 16384)
                        throw new ActivationException("The activation response is invalid.");
                    content.Write(buffer, 0, count);
                }
                content.Position = 0;
                try
                {
                    return (LicenseEnvelope)new DataContractJsonSerializer(
                        typeof(LicenseEnvelope)).ReadObject(content);
                }
                catch (SerializationException)
                {
                    throw new ActivationException("The activation response is invalid.");
                }
                catch (XmlException)
                {
                    throw new ActivationException("The activation response is invalid.");
                }
            }
        }
    }
}
