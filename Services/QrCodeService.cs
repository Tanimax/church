using QRCoder;

namespace ChurchAttendance.Services;

public static class QrCodeService
{
    public static byte[] GeneratePng(string content, int pixelsPerModule = 20)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var pngQrCode = new PngByteQRCode(data);
        return pngQrCode.GetGraphic(pixelsPerModule);
    }
}
