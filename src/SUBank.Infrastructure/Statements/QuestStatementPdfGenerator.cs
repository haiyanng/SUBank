using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Statements;

namespace SUBank.Infrastructure.Statements;

public sealed class QuestStatementPdfGenerator : IStatementPdfGenerator
{
    public QuestStatementPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Generate(AccountStatement statement) => Document.Create(document => document.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(30);
        page.DefaultTextStyle(x => x.FontSize(9));
        page.Header().Column(column =>
        {
            column.Item().Text("SUBank - SAO KE TAI KHOAN").Bold().FontSize(18).FontColor(Colors.Orange.Darken2);
            column.Item().Text($"Tai khoan: {statement.AccountNumber} | {statement.Currency}");
            column.Item().Text($"Ky: {VietnamTime(statement.FromUtc):dd/MM/yyyy} - {VietnamTime(statement.ToUtc.AddTicks(-1)):dd/MM/yyyy}");
        });
        page.Content().PaddingVertical(15).Column(column =>
        {
            column.Spacing(8);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"So du dau ky: {statement.OpeningBalance:N0}");
                row.RelativeItem().Text($"So du cuoi ky: {statement.ClosingBalance:N0}").Bold();
            });
            column.Item().Text($"Tong ghi co: {statement.TotalCredit:N0} | Tong ghi no: {statement.TotalDebit:N0}");
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(70); columns.RelativeColumn(2); columns.RelativeColumn(); columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Thoi gian");
                    header.Cell().Element(HeaderCell).Text("Ma giao dich");
                    header.Cell().Element(HeaderCell).Text("Chieu");
                    header.Cell().Element(HeaderCell).AlignRight().Text("So tien");
                });
                foreach (var item in statement.Transactions)
                {
                    table.Cell().Element(BodyCell).Text(VietnamTime(item.CreatedAtUtc).ToString("dd/MM HH:mm"));
                    table.Cell().Element(BodyCell).Text(item.ReferenceNo);
                    table.Cell().Element(BodyCell).Text(item.Direction);
                    table.Cell().Element(BodyCell).AlignRight().Text(item.Amount.ToString("N0"));
                }
            });
        });
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Trang "); text.CurrentPageNumber(); text.Span("/"); text.TotalPages();
        });
    })).GeneratePdf();

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(Colors.Grey.Lighten2).Padding(4).DefaultTextStyle(x => x.Bold());
    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4);
    private static DateTimeOffset VietnamTime(DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(7));
}
