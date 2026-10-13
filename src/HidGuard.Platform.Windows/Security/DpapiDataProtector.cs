using System.Security.Cryptography;
using HidGuard.Core.Security;

namespace HidGuard.Platform.Windows.Security;

public sealed class DpapiDataProtector : IDataProtector
{
    private readonly byte[]? optionalEntropy;
    private readonly DataProtectionScope scope;

    public DpapiDataProtector(DataProtectionScope scope = DataProtectionScope.CurrentUser, byte[]? optionalEntropy = null)
    {
        this.scope = scope;
        this.optionalEntropy = optionalEntropy;
    }

    public byte[] Protect(byte[] userData)
    {
        ArgumentNullException.ThrowIfNull(userData);
        return ProtectedData.Protect(userData, optionalEntropy, scope);
    }

    public byte[] Unprotect(byte[] encryptedData)
    {
        ArgumentNullException.ThrowIfNull(encryptedData);
        return ProtectedData.Unprotect(encryptedData, optionalEntropy, scope);
    }
}
