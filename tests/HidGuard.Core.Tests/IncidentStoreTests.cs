using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;
using HidGuard.Core.Configuration;
using HidGuard.Core.Incidents;
using HidGuard.Core.Security;

namespace HidGuard.Core.Tests;

public sealed class IncidentStoreTests
{
    private sealed class XorDataProtector : IDataProtector
    {
        private const byte Key = 0xAA;

        public byte[] Protect(byte[] userData)
        {
            ArgumentNullException.ThrowIfNull(userData);
            var result = new byte[userData.Length];
            for (var i = 0; i < userData.Length; i++)
            {
                result[i] = (byte)(userData[i] ^ Key);
            }
            return result;
        }

        public byte[] Unprotect(byte[] encryptedData)
        {
            ArgumentNullException.ThrowIfNull(encryptedData);
            var result = new byte[encryptedData.Length];
            for (var i = 0; i < encryptedData.Length; i++)
            {
                result[i] = (byte)(encryptedData[i] ^ Key);
            }
            return result;
        }
    }

    [Fact]
    public void ConfigurationValidationShouldThrowOnInvalidValues()
    {
        Should.Throw<InvalidOperationException>(() =>
            new IncidentStoreConfiguration { MaxIncidentCount = 0 }.Validate());

        Should.Throw<InvalidOperationException>(() =>
            new IncidentStoreConfiguration { RetentionDays = 0 }.Validate());

        Should.Throw<InvalidOperationException>(() =>
            new IncidentStoreConfiguration { MaxPayloadBytes = 0 }.Validate());

        Should.Throw<InvalidOperationException>(() =>
            new IncidentStoreConfiguration { MaxPayloadBytes = 100_000 }.Validate());
    }

    [Fact]
    public async Task OptInPayloadCaptureDisabledByDefaultShouldNeverStorePayload()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_IncTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            var config = new IncidentStoreConfiguration
            {
                EnableOptInPayloadCapture = false
            };

            using var store = new EncryptedIncidentStore(tempDir, config, new XorDataProtector());

            var rawPayload = Encoding.UTF8.GetBytes("MALICIOUS_KEYSTROKE_PAYLOAD");
            var request = new CreateIncidentRequest
            {
                DeviceId = "VID_16C0&PID_05DF",
                AnomalyScore = 95.5,
                TriggeredRule = "SubHumanTypingSpeed",
                CountermeasureAction = "LockWorkstation",
                RawPayload = rawPayload
            };

            var record = await store.RecordIncidentAsync(request);

            record.HasEncryptedPayload.ShouldBeFalse();
            record.EncryptedPayload.ShouldBeNull();
            record.UnencryptedPayloadLength.ShouldBeNull();

            var loaded = await store.GetIncidentByIdAsync(record.Id);
            loaded.ShouldNotBeNull();
            loaded.HasEncryptedPayload.ShouldBeFalse();
            loaded.EncryptedPayload.ShouldBeNull();

            var decrypted = await store.DecryptPayloadAsync(record.Id);
            decrypted.ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task OptInPayloadCaptureEnabledShouldEncryptAndAllowDecryption()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_IncTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            var config = new IncidentStoreConfiguration
            {
                EnableOptInPayloadCapture = true,
                MaxPayloadBytes = 4096
            };

            var protector = new XorDataProtector();
            using var store = new EncryptedIncidentStore(tempDir, config, protector);

            var rawPayload = Encoding.UTF8.GetBytes("GUI r\nDELAY 100\nSTRING powershell\nENTER");
            var request = new CreateIncidentRequest
            {
                DeviceId = "VID_046D&PID_C31C",
                AnomalyScore = 88.0,
                TriggeredRule = "LowIntervalVariance",
                CountermeasureAction = "BlockDevice",
                RawPayload = rawPayload
            };

            var record = await store.RecordIncidentAsync(request);

            record.HasEncryptedPayload.ShouldBeTrue();
            record.EncryptedPayload.ShouldNotBeNull();
            record.EncryptedPayload.ShouldNotBe(rawPayload);
            record.UnencryptedPayloadLength.ShouldBe(rawPayload.Length);

            var decrypted = await store.DecryptPayloadAsync(record.Id);
            decrypted.ShouldNotBeNull();
            decrypted.ShouldBe(rawPayload);
            Encoding.UTF8.GetString(decrypted).ShouldBe("GUI r\nDELAY 100\nSTRING powershell\nENTER");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task PayloadExceedingMaxPayloadBytesShouldBeTruncated()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_IncTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            const int maxBytes = 32;
            var config = new IncidentStoreConfiguration
            {
                EnableOptInPayloadCapture = true,
                MaxPayloadBytes = maxBytes
            };

            using var store = new EncryptedIncidentStore(tempDir, config, new XorDataProtector());

            var largePayload = new byte[128];
            Array.Fill(largePayload, (byte)0x41);

            var record = await store.RecordIncidentAsync(new CreateIncidentRequest
            {
                DeviceId = "VID_1234&PID_5678",
                AnomalyScore = 90.0,
                TriggeredRule = "SubHumanTypingSpeed",
                CountermeasureAction = "LockWorkstation",
                RawPayload = largePayload
            });

            record.UnencryptedPayloadLength.ShouldBe(maxBytes);
            var decrypted = await store.DecryptPayloadAsync(record.Id);
            decrypted.ShouldNotBeNull();
            decrypted.Length.ShouldBe(maxBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task CapacityLimitShouldPruneOldestIncidentsWhenExceeded()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_IncTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            var config = new IncidentStoreConfiguration
            {
                MaxIncidentCount = 3
            };

            using var store = new EncryptedIncidentStore(tempDir, config, new XorDataProtector());

            for (var i = 1; i <= 5; i++)
            {
                await store.RecordIncidentAsync(new CreateIncidentRequest
                {
                    DeviceId = $"DEV_{i}",
                    AnomalyScore = 70.0 + i,
                    TriggeredRule = "Rule",
                    CountermeasureAction = "Action"
                });
                await Task.Delay(20);
            }

            var incidents = await store.GetIncidentsAsync(10);
            incidents.Count.ShouldBe(3);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task PurgeExpiredIncidentsShouldRemoveExpiredRecordsOnly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_IncTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            var config = new IncidentStoreConfiguration
            {
                RetentionDays = 10
            };

            using var store = new EncryptedIncidentStore(tempDir, config, new XorDataProtector());

            var active = await store.RecordIncidentAsync(new CreateIncidentRequest
            {
                DeviceId = "ACTIVE_DEVICE",
                AnomalyScore = 80.0,
                TriggeredRule = "Rule",
                CountermeasureAction = "Action"
            });

            var expiredId = Guid.NewGuid();
            var expiredRecord = new IncidentRecord
            {
                Id = expiredId,
                Timestamp = DateTimeOffset.UtcNow.AddDays(-20),
                DeviceId = "EXPIRED_DEVICE",
                AnomalyScore = 90.0,
                TriggeredRule = "Rule",
                CountermeasureAction = "Action",
                RetentionExpiresAt = DateTimeOffset.UtcNow.AddDays(-5)
            };

            var expiredPath = Path.Combine(tempDir, $"incident_{expiredId:N}.json");
            await File.WriteAllTextAsync(expiredPath, JsonSerializer.Serialize(expiredRecord));

            var purged = await store.PurgeExpiredIncidentsAsync();
            purged.ShouldBe(1);

            var activeLoaded = await store.GetIncidentByIdAsync(active.Id);
            activeLoaded.ShouldNotBeNull();

            var expiredLoaded = await store.GetIncidentByIdAsync(expiredId);
            expiredLoaded.ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ExportMetadataReportJsonShouldNeverExposeRawOrEncryptedPayload()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_IncTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            var config = new IncidentStoreConfiguration
            {
                EnableOptInPayloadCapture = true
            };

            using var store = new EncryptedIncidentStore(tempDir, config, new XorDataProtector());

            await store.RecordIncidentAsync(new CreateIncidentRequest
            {
                DeviceId = "VID_9999&PID_8888",
                AnomalyScore = 92.5,
                TriggeredRule = "UltraFastBurst",
                CountermeasureAction = "LockWorkstation",
                RawPayload = Encoding.UTF8.GetBytes("SECRET_PAYLOAD_DATA")
            });

            using var memoryStream = new MemoryStream();
            await store.ExportMetadataReportAsync(memoryStream, IncidentExportFormat.Json);

            var json = Encoding.UTF8.GetString(memoryStream.ToArray());

            json.ShouldContain("VID_9999&PID_8888");
            json.ShouldContain("UltraFastBurst");
            json.ShouldContain("LockWorkstation");
            json.ShouldContain("HasEncryptedPayload");
            json.ShouldNotContain("SECRET_PAYLOAD_DATA");
            json.ShouldNotContain("\"EncryptedPayload\":");
            json.ShouldNotContain("RawPayload");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ExportMetadataReportCsvShouldProduceValidCsvWithoutPayload()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_IncTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            var config = new IncidentStoreConfiguration
            {
                EnableOptInPayloadCapture = true
            };

            using var store = new EncryptedIncidentStore(tempDir, config, new XorDataProtector());

            await store.RecordIncidentAsync(new CreateIncidentRequest
            {
                DeviceId = "VID_1111&PID_2222",
                AnomalyScore = 85.0,
                TriggeredRule = "SubHumanTypingSpeed",
                CountermeasureAction = "SuppressKeys",
                RawPayload = Encoding.UTF8.GetBytes("KEYSTROKE_CHARS")
            });

            using var memoryStream = new MemoryStream();
            await store.ExportMetadataReportAsync(memoryStream, IncidentExportFormat.Csv);

            var csv = Encoding.UTF8.GetString(memoryStream.ToArray());
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            lines.Length.ShouldBe(2);
            lines[0].ShouldContain("IncidentId,Timestamp,DeviceId,AnomalyScore");
            lines[1].ShouldContain("VID_1111&PID_2222");
            lines[1].ShouldContain("SubHumanTypingSpeed");
            lines[1].ShouldNotContain("KEYSTROKE_CHARS");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
