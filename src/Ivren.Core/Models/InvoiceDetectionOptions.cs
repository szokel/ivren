namespace Ivren.Core.Models;

public sealed record InvoiceDetectionOptions(
    int NearbyLabelScanWindow = 4,
    bool RejectDateLikeCandidates = true);
