namespace SUBank.Application.Abstractions;

public interface IActiveSessionValidator
{
    Task<bool> IsValidAsync(string userId, string sessionId, CancellationToken cancellationToken);
}
