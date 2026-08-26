using SUBank.Application.Exceptions;
using SUBank.Application.Rules;

namespace SUBank.Application.Tests;

public sealed class QrPayloadRulesTests
{
    [Fact]
    public void CreateAndParse_PreserveTransferData()
    {
        var payload = QrPayloadRules.Create("1000000001", 1234.50m, "  Thanh toán demo  ");

        var result = QrPayloadRules.Parse(payload);

        Assert.Equal("1000000001", result.AccountNumber);
        Assert.Equal(1234.50m, result.Amount);
        Assert.Equal("Thanh toán demo", result.Message);
    }

    [Theory]
    [InlineData("https://example.com/transfer?v=1&account=1000000001")]
    [InlineData("subank://transfer?v=2&account=1000000001")]
    [InlineData("subank://transfer?v=1&account=abc")]
    [InlineData("subank://transfer?v=1&account=1000000001&amount=-1")]
    [InlineData("subank://transfer?v=1&v=1&account=1000000001")]
    public void Parse_RejectsForeignOrMalformedPayload(string payload) =>
        Assert.Throws<BusinessRuleException>(() => QrPayloadRules.Parse(payload));
}
