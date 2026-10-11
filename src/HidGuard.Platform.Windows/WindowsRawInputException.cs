namespace HidGuard.Platform.Windows;

public sealed class WindowsRawInputException : Exception
{
    public int ErrorCode { get; }

    public WindowsRawInputException(string message)
        : base(message)
    {
        ErrorCode = 0;
    }

    public WindowsRawInputException(string message, int errorCode)
        : base($"{message} (Win32 Error: {errorCode})")
    {
        ErrorCode = errorCode;
    }

    public WindowsRawInputException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = 0;
    }
}
