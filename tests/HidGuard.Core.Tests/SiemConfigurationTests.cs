using HidGuard.Core.Configuration;
using Shouldly;
using Xunit;

namespace HidGuard.Core.Tests;

public sealed class SiemConfigurationTests
{
    [Fact]
    public void DefaultConfigurationShouldBeValid()
    {
        var config = new SiemConfiguration();

        config.Enabled.ShouldBeFalse();
        config.Host.ShouldBe("127.0.0.1");
        config.Port.ShouldBe(514);
        config.Protocol.ShouldBe(SiemProtocol.Udp);
        config.Format.ShouldBe(SiemFormat.Cef);
        config.Facility.ShouldBe(10);
        config.AppName.ShouldBe("HIDGuard");

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void ValidateShouldThrowWhenEnabledAndHostIsEmpty()
    {
        var config = new SiemConfiguration
        {
            Enabled = true,
            Host = string.Empty
        };

        var ex = Should.Throw<ArgumentException>(() => config.Validate());
        ex.ParamName.ShouldBe("Host");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void ValidateShouldThrowForInvalidPort(int port)
    {
        var config = new SiemConfiguration
        {
            Enabled = true,
            Host = "10.0.0.1",
            Port = port
        };

        var ex = Should.Throw<ArgumentOutOfRangeException>(() => config.Validate());
        ex.ParamName.ShouldBe("Port");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void ValidateShouldThrowForInvalidFacility(int facility)
    {
        var config = new SiemConfiguration
        {
            Enabled = true,
            Host = "10.0.0.1",
            Facility = facility
        };

        var ex = Should.Throw<ArgumentOutOfRangeException>(() => config.Validate());
        ex.ParamName.ShouldBe("Facility");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(40000)]
    public void ValidateShouldThrowForInvalidTimeout(int timeout)
    {
        var config = new SiemConfiguration
        {
            Enabled = true,
            Host = "10.0.0.1",
            TimeoutMilliseconds = timeout
        };

        var ex = Should.Throw<ArgumentOutOfRangeException>(() => config.Validate());
        ex.ParamName.ShouldBe("TimeoutMilliseconds");
    }

    [Fact]
    public void HidGuardConfigurationValidateShouldIncludeSiemValidation()
    {
        var config = new HidGuardConfiguration
        {
            Siem = new SiemConfiguration
            {
                Enabled = true,
                Host = ""
            }
        };

        Should.Throw<ArgumentException>(() => config.Validate());
    }
}
