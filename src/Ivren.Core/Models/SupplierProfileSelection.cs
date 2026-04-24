namespace Ivren.Core.Models;

public sealed record SupplierProfileSelection(
    SupplierProfile Profile,
    string MatchSource,
    string Message);
