namespace HidGuard.Core.Security;

public interface IDataProtector
{
    byte[] Protect(byte[] userData);
    byte[] Unprotect(byte[] encryptedData);
}
