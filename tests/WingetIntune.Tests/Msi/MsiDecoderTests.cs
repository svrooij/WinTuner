using WingetIntune.Msi;

namespace WingetIntune.Tests.Msi;

public class MsiDecoderTests
{
    Task<byte[]> msiBytes;

    public MsiDecoderTests()
    {
        using var client = new HttpClient();

        // Using LAPS 6.2.0.0 as it's small (about 1MB) and likely to stay up forever
        msiBytes = client.GetByteArrayAsync("https://download.microsoft.com/download/C/7/A/C7AAD914-A8A6-4904-88A1-29E657445D03/LAPS.x64.msi");
    }

    [Fact]
    public async Task GetCode_ReturnsCorrectString()
    {
        var msiStream = new MemoryStream(await msiBytes);

        var decoder = new MsiDecoder(msiStream);
        var codeFromMsi = decoder.GetCode();
        Assert.Equal("{97E2CA7B-B657-4FF7-A6DB-30ECC73E1E28}", codeFromMsi);
    }

    [Fact]
    public async Task GetVersion_ReturnsCorrectString()
    {
        var msiStream = new MemoryStream(await msiBytes);

        var decoder = new MsiDecoder(msiStream);
        var versionFromMsi = decoder.GetVersion();

        Assert.Equal("6.2.0.0", versionFromMsi);
    }
}
