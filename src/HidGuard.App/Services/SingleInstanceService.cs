namespace HidGuard.App.Services;

public sealed class SingleInstanceService : ISingleInstanceService
{
    private const string DefaultMutexName = "Local\\HIDGuard_Application_SingleInstance_Mutex";
    private readonly Mutex? mutex;
    private readonly bool isSingleInstance;
    private bool disposed;

    public bool IsSingleInstance => isSingleInstance;

    public SingleInstanceService(string mutexName = DefaultMutexName)
    {
        try
        {
            mutex = new Mutex(true, mutexName, out var createdNew);
            isSingleInstance = createdNew;
        }
        catch (Exception)
        {
            isSingleInstance = false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (isSingleInstance && mutex != null)
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            mutex.Dispose();
        }

        disposed = true;
    }
}
