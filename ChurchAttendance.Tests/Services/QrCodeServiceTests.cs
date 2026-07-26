using ChurchAttendance.Services;

namespace ChurchAttendance.Tests.Services;

public class QrCodeServiceTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void GeneratePng_ReturnsBytesStartingWithPngSignature()
    {
        var png = QrCodeService.GeneratePng("https://example.org/card/abc123");

        Assert.True(png.Length > PngSignature.Length);
        Assert.Equal(PngSignature, png[..PngSignature.Length]);
    }

    [Fact]
    public void GeneratePng_DifferentContentProducesDifferentBytes()
    {
        var pngA = QrCodeService.GeneratePng("https://example.org/card/aaa");
        var pngB = QrCodeService.GeneratePng("https://example.org/card/bbb");

        Assert.NotEqual(pngA, pngB);
    }

    [Fact]
    public void GeneratePng_LargerPixelsPerModuleProducesLargerImage()
    {
        var small = QrCodeService.GeneratePng("https://example.org/card/abc123", pixelsPerModule: 4);
        var large = QrCodeService.GeneratePng("https://example.org/card/abc123", pixelsPerModule: 20);

        Assert.True(large.Length > small.Length);
    }
}
