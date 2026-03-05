using EveOPreview.Services.Interface;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EveOPreview.Services.Interop;

namespace EveOPreview.Services.Implementation
{
    public class PremiumService : IPremiumService
    {
        private readonly byte[] _publicKey;
        
        public PremiumService()
        {
            _publicKey = GetEmbeddedPublicKey();
        }

        public bool ValidateSignature(string licenseKey)
        {
            try
            {
                KernelNativeMethods.OutputDebug($"Validating licenseKey: {licenseKey}");
                if (string.IsNullOrEmpty(licenseKey) || !licenseKey.Contains("."))
                {
                    return false;
                }

                var parts = licenseKey.Split('.');
                using (CngKey key = CngKey.Import(_publicKey, CngKeyBlobFormat.GenericPublicBlob))
                using (ECDsaCng dsa = new ECDsaCng(key))
                {
                    byte[] dataBytes = Encoding.UTF8.GetBytes(parts[0]);
                    byte[] signatureBytes = Convert.FromBase64String(parts[1]);
                    return dsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);
                }
            }
            catch (Exception ex)
            {
                KernelNativeMethods.OutputDebug($"Unhandled error in {nameof(PremiumService)}.{nameof(ValidateSignature)} processing licenseKey: {licenseKey}");
                return false;
            }
        }

        public DateTime GetPremiumExpirationUtcDate(string licenseKey)
        {
            if (!ValidateSignature(licenseKey))
            {
                return DateTime.MinValue;
            }

            var parts = licenseKey.Split('.');
            var dataParts = parts[0].Split('|');
            var dateString = dataParts[2];

            var expiresAt = DateTime.Parse(dateString);
            return expiresAt;
        }

        public bool IsLicenseValidAndCurrent(string licenseKey)
        {
            var now = DateTime.UtcNow;
            var isPremium = now < GetPremiumExpirationUtcDate(licenseKey);
            return isPremium;
        }

        private byte[] GetEmbeddedPublicKey()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            
            string resourceName = "EveOPreview.PremiumLicenseValidationPublicKey.bin";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new Exception("Resource not found.");
                byte[] ba = new byte[stream.Length];
                stream.Read(ba, 0, ba.Length);
                return ba;
            }
        }
    }
}