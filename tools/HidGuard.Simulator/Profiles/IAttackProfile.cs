using HidGuard.Simulator.Injection;

namespace HidGuard.Simulator.Profiles;

public interface IAttackProfile
{
    AttackProfileType ProfileType { get; }
    string Name { get; }
    Task ExecuteAsync(IKeystrokeInjector injector, CancellationToken cancellationToken = default);
}
