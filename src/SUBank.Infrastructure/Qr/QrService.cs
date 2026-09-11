using Microsoft.EntityFrameworkCore;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Application.Rules;
using SUBank.Contracts.Qr;
using SUBank.Domain.Enums;
using SUBank.Infrastructure.Persistence;
using ZXing;
using ZXing.ImageSharp;

namespace SUBank.Infrastructure.Qr;

public sealed class QrService(SUBankDbContext dbContext) : IQrService
{
    public async Task<GeneratedQr> GenerateAsync(string userId, GenerateQrRequest request, CancellationToken cancellationToken)
    {
        var owned = await dbContext.BankAccounts.AsNoTracking().AnyAsync(x => x.AccountNumber == request.AccountNumber &&
            x.CustomerProfile.UserId == userId && x.Status == AccountStatus.Active, cancellationToken);
        if (!owned) throw new NotFoundException("Không tìm thấy tài khoản nhận hợp lệ.");
        var payload = QrPayloadRules.Create(request.AccountNumber, request.Amount, request.Message);
        using var data = QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return new GeneratedQr(payload, Convert.ToBase64String(png.GetGraphic(10)));
    }

    public QrTransferData Decode(ReadOnlyMemory<byte> image)
    {
        if (image.IsEmpty || image.Length > QrPayloadRules.MaxImageBytes)
            throw new BusinessRuleException("Ảnh QR phải có dung lượng từ 1 byte đến 5 MB.");
        try
        {
            using var decodedImage = Image.Load<Rgba32>(image.Span);
            if (decodedImage.Width > 4_096 || decodedImage.Height > 4_096)
                throw new BusinessRuleException("Kích thước ảnh QR quá lớn.");
            var reader = new ZXing.ImageSharp.BarcodeReader<Rgba32> { AutoRotate = true, Options = { TryHarder = true } };
            var result = reader.Decode(decodedImage)
                ?? throw new BusinessRuleException("Không tìm thấy QR hợp lệ trong ảnh.");
            return QrPayloadRules.Parse(result.Text);
        }
        catch (UnknownImageFormatException)
        {
            throw new BusinessRuleException("Định dạng ảnh QR không được hỗ trợ.");
        }
    }
}
