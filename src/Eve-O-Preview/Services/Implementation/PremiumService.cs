//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

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