using System;

namespace EveOPreview.Services.Interface
{
    public interface IPremiumService
    {
        bool ValidateSignature(string licenseKey);
        DateTime GetPremiumExpirationUtcDate(string licenseKey);
        bool IsLicenseValidAndCurrent(string licenseKey);
    }
}