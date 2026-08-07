using Blackbird.Applications.Sdk.Common;
using Blackbird.Filters.Shared;
using Blackbird.Filters.Transformations;

namespace Apps.GitLab.Models.Responses;

public class MetadataResponse
{
    [Display("Language")]
    public string? Language { get; set; }

    [Display("Source language")]
    public string? SourceLanguage { get; set; }

    [Display("Target language")]
    public string? TargetLanguage { get; set; }

    [Display("Date changed")]
    public DateTimeOffset DateChanged { get; set; }

    [Display("System reference")]
    public SystemReferenceResponse SystemReference { get; set; } = new();

    [Display("Source system reference")]
    public SystemReferenceResponse SourceSystemReference { get; set; } = new();

    [Display("Target system reference")]
    public SystemReferenceResponse TargetSystemReference { get; set; } = new();

    [Display("Provenance")]
    public ProvenanceResponse Provenance { get; set; } = new();

    public static MetadataResponse FromTransformation(
        string? language,
        DateTimeOffset dateChanged,
        SystemReference systemReference,
        Transformation transformation)
        => new()
        {
            Language = language,
            SourceLanguage = transformation.SourceLanguage,
            TargetLanguage = transformation.TargetLanguage,
            DateChanged = dateChanged,
            SystemReference = SystemReferenceResponse.FromSystemReference(systemReference),
            SourceSystemReference = SystemReferenceResponse.FromSystemReference(transformation.SourceSystemReference),
            TargetSystemReference = SystemReferenceResponse.FromSystemReference(transformation.TargetSystemReference),
            Provenance = ProvenanceResponse.FromProvenance(transformation.Provenance)
        };
}

public class SystemReferenceResponse
{
    [Display("Content ID")]
    public string? ContentId { get; set; }

    [Display("Content name")]
    public string? ContentName { get; set; }

    [Display("Admin URL")]
    public string? AdminUrl { get; set; }

    [Display("Public URL")]
    public string? PublicUrl { get; set; }

    [Display("System name")]
    public string? SystemName { get; set; }

    [Display("System reference")]
    public string? SystemRef { get; set; }

    public static SystemReferenceResponse FromSystemReference(SystemReference systemReference)
        => new()
        {
            ContentId = systemReference.ContentId,
            ContentName = systemReference.ContentName,
            AdminUrl = systemReference.AdminUrl,
            PublicUrl = systemReference.PublicUrl,
            SystemName = systemReference.SystemName,
            SystemRef = systemReference.SystemRef
        };
}

public class ProvenanceResponse
{
    [Display("Translation provenance")]
    public ProvenanceRecordResponse Translation { get; set; } = new();

    [Display("Review provenance")]
    public ProvenanceRecordResponse Review { get; set; } = new();

    public static ProvenanceResponse FromProvenance(Provenance provenance)
        => new()
        {
            Translation = ProvenanceRecordResponse.FromProvenanceRecord(provenance.Translation),
            Review = ProvenanceRecordResponse.FromProvenanceRecord(provenance.Review)
        };
}

public class ProvenanceRecordResponse
{
    [Display("Person")]
    public string? Person { get; set; }

    [Display("Person reference")]
    public string? PersonReference { get; set; }

    [Display("Organization")]
    public string? Organization { get; set; }

    [Display("Organization reference")]
    public string? OrganizationReference { get; set; }

    [Display("Tool")]
    public string? Tool { get; set; }

    [Display("Tool reference")]
    public string? ToolReference { get; set; }

    public static ProvenanceRecordResponse FromProvenanceRecord(ProvenanceRecord provenanceRecord)
        => new()
        {
            Person = provenanceRecord.Person,
            PersonReference = provenanceRecord.PersonReference,
            Organization = provenanceRecord.Organization,
            OrganizationReference = provenanceRecord.OrganizationReference,
            Tool = provenanceRecord.Tool,
            ToolReference = provenanceRecord.ToolReference
        };
}
