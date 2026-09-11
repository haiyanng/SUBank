using SUBank.Contracts.Qr;

namespace SUBank.Application.Abstractions;

public interface IQrService
{
    Task<GeneratedQr> GenerateAsync(string userId, GenerateQrRequest request, CancellationToken cancellationToken);
    QrTransferData Decode(ReadOnlyMemory<byte> image);
}
