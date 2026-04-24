using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class JsonSupplierProfileProvider : ISupplierProfileProvider
{
    private static readonly Regex TaxNumberRegex = new(
        @"\b\d{8}-\d-\d{2}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly SupplierProfilesConfiguration _configuration;

    public JsonSupplierProfileProvider(string? configurationFilePath = null)
    {
        _configuration = LoadConfiguration(configurationFilePath);
    }

    public SupplierProfileSelection SelectProfile(
        XmlInvoiceExtractionResult xmlExtractionResult,
        TextExtractionResult textExtractionResult)
    {
        ArgumentNullException.ThrowIfNull(xmlExtractionResult);
        ArgumentNullException.ThrowIfNull(textExtractionResult);

        var xmlTaxNumbers = ExtractTaxNumbersFromXml(xmlExtractionResult);
        var xmlTaxMatch = FindProfileByTaxNumber(xmlTaxNumbers);
        if (xmlTaxMatch is not null)
        {
            return new SupplierProfileSelection(
                xmlTaxMatch,
                "XmlTaxNumber",
                BuildSelectedProfileMessage(xmlTaxMatch, "XML supplier tax number"));
        }

        var textTaxNumbers = TaxNumberRegex.Matches(textExtractionResult.FullText)
            .Select(static match => match.Value)
            .ToArray();
        var textTaxMatch = FindProfileByTaxNumber(textTaxNumbers);
        if (textTaxMatch is not null)
        {
            return new SupplierProfileSelection(
                textTaxMatch,
                "TextTaxNumber",
                BuildSelectedProfileMessage(textTaxMatch, "text supplier tax number"));
        }

        var nameMatch = FindProfileByName(textExtractionResult.FullText);
        if (nameMatch is not null)
        {
            return new SupplierProfileSelection(
                nameMatch,
                "SupplierName",
                BuildSelectedProfileMessage(nameMatch, "supplier name"));
        }

        return new SupplierProfileSelection(
            _configuration.Default,
            "Default",
            $"Using default supplier profile. Nearby label scan window: {_configuration.Default.NearbyLabelScanWindow}.");
    }

    private SupplierProfile? FindProfileByTaxNumber(IReadOnlyCollection<string> taxNumbers)
    {
        if (taxNumbers.Count == 0)
        {
            return null;
        }

        return _configuration.Suppliers.FirstOrDefault(profile =>
            !string.IsNullOrWhiteSpace(profile.TaxNumber)
            && taxNumbers.Contains(profile.TaxNumber, StringComparer.OrdinalIgnoreCase));
    }

    private SupplierProfile? FindProfileByName(string text)
        => _configuration.Suppliers.FirstOrDefault(profile =>
            !string.IsNullOrWhiteSpace(profile.Name)
            && text.Contains(profile.Name, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> ExtractTaxNumbersFromXml(XmlInvoiceExtractionResult xmlExtractionResult)
    {
        var taxNumbers = new List<string>();

        foreach (var document in xmlExtractionResult.Documents)
        {
            try
            {
                var xDocument = XDocument.Parse(document.Content, LoadOptions.None);
                taxNumbers.AddRange(TaxNumberRegex.Matches(xDocument.ToString(SaveOptions.DisableFormatting))
                    .Select(static match => match.Value));
            }
            catch
            {
                taxNumbers.AddRange(TaxNumberRegex.Matches(document.Content)
                    .Select(static match => match.Value));
            }
        }

        return taxNumbers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildSelectedProfileMessage(SupplierProfile profile, string matchSource)
        => $"Using supplier profile '{profile.Name ?? profile.TaxNumber ?? "Unnamed"}' matched by {matchSource}. Nearby label scan window: {profile.NearbyLabelScanWindow}.";

    private static SupplierProfilesConfiguration LoadConfiguration(string? configurationFilePath)
    {
        if (string.IsNullOrWhiteSpace(configurationFilePath) || !File.Exists(configurationFilePath))
        {
            return SupplierProfilesConfiguration.CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(configurationFilePath);
            var fileConfiguration = JsonSerializer.Deserialize<SupplierProfilesFileConfiguration>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            return SupplierProfilesConfiguration.FromFile(fileConfiguration);
        }
        catch
        {
            return SupplierProfilesConfiguration.CreateDefault();
        }
    }

    private sealed record SupplierProfilesFileConfiguration(
        SupplierProfileConfiguration? Default,
        SupplierProfileConfiguration[]? Suppliers);

    private sealed record SupplierProfileConfiguration(
        string? Name,
        string? TaxNumber,
        int? NearbyLabelScanWindow,
        bool? RejectDateLikeCandidates);

    private sealed record SupplierProfilesConfiguration(
        SupplierProfile Default,
        SupplierProfile[] Suppliers)
    {
        public static SupplierProfilesConfiguration CreateDefault()
            => new(
                new SupplierProfile("Default", null, NearbyLabelScanWindow: 4, RejectDateLikeCandidates: true),
                []);

        public static SupplierProfilesConfiguration FromFile(SupplierProfilesFileConfiguration? configuration)
        {
            if (configuration is null)
            {
                return CreateDefault();
            }

            var fallbackDefault = CreateDefault().Default;
            var defaultProfile = ToProfile(configuration.Default, fallbackDefault, "Default");
            var suppliers = (configuration.Suppliers ?? [])
                .Select(supplier => ToProfile(supplier, defaultProfile, supplier.Name ?? supplier.TaxNumber))
                .ToArray();

            return new SupplierProfilesConfiguration(defaultProfile, suppliers);
        }

        private static SupplierProfile ToProfile(
            SupplierProfileConfiguration? configuration,
            SupplierProfile fallback,
            string? fallbackName)
        {
            if (configuration is null)
            {
                return fallback;
            }

            var nearbyLabelScanWindow = configuration.NearbyLabelScanWindow.GetValueOrDefault(fallback.NearbyLabelScanWindow);
            if (nearbyLabelScanWindow < 1)
            {
                nearbyLabelScanWindow = fallback.NearbyLabelScanWindow;
            }

            return new SupplierProfile(
                configuration.Name ?? fallbackName,
                configuration.TaxNumber,
                nearbyLabelScanWindow,
                configuration.RejectDateLikeCandidates.GetValueOrDefault(fallback.RejectDateLikeCandidates));
        }
    }
}
