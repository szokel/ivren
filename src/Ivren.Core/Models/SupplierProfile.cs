namespace Ivren.Core.Models;

public sealed record SupplierProfile(
    string? Name,
    string? TaxNumber,
    int NearbyLabelScanWindow,
    bool RejectDateLikeCandidates)
{
    public InvoiceDetectionOptions ToDetectionOptions()
        => new(NearbyLabelScanWindow, RejectDateLikeCandidates);
}
