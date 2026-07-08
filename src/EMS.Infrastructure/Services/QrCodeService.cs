using EMS.Application.Common.Interfaces;
using QRCoder;

namespace EMS.Infrastructure.Services;

public sealed class QrCodeService : IQrCodeGenerator
{
    public byte[] GeneratePng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule: 8);
    }
}
