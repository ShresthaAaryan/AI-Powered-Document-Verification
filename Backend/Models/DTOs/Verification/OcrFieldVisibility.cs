namespace DocumentVerification.API.Models.DTOs.Verification;

/// <summary>
/// OCR fields stored on the entity for scoring / internal checks but not returned to end users.
/// </summary>
public static class OcrFieldVisibility
{
    /// <summary>Keys never included in public API <see cref="OcrResultDto.ExtractedFields"/>.</summary>
    public static readonly HashSet<string> InternalOnlyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "hasTopLeftNationalEmblem",
        "hasTopRightNepalFlag",
        // Citizenship security heuristics: used only for scoring.
        "hasNationalEmblemFront",
        "hasFrontStamp",
        "hasBackSignatureWithDate",
        // Same value as documentNumber (NIN); show once as documentNumber only.
        "nationalIdentityNumber",
    };

    public static Dictionary<string, ExtractedFieldDto>? ForPublicApi(Dictionary<string, ExtractedFieldDto>? fields)
    {
        if (fields == null || fields.Count == 0)
            return fields;

        return fields
            .Where(kv => !InternalOnlyKeys.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
