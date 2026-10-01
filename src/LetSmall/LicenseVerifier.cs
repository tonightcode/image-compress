using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace LetSmall
{
    [DataContract]
    public sealed class LicenseEnvelope
    {
        [DataMember(Name = "payload", IsRequired = true)]
        public string Payload { get; set; }
        [DataMember(Name = "signature", IsRequired = true)]
        public string Signature { get; set; }
    }

    [DataContract]
    public sealed class LicenseClaims
    {
        [DataMember(Name = "v", IsRequired = true)]
        public int Version { get; set; }
        [DataMember(Name = "product", IsRequired = true)]
        public string Product { get; set; }
        [DataMember(Name = "orderId", IsRequired = true)]
        public string OrderId { get; set; }
        [DataMember(Name = "deviceId", IsRequired = true)]
        public string DeviceId { get; set; }
    }

    internal static class LicenseVerifier
    {
        internal static bool Verify(LicenseEnvelope envelope, string publicKey,
            string expectedDeviceId)
        {
            if (envelope == null || string.IsNullOrWhiteSpace(publicKey) ||
                string.IsNullOrWhiteSpace(expectedDeviceId) ||
                string.IsNullOrEmpty(envelope.Payload) || envelope.Payload.Length > 5500 ||
                string.IsNullOrEmpty(envelope.Signature) || envelope.Signature.Length > 700)
                return false;

            try
            {
                var payload = Convert.FromBase64String(envelope.Payload);
                var signature = Convert.FromBase64String(envelope.Signature);
                if (payload.Length == 0 || payload.Length > 4096)
                    return false;

                using (var rsa = RSA.Create())
                {
                    rsa.FromXmlString(publicKey);
                    if (rsa.KeySize < 2048 || rsa.KeySize > 4096 ||
                        signature.Length != rsa.KeySize / 8 ||
                        !rsa.VerifyData(SigningBytes(payload), signature,
                            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                        return false;
                }

                // Only parse claims after authenticating the exact payload bytes.
                using (var stream = new MemoryStream(payload, false))
                {
                    var serializer = new DataContractJsonSerializer(typeof(LicenseClaims));
                    var claims = (LicenseClaims)serializer.ReadObject(stream);
                    return claims != null && claims.Version == 1 &&
                        claims.Product == "letsmall" &&
                        !string.IsNullOrWhiteSpace(claims.OrderId) &&
                        claims.OrderId.Length <= 128 &&
                        string.Equals(claims.DeviceId, expectedDeviceId,
                            StringComparison.Ordinal);
                }
            }
            catch (FormatException) { return false; }
            catch (CryptographicException) { return false; }
            catch (SerializationException) { return false; }
            catch (XmlException) { return false; }
            catch (ArgumentException) { return false; }
        }

        internal static byte[] SigningBytes(byte[] payload)
        {
            var prefix = Encoding.UTF8.GetBytes("letsmall-license-v1\n");
            var bytes = new byte[prefix.Length + payload.Length];
            Buffer.BlockCopy(prefix, 0, bytes, 0, prefix.Length);
            Buffer.BlockCopy(payload, 0, bytes, prefix.Length, payload.Length);
            return bytes;
        }
    }
}
