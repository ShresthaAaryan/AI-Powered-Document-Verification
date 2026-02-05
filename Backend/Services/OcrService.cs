using DocumentVerification.API.Data;
using DocumentVerification.API.Models.DTOs.Verification;
using DocumentVerification.API.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Text.Json;
using Tesseract;
 using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

namespace DocumentVerification.API.Services;

public class OcrService : IOcrService
{
    private readonly DocumentVerificationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OcrService> _logger;
    private readonly string _tesseractDataPath;
    private readonly string _tesseractLanguage;

    public OcrService(
        DocumentVerificationDbContext context,
        IConfiguration configuration,
        ILogger<OcrService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _tesseractDataPath = ResolveTesseractDataPath(configuration);
        _tesseractLanguage = configuration["Tesseract:Language"] ?? "eng";
        
        // Validate the path
        if (!Directory.Exists(_tesseractDataPath))
        {
            _logger.LogWarning("Tesseract data path does not exist: {TesseractDataPath}. OCR functionality may not work.", _tesseractDataPath);
        }
        else
        {
            // Validate language files exist
            var languages = _tesseractLanguage.Split('+');
            var missingLanguages = new List<string>();
            
            foreach (var lang in languages)
            {
                var langDataFile = Path.Combine(_tesseractDataPath, $"{lang.Trim()}.traineddata");
                if (!File.Exists(langDataFile))
                {
                    missingLanguages.Add(lang.Trim());
                }
            }
            
            if (missingLanguages.Count > 0)
            {
                _logger.LogWarning("Language data file(s) not found in {TesseractDataPath}: {MissingLanguages}. OCR functionality may not work for these languages.", 
                    _tesseractDataPath, string.Join(", ", missingLanguages));
            }
            else
            {
                _logger.LogInformation("Tesseract data path resolved successfully: {TesseractDataPath}, Language: {Language}", 
                    _tesseractDataPath, _tesseractLanguage);
            }
        }
    }

    private string ResolveTesseractDataPath(IConfiguration configuration)
    {
        var configuredPath = configuration["Tesseract:DataPath"];
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        
        // If path is configured, try to resolve it
        if (!string.IsNullOrEmpty(configuredPath))
        {
            // If relative path, make it absolute relative to base directory
            if (!Path.IsPathRooted(configuredPath))
            {
                var resolvedPath = Path.GetFullPath(Path.Combine(baseDirectory, configuredPath));
                if (Directory.Exists(resolvedPath))
                {
                    return resolvedPath;
                }
            }
            else if (Directory.Exists(configuredPath))
            {
                return configuredPath;
            }
        }
        
        // Try common Windows locations
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            var windowsPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tessdata"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tessdata"),
                Path.Combine(baseDirectory, "tessdata"),
                Path.Combine(Directory.GetCurrentDirectory(), "tessdata"),
                @"C:\Program Files\Tesseract-OCR\tessdata",
                @"C:\Program Files (x86)\Tesseract-OCR\tessdata"
            };
            
            foreach (var path in windowsPaths)
            {
                if (Directory.Exists(path))
                {
                    _logger.LogInformation("Found Tesseract data path at: {Path}", path);
                    return path;
                }
            }
        }
        
        // Try common Linux/macOS locations
        var unixPaths = new[]
        {
            "/usr/share/tesseract-ocr/5/tessdata",
            "/usr/share/tesseract-ocr/4.00/tessdata",
            "/usr/share/tesseract-ocr/tessdata",
            "/usr/local/share/tesseract-ocr/tessdata",
            Path.Combine(baseDirectory, "tessdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "tessdata")
        };
        
        foreach (var path in unixPaths)
        {
            if (Directory.Exists(path))
            {
                _logger.LogInformation("Found Tesseract data path at: {Path}", path);
                return path;
            }
        }
        
        // Fallback: use relative path from base directory
        var fallbackPath = Path.Combine(baseDirectory, "tessdata");
        _logger.LogWarning("Tesseract data path not found. Using fallback path: {FallbackPath}. Please ensure tessdata directory exists with eng.traineddata file.", fallbackPath);
        return fallbackPath;
    }

    public async Task<OcrResultDto> ExtractTextAsync(Guid verificationId, string imagePath, string documentType)
    {
        try
        {
            _logger.LogInformation("Starting OCR extraction for verification: {VerificationId}", verificationId);

            // Validate Tesseract data path before proceeding
            if (!Directory.Exists(_tesseractDataPath))
            {
                throw new InvalidOperationException(
                    $"Tesseract data path does not exist: {_tesseractDataPath}. " +
                    "Please ensure Tesseract is installed and the tessdata directory is accessible. " +
                    "On Windows, install Tesseract from https://github.com/UB-Mannheim/tesseract/wiki. " +
                    "Then either set the Tesseract:DataPath in appsettings.json or place tessdata in the application directory.");
            }

            // Validate language files exist
            var languages = _tesseractLanguage.Split('+');
            var missingLanguages = new List<string>();
            
            foreach (var lang in languages)
            {
                var langDataFile = Path.Combine(_tesseractDataPath, $"{lang.Trim()}.traineddata");
                if (!File.Exists(langDataFile))
                {
                    missingLanguages.Add(lang.Trim());
                }
            }
            
            if (missingLanguages.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Language data file(s) not found in {_tesseractDataPath}: {string.Join(", ", missingLanguages)}. " +
                    $"Please download the required language data file(s) from https://github.com/tesseract-ocr/tessdata " +
                    $"and place them in the tessdata directory. Configured language: {_tesseractLanguage}");
            }

            // Validate image file exists
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            TesseractEngine engine;
            try
            {
                engine = new TesseractEngine(_tesseractDataPath, _tesseractLanguage, EngineMode.Default);
                _logger.LogInformation("Tesseract engine initialized with language: {Language}", _tesseractLanguage);
            }
            catch (Tesseract.TesseractException ex)
            {
                _logger.LogError(ex, "Failed to initialize Tesseract engine with data path: {TesseractDataPath}, Language: {Language}", 
                    _tesseractDataPath, _tesseractLanguage);
                throw new InvalidOperationException(
                    $"Failed to initialize Tesseract OCR engine. " +
                    $"Data path: {_tesseractDataPath}. " +
                    $"Language: {_tesseractLanguage}. " +
                    $"Error: {ex.Message}. " +
                    "Please ensure Tesseract is properly installed and the tessdata directory contains the required language files.",
                    ex);
            }

            using (engine)
            {
                // Configure Tesseract for better passport OCR
                // PSM 6 = Uniform block of text (good for structured documents like passports)
                // PSM 11 = Sparse text (good for MRZ lines)
                engine.SetVariable("tessedit_pageseg_mode", "6");
                engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789< ");
                
                // Preprocess image for better OCR accuracy
                var preprocessedImagePath = await PreprocessImageForOcrAsync(imagePath, documentType);
                
                Pix img;
                try
                {
                    img = Pix.LoadFromFile(preprocessedImagePath);
                }
                catch
                {
                    // Fallback to original image if preprocessing fails
                    _logger.LogWarning("Image preprocessing failed, using original image");
                    img = Pix.LoadFromFile(imagePath);
                }
                
                using (img)
                {
                    using var page = engine.Process(img);

                    var rawText = page.GetText();
                    var confidence = page.GetMeanConfidence();
                    
                    // Try MRZ-specific extraction if confidence is low or for passports
                    if (documentType.ToLowerInvariant() == "passport" && (confidence < 70 || string.IsNullOrWhiteSpace(rawText)))
                    {
                        _logger.LogInformation("Attempting MRZ-specific OCR extraction");
                        var mrzText = await ExtractMrzTextAsync(imagePath, engine);
                        if (!string.IsNullOrWhiteSpace(mrzText))
                        {
                            rawText = mrzText + "\n" + rawText;
                            confidence = Math.Max(confidence, 85); // MRZ usually has high confidence
                        }
                    }

                    stopwatch.Stop();

                    // Clean up preprocessed image
                    if (preprocessedImagePath != imagePath && File.Exists(preprocessedImagePath))
                    {
                        try
                        {
                            File.Delete(preprocessedImagePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete preprocessed image: {Path}", preprocessedImagePath);
                        }
                    }

                    // Log raw text for debugging (first 500 chars to avoid log bloat)
                    var previewText = rawText.Length > 500 ? rawText.Substring(0, 500) + "..." : rawText;
                    _logger.LogInformation("OCR raw text extracted (preview): {PreviewText}", previewText);
                    _logger.LogInformation("OCR confidence score: {Confidence}, Processing time: {TimeMs}ms", confidence, stopwatch.ElapsedMilliseconds);

                    var extractedFields = await ParseExtractedFieldsAsync(rawText, documentType);

                    // Attempt to extract portrait image from passport
                    var (portraitPath, portraitBox) = await ExtractPortraitImageAsync(imagePath, documentType);
                    if (!string.IsNullOrWhiteSpace(portraitPath))
                    {
                        extractedFields["portraitImage"] = new ExtractedFieldDto
                        {
                            Value = portraitPath,
                            Confidence = 0.70m,
                            BoundingBox = portraitBox
                        };
                    }
                
                    // Log extracted fields for debugging
                    _logger.LogInformation("Extracted {FieldCount} fields from OCR: {FieldNames}", 
                        extractedFields.Count, string.Join(", ", extractedFields.Keys));

                    // Detect primary language from configured languages
                    var primaryLanguage = _tesseractLanguage.Split('+').First().Trim();

                    var ocrResult = new OcrResult
                    {
                        Id = Guid.NewGuid(),
                        VerificationId = verificationId,
                        RawText = rawText,
                        ConfidenceScore = (decimal)Math.Round(confidence * 10000) / 10000, // Convert to 5 decimal places
                        ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                        LanguageDetected = primaryLanguage,
                        TesseractVersion = "5.x",
                        ExtractedFields = JsonSerializer.Serialize(extractedFields),
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.OcrResults.Add(ocrResult);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("OCR extraction completed for verification: {VerificationId}, Confidence: {Confidence}",
                        verificationId, confidence);

                    return new OcrResultDto
                    {
                        Id = ocrResult.Id,
                        RawText = ocrResult.RawText,
                        ConfidenceScore = ocrResult.ConfidenceScore,
                        ProcessingTimeMs = ocrResult.ProcessingTimeMs,
                        LanguageDetected = ocrResult.LanguageDetected,
                        ExtractedFields = extractedFields
                    };
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Re-throw InvalidOperationException as-is (these are our validation errors)
            throw;
        }
        catch (Tesseract.TesseractException ex)
        {
            _logger.LogError(ex, "Tesseract error during OCR extraction for verification: {VerificationId}", verificationId);
            throw new InvalidOperationException(
                $"Tesseract OCR error: {ex.Message}. " +
                "Please ensure Tesseract is properly installed and the tessdata directory is accessible.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OCR extraction for verification: {VerificationId}", verificationId);
            throw;
        }
    }

    public async Task<OcrResultDto> ProcessVerificationAsync(Guid verificationId, string idDocumentPath)
    {
        try
        {
            var verification = await _context.Verifications.FindAsync(verificationId);
            if (verification == null)
            {
                throw new InvalidOperationException("Verification not found");
            }

            return await ExtractTextAsync(verificationId, idDocumentPath, verification.DocumentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing verification OCR: {VerificationId}", verificationId);
            throw;
        }
    }

    /// <summary>
    /// Quickly detects the document type from an image by performing a lightweight OCR scan.
    /// This is used for validation to ensure the uploaded document matches the claimed type.
    /// </summary>
    public async Task<string?> DetectDocumentTypeAsync(string imagePath)
    {
        try
        {
            _logger.LogInformation("Detecting document type from image: {ImagePath}", imagePath);

            // Validate image file exists
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            // Validate Tesseract data path
            if (!Directory.Exists(_tesseractDataPath))
            {
                _logger.LogWarning("Tesseract data path does not exist, cannot detect document type");
                return null;
            }

            TesseractEngine engine;
            try
            {
                engine = new TesseractEngine(_tesseractDataPath, _tesseractLanguage, EngineMode.Default);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Tesseract engine for document type detection");
                return null;
            }

            using (engine)
            {
                // Configure for quick scan - use PSM 6 (uniform block) for faster processing
                engine.SetVariable("tessedit_pageseg_mode", "6");

                Pix img;
                try
                {
                    img = Pix.LoadFromFile(imagePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load image for document type detection: {ImagePath}", imagePath);
                    return null;
                }

                using (img)
                {
                    using var page = engine.Process(img);
                    var rawText = page.GetText();

                    if (string.IsNullOrWhiteSpace(rawText))
                    {
                        _logger.LogWarning("No text extracted from image for document type detection");
                        return null;
                    }

                    // Detect document type from extracted text
                    var detectedType = InferDocumentTypeFromText(rawText);
                    _logger.LogInformation("Detected document type: {DetectedType} from image: {ImagePath}", detectedType, imagePath);
                    return detectedType;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting document type from image: {ImagePath}", imagePath);
            return null;
        }
    }

    /// <summary>
    /// Infers document type from OCR text using keyword matching
    /// </summary>
    private string? InferDocumentTypeFromText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        var text = rawText.ToUpperInvariant();

        // Check for Passport indicators
        if (text.Contains("PASSPORT") || text.Contains("P<") || text.Contains("MRP") || 
            text.Contains("MACHINE READABLE") || text.Contains("MRZ"))
        {
            return "Passport";
        }

        // Check for Driver's License indicators
        if (text.Contains("DRIVING LICENSE") || text.Contains("DRIVER'S LICENSE") ||
            text.Contains("DRIVER LICENCE") || text.Contains("D.L.NO") || 
            text.Contains("DRIVING LICENCE") || text.Contains("DRIVING LICENCE") ||
            text.Contains("D.L. NO") || text.Contains("CATEGORY"))
        {
            return "DriversLicense";
        }

        // Check for National ID indicators
        if (text.Contains("NATIONAL IDENTITY") || text.Contains("NATIONAL IDENTITY CARD") ||
            text.Contains("NATIONAL IDENTITY NUMBER") || text.Contains("NIN") ||
            text.Contains("परिचय नम्वर"))
        {
            return "NationalID";
        }

        // Check for Citizenship Card indicators
        if (text.Contains("CITIZENSHIP") || text.Contains("नागरिकता") ||
            text.Contains("CITIZENSHIP CERTIFICATE") || text.Contains("CITIZENSHIP CARD"))
        {
            return "CitizenshipCard";
        }

        return null;
    }

    public async Task<Dictionary<string, ExtractedFieldDto>> ParseExtractedFieldsAsync(string rawText, string documentType)
    {
        await Task.CompletedTask;

        var fields = new Dictionary<string, ExtractedFieldDto>();

        try
        {
            switch (documentType.ToLowerInvariant())
            {
                case "passport":
                    fields = ParsePassportFields(rawText);
                    break;
                case "driverslicense":
                    fields = ParseDriversLicenseFields(rawText);
                    break;
                case "nationalid":
                    fields = ParseNationalIdFields(rawText);
                    break;
                case "citizenshipcard":
                case "citizenship":
                    fields = ParseCitizenshipCardFields(rawText);
                    break;
                default:
                    fields = ParseGenericFields(rawText);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing extracted fields for document type: {DocumentType}", documentType);
        }

        return fields;
    }

    public async Task<bool> ValidateExtractedFieldsAsync(Dictionary<string, ExtractedFieldDto> fields, string documentType)
    {
        await Task.CompletedTask;

        try
        {
            var requiredFields = GetRequiredFields(documentType);

            foreach (var field in requiredFields)
            {
                if (!fields.ContainsKey(field) || string.IsNullOrWhiteSpace(fields[field].Value))
                {
                    return false;
                }
            }

            // Validate specific field formats
            if (fields.TryGetValue("dateOfBirth", out var dob) &&
                !string.IsNullOrWhiteSpace(dob?.Value) &&
                !IsValidDate(dob.Value))
            {
                return false;
            }

            if (fields.TryGetValue("expirationDate", out var exp) &&
                !string.IsNullOrWhiteSpace(exp?.Value) &&
                !IsValidDate(exp.Value))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating extracted fields");
            return false;
        }
    }

    /// <summary>
    /// Preprocesses image for better OCR accuracy
    /// </summary>
    private async Task<string> PreprocessImageForOcrAsync(string imagePath, string documentType)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imagePath);
            
            // Create a copy for processing
            image.Mutate(ctx =>
            {
                // 1. Convert to grayscale for better OCR (Tesseract works better with grayscale)
                ctx.Grayscale();
                
                // 2. Enhance contrast (important for faded text)
                ctx.Contrast(1.2f);
                
                // 3. Enhance brightness if needed
                var brightness = EstimateBrightness(image);
                if (brightness < 0.4f)
                {
                    ctx.Brightness(1.1f);
                }
                else if (brightness > 0.7f)
                {
                    ctx.Brightness(0.95f);
                }
                
                // 4. Sharpen image (helps with character recognition)
                ctx.GaussianSharpen(1.5f);
                
                // 5. Resize if too small (Tesseract works better at 300 DPI)
                // Passport documents are typically ~125mm x 88mm
                // At 300 DPI that's ~1476 x 1039 pixels
                var minDimension = Math.Min(image.Width, image.Height);
                if (minDimension < 1000)
                {
                    var scaleFactor = 1000f / minDimension;
                    var newWidth = (int)(image.Width * scaleFactor);
                    var newHeight = (int)(image.Height * scaleFactor);
                    ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(newWidth, newHeight),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3
                    });
                }
                
                // Note: MRZ-specific processing is done separately in ExtractMrzTextAsync
            });
            
            // Save preprocessed image to temp file
            var tempPath = Path.Combine(Path.GetTempPath(), $"ocr_preprocessed_{Guid.NewGuid()}.png");
            await image.SaveAsPngAsync(tempPath);
            
            _logger.LogInformation("Image preprocessed and saved to: {TempPath}", tempPath);
            return tempPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image preprocessing failed, will use original image");
            return imagePath; // Return original path if preprocessing fails
        }
    }
    
    /// <summary>
    /// Extracts MRZ (Machine Readable Zone) text specifically
    /// </summary>
    private async Task<string> ExtractMrzTextAsync(string imagePath, TesseractEngine engine)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imagePath);
            
            // MRZ is typically in the bottom 15-20% of passport
            var mrzHeight = (int)(image.Height * 0.18);
            var mrzY = image.Height - mrzHeight;
            
            image.Mutate(ctx =>
            {
                // Crop to MRZ region
                ctx.Crop(new Rectangle(0, mrzY, image.Width, mrzHeight));
                
                // Convert to grayscale
                ctx.Grayscale();
                
                // High contrast for MRZ (it's usually black text on white)
                ctx.Contrast(1.8f);
                
                // Sharpen aggressively
                ctx.GaussianSharpen(2.5f);
                
                // Resize if needed for better OCR
                if (image.Width < 2000)
                {
                    var scaleFactor = 2000f / image.Width;
                    ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(2000, (int)(image.Height * scaleFactor)),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3
                    });
                }
            });
            
            // Save MRZ region to temp file
            var tempPath = Path.Combine(Path.GetTempPath(), $"mrz_{Guid.NewGuid()}.png");
            await image.SaveAsPngAsync(tempPath);
            
            try
            {
                // Configure engine specifically for MRZ
                engine.SetVariable("tessedit_pageseg_mode", "6"); // Uniform block
                engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789< ");
                
                using var mrzPix = Pix.LoadFromFile(tempPath);
                using var mrzPage = engine.Process(mrzPix);
                
                var mrzText = mrzPage.GetText();
                
                // Clean up temp file
                File.Delete(tempPath);
                
                return mrzText;
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MRZ extraction failed");
            return string.Empty;
        }
    }
    
    private float EstimateBrightness(Image<Rgb24> image)
    {
        long totalBrightness = 0;
        long pixelCount = 0;
        
        // Sample pixels for performance
        var step = Math.Max(1, image.Width / 100);
        
        for (int y = 0; y < image.Height; y += step)
        {
            for (int x = 0; x < image.Width; x += step)
            {
                var pixel = image[x, y];
                // Calculate perceived brightness
                var brightness = (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f;
                totalBrightness += (long)(brightness * 1000);
                pixelCount++;
            }
        }
        
        return pixelCount > 0 ? (totalBrightness / (float)pixelCount) / 1000f : 0.5f;
    }

    /// <summary>
    /// Crops the portrait photo from a passport data page and saves it alongside the source.
    /// Nepalese passport: photo is upper-left, below emblem; excludes header, text column, signature, and MRZ.
    /// </summary>
    private async Task<(string? path, BoundingBoxDto? box)> ExtractPortraitImageAsync(string imagePath, string documentType)
    {
        if (!string.Equals(documentType, "passport", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imagePath);

            // Nepalese passport: photo upper-left, below emblem/header; signature below photo; MRZ at bottom.
            // Exclude: top ~16% (emblem, PASSPORT, NEPAL), right beyond ~33% (SURNAME/GIVEN NAMES), bottom ~52%+ (signature, MRZ).
            var x = (int)(image.Width * 0.03);
            var y = (int)(image.Height * 0.16);
            var width = (int)(image.Width * 0.30);
            var height = (int)(image.Height * 0.36);

            x = Math.Clamp(x, 0, image.Width - 1);
            y = Math.Clamp(y, 0, image.Height - 1);
            width = Math.Clamp(width, 20, image.Width - x);
            height = Math.Clamp(height, 20, image.Height - y);

            var cropRect = new Rectangle(x, y, width, height);
            var portraitBox = new BoundingBoxDto { X = x, Y = y, Width = width, Height = height };

            image.Mutate(ctx =>
            {
                ctx.Crop(cropRect);
                // Light enhancements for the portrait
                ctx.Grayscale();
                ctx.Contrast(1.12f);
                ctx.GaussianSharpen(1.2f);
            });

            var directory = Path.GetDirectoryName(imagePath) ?? ".";
            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            var portraitPath = Path.Combine(directory, $"{fileName}_portrait.jpg");

            await image.SaveAsJpegAsync(portraitPath);

            _logger.LogInformation("Portrait cropped and saved to: {PortraitPath}", portraitPath);

            return (portraitPath, portraitBox);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract portrait image from {DocumentType}", documentType);
            return (null, null);
        }
    }

    private Dictionary<string, ExtractedFieldDto> ParsePassportFields(string text)
    {
        var fields = new Dictionary<string, ExtractedFieldDto>();
        
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Empty text provided for passport parsing");
            return fields;
        }
        
        // Normalize text - replace common OCR errors
        text = NormalizeOcrText(text);
        
        var allLines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        _logger.LogDebug("Parsing passport fields from {LineCount} lines of text", allLines.Length);

        // Improved MRZ pattern for Nepali and international passports
        // Pattern Line 1: P<[country]<[surname]<<[given names]<<<<...
        // Pattern Line 2: [passport number][country][DOB YYMMDD][sex][expiry YYMMDD][personal number][check digits]
        // Example: P<NPLSHRESTHA<<AARYAN<<<<...
        //          PA02248555NPL0212311M320327131017604707<<<80
        //          PAD2248555NPL0212311M320327131017604707<<< (single-line format)
        //          PBNPLCITIZEN<<JOHN<<<<... (from Nepali passport)
        //          BA00000000NPL0000000M00000000... (MRZ line 2)
        var mrzPattern = @"P<([A-Z]{3})<([A-Z]+)<<([A-Z]+(?:<<[A-Z]+)*)";
        // MRZ Line 2 pattern: [passport(9)][country(3)][DOB(6)][check(1)][sex(1)][expiry(6)][personal(14)][checks]
        // Groups: 1=passport, 2=country, 3=DOB, 4=check, 5=sex, 6=expiry, 7=personal
        // More flexible pattern to handle various formats
        var mrzLine2Pattern = @"([A-Z0-9]{9})([A-Z]{3})([0-9]{6})([0-9])([MF<])([0-9]{6})([A-Z0-9<]{0,14})";
        
        // Also try single-line MRZ format: P[A-Z][passport][country][DOB][check][sex][expiry][personal]
        var singleLineMrzPattern = @"P([A-Z])([A-Z0-9]{7,9})([A-Z]{3})([0-9]{6})([0-9])([MF<])([0-9]{6})([A-Z0-9<]{0,14})";
        
        // Try to match MRZ (usually at the end of the text)
        // First, try to find MRZ lines - they're usually the last 2 lines
        var mrzText = allLines.Length >= 2 ? string.Join("\n", allLines.TakeLast(2)) : text;
        
        var mrzMatch = System.Text.RegularExpressions.Regex.Match(mrzText, mrzPattern, System.Text.RegularExpressions.RegexOptions.Multiline);
        var mrzLine2Match = System.Text.RegularExpressions.Regex.Match(mrzText, mrzLine2Pattern, System.Text.RegularExpressions.RegexOptions.Multiline);
        var singleLineMrzMatch = System.Text.RegularExpressions.Regex.Match(mrzText, singleLineMrzPattern, System.Text.RegularExpressions.RegexOptions.Multiline);
        
        // If not found in last 2 lines, try the whole text
        if ((!mrzMatch.Success || !mrzLine2Match.Success) && !singleLineMrzMatch.Success)
        {
            mrzMatch = System.Text.RegularExpressions.Regex.Match(text, mrzPattern, System.Text.RegularExpressions.RegexOptions.Multiline);
            mrzLine2Match = System.Text.RegularExpressions.Regex.Match(text, mrzLine2Pattern, System.Text.RegularExpressions.RegexOptions.Multiline);
            singleLineMrzMatch = System.Text.RegularExpressions.Regex.Match(text, singleLineMrzPattern, System.Text.RegularExpressions.RegexOptions.Multiline);
        }
        
        // Handle single-line MRZ format
        if (singleLineMrzMatch.Success)
        {
            var passportPrefix = singleLineMrzMatch.Groups[1].Value;
            var passportNumber = singleLineMrzMatch.Groups[2].Value;
            var countryCode = singleLineMrzMatch.Groups[3].Value;
            var dobStr = singleLineMrzMatch.Groups[4].Value;
            var sex = singleLineMrzMatch.Groups[6].Value.Trim('<');
            var expiryStr = singleLineMrzMatch.Groups[7].Value;
            var personalRaw = singleLineMrzMatch.Groups[8].Value;
            // In Nepali passports, the first character in this group is a check digit for the personal number.
            var personalNumber = personalRaw.Length > 1
                ? personalRaw.Substring(1).Trim('<')
                : personalRaw.Trim('<');
            
            // Extract passport number with prefix
            fields["documentNumber"] = new ExtractedFieldDto
            {
                Value = $"{passportPrefix}{passportNumber}",
                Confidence = 0.95m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 60, Width = 100, Height = 20 }
            };
            
            fields["nationality"] = new ExtractedFieldDto
            {
                Value = countryCode,
                Confidence = 0.92m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 20, Width = 100, Height = 20 }
            };
            
            // Parse date of birth
            if (DateTime.TryParseExact(dobStr, "yyMMdd", null, System.Globalization.DateTimeStyles.None, out var birthDate))
            {
                fields["dateOfBirth"] = new ExtractedFieldDto
                {
                    Value = birthDate.ToString("yyyy-MM-dd"),
                    Confidence = 0.96m,
                    BoundingBox = new BoundingBoxDto { X = 0, Y = 80, Width = 100, Height = 20 },
                    Format = "yyyy-MM-dd"
                };
            }
            
            // Parse sex
            if (!string.IsNullOrWhiteSpace(sex) && (sex == "M" || sex == "F"))
            {
                fields["sex"] = new ExtractedFieldDto
                {
                    Value = sex == "M" ? "Male" : "Female",
                    Confidence = 0.98m,
                    BoundingBox = new BoundingBoxDto { X = 0, Y = 85, Width = 100, Height = 20 }
                };
            }
            
            // Parse expiry date
            if (DateTime.TryParseExact(expiryStr, "yyMMdd", null, System.Globalization.DateTimeStyles.None, out var expiryDate))
            {
                fields["expirationDate"] = new ExtractedFieldDto
                {
                    Value = expiryDate.ToString("yyyy-MM-dd"),
                    Confidence = 0.96m,
                    BoundingBox = new BoundingBoxDto { X = 0, Y = 100, Width = 100, Height = 20 },
                    Format = "yyyy-MM-dd"
                };
            }
            
            // Extract personal number if available
            if (!string.IsNullOrWhiteSpace(personalNumber) && personalNumber.Length > 0 && !personalNumber.All(c => c == '<' || c == ' '))
            {
                fields["personalNumber"] = new ExtractedFieldDto
                {
                    Value = personalNumber,
                    Confidence = 0.90m,
                    BoundingBox = new BoundingBoxDto { X = 0, Y = 110, Width = 100, Height = 20 }
                };
            }
            
            fields["documentType"] = new ExtractedFieldDto
            {
                Value = "Passport",
                Confidence = 0.95m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 0, Width = 100, Height = 20 }
            };
            
            _logger.LogInformation("Single-line MRZ pattern matched successfully for passport");
        }

        if (mrzMatch.Success && mrzLine2Match.Success && !singleLineMrzMatch.Success)
        {
            fields["documentType"] = new ExtractedFieldDto
            {
                Value = "Passport",
                Confidence = 0.95m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 0, Width = 100, Height = 20 }
            };

            fields["nationality"] = new ExtractedFieldDto
            {
                Value = mrzMatch.Groups[1].Value.Trim('<'),
                Confidence = 0.92m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 20, Width = 100, Height = 20 }
            };

            // Extract surname and given names
            var surname = mrzMatch.Groups[2].Value.Trim('<');
            var givenNames = mrzMatch.Groups[3].Value.Replace("<<", " ").Trim();
            var fullName = $"{surname} {givenNames}".Trim();

            fields["surname"] = new ExtractedFieldDto
            {
                Value = surname,
                Confidence = 0.90m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 30, Width = 100, Height = 20 }
            };

            fields["givenNames"] = new ExtractedFieldDto
            {
                Value = givenNames,
                Confidence = 0.90m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 35, Width = 100, Height = 20 }
            };

            fields["fullName"] = new ExtractedFieldDto
            {
                Value = fullName,
                Confidence = 0.88m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 40, Width = 100, Height = 20 }
            };

            // Extract passport number (first 9 characters of line 2)
            var passportNumber = mrzLine2Match.Groups[1].Value;
            fields["documentNumber"] = new ExtractedFieldDto
            {
                Value = passportNumber,
                Confidence = 0.95m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 60, Width = 100, Height = 20 }
            };

            // Parse date of birth (YYMMDD format) - Group 3
            var dobStr = mrzLine2Match.Groups[3].Value;
            if (DateTime.TryParseExact(dobStr, "yyMMdd", null, System.Globalization.DateTimeStyles.None, out var birthDate))
            {
                fields["dateOfBirth"] = new ExtractedFieldDto
                {
                    Value = birthDate.ToString("yyyy-MM-dd"),
                    Confidence = 0.96m,
                    BoundingBox = new BoundingBoxDto { X = 0, Y = 80, Width = 100, Height = 20 },
                    Format = "yyyy-MM-dd"
                };
            }

            // Parse sex - Group 5 (handle '<' character which might appear)
            var sex = mrzLine2Match.Groups[5].Value.Trim('<');
            if (!string.IsNullOrWhiteSpace(sex) && (sex == "M" || sex == "F"))
            {
                fields["sex"] = new ExtractedFieldDto
                {
                    Value = sex == "M" ? "Male" : "Female",
                    Confidence = 0.98m,
                    BoundingBox = new BoundingBoxDto { X = 0, Y = 85, Width = 100, Height = 20 }
                };
            }

            // Parse expiry date (YYMMDD format) - Group 6
            var expiryStr = mrzLine2Match.Groups[6].Value;
            if (DateTime.TryParseExact(expiryStr, "yyMMdd", null, System.Globalization.DateTimeStyles.None, out var expiryDate))
            {
                fields["expirationDate"] = new ExtractedFieldDto
                {
                    Value = expiryDate.ToString("yyyy-MM-dd"),
                    Confidence = 0.96m,
                    BoundingBox = new BoundingBoxDto { X = 0, Y = 100, Width = 100, Height = 20 },
                    Format = "yyyy-MM-dd"
                };
            }

            // Extract personal number if available - Group 7
            var personalRaw = mrzLine2Match.Groups[7].Value;
            // In Nepali passports, the first character is a check digit; drop it before trimming '<'.
            var personalNumber = personalRaw.Length > 1
                ? personalRaw.Substring(1).Trim('<')
                : personalRaw.Trim('<');
            if (!string.IsNullOrWhiteSpace(personalNumber) && personalNumber.Length > 0 && !personalNumber.All(c => c == '<' || c == ' '))
            {
                fields["personalNumber"] = new ExtractedFieldDto
                {
                    Value = personalNumber,
                    Confidence = 0.90m,
                    BoundingBox = new BoundingBoxDto { X = 0, Y = 110, Width = 100, Height = 20 }
                };
            }
            
            _logger.LogInformation("MRZ pattern matched successfully for passport");
        }

        // Also try to extract from visible text fields (for Nepali passports with bilingual text)
        ExtractPassportVisibleFields(text, fields);

        // Fallback to regex pattern matching if MRZ parsing failed
        if (fields.Count == 0)
        {
            return ExtractFieldsWithRegex(text, fields);
        }

        return fields;
    }

    private void ExtractPassportVisibleFields(string text, Dictionary<string, ExtractedFieldDto> fields)
    {
        // Nepali passport patterns - look for both English and Nepali labels
        // More flexible patterns to handle OCR errors and various formats
        var patterns = new Dictionary<string, (string[] patterns, decimal confidence)>
        {
            ["documentNumber"] = (new[] {
                @"(?:PASSPORT\s*NO|राहदानी\s*नं|PASSPORT\s*NUMBER|PASSPORT\s*#|PASSPORT\s*O)[:\s]*([A-Z]{2}\d{7,9})",
                @"(?:PASSPORT\s*NO|PASSPORT\s*NUMBER|PASSPORT\s*O)[:\s]*([A-Z]{2}\d{6,10})",
                @"PASSPORT\s+O\s+([A-Z]{2}\d{7,9})", // Matches "PASSPORT O PA0224855"
                @"\b([A-Z]{2}\d{7,9})\b", // Nepali passport format: PA0224855, BA0000000, etc.
                @"\b([A-Z]{2}\d{6,10})\b" // Alternative format
            }, 0.90m),
            
            ["surname"] = (new[] {
                @"(?:X\s*)?(?:SURNAME|थर)[:\s]*([A-Z]{2,30})",
                @"(?:X\s*)?SURNAME[:\s]*([A-Z]{2,30})",
                @"SURNAME\s+([A-Z]{2,30})",
                @"(?:SURNAME|थर)[:\s]*([A-Z\s]{2,30})"
            }, 0.85m),
            
            ["givenNames"] = (new[] {
                @"(?:A\s*)?(?:GIVEN\s*NAMES|नाम|GIVEN\s*NAME)[:\s]*([A-Z]{2,30})",
                @"(?:A\s*)?GIVEN\s*NAMES[:\s]*([A-Z]{2,30})",
                @"GIVEN\s*NAMES\s+([A-Z]{2,30})",
                @"(?:GIVEN\s*NAMES|नाम|GIVEN\s*NAME)[:\s]*([A-Z\s]{2,30})"
            }, 0.85m),
            
            ["fullName"] = (new[] {
                @"(?:NAME|नाम|FULL\s*NAME)[:\s]*([A-Z\s,]{3,50})",
                @"([A-Z]{2,}\s+[A-Z]{2,})" // Simple pattern: UPPERCASE UPPERCASE
            }, 0.80m),
            
            ["dateOfBirth"] = (new[] {
                // Nepali passport format: "00 DEC 0000" or "00 DEC 2000"
                @"(?:DATE\s*OF\s*BIRTH|जन्म\s*मिति|DOB|BIRTH\s*DATE)[:\s]*(\d{1,2}\s+(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\s+\d{4})",
                // Standard formats
                @"(?:DATE\s*OF\s*BIRTH|जन्म\s*मिति|DOB|BIRTH\s*DATE)[:\s]*(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})",
                @"(?:DATE\s*OF\s*BIRTH|जन्म\s*मिति|DOB)[:\s]*(\d{4}[-/]\d{1,2}[-/]\d{1,2})",
                // Generic date pattern (fallback)
                @"(\d{1,2}\s+(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\s+\d{4})",
                @"(\d{1,2}[-/]\d{1,2}[-/]\d{4})"
            }, 0.88m),
            
            ["placeOfBirth"] = (new[] {
                @"(?:SFHEATT\s*)?(?:PLACE\s*OF\s*BIRTH|जन्मस्थान)[:\s]*([A-Z]{2,30})",
                @"PLACE\s*OF\s*BIRTH\s+([A-Z]{2,30})",
                @"(?:PLACE\s*OF\s*BIRTH|जन्मस्थान)[:\s]*([A-Z\s]{2,30})"
            }, 0.85m),
            
            ["expirationDate"] = (new[] {
                // Nepali passport format: "00 DEC 0000"
                @"(?:DATE\s*OF\s*EXPIRY|म्याद\s*सकिने\s*मिति|EXPIRY|EXPIRES|EXPIRATION|DATE\s*OF\s*EXPIRY)[:\s]*(\d{1,2}\s+(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\s+\d{4})",
                // Standard formats
                @"(?:DATE\s*OF\s*EXPIRY|म्याद\s*सकिने\s*मिति|EXPIRY|EXPIRES|EXPIRATION)[:\s]*(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})",
                @"(?:DATE\s*OF\s*EXPIRY|EXPIRY|EXPIRES)[:\s]*(\d{4}[-/]\d{1,2}[-/]\d{1,2})"
            }, 0.88m),
            
            ["dateOfIssue"] = (new[] {
                // Nepali passport format: "00 DEC 0000"
                @"(?:DATE\s*AND\s*PLACE\s*OF\s*ISSUE|DATE\s*OF\s*ISSUE|जारी\s*मिति|ISSUE\s*DATE)[:\s]*(\d{1,2}\s+(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\s+\d{4})",
                // Standard formats
                @"(?:DATE\s*OF\s*ISSUE|जारी\s*मिति|ISSUE\s*DATE)[:\s]*(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})",
                @"(?:DATE\s*OF\s*ISSUE|जारी\s*मिति)[:\s]*(\d{4}[-/]\d{1,2}[-/]\d{1,2})"
            }, 0.88m),
            
            ["issuingAuthority"] = (new[] {
                @"(?:ISSUING\s*AUTHORITY|जारी\s*गर्ने\s*निकाय)[:\s]*([A-Z\s,\.]+(?:MOFA|DEPARTMENT|PASSPORTS|GOVERNMENT))",
                @"(?:ISSUING\s*AUTHORITY)[:\s]*([A-Z\s,\.]+)"
            }, 0.85m),
            
            ["nationality"] = (new[] {
                @"(?:A\s*)?(?:NATIONALITY|राष्ट्रियता)[:\s]*([A-Z]{2,15})",
                @"NATIONALITY\s+([A-Z]{2,15})",
                @"(?:NATIONALITY|राष्ट्रियता)[:\s]*([A-Z]{2,15})",
                @"(?:NATIONALITY)[:\s]*(NEPALESE|NEPALI|NEPAL)"
            }, 0.90m),
            
            ["sex"] = (new[] {
                @"(?:SEX|लिङ्ग|GENDER)[:\s]*([MF]|MALE|FEMALE)",
                @"(?:SEX)[:\s]*([MF])"
            }, 0.95m)
        };

        foreach (var kvp in patterns)
        {
            // For documentNumber, always try visible text extraction as it's more reliable
            // For other fields, skip if already extracted from MRZ
            if (fields.ContainsKey(kvp.Key) && kvp.Key != "documentNumber") continue;

            foreach (var pattern in kvp.Value.patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var value = match.Groups[1].Value.Trim();
                    
                    // Clean up OCR artifacts - remove extra spaces and normalize
                    value = System.Text.RegularExpressions.Regex.Replace(value, @"\s+", " ");
                    value = value.Trim();
                    
                    // Special handling for dates - try multiple formats
                    if (kvp.Key.Contains("Date"))
                    {
                        // Try parsing various date formats (including Nepali passport format)
                        var dateFormats = new[] { 
                            "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy", "d MMMM yyyy", // "00 DEC 0000"
                            "dd-MM-yyyy", "dd/MM/yyyy", "MM-dd-yyyy", "MM/dd/yyyy",
                            "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yy", "dd/MM/yy"
                        };
                        
                        DateTime? parsedDate = null;
                        foreach (var format in dateFormats)
                        {
                            if (DateTime.TryParseExact(value, format, null, System.Globalization.DateTimeStyles.None, out var dateValue))
                            {
                                parsedDate = dateValue;
                                break;
                            }
                        }
                        
                        // Fallback to general parsing
                        if (!parsedDate.HasValue && DateTime.TryParse(value, out var generalDate))
                        {
                            parsedDate = generalDate;
                        }
                        
                        if (parsedDate.HasValue)
                        {
                            value = parsedDate.Value.ToString("yyyy-MM-dd");
                        }
                    }
                    
                    // Normalize sex values
                    if (kvp.Key == "sex")
                    {
                        value = value.ToUpper();
                        if (value == "MALE" || value == "M") value = "M";
                        else if (value == "FEMALE" || value == "F") value = "F";
                    }
                    
                    // Clean OCR noise for issuingAuthority (e.g. leading single letter like "F MOFA ...")
                    if (kvp.Key == "issuingAuthority" && value.Length > 2)
                    {
                        // If it starts with a single letter + space and the rest looks like the actual authority, trim the prefix.
                        var trimmed = value.Substring(2).TrimStart();
                        if (char.IsLetter(value[0]) && value[1] == ' ' &&
                            (trimmed.Contains("MOFA") || trimmed.Contains("DEPARTMENT") || trimmed.Contains("PASSPORTS")))
                        {
                            value = trimmed;
                        }
                    }
                    
                    // Only add if value is meaningful
                    if (!string.IsNullOrWhiteSpace(value) && value.Length >= 1)
                    {
                        fields[kvp.Key] = new ExtractedFieldDto
                        {
                            Value = value,
                            Confidence = kvp.Value.confidence,
                            BoundingBox = new BoundingBoxDto { X = 0, Y = 0, Width = 100, Height = 20 }
                        };
                        _logger.LogDebug("Extracted field {FieldName} = {Value} from visible text", kvp.Key, value);
                        break; // Use first successful match
                    }
                }
            }
        }

        // Build fullName from surname and givenNames if not already set
        if (!fields.ContainsKey("fullName") && fields.ContainsKey("surname") && fields.ContainsKey("givenNames"))
        {
            var surname = fields["surname"].Value?.Trim() ?? "";
            var givenNames = fields["givenNames"].Value?.Trim() ?? "";
            // Ensure proper order: Given Names + Surname (e.g., "AARYAN SHRESTHA")
            var fullName = $"{givenNames} {surname}".Trim();
            fields["fullName"] = new ExtractedFieldDto
            {
                Value = fullName,
                Confidence = Math.Max(fields["surname"].Confidence, fields["givenNames"].Confidence),
                BoundingBox = new BoundingBoxDto { X = 0, Y = 0, Width = 100, Height = 20 }
            };
        }
        
        // Also handle case where surname comes before given names in fullName
        if (fields.ContainsKey("fullName") && fields.ContainsKey("surname") && fields.ContainsKey("givenNames"))
        {
            var fullName = fields["fullName"].Value?.Trim() ?? "";
            var surname = fields["surname"].Value?.Trim() ?? "";
            var givenNames = fields["givenNames"].Value?.Trim() ?? "";
            
            // If fullName has surname first but we want given names first, reconstruct it
            if (fullName.StartsWith(surname, StringComparison.OrdinalIgnoreCase) && 
                !fullName.StartsWith(givenNames, StringComparison.OrdinalIgnoreCase))
            {
                fields["fullName"] = new ExtractedFieldDto
                {
                    Value = $"{givenNames} {surname}".Trim(),
                    Confidence = fields["fullName"].Confidence,
                    BoundingBox = fields["fullName"].BoundingBox
                };
            }
        }
    }

    private Dictionary<string, ExtractedFieldDto> ParseDriversLicenseFields(string text)
    {
        var fields = new Dictionary<string, ExtractedFieldDto>();
        
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Empty text provided for driver's license parsing");
            return fields;
        }
        
        _logger.LogDebug("Parsing driver's license fields");

        // Nepalese Driver's License patterns
        // D.L. No.: 01-06-00093515
        var nepaliLicensePattern = @"(?:D\.L\.\s*NO|DRIVING\s*LICENSE\s*NO)[:\s]*(\d{2}-\d{2}-\d{8})";
        var genericLicensePattern = @"(?:LICENSE|LICENCE)\s*(?:NO|NUMBER|#)[:\s]*([A-Z0-9-]+)";
        
        // Name: YAN BAHADUR THAPA MAGAR
        var namePattern = @"(?:NAME|नाम)[:\s]*([A-Z\s]+(?:MAGAR|THAPA|SHRESTHA|TAMANG|GURUNG|RAI|LIMBU|SUNUWAR|NEWAR|KUMAR|BAHADUR|PRASAD|LAL|DEVI|KUMARI)?)";
        
        // D.O.B.: 15-03-1983
        var dobPattern = @"(?:D\.O\.B\.|DATE\s*OF\s*BIRTH|जन्म\s*मिति)[:\s]*(\d{1,2}[-/]\d{1,2}[-/]\d{4})";
        
        // D.O.E.: 23-09-2017
        var expiryPattern = @"(?:D\.O\.E\.|EXPIRY|EXPIRES?|EXPIRATION|म्याद)[:\s]*(\d{1,2}[-/]\d{1,2}[-/]\d{4})";
        
        // D.O.I.: 26-06-2002
        var issueDatePattern = @"(?:D\.O\.I\.|DATE\s*OF\s*ISSUE|जारी\s*मिति)[:\s]*(\d{1,2}[-/]\d{1,2}[-/]\d{4})";
        
        // Citizenship No.: 42003
        var citizenshipPattern = @"(?:CITIZENSHIP\s*NO|नागरिकता\s*नं)[:\s]*(\d+)";
        
        // Category: B.A.
        var categoryPattern = @"(?:CATEGORY|श्रेणी)[:\s]*([A-Z\.]+)";
        
        // Address
        var addressPattern = @"(?:ADDRESS|ठेगाना)[:\s]*([A-Za-z0-9\s,.-]+)";
        
        // F/H Name: J.B. Thapa Magar
        var fatherHusbandPattern = @"(?:F/H\s*NAME|FATHER|HUSBAND)[:\s]*([A-Z\s\.]+)";
        
        // Phone No.: 9841014418
        var phonePattern = @"(?:PHONE\s*NO|PHONE)[:\s]*(\d{10})";
        
        // B.G.: B+
        var bloodGroupPattern = @"(?:B\.G\.|BLOOD\s*GROUP)[:\s]*([A-Z][+-]?)";

        // Try Nepali format first
        ExtractFieldFromRegex(text, nepaliLicensePattern, 1, "documentNumber", fields, 0.95m);
        if (!fields.ContainsKey("documentNumber"))
        {
            ExtractFieldFromRegex(text, genericLicensePattern, 1, "documentNumber", fields, 0.90m);
        }
        
        ExtractFieldFromRegex(text, namePattern, 1, "fullName", fields, 0.85m);
        ExtractFieldFromRegex(text, dobPattern, 1, "dateOfBirth", fields, 0.88m);
        ExtractFieldFromRegex(text, expiryPattern, 1, "expirationDate", fields, 0.88m);
        ExtractFieldFromRegex(text, issueDatePattern, 1, "dateOfIssue", fields, 0.85m);
        ExtractFieldFromRegex(text, citizenshipPattern, 1, "citizenshipNumber", fields, 0.90m);
        ExtractFieldFromRegex(text, categoryPattern, 1, "licenseCategory", fields, 0.90m);
        ExtractFieldFromRegex(text, addressPattern, 1, "address", fields, 0.80m);
        ExtractFieldFromRegex(text, fatherHusbandPattern, 1, "fatherHusbandName", fields, 0.85m);
        ExtractFieldFromRegex(text, phonePattern, 1, "phoneNumber", fields, 0.85m);
        ExtractFieldFromRegex(text, bloodGroupPattern, 1, "bloodGroup", fields, 0.90m);

        if (fields.Count > 0)
        {
            _logger.LogInformation("Driver's license parsing completed with {FieldCount} fields extracted", fields.Count);
            return fields;
        }
        
        _logger.LogWarning("No driver's license fields extracted, falling back to generic parsing");
        return ParseGenericFields(text);
    }

    private Dictionary<string, ExtractedFieldDto> ParseNationalIdFields(string text)
    {
        var fields = new Dictionary<string, ExtractedFieldDto>();

        // Nepalese National ID patterns
        // NIN: 023-456-2130 or परिचय नम्वर
        var ninPattern = @"(?:NIN|परिचय\s*नम्वर|NATIONAL\s*IDENTITY\s*NUMBER)[:\s]*(\d{3}[-/]\d{3}[-/]\d{4})";
        var genericIdPattern = @"(?:ID|IDENTITY)\s*(?:NO|NUMBER|#)[:\s]*([A-Z0-9-]+)";
        
        // Surname: वर | SURNAME
        var surnamePattern = @"(?:SURNAME|वर|थर)[:\s]*([A-Z\s]+)";
        
        // Given Name: बाम | GIVEN NAME
        var givenNamePattern = @"(?:GIVEN\s*NAME|GIVEN\s*NAS|बाम|नाम)[:\s]*([A-Z\s]+)";
        
        // Date of Birth: 2034-10-23 or 1978-02-05
        var dobPattern = @"(?:DATE\s*OF\s*BIRTH|जन्म\s*मिति|DOB)[:\s]*(\d{4}[-/]\d{1,2}[-/]\d{1,2})";
        
        // Mother's Name: आमाको नाम | MOTHER'S NAME
        var motherNamePattern = @"(?:MOTHER['\s]*S\s*NAME|आमाको\s*नाम)[:\s]*([A-Z\s]+)";
        
        // Father's Name: बापकी गाम | FATHER'S NAME
        var fatherNamePattern = @"(?:FATHER['\s]*S\s*NAME|बापकी|बाबुको\s*नाम)[:\s]*([A-Z\s]+)";
        
        // Date of Issue: 01-01-2017
        var issueDatePattern = @"(?:DATE\s*OF\s*ISSUE|जारी\s*विति|जारी\s*मिति)[:\s]*(\d{1,2}[-/]\d{1,2}[-/]\d{4})";
        
        // Nationality: Nepalese
        var nationalityPattern = @"(?:NATIONALITY|राष्ट्रियता)[:\s]*(NEPALESE|NEPALI)";
        
        // Sex: F or M
        var sexPattern = @"(?:SEX|लिङ्ग)[:\s]*([MF])";

        // Try NIN pattern first (Nepali format)
        ExtractFieldFromRegex(text, ninPattern, 1, "documentNumber", fields, 0.95m);
        if (!fields.ContainsKey("documentNumber"))
        {
            ExtractFieldFromRegex(text, genericIdPattern, 1, "documentNumber", fields, 0.90m);
        }
        
        ExtractFieldFromRegex(text, surnamePattern, 1, "surname", fields, 0.85m);
        ExtractFieldFromRegex(text, givenNamePattern, 1, "givenNames", fields, 0.85m);
        ExtractFieldFromRegex(text, dobPattern, 1, "dateOfBirth", fields, 0.88m);
        ExtractFieldFromRegex(text, motherNamePattern, 1, "motherName", fields, 0.85m);
        ExtractFieldFromRegex(text, fatherNamePattern, 1, "fatherName", fields, 0.85m);
        ExtractFieldFromRegex(text, issueDatePattern, 1, "dateOfIssue", fields, 0.85m);
        ExtractFieldFromRegex(text, nationalityPattern, 1, "nationality", fields, 0.90m);
        ExtractFieldFromRegex(text, sexPattern, 1, "sex", fields, 0.95m);
        
        // Build fullName from surname and givenNames
        if (!fields.ContainsKey("fullName") && fields.ContainsKey("surname") && fields.ContainsKey("givenNames"))
        {
            fields["fullName"] = new ExtractedFieldDto
            {
                Value = $"{fields["surname"].Value} {fields["givenNames"].Value}".Trim(),
                Confidence = 0.85m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 0, Width = 100, Height = 20 }
            };
        }
        else if (!fields.ContainsKey("fullName"))
        {
            // Fallback to generic name pattern
            var namePattern = @"(?:NAME|नाम)[:\s]*([A-Z\s]+)";
            ExtractFieldFromRegex(text, namePattern, 1, "fullName", fields, 0.80m);
        }

        return fields.Count > 0 ? fields : ParseGenericFields(text);
    }

    private Dictionary<string, ExtractedFieldDto> ParseCitizenshipCardFields(string text)
    {
        var fields = new Dictionary<string, ExtractedFieldDto>();

        // Nepalese Citizenship Certificate: front = photo + Nepali; back = English.
        // When the back is uploaded, it is used for data extraction (here). The front is used for face matching. Patterns support both Nepali and English.
        // Citizenship Certificate No.: 77332286
        var citizenshipNumberPattern = @"(?:CITIZENSHIP\s*(?:CERTIFICATE\s*)?NO|नागरिकता\s*(?:प्रमाणपत्र\s*)?नं|ना\.प्र\.नं)[:\s]*(\d{8})";
        var genericCitizenshipPattern = @"(?:CITIZENSHIP\s*NO|नागरिकता\s*नं)[:\s]*(\d+)";
        
        // Full Name: GANGA RAM SAH
        var fullNamePattern = @"(?:FULL\s*NAME|नाम\s*थर|NAME)[:\s]*([A-Z\s]+(?:SAH|SHRESTHA|THAPA|MAGAR|TAMANG|GURUNG|RAI|LIMBU|KUMAR|BAHADUR|PRASAD|LAL|DEVI|KUMARI)?)";
        
        // Sex: Male or Female
        var sexPattern = @"(?:SEX|लिङ्ग)[:\s]*(MALE|FEMALE|पुरुष|महिला|[MF])";
        
        // Date of Birth (AD): (may be blank or in BS format)
        var dobPattern = @"(?:DATE\s*OF\s*BIRTH\s*\(AD\)|जन्म\s*मिति)[:\s]*(\d{4}[-/]\d{1,2}[-/]\d{1,2})";
        var dobBSPattern = @"(?:जन्म\s*मिति|DATE\s*OF\s*BIRTH)[:\s]*(\d{4}\s*साल|\d{4}[-/]\d{1,2}[-/]\d{1,2})";
        
        // Birth Place - District: Sarlahi
        var birthDistrictPattern = @"(?:BIRTH\s*PLACE|जन्म\s*स्थान).*?DISTRICT[:\s]*([A-Z]+)";
        var birthVDCPattern = @"(?:VDC|गा\.वि\.\s*स\.)[:\s]*([A-Za-z\s]+)";
        var birthWardPattern = @"(?:WARD\s*NO|घडा\s*नं|वडा\s*नं)[:\s]*(\d+)";
        
        // Permanent Address
        var permanentAddressPattern = @"(?:PERMANENT\s*ADDRESS|स्थायी\s*बासस्थान).*?DISTRICT[:\s]*([A-Z]+)";
        
        // Father's Name: बाबुको नाम थर
        var fatherNamePattern = @"(?:FATHER['\s]*S\s*NAME|बाबुको\s*नाम|F/H\s*NAME)[:\s]*([A-Z\s]+)";
        
        // Mother's Name: आमाको नाम थर
        var motherNamePattern = @"(?:MOTHER['\s]*S\s*NAME|आमाको\s*नाम)[:\s]*([A-Z\s]+)";
        
        // Citizenship Type: वंशज (By Descent)
        var citizenshipTypePattern = @"(?:CITIZENSHIP\s*TYPE|नागरिकता\s*किसिम|ना\.\s*कि\.)[:\s]*([A-Z\s]+|वंशज)";
        
        // Date of Issue: जारी मिति
        var issueDatePattern = @"(?:DATE\s*OF\s*ISSUE|जारी\s*मिति|जारी\s*विति)[:\s]*(\d{4}[-/]\d{1,2}[-/]\d{1,2})";
        var issueDateBSPattern = @"(?:जारी\s*मिति)[:\s]*(\d{4}[-/]\d{1,2}[-/]\d{1,2})";

        // Extract citizenship number
        ExtractFieldFromRegex(text, citizenshipNumberPattern, 1, "documentNumber", fields, 0.95m);
        if (!fields.ContainsKey("documentNumber"))
        {
            ExtractFieldFromRegex(text, genericCitizenshipPattern, 1, "documentNumber", fields, 0.90m);
        }
        
        ExtractFieldFromRegex(text, fullNamePattern, 1, "fullName", fields, 0.85m);
        ExtractFieldFromRegex(text, sexPattern, 1, "sex", fields, 0.90m);
        ExtractFieldFromRegex(text, dobPattern, 1, "dateOfBirth", fields, 0.88m);
        if (!fields.ContainsKey("dateOfBirth"))
        {
            ExtractFieldFromRegex(text, dobBSPattern, 1, "dateOfBirthBS", fields, 0.85m);
        }
        
        ExtractFieldFromRegex(text, birthDistrictPattern, 1, "birthDistrict", fields, 0.85m);
        ExtractFieldFromRegex(text, birthVDCPattern, 1, "birthVDC", fields, 0.80m);
        ExtractFieldFromRegex(text, birthWardPattern, 1, "birthWard", fields, 0.85m);
        
        ExtractFieldFromRegex(text, permanentAddressPattern, 1, "permanentDistrict", fields, 0.85m);
        
        ExtractFieldFromRegex(text, fatherNamePattern, 1, "fatherName", fields, 0.85m);
        ExtractFieldFromRegex(text, motherNamePattern, 1, "motherName", fields, 0.85m);
        ExtractFieldFromRegex(text, citizenshipTypePattern, 1, "citizenshipType", fields, 0.85m);
        ExtractFieldFromRegex(text, issueDatePattern, 1, "dateOfIssue", fields, 0.85m);
        if (!fields.ContainsKey("dateOfIssue"))
        {
            ExtractFieldFromRegex(text, issueDateBSPattern, 1, "dateOfIssueBS", fields, 0.80m);
        }

        return fields.Count > 0 ? fields : ParseNationalIdFields(text);
    }

    private Dictionary<string, ExtractedFieldDto> ParseGenericFields(string text)
    {
        var fields = new Dictionary<string, ExtractedFieldDto>();

        // Generic patterns that work across most document types
        var patterns = new Dictionary<string, (string pattern, int group, decimal confidence)>
        {
            ["documentNumber"] = (@"(?:NO|NUMBER|#)[:\s]*([A-Z0-9-]+)", 1, 0.75m),
            ["fullName"] = (@"NAME[:\s]*([A-Z\s,\.]+)", 1, 0.70m),
            ["dateOfBirth"] = (@"(?:DOB|B\.O\.B|BIRTH)[:\s]*(\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}[/-]\d{1,2}[/-]\d{1,2})", 1, 0.72m),
            ["expirationDate"] = (@"(?:EXP|EXPIRES?|VALID\s*UNTIL)[:\s]*(\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}[/-]\d{1,2}[/-]\d{1,2})", 1, 0.72m)
        };

        foreach (var kvp in patterns)
        {
            ExtractFieldFromRegex(text, kvp.Value.pattern, kvp.Value.group, kvp.Key, fields, kvp.Value.confidence);
        }

        return fields;
    }

    private Dictionary<string, ExtractedFieldDto> ExtractFieldsWithRegex(string text, Dictionary<string, ExtractedFieldDto> existingFields)
    {
        var fields = existingFields ?? new Dictionary<string, ExtractedFieldDto>();

        // Email pattern
        var emailMatch = System.Text.RegularExpressions.Regex.Match(text, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
        if (emailMatch.Success && !fields.ContainsKey("email"))
        {
            fields["email"] = new ExtractedFieldDto
            {
                Value = emailMatch.Value,
                Confidence = 0.85m,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 0, Width = 100, Height = 20 }
            };
        }

        return fields;
    }

    private void ExtractFieldFromRegex(string text, string pattern, int group, string fieldName,
        Dictionary<string, ExtractedFieldDto> fields, decimal confidence)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && !fields.ContainsKey(fieldName))
        {
            fields[fieldName] = new ExtractedFieldDto
            {
                Value = match.Groups[group].Value.Trim(),
                Confidence = confidence,
                BoundingBox = new BoundingBoxDto { X = 0, Y = 0, Width = 100, Height = 20 }
            };
        }
    }

    private List<string> GetRequiredFields(string documentType)
    {
        // Made more lenient - match AIAnalysisService requirements
        return documentType.ToLowerInvariant() switch
        {
            "passport" => new List<string> { "fullName", "documentNumber", "dateOfBirth", "nationality" },
            "driverslicense" => new List<string> { "fullName", "documentNumber", "dateOfBirth" },
            "nationalid" => new List<string> { "fullName", "documentNumber", "dateOfBirth" },
            "citizenshipcard" => new List<string> { "fullName", "documentNumber" },
            "citizenship" => new List<string> { "fullName", "documentNumber" },
            _ => new List<string> { "fullName", "documentNumber" }
        };
    }

    private string NormalizeOcrText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;
            
        // Normalize whitespace
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        text = text.Trim();
        
        return text;
    }

    private bool IsValidDate(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return false;
            
        // Try general parsing first
        if (DateTime.TryParse(dateString, out _))
            return true;
            
        // Try specific formats
        var formats = new[] { 
            "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "MM-dd-yyyy", "dd-MM-yyyy",
            "yyyy/MM/dd", "dd-MM-yy", "dd/MM/yy", "MM-dd-yy", "MM/dd/yy",
            "d MMM yyyy", "dd MMM yyyy", "d MMMM yyyy", "dd MMMM yyyy"
        };
        
        return formats.Any(format => DateTime.TryParseExact(dateString, format, null, System.Globalization.DateTimeStyles.None, out _));
    }
}