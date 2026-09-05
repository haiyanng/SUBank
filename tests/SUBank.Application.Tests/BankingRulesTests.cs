using SUBank.Application.Exceptions;
using SUBank.Application.Rules;

namespace SUBank.Application.Tests;

public sealed class BankingRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.01)]
    public void ValidateAmount_RejectsNonPositiveOrFractionalAmounts(decimal amount) =>
        Assert.Throws<BusinessRuleException>(() => BankingRules.ValidateAmount(amount));

    [Theory]
    [InlineData(1)]
    [InlineData(999999999999999999)]
    public void ValidateAmount_AcceptsPositiveWholeVndAmount(decimal amount) =>
        BankingRules.ValidateAmount(amount);

    [Fact]
    public void ValidateIdempotencyKey_RejectsMissingAndOversizedValues()
    {
        Assert.Throws<BusinessRuleException>(() => BankingRules.ValidateIdempotencyKey(null));
        Assert.Throws<BusinessRuleException>(() => BankingRules.ValidateIdempotencyKey(" "));
        Assert.Throws<BusinessRuleException>(() => BankingRules.ValidateIdempotencyKey(new string('x', 65)));
        BankingRules.ValidateIdempotencyKey(new string('x', 64));
    }

    [Fact]
    public void NormalizeDescription_TrimsAndRejectsOversizedValue()
    {
        Assert.Null(BankingRules.NormalizeDescription("  "));
        Assert.Equal("Chuyen tien", BankingRules.NormalizeDescription("  Chuyen tien  "));
        Assert.Throws<BusinessRuleException>(() => BankingRules.NormalizeDescription(new string('x', 281)));
    }
}
