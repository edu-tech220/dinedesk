namespace DineDesk.Api.Options;

public class BillingOptions
{
    public const string SectionName = "Billing";

    public decimal TaxRate { get; set; } = 0.08m;

    public int MaxConcurrencyRetries { get; set; } = 3;
}
