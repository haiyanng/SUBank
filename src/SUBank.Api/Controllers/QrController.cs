using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Application.Rules;
using SUBank.Contracts.Qr;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Customer")]
[Route("api/qr")]
public sealed class QrController(IQrService service) : ControllerBase
{
    [HttpPost("generate")]
    public Task<GeneratedQr> Generate(GenerateQrRequest request, CancellationToken cancellationToken) =>
        service.GenerateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, request, cancellationToken);

    [HttpPost("decode")]
    [EnableRateLimiting("QrDecode")]
    // Cho phép phần metadata multipart; giới hạn chính xác 5 MB vẫn được kiểm tra trên IFormFile.
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<QrTransferData>> Decode(IFormFile image, CancellationToken cancellationToken)
    {
        if (image.Length <= 0 || image.Length > QrPayloadRules.MaxImageBytes)
            throw new BusinessRuleException("Ảnh QR phải có dung lượng từ 1 byte đến 5 MB.");
        if (image.ContentType is not ("image/png" or "image/jpeg" or "image/webp"))
            throw new BusinessRuleException("Chỉ hỗ trợ ảnh PNG, JPEG hoặc WebP.");
        await using var stream = new MemoryStream();
        await image.CopyToAsync(stream, cancellationToken);
        return Ok(service.Decode(stream.ToArray()));
    }
}
