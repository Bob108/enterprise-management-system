namespace EMS.Application.Common.Interfaces;

public interface IQrCodeGenerator
{
    /// <summary>Renders the content as a PNG QR code.</summary>
    byte[] GeneratePng(string content);
}
