using DocumentVerification.API.Data;
using DocumentVerification.API.Models.DTOs.Verification;
using DocumentVerification.API.Models.Entities;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json;

namespace DocumentVerification.API.Services;

public class AIAnalysisService : IAIAnalysisService
{
    private readonly DocumentVerificationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIAnalysisService> _logger;

    public AIAnalysisService(
        DocumentVerificationDbContext context,
        IConfiguration configuration,
        ILogger<AIAnalysisService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthenticityScoreDto> AnalyzeDocumentAsync(
        Guid verificationId,
        string imagePath,
        Dictionary<string, ExtractedFieldDto> extractedFields,
        string? rawText = null)
    {
        try
        {
            _logger.LogInformation("Starting AI analysis for verification: {VerificationId}", verificationId);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Get verification details
            var verification = await _context.Verifications.FindAsync(verificationId);
            if (verification == null)
            {
                throw new InvalidOperationException("Verification not found");
            }

            // Log extracted fields for debugging
            _logger.LogInformation("Extracted fields for verification {VerificationId}: {FieldCount} fields", 
                verificationId, extractedFields.Count);
            foreach (var field in extractedFields)
            {
                _logger.LogInformation("  - {FieldName}: {Value} (Confidence: {Confidence})", 
                    field.Key, field.Value?.Value ?? "null", field.Value?.Confidence ?? 0);
            }

            // Calculate individual scores
            var fieldCompletenessScore = await CalculateFieldCompletenessScore(extractedFields, verification.DocumentType);
            var formatConsistencyScore = await CalculateFormatConsistencyScore(extractedFields, verification.DocumentType);
            var imageQualityScore = await CalculateImageQualityScore(imagePath);
            var templateMatchScore = CalculateTemplateMatchScore(rawText ?? string.Empty, verification.DocumentType);
            var securityFeaturesScore = await CalculateSecurityFeaturesScore(imagePath);
            var metadataConsistencyScore = await CalculateMetadataConsistencyScore(imagePath);
            
            _logger.LogInformation("Individual scores for verification {VerificationId}: " +
                "FieldCompleteness={FieldCompleteness}, FormatConsistency={FormatConsistency}, TemplateMatch={TemplateMatch}, " +
                "ImageQuality={ImageQuality}, SecurityFeatures={SecurityFeatures}, MetadataConsistency={MetadataConsistency}",
                verificationId, fieldCompletenessScore, formatConsistencyScore, templateMatchScore,
                imageQualityScore, securityFeaturesScore, metadataConsistencyScore);

            // Calculate overall score using weighted average
            var overallScore = CalculateOverallScore(
                fieldCompletenessScore,
                formatConsistencyScore,
                templateMatchScore,
                imageQualityScore,
                securityFeaturesScore,
                metadataConsistencyScore
            );

            // Determine classification
            var classification = DetermineClassification(overallScore);

            stopwatch.Stop();

            // Create detailed analysis object
            var detailedAnalysis = new
            {
                fieldCompleteness = new { score = fieldCompletenessScore, weight = 25 },
                formatConsistency = new { score = formatConsistencyScore, weight = 20 },
                imageQuality = new
                {
                    score = imageQualityScore,
                    weight = 25,
                    details = await GetImageQualityDetails(imagePath)
                },
                templateMatch = new { score = templateMatchScore, weight = 15 },
                securityFeatures = new { score = securityFeaturesScore, weight = 15 },
                metadataConsistency = new { score = metadataConsistencyScore, weight = 10 }
            };

            // Save to database
            var authenticityScore = new AuthenticityScore
            {
                Id = Guid.NewGuid(),
                VerificationId = verificationId,
                OverallScore = overallScore,
                Classification = classification,
                FieldCompletenessScore = fieldCompletenessScore,
                FormatConsistencyScore = formatConsistencyScore,
                ImageQualityScore = imageQualityScore,
                SecurityFeaturesScore = securityFeaturesScore, // now includes template-match weighting in overall score
                MetadataConsistencyScore = metadataConsistencyScore,
                DetailedAnalysis = JsonSerializer.Serialize(detailedAnalysis),
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                ModelVersion = _configuration["ONNX:ModelVersion"] ?? "1.0.0",
                CreatedAt = DateTime.UtcNow
            };

            _context.AuthenticityScores.Add(authenticityScore);
            await _context.SaveChangesAsync();

            _logger.LogInformation("AI analysis completed for verification: {VerificationId}, Score: {Score}, Classification: {Classification}",
                verificationId, overallScore, classification);

            return new AuthenticityScoreDto
            {
                Id = authenticityScore.Id,
                OverallScore = authenticityScore.OverallScore,
                Classification = authenticityScore.Classification,
                FieldCompletenessScore = authenticityScore.FieldCompletenessScore,
                FormatConsistencyScore = authenticityScore.FormatConsistencyScore,
                ImageQualityScore = authenticityScore.ImageQualityScore,
                SecurityFeaturesScore = authenticityScore.SecurityFeaturesScore,
                MetadataConsistencyScore = authenticityScore.MetadataConsistencyScore,
                DetailedAnalysis = detailedAnalysis,
                ProcessingTimeMs = authenticityScore.ProcessingTimeMs,
                ModelVersion = authenticityScore.ModelVersion
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI analysis for verification: {VerificationId}", verificationId);
            throw;
        }
    }

    public async Task<int> CalculateFieldCompletenessScore(Dictionary<string, ExtractedFieldDto> fields, string documentType)
    {
        await Task.CompletedTask;

        try
        {
            var requiredFields = GetRequiredFields(documentType);
            _logger.LogInformation("Required fields for {DocumentType}: {RequiredFields}", 
                documentType, string.Join(", ", requiredFields));
            
            // Check for present fields
            var presentFields = new List<string>();
            var missingFields = new List<string>();
            
            foreach (var requiredField in requiredFields)
            {
                // Check if field exists directly
                if (fields.ContainsKey(requiredField) && !string.IsNullOrWhiteSpace(fields[requiredField]?.Value))
                {
                    presentFields.Add(requiredField);
                }
                // Special handling: if fullName is required but we have surname + givenNames, count it
                else if (requiredField == "fullName" && 
                         (fields.ContainsKey("surname") || fields.ContainsKey("givenNames")))
                {
                    // Give partial credit if we have at least surname or givenNames
                    if (fields.ContainsKey("surname") && fields.ContainsKey("givenNames"))
                    {
                        presentFields.Add(requiredField); // Full credit if both exist
                    }
                    else
                    {
                        // Partial credit (50%) - we'll handle this in the calculation
                    }
                }
                else
                {
                    missingFields.Add(requiredField);
                }
            }

            if (requiredFields.Count == 0)
                return 0;

            // Calculate base completeness
            var baseCompleteness = (double)presentFields.Count / requiredFields.Count;
            
            // Give partial credit for fullName if we have surname or givenNames but not both
            if (requiredFields.Contains("fullName") && !presentFields.Contains("fullName"))
            {
                var hasSurname = fields.ContainsKey("surname") && !string.IsNullOrWhiteSpace(fields["surname"]?.Value);
                var hasGivenNames = fields.ContainsKey("givenNames") && !string.IsNullOrWhiteSpace(fields["givenNames"]?.Value);
                
                if (hasSurname || hasGivenNames)
                {
                    // Add 0.5 to the present count for partial credit
                    baseCompleteness = ((double)presentFields.Count + 0.5) / requiredFields.Count;
                }
            }

            var score = (int)(baseCompleteness * 100);
            _logger.LogInformation("Field completeness: {PresentCount}/{RequiredCount} = {Score}% (Missing: {MissingFields})", 
                presentFields.Count, requiredFields.Count, score, string.Join(", ", missingFields));
            
            return score;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating field completeness score");
            return 0;
        }
    }

    public async Task<int> CalculateFormatConsistencyScore(Dictionary<string, ExtractedFieldDto> fields, string documentType)
    {
        await Task.CompletedTask;

        try
        {
            if (fields.Count == 0)
            {
                _logger.LogWarning("No fields to validate for format consistency");
                return 0;
            }

            var totalFields = 0;
            var validFields = 0;

            foreach (var field in fields)
            {
                // Skip non-critical fields in format validation
                if (field.Key == "personalNumber" || field.Key == "placeOfBirth" || 
                    field.Key == "dateOfIssue" || field.Key == "citizenshipType")
                {
                    continue; // These are optional fields
                }

                totalFields++;

                if (ValidateFieldFormat(field.Key, field.Value?.Value))
                {
                    validFields++;
                }
                else
                {
                    _logger.LogDebug("Field {FieldName} with value '{Value}' failed format validation", 
                        field.Key, field.Value?.Value ?? "null");
                }
            }

            if (totalFields == 0)
                return 100; // If no critical fields, give full score

            var score = (int)((double)validFields / totalFields * 100);
            _logger.LogInformation("Format consistency: {ValidCount}/{TotalCount} = {Score}%", 
                validFields, totalFields, score);
            
            return score;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating format consistency score");
            return 0;
        }
    }

    public async Task<int> CalculateImageQualityScore(string imagePath)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imagePath);

            var scores = new List<int>();

            // Resolution check (fast)
            var resolutionScore = CalculateResolutionScore(image.Width, image.Height);
            scores.Add(resolutionScore);

            // Blur detection (optimized)
            var blurScore = await CalculateBlurScore(imagePath);
            scores.Add(blurScore);

            // Brightness and contrast (optimized)
            var brightnessContrastScore = CalculateBrightnessContrastScore(image);
            scores.Add(brightnessContrastScore);

            return scores.Count > 0 ? (int)scores.Average() : 50;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating image quality score for: {ImagePath}", imagePath);
            return 50; // Default score instead of 0
        }
    }

    public async Task<int> CalculateSecurityFeaturesScore(string imagePath)
    {
        await Task.CompletedTask;

        try
        {
            // Optimized - simplified security checks for faster processing
            // In production, these would be more sophisticated
            var score = 50; // Default baseline score

            // Quick checks without heavy image processing
            // Check for hologram patterns (simplified detection)
            if (CheckForHologramPatterns(imagePath)) score += 25;

            // Check for watermark patterns
            if (CheckForWatermarkPatterns(imagePath)) score += 15;

            // Check for microprint (simplified)
            if (CheckForMicroprintPatterns(imagePath)) score += 10;

            return Math.Min(score, 100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating security features score for: {ImagePath}", imagePath);
            return 50; // Default score instead of 0
        }
    }

    public async Task<int> CalculateMetadataConsistencyScore(string imagePath)
    {
        try
        {
            using var image = await Image.LoadAsync(imagePath);
            var score = 0;

            // Check EXIF data consistency
            if (image.Metadata.ExifProfile != null)
            {
                score += 25;

                // Check creation date
                if (image.Metadata.ExifProfile.Values.Any(v => v.Tag == ExifTag.DateTimeOriginal))
                {
                    score += 25;
                }
            }
            else
            {
                // Missing EXIF data might indicate manipulation
                score += 10;
            }

            // Check file format consistency
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    score += 25;
                    break;
                case ".png":
                    score += 20;
                    break;
                default:
                    score += 15;
                    break;
            }

            // Check for compression artifacts
            using var rgbImage = image.CloneAs<Rgb24>();
            score += CheckCompressionConsistency(rgbImage) ? 25 : 0;

            return Math.Min(score, 100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating metadata consistency score for: {ImagePath}", imagePath);
            return 0;
        }
    }

    private int CalculateOverallScore(
        int fieldCompleteness,
        int formatConsistency,
        int templateMatch,
        int imageQuality,
        int securityFeatures,
        int metadataConsistency)
    {
        var weights = new Dictionary<string, double>
        {
            ["fieldCompleteness"] = 0.22,
            ["formatConsistency"] = 0.18,
            ["templateMatch"] = 0.15,
            ["imageQuality"] = 0.20,
            ["securityFeatures"] = 0.15,
            ["metadataConsistency"] = 0.10
        };

        var overallScore = (
            fieldCompleteness * weights["fieldCompleteness"] +
            formatConsistency * weights["formatConsistency"] +
            templateMatch * weights["templateMatch"] +
            imageQuality * weights["imageQuality"] +
            securityFeatures * weights["securityFeatures"] +
            metadataConsistency * weights["metadataConsistency"]
        );

        return (int)Math.Round(overallScore);
    }

    private int CalculateTemplateMatchScore(string rawText, string documentType)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return 40; // neutral default when OCR text unavailable
        }

        var text = rawText.ToUpperInvariant();
        var score = 0;

        bool ContainsAll(params string[] tokens) => tokens.All(t => text.Contains(t));
        bool ContainsAny(params string[] tokens) => tokens.Any(t => text.Contains(t));

        switch (documentType.ToLowerInvariant())
        {
            case "passport":
                // Look for MRZ markers and passport keywords
                if (ContainsAny("P<", "<<", "<<<")) score += 40;
                if (ContainsAll("PASSPORT", "NEPAL")) score += 25;
                if (ContainsAny("NPL", "NEPAL", "NEPALESE")) score += 15;
                if (ContainsAny("DATE OF ISSUE", "DATE OF EXPIRY", "DOB", "SEX")) score += 10;
                break;

            case "nationalid":
            case "national id":
                if (ContainsAll("NATIONAL", "IDENTITY")) score += 35;
                if (ContainsAny("NEPAL", "NEPALESE", "NEPALI")) score += 20;
                if (ContainsAny("NIN", "IDENTITY NUMBER", "NATIONAL IDENTITY NUMBER")) score += 25;
                if (ContainsAny("DATE OF BIRTH", "DOB")) score += 10;
                if (ContainsAny("SIGNATURE", "MOTHER", "FATHER")) score += 10;
                break;

            case "citizenshipcard":
            case "citizenship":
                if (ContainsAny("CITIZENSHIP", "नागरिकता")) score += 40;
                if (ContainsAny("GOVERNMENT OF NEPAL", "नेपाल सरकार")) score += 20;
                if (ContainsAny("DISTRICT", "जिल्ला")) score += 15;
                if (ContainsAny("DATE OF ISSUE", "जारी", "DOB", "DATE OF BIRTH", "जन्म")) score += 15;
                break;

            case "driverslicense":
            case "driver's license":
            case "drivers license":
                if (ContainsAny("DRIVING LICENSE", "DRIVER", "DL.NO", "D.L.NO")) score += 40;
                if (ContainsAny("NEPAL", "GOVERNMENT OF NEPAL")) score += 20;
                if (ContainsAny("D.O.B", "DATE OF BIRTH")) score += 10;
                if (ContainsAny("CATEGORY", "BLOOD GROUP", "B.G")) score += 15;
                if (ContainsAny("EXPIRY", "D.O.E", "VALID")) score += 15;
                break;

            default:
                score = 50;
                break;
        }

        return Math.Max(0, Math.Min(100, score));
    }

    private string DetermineClassification(int score)
    {
        var genuineMinScore = int.TryParse(_configuration["AuthenticityScoring:GenuineMinScore"], out var genuineMin) ? genuineMin : 80;
        var suspiciousMinScore = int.TryParse(_configuration["AuthenticityScoring:SuspiciousMinScore"], out var suspiciousMin) ? suspiciousMin : 50;

        if (score >= genuineMin)
            return "Genuine";
        else if (score >= suspiciousMin)
            return "Suspicious";
        else
            return "Invalid";
    }

    private List<string> GetRequiredFields(string documentType)
    {
        // Made more lenient - expirationDate is optional for passports (some may not have it)
        return documentType.ToLowerInvariant() switch
        {
            "passport" => new List<string> { "fullName", "documentNumber", "dateOfBirth", "nationality" }, // Removed expirationDate as required
            "driverslicense" => new List<string> { "fullName", "documentNumber", "dateOfBirth" }, // Removed expirationDate as required
            "nationalid" => new List<string> { "fullName", "documentNumber", "dateOfBirth" },
            "citizenshipcard" => new List<string> { "fullName", "documentNumber" }, // Removed dateOfBirth as required
            "citizenship" => new List<string> { "fullName", "documentNumber" },
            _ => new List<string> { "fullName", "documentNumber" }
        };
    }

    private bool ValidateFieldFormat(string fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
            
        // Be more lenient - if value exists and has reasonable length, accept it
        if (value.Length < 1)
            return false;

        return fieldName.ToLowerInvariant() switch
        {
            "email" => System.Text.RegularExpressions.Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"),
            "dateOfBirth" or "expirationDate" or "dateofissue" => 
                // Try multiple date formats - be lenient
                DateTime.TryParse(value, out _) || 
                System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}[-/]\d{1,2}[-/]\d{1,2}$") ||
                System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{1,2}[-/]\d{1,2}[-/]\d{2,4}$"),
            "documentNumber" => 
                // More lenient - accept 3+ characters (was 5+)
                value.Length >= 3 && value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '/'),
            "fullName" or "surname" or "givennames" => 
                // More lenient - accept single word names or names with spaces, allow more characters
                value.Trim().Length >= 2 && value.All(c => char.IsLetter(c) || c == ' ' || c == '.' || c == '-' || c == ','),
            "nationality" => 
                // Accept 2-20 character country names
                value.Length >= 2 && value.Length <= 20 && value.All(c => char.IsLetter(c) || c == ' '),
            _ => !string.IsNullOrWhiteSpace(value) && value.Length >= 1
        };
    }

    private int CalculateResolutionScore(int width, int height)
    {
        // Calculate DPI (assuming standard document size of 3.37 x 2.12 inches for ID cards)
        var widthDPI = width / 3.37;
        var heightDPI = height / 2.12;
        var avgDPI = (widthDPI + heightDPI) / 2;

        return avgDPI switch
        {
            >= 300 => 100,
            >= 200 => 80,
            >= 150 => 60,
            >= 100 => 40,
            _ => 20
        };
    }

    private async Task<int> CalculateBlurScore(string imagePath)
    {
        // Optimized blur detection - faster sampling
        await Task.CompletedTask;

        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imagePath);

            // Check edge density as a proxy for sharpness - reduced sample size for speed
            var edgeDensity = CalculateEdgeDensity(image);

            return edgeDensity switch
            {
                >= 0.1 => 100,  // Sharp
                >= 0.05 => 80,   // Mostly sharp
                >= 0.02 => 60,   // Slightly blurry
                >= 0.01 => 40,   // Blurry
                _ => 20          // Very blurry
            };
        }
        catch
        {
            return 50; // Default score instead of 0
        }
    }

    private double CalculateEdgeDensity(Image<Rgb24> image)
    {
        // Optimized edge detection - reduced sample size for faster processing
        var totalPixels = image.Width * image.Height;
        var edgePixels = 0;

        // Reduced sample size for faster processing (was 1000, now 200)
        var sampleSize = Math.Min(200, totalPixels / 500);
        if (sampleSize < 50) sampleSize = 50; // Minimum sample
        
        var random = new Random();
        var step = Math.Max(1, totalPixels / sampleSize);

        // Use grid sampling instead of random for better performance
        var stepX = Math.Max(1, image.Width / 20);
        var stepY = Math.Max(1, image.Height / 20);

        for (int y = stepY; y < image.Height - stepY; y += stepY)
        {
            for (int x = stepX; x < image.Width - stepX; x += stepX)
            {
                var pixel = image[x, y];
                var leftPixel = image[x - stepX, y];
                var topPixel = image[x, y - stepY];

                var diffLeft = Math.Abs(pixel.R - leftPixel.R) + Math.Abs(pixel.G - leftPixel.G) + Math.Abs(pixel.B - leftPixel.B);
                var diffTop = Math.Abs(pixel.R - topPixel.R) + Math.Abs(pixel.G - topPixel.G) + Math.Abs(pixel.B - topPixel.B);

                if (diffLeft > 30 || diffTop > 30)
                {
                    edgePixels++;
                }
            }
        }

        var totalSamples = ((image.Width / stepX) * (image.Height / stepY));
        return totalSamples > 0 ? (double)edgePixels / totalSamples : 0.05;
    }

    private int CalculateBrightnessContrastScore(Image<Rgb24> image)
    {
        // Optimized - use grid sampling instead of random for better performance
        var pixels = new List<(byte r, byte g, byte b)>(200);

        // Reduced sample size and use grid pattern for faster processing
        var stepX = Math.Max(1, image.Width / 20);
        var stepY = Math.Max(1, image.Height / 20);

        for (int y = 0; y < image.Height; y += stepY)
        {
            for (int x = 0; x < image.Width; x += stepX)
            {
                var pixel = image[x, y];
                pixels.Add((pixel.R, pixel.G, pixel.B));
                if (pixels.Count >= 200) break; // Cap at 200 samples
            }
            if (pixels.Count >= 200) break;
        }

        if (pixels.Count == 0) return 50; // Default score

        var brightness = pixels.Average(p => (p.r + p.g + p.b) / 3.0);
        var variance = pixels.Select(p => Math.Pow((p.r + p.g + p.b) / 3.0 - brightness, 2)).Average();
        var contrast = Math.Sqrt(variance);

        // Score based on ideal brightness (128) and good contrast (>50)
        var brightnessScore = Math.Max(0, 100 - Math.Abs(brightness - 128));
        var contrastScore = Math.Min(contrast * 2, 100);

        return (int)((brightnessScore + contrastScore) / 2);
    }

    private async Task<object> GetImageQualityDetails(string imagePath)
    {
        try
        {
            using var image = await Image.LoadAsync(imagePath);

            return new
            {
                resolution = new { width = image.Width, height = image.Height, dpi = CalculateResolutionScore(image.Width, image.Height) },
                fileSize = new FileInfo(imagePath).Length,
                format = Path.GetExtension(imagePath).ToUpperInvariant(),
                colorSpace = image.PixelType.ToString(),
                hasAlpha = false // Simplified for Rgb24 images
            };
        }
        catch
        {
            return new { error = "Unable to analyze image quality" };
        }
    }

    private bool CheckForHologramPatterns(string imagePath)
    {
        // Simplified hologram detection - in production, use advanced image processing
        // This would analyze for rainbow patterns, metallic sheen, etc.
        return false; // Placeholder
    }

    private bool CheckForWatermarkPatterns(string imagePath)
    {
        // Simplified watermark detection
        // This would look for subtle background patterns or translucent text
        return false; // Placeholder
    }

    private bool CheckForMicroprintPatterns(string imagePath)
    {
        // Simplified microprint detection
        // This would analyze for very small text patterns that are hard to reproduce
        return false; // Placeholder
    }

    private bool CheckForUVFeatures(string imagePath)
    {
        // Simplified UV feature detection
        // This would require UV imaging capabilities
        return false; // Placeholder
    }

    private bool CheckCompressionConsistency(Image<Rgb24> image)
    {
        // Check if compression artifacts are consistent with the claimed format
        try
        {
            // This is a very simplified check
            return image.Metadata.ExifProfile != null;
        }
        catch
        {
            return false;
        }
    }
}