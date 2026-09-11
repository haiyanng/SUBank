using SUBank.Contracts.Statements;

namespace SUBank.Application.Abstractions;

public interface IStatementPdfGenerator
{
    byte[] Generate(AccountStatement statement);
}
