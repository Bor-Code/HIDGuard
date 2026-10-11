namespace HidGuard.Core.Configuration;

public interface IConfigurationManager : IDisposable
{
    HidGuardConfiguration CurrentConfiguration { get; }
    event EventHandler<HidGuardConfiguration>? ConfigurationChanged;
    Task<HidGuardConfiguration> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(HidGuardConfiguration configuration, CancellationToken cancellationToken = default);
}
