using SUBank.Domain.Entities;
using SUBank.Domain.Enums;

namespace SUBank.Domain.Tests;

public sealed class BankAccountTests
{
    [Fact]
    public void NewAccount_UsesVndAndActiveStatusByDefault()
    {
        var account = new BankAccount { AccountNumber = "1000000001" };
        Assert.Equal("VND", account.Currency);
        Assert.Equal(AccountStatus.Active, account.Status);
    }
}
