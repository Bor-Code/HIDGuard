using Shouldly;
using Xunit;
using HidGuard.Core.Domain;
using HidGuard.Core.Trust;

namespace HidGuard.Core.Tests;

public sealed class TrustStoreTests
{
    private static readonly DeviceIdentifier UsbKeyboard = new(@"\\?\HID#VID_046D&PID_C52B#0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}", 0x046D, 0xC52B);
    private static readonly DeviceIdentifier GenericKeyboard = new(@"\\?\Root#RDP_KBD#0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}", 0, 0);

    private sealed class InMemoryTrustStoreStorage : ITrustStoreStorage
    {
        public string? Content { get; set; }

        public Task<string?> ReadRawAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Content);
        }

        public Task WriteRawAsync(string content, CancellationToken cancellationToken = default)
        {
            Content = content;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetTrustLevelAsyncWhenDeviceNotEnrolledShouldReturnUnknown()
    {
        var storage = new InMemoryTrustStoreStorage();
        using var store = new TrustStore(storage);

        var level = await store.GetTrustLevelAsync(UsbKeyboard);

        level.ShouldBe(DeviceTrustLevel.Unknown);
    }

    [Fact]
    public async Task SetTrustLevelAsyncWhenNewDeviceShouldAddRecord()
    {
        var storage = new InMemoryTrustStoreStorage();
        using var store = new TrustStore(storage);

        await store.SetTrustLevelAsync(UsbKeyboard, DeviceTrustLevel.Trusted, "Administrator", "Office Keyboard");

        var level = await store.GetTrustLevelAsync(UsbKeyboard);
        level.ShouldBe(DeviceTrustLevel.Trusted);

        var records = await store.GetAllRecordsAsync();
        records.Count.ShouldBe(1);
        records[0].DeviceId.ShouldBe(UsbKeyboard);
        records[0].TrustLevel.ShouldBe(DeviceTrustLevel.Trusted);
        records[0].EnrolledBy.ShouldBe("Administrator");
        records[0].FriendlyName.ShouldBe("Office Keyboard");
        storage.Content.ShouldNotBeNull();
    }

    [Fact]
    public async Task SetTrustLevelAsyncWhenExistingDeviceShouldUpdateRecord()
    {
        var storage = new InMemoryTrustStoreStorage();
        using var store = new TrustStore(storage);

        await store.SetTrustLevelAsync(UsbKeyboard, DeviceTrustLevel.Trusted, "User", "Office Keyboard");
        var recordsBefore = await store.GetAllRecordsAsync();
        var originalFirstSeen = recordsBefore[0].FirstSeenAt;

        await Task.Delay(20);

        await store.SetTrustLevelAsync(UsbKeyboard, DeviceTrustLevel.Blocked, "Admin");

        var level = await store.GetTrustLevelAsync(UsbKeyboard);
        level.ShouldBe(DeviceTrustLevel.Blocked);

        var recordsAfter = await store.GetAllRecordsAsync();
        recordsAfter.Count.ShouldBe(1);
        recordsAfter[0].TrustLevel.ShouldBe(DeviceTrustLevel.Blocked);
        recordsAfter[0].EnrolledBy.ShouldBe("Admin");
        recordsAfter[0].FriendlyName.ShouldBe("Office Keyboard");
        recordsAfter[0].FirstSeenAt.ShouldBe(originalFirstSeen);
        recordsAfter[0].LastSeenAt.ShouldBeGreaterThanOrEqualTo(originalFirstSeen);
    }

    [Fact]
    public async Task SetTrustLevelAsyncWithInvalidArgumentsShouldThrow()
    {
        var storage = new InMemoryTrustStoreStorage();
        using var store = new TrustStore(storage);

        await Should.ThrowAsync<ArgumentNullException>(() =>
            store.SetTrustLevelAsync(null!, DeviceTrustLevel.Trusted, "Admin"));

        await Should.ThrowAsync<ArgumentException>(() =>
            store.SetTrustLevelAsync(UsbKeyboard, DeviceTrustLevel.Trusted, ""));
    }

    [Fact]
    public async Task EnrollInitialDevicesAsyncWhenEmptyShouldEnrollNonSyntheticDevices()
    {
        var storage = new InMemoryTrustStoreStorage();
        using var store = new TrustStore(storage);

        var devices = new[] { UsbKeyboard, DeviceIdentifier.Synthetic, GenericKeyboard };

        var enrolledCount = await store.EnrollInitialDevicesAsync(devices);

        enrolledCount.ShouldBe(2);

        var records = await store.GetAllRecordsAsync();
        records.Count.ShouldBe(2);

        (await store.GetTrustLevelAsync(UsbKeyboard)).ShouldBe(DeviceTrustLevel.Trusted);
        (await store.GetTrustLevelAsync(GenericKeyboard)).ShouldBe(DeviceTrustLevel.Trusted);
        (await store.GetTrustLevelAsync(DeviceIdentifier.Synthetic)).ShouldBe(DeviceTrustLevel.Unknown);
    }

    [Fact]
    public async Task EnrollInitialDevicesAsyncWhenNotEmptyShouldReturnZeroAndNotModify()
    {
        var storage = new InMemoryTrustStoreStorage();
        using var store = new TrustStore(storage);

        await store.SetTrustLevelAsync(UsbKeyboard, DeviceTrustLevel.Blocked, "Admin");

        var enrolledCount = await store.EnrollInitialDevicesAsync(new[] { GenericKeyboard });

        enrolledCount.ShouldBe(0);
        var records = await store.GetAllRecordsAsync();
        records.Count.ShouldBe(1);
        records[0].DeviceId.ShouldBe(UsbKeyboard);
    }

    [Fact]
    public async Task IsEmptyAsyncShouldReflectStoreStatus()
    {
        var storage = new InMemoryTrustStoreStorage();
        using var store = new TrustStore(storage);

        (await store.IsEmptyAsync()).ShouldBeTrue();

        await store.SetTrustLevelAsync(UsbKeyboard, DeviceTrustLevel.Trusted, "Admin");

        (await store.IsEmptyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task TrustStoreWhenJsonCorruptedShouldRecoverGracefully()
    {
        var storage = new InMemoryTrustStoreStorage
        {
            Content = "{ invalid json content !!!"
        };
        using var store = new TrustStore(storage);

        await store.InitializeAsync();

        (await store.IsEmptyAsync()).ShouldBeTrue();
        (await store.GetTrustLevelAsync(UsbKeyboard)).ShouldBe(DeviceTrustLevel.Unknown);
    }

    [Fact]
    public async Task FileTrustStoreStorageShouldWriteAndReadAtomicallyWithBackup()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "HIDGuard_Test_" + Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(tempDirectory, "truststore.json");
        var backupPath = filePath + ".bak";

        try
        {
            var storage = new FileTrustStoreStorage(filePath);

            var initialRead = await storage.ReadRawAsync();
            initialRead.ShouldBeNull();

            var firstContent = "{\"SchemaVersion\":1,\"Records\":[{\"First\":true}]}";
            await storage.WriteRawAsync(firstContent);

            File.Exists(filePath).ShouldBeTrue();
            File.Exists(backupPath).ShouldBeFalse();

            var readFirst = await storage.ReadRawAsync();
            readFirst.ShouldBe(firstContent);

            var secondContent = "{\"SchemaVersion\":1,\"Records\":[{\"Second\":true}]}";
            await storage.WriteRawAsync(secondContent);

            File.Exists(filePath).ShouldBeTrue();
            File.Exists(backupPath).ShouldBeTrue();

            var readSecond = await storage.ReadRawAsync();
            readSecond.ShouldBe(secondContent);

            File.Delete(filePath);
            var recoveredFromBackup = await storage.ReadRawAsync();
            recoveredFromBackup.ShouldBe(firstContent);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    [Fact]
    public void FileTrustStoreStorageWithInvalidPathShouldThrow()
    {
        Should.Throw<ArgumentException>(() => new FileTrustStoreStorage(""));
        Should.Throw<ArgumentException>(() => new FileTrustStoreStorage("   "));
    }
}
