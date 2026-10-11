namespace HidGuard.Core.Policy;

public enum PolicyAction
{
    Allow,
    PromptUser,
    AlertOnly,
    LockWorkstation,
    BlockDevice,
    LockAndBlock
}
