using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Contracts.AddressChanges;
using SUBank.Domain.Entities;
using SUBank.Domain.Enums;
using SUBank.Infrastructure.Persistence;

namespace SUBank.Infrastructure.AddressChanges;

public sealed class AddressChangeService(SUBankDbContext dbContext) : IAddressChangeService
{
    public async Task<AddressChangeRequestSummary> CreateAsync(string userId, CreateAddressChangeRequest request,
        CancellationToken cancellationToken)
    {
        var permanent = NormalizeRequired(request.PermanentAddress, "Địa chỉ thường trú", 500);
        var temporary = NormalizeOptional(request.TemporaryAddress, "Địa chỉ tạm trú", 500);
        var profile = await dbContext.CustomerProfiles.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ khách hàng.");
        if (await dbContext.AddressChangeRequests.AnyAsync(
                x => x.CustomerProfileId == profile.Id && x.Status == AddressChangeRequestStatus.Pending, cancellationToken))
            throw new ConflictException("Bạn đang có một yêu cầu đổi địa chỉ chờ xử lý.");
        if (profile.PermanentAddress == permanent && profile.TemporaryAddress == temporary)
            throw new BusinessRuleException("Địa chỉ mới không khác hồ sơ hiện tại.");

        var item = new AddressChangeRequest
        {
            RequestNo = NewRequestNo(), CustomerProfileId = profile.Id, PermanentAddress = permanent,
            TemporaryAddress = temporary, Status = AddressChangeRequestStatus.Pending, RequestedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.AddressChangeRequests.Add(item);
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "ADDRESS_CHANGE_REQUESTED", EntityType = "AddressChangeRequest",
            EntityId = item.RequestNo, Result = AuditResult.Success, CreatedAtUtc = item.RequestedAtUtc
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSummary(item, profile.FullName);
    }

    public async Task<IReadOnlyList<AddressChangeRequestSummary>> GetMineAsync(string userId, CancellationToken cancellationToken) =>
        await dbContext.AddressChangeRequests.AsNoTracking().Where(x => x.CustomerProfile.UserId == userId)
            .OrderByDescending(x => x.RequestedAtUtc).Select(x => new AddressChangeRequestSummary(
                x.RequestNo, x.CustomerProfile.FullName, x.PermanentAddress, x.TemporaryAddress, x.Status.ToString(),
                x.RequestedAtUtc, x.DecidedAtUtc, x.RejectionReason))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AddressChangeRequestSummary>> GetPendingAsync(CancellationToken cancellationToken) =>
        await dbContext.AddressChangeRequests.AsNoTracking().Where(x => x.Status == AddressChangeRequestStatus.Pending)
            .OrderBy(x => x.RequestedAtUtc).Select(x => new AddressChangeRequestSummary(
                x.RequestNo, x.CustomerProfile.FullName, x.PermanentAddress, x.TemporaryAddress, x.Status.ToString(),
                x.RequestedAtUtc, x.DecidedAtUtc, x.RejectionReason))
            .ToListAsync(cancellationToken);

    public Task ApproveAsync(string adminUserId, string requestNo, CancellationToken cancellationToken) =>
        DecideAsync(adminUserId, requestNo, true, null, cancellationToken);

    public Task RejectAsync(string adminUserId, string requestNo, RejectAddressChangeRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(adminUserId, requestNo, false, NormalizeRequired(request.Reason, "Lý do từ chối", 280), cancellationToken);

    private async Task DecideAsync(string adminUserId, string requestNo, bool approve, string? reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var item = await dbContext.AddressChangeRequests.Include(x => x.CustomerProfile)
            .SingleOrDefaultAsync(x => x.RequestNo == requestNo, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy yêu cầu đổi địa chỉ.");
        if (item.Status != AddressChangeRequestStatus.Pending)
            throw new ConflictException("Yêu cầu này đã được xử lý.");

        var now = DateTimeOffset.UtcNow;
        item.Status = approve ? AddressChangeRequestStatus.Approved : AddressChangeRequestStatus.Rejected;
        item.DecidedAtUtc = now;
        item.DecidedByUserId = adminUserId;
        item.RejectionReason = reason;
        if (approve)
        {
            item.CustomerProfile.PermanentAddress = item.PermanentAddress;
            item.CustomerProfile.TemporaryAddress = item.TemporaryAddress;
            item.CustomerProfile.UpdatedAtUtc = now;
        }
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId, Action = approve ? "ADDRESS_CHANGE_APPROVED" : "ADDRESS_CHANGE_REJECTED",
            EntityType = "AddressChangeRequest", EntityId = item.RequestNo, Result = AuditResult.Success, CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static AddressChangeRequestSummary ToSummary(AddressChangeRequest item, string customerName) =>
        new(item.RequestNo, customerName, item.PermanentAddress, item.TemporaryAddress, item.Status.ToString(),
            item.RequestedAtUtc, item.DecidedAtUtc, item.RejectionReason);

    private static string NormalizeRequired(string? value, string field, int maxLength)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result)) throw new BusinessRuleException($"{field} là bắt buộc.");
        if (result.Length > maxLength) throw new BusinessRuleException($"{field} không được vượt quá {maxLength} ký tự.");
        return result;
    }

    private static string? NormalizeOptional(string? value, string field, int maxLength)
    {
        var result = value?.Trim();
        if (string.IsNullOrEmpty(result)) return null;
        if (result.Length > maxLength) throw new BusinessRuleException($"{field} không được vượt quá {maxLength} ký tự.");
        return result;
    }

    private static string NewRequestNo() =>
        $"ADR{DateTime.UtcNow:yyyyMMddHHmmssfff}{RandomNumberGenerator.GetInt32(1000, 9999)}";
}
