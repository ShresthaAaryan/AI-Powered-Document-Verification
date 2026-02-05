using DocumentVerification.API.Data;
using DocumentVerification.API.Models.DTOs.Verification;
using DocumentVerification.API.Models.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;

namespace DocumentVerification.API.Services;

public class FaceMatchingService : IFaceMatchingService
{
    private readonly DocumentVerificationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FaceMatchingService> _logger;
    private readonly InferenceSession? _faceRecognitionSession;
    private readonly float _similarityThreshold;
    private readonly float _highConfidenceThreshold;

    public FaceMatchingService(
        DocumentVerificationDbContext context,
        IConfiguration configuration,
        ILogger<FaceMatchingService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;

        // Load ONNX model
        var modelPath = configuration["ONNX:ArcFaceModelPath"];
        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
        {
            try
            {
                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                _faceRecognitionSession = new InferenceSession(modelPath, sessionOptions);
                _logger.LogInformation("ONNX face recognition model loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load ONNX face recognition model");
                _faceRecognitionSession = null;
            }
        }

        float.TryParse(configuration["FaceMatching:SimilarityThreshold"], out _similarityThreshold);
        float.TryParse(configuration["FaceMatching:HighConfidenceThreshold"], out _highConfidenceThreshold);

        if (_similarityThreshold == 0) _similarityThreshold = 0.6f;
        if (_highConfidenceThreshold == 0) _highConfidenceThreshold = 0.7f;
    }

    public async Task<FaceMatchResultDto> CompareFacesAsync(Guid verificationId, string idDocumentPath, string selfiePath)
    {
        try
        {
            _logger.LogInformation("Starting face comparison for verification: {VerificationId}", verificationId);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Detect faces in both images
            var idFaceDetected = await DetectFaceAsync(idDocumentPath);
            var selfieFaceDetected = await DetectFaceAsync(selfiePath);

            if (!idFaceDetected || !selfieFaceDetected)
            {
                return CreateFailedResult(verificationId, idFaceDetected, selfieFaceDetected, stopwatch.ElapsedMilliseconds, "FaceNotDetected");
            }

            // Generate face embeddings
            var idEmbedding = await GenerateFaceEmbeddingAsync(idDocumentPath);
            var selfieEmbedding = await GenerateFaceEmbeddingAsync(selfiePath);

            if (idEmbedding == null || selfieEmbedding == null)
            {
                return CreateFailedResult(verificationId, idFaceDetected, selfieFaceDetected, stopwatch.ElapsedMilliseconds, "EmbeddingGenerationFailed");
            }

            // Calculate similarity score
            var similarityScore = await CalculateSimilarityScore(idEmbedding, selfieEmbedding);

            // Determine match decision
            var matchDecision = DetermineMatch(similarityScore);

            // Get face detection details (simplified for performance)
            var faceDetectionDetails = new
            {
                idDocumentFace = new { detected = idFaceDetected },
                selfieFace = new { detected = selfieFaceDetected }
            };

            stopwatch.Stop();

            // Save to database
            var faceMatchResult = new FaceMatchResult
            {
                Id = Guid.NewGuid(),
                VerificationId = verificationId,
                IdFaceDetected = idFaceDetected,
                SelfieFaceDetected = selfieFaceDetected,
                SimilarityScore = (decimal)Math.Round(similarityScore * 10000) / 10000,
                MatchDecision = matchDecision,
                ConfidenceThreshold = (decimal)_similarityThreshold,
                IdFaceEmbedding = idEmbedding,
                SelfieFaceEmbedding = selfieEmbedding,
                FaceDetectionDetails = JsonSerializer.Serialize(faceDetectionDetails),
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                ModelVersion = _configuration["ONNX:ModelVersion"] ?? "1.2.0",
                CreatedAt = DateTime.UtcNow
            };

            _context.FaceMatchResults.Add(faceMatchResult);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Face comparison completed for verification: {VerificationId}, Similarity: {Similarity}, Match: {Match}",
                verificationId, similarityScore, matchDecision);

            return new FaceMatchResultDto
            {
                Id = faceMatchResult.Id,
                IdFaceDetected = faceMatchResult.IdFaceDetected,
                SelfieFaceDetected = faceMatchResult.SelfieFaceDetected,
                SimilarityScore = faceMatchResult.SimilarityScore,
                MatchDecision = faceMatchResult.MatchDecision,
                ConfidenceThreshold = faceMatchResult.ConfidenceThreshold,
                FaceDetectionDetails = faceDetectionDetails,
                ProcessingTimeMs = faceMatchResult.ProcessingTimeMs,
                ModelVersion = faceMatchResult.ModelVersion
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during face comparison for verification: {VerificationId}", verificationId);
            throw;
        }
    }

    public async Task<float[]?> GenerateFaceEmbeddingAsync(string imagePath)
    {
        try
        {
            if (_faceRecognitionSession == null)
            {
                _logger.LogWarning("ONNX face recognition model not loaded. Face embeddings cannot be generated.");
                return null;
            }

            // Load and preprocess image; crop to detected face if available
            using var image = await Image.LoadAsync<Rgb24>(imagePath);
            Rectangle? faceBox = null;
            var (detected, box) = await DetectFaceBoundingBoxAsync(imagePath);
            if (detected && box.HasValue)
            {
                faceBox = box;
            }

            var processedImage = PreprocessFaceImage(image, faceBox);

            // Create input tensor
            var inputTensor = new DenseTensor<float>(processedImage, new[] { 1, 3, 112, 112 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("data", inputTensor)
            };

            // Run inference
            using var results = _faceRecognitionSession.Run(inputs);
            var embedding = results.First().AsEnumerable<float>().ToArray();

            // Normalize embedding vector
            return NormalizeEmbedding(embedding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating face embedding for: {ImagePath}", imagePath);
            return null;
        }
    }

    public async Task<float> CalculateSimilarityScore(float[] embedding1, float[] embedding2)
    {
        return await Task.Run(() =>
        {
            if (embedding1 == null || embedding2 == null || embedding1.Length != embedding2.Length)
            {
                return 0f;
            }

            // Calculate cosine similarity
            var dotProduct = 0f;
            for (int i = 0; i < embedding1.Length; i++)
            {
                dotProduct += embedding1[i] * embedding2[i];
            }

            return Math.Max(0f, Math.Min(1f, dotProduct)); // Clamp between 0 and 1
        });
    }

    public async Task<bool> DetectFaceAsync(string imagePath)
    {
        try
        {
            // If embedding model is not loaded, we can't do robust detection; assume true.
            if (_faceRecognitionSession == null)
            {
                return true;
            }

            var (_, box) = await DetectFaceBoundingBoxAsync(imagePath);
            return box.HasValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting face in: {ImagePath}", imagePath);
            // If detection fails but model exists, try to generate embedding anyway
            return _faceRecognitionSession != null;
        }
    }

    public async Task<FaceDetectionDetails> DetectFaceDetailsAsync(string imagePath)
    {
        try
        {
            var (detected, box) = await DetectFaceBoundingBoxAsync(imagePath);

            if (detected && box.HasValue)
            {
                using var image = await Image.LoadAsync<Rgb24>(imagePath);
                var metrics = new FaceQualityMetrics
                {
                    Sharpness = EstimateSharpness(image),
                    Brightness = EstimateBrightness(image),
                    Contrast = EstimateContrast(image)
                };

                return new FaceDetectionDetails
                {
                    FaceDetected = true,
                    BoundingBox = new BoundingBoxDto { X = box.Value.X, Y = box.Value.Y, Width = box.Value.Width, Height = box.Value.Height },
                    Confidence = 0.9f,
                    QualityMetrics = metrics
                };
            }

            return new FaceDetectionDetails
            {
                FaceDetected = false,
                Confidence = 0f
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting face details in: {ImagePath}", imagePath);
            return new FaceDetectionDetails
            {
                FaceDetected = false,
                Confidence = 0f,
                ErrorMessage = ex.Message
            };
        }
    }

    private float[] PreprocessFaceImage(Image<Rgb24> image, Rectangle? faceBox)
    {
        try
        {
            if (faceBox.HasValue)
            {
                // Pad face box slightly
                var box = faceBox.Value;
                var padX = (int)(box.Width * 0.12);
                var padY = (int)(box.Height * 0.12);
                var rx = Math.Clamp(box.X - padX, 0, image.Width - 1);
                var ry = Math.Clamp(box.Y - padY, 0, image.Height - 1);
                var rw = Math.Clamp(box.Width + 2 * padX, 10, image.Width - rx);
                var rh = Math.Clamp(box.Height + 2 * padY, 10, image.Height - ry);
                var padded = new Rectangle(rx, ry, rw, rh);

                image.Mutate(x => x.Crop(padded));
            }

            // Resize to 112x112 (ArcFace input size)
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(112, 112),
                Mode = ResizeMode.Crop
            }));

            // Convert to float array and normalize
            var processedImage = new float[112 * 112 * 3];
            var index = 0;

            for (int y = 0; y < 112; y++)
            {
                for (int x = 0; x < 112; x++)
                {
                    var pixel = image[x, y];
                    // Normalize to [0, 1] and apply standard normalization
                    processedImage[index++] = (pixel.R / 255.0f - 0.5f) / 0.5f;
                    processedImage[index++] = (pixel.G / 255.0f - 0.5f) / 0.5f;
                    processedImage[index++] = (pixel.B / 255.0f - 0.5f) / 0.5f;
                }
            }

            return processedImage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preprocessing face image");
            throw;
        }
    }

    private float[] NormalizeEmbedding(float[] embedding)
    {
        // Calculate L2 norm
        var norm = 0f;
        foreach (var value in embedding)
        {
            norm += value * value;
        }
        norm = (float)Math.Sqrt(norm);

        // Normalize vector
        if (norm > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= norm;
            }
        }

        return embedding;
    }

    /// <summary>
    /// Simple skin-tone and geometry-based heuristic to find a likely face box.
    /// </summary>
    private async Task<(bool detected, Rectangle? box)> DetectFaceBoundingBoxAsync(string imagePath)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imagePath);

            // Downscale for faster scanning if very large
            var scale = 1.0;
            if (image.Width > 1200 || image.Height > 1200)
            {
                scale = Math.Min(1200.0 / image.Width, 1200.0 / image.Height);
                image.Mutate(x => x.Resize((int)(image.Width * scale), (int)(image.Height * scale)));
            }

            // Skin-tone heuristic (simple RGB rules)
            var mask = new bool[image.Width * image.Height];
            int idx = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var p = image[x, y];
                    var r = p.R; var g = p.G; var b = p.B;
                    var max = Math.Max(r, Math.Max(g, b));
                    var min = Math.Min(r, Math.Min(g, b));

                    bool skin = r > 95 && g > 40 && b > 20 &&
                                (max - min) > 15 &&
                                Math.Abs(r - g) > 15 &&
                                r > g && r > b;
                    mask[idx++] = skin;
                }
            }

            // Find bounding box of skin pixels
            int minX = image.Width, minY = image.Height, maxX = -1, maxY = -1, skinCount = 0;
            idx = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++, idx++)
                {
                    if (!mask[idx]) continue;
                    skinCount++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (skinCount == 0 || maxX <= minX || maxY <= minY)
            {
                return (false, null);
            }

            var w = maxX - minX + 1;
            var h = maxY - minY + 1;
            var area = w * h;
            var imgArea = image.Width * image.Height;

            // Heuristic checks: area and aspect ratio
            var aspect = w / (double)h;
            if (area < imgArea * 0.02) // too small
                return (false, null);
            if (aspect < 0.5 || aspect > 1.8)
                return (false, null);

            // Convert back to original scale if we resized
            if (Math.Abs(scale - 1.0) > 0.0001)
            {
                minX = (int)(minX / scale);
                maxX = (int)(maxX / scale);
                minY = (int)(minY / scale);
                maxY = (int)(maxY / scale);
                w = maxX - minX + 1;
                h = maxY - minY + 1;
            }

            var rect = new Rectangle(minX, minY, w, h);
            return (true, rect);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heuristic face box detection failed");
            return (false, null);
        }
    }

    private bool DetermineMatch(float similarityScore)
    {
        return similarityScore >= _similarityThreshold;
    }

    private FaceMatchResultDto CreateFailedResult(
        Guid verificationId,
        bool idFaceDetected,
        bool selfieFaceDetected,
        long elapsedMs,
        string reason)
    {
        // Persist a result record even when comparison fails so the UI can
        // show that face matching completed but was inconclusive.
        var faceDetectionDetails = new
        {
            idDocumentFace = new { detected = idFaceDetected },
            selfieFace = new { detected = selfieFaceDetected },
            reason
        };

        var faceMatchResult = new FaceMatchResult
        {
            Id = Guid.NewGuid(),
            VerificationId = verificationId,
            IdFaceDetected = idFaceDetected,
            SelfieFaceDetected = selfieFaceDetected,
            SimilarityScore = null,
            MatchDecision = null,
            ConfidenceThreshold = (decimal)_similarityThreshold,
            IdFaceEmbedding = null,
            SelfieFaceEmbedding = null,
            FaceDetectionDetails = JsonSerializer.Serialize(faceDetectionDetails),
            ProcessingTimeMs = (int)elapsedMs,
            ModelVersion = _configuration["ONNX:ModelVersion"] ?? "1.2.0",
            CreatedAt = DateTime.UtcNow
        };

        _context.FaceMatchResults.Add(faceMatchResult);
        _context.SaveChanges();

        return new FaceMatchResultDto
        {
            Id = faceMatchResult.Id,
            IdFaceDetected = faceMatchResult.IdFaceDetected,
            SelfieFaceDetected = faceMatchResult.SelfieFaceDetected,
            SimilarityScore = faceMatchResult.SimilarityScore,
            MatchDecision = faceMatchResult.MatchDecision,
            ConfidenceThreshold = faceMatchResult.ConfidenceThreshold,
            FaceDetectionDetails = faceDetectionDetails,
            ProcessingTimeMs = faceMatchResult.ProcessingTimeMs,
            ModelVersion = faceMatchResult.ModelVersion
        };
    }

    private bool DetectBasicFaceFeatures(Image<Rgb24> image)
    {
        // Simplified face detection using heuristics
        // In production, use proper face detection models

        // Check image size (face should be reasonable size)
        if (image.Width < 50 || image.Height < 50)
            return false;

        // Check for skin-like colors (very basic)
        var skinPixelCount = 0;
        var totalPixels = image.Width * image.Height;
        var sampleSize = Math.Min(10000, totalPixels);
        var random = new Random();

        for (int i = 0; i < sampleSize; i++)
        {
            var x = random.Next(0, image.Width);
            var y = random.Next(0, image.Height);
            var pixel = image[x, y];

            // Basic skin color detection (RGB ranges)
            if (IsSkinColor(pixel))
            {
                skinPixelCount++;
            }
        }

        var skinRatio = (double)skinPixelCount / sampleSize;
        return skinRatio > 0.1 && skinRatio < 0.8; // Reasonable skin color ratio
    }

    private bool IsSkinColor(Rgb24 pixel)
    {
        // Very basic skin color detection
        var r = pixel.R;
        var g = pixel.G;
        var b = pixel.B;

        return r > 95 && g > 40 && b > 20 &&
               r > g && r > b &&
               Math.Abs(r - g) > 15 && r - g > 15 &&
               r > g && r > b;
    }

    private BoundingBoxDto EstimateFaceBoundingBox(Image<Rgb24> image)
    {
        // Simplified face bounding box estimation
        // In production, use proper face detection models

        // Assume face is in the upper central portion of the image
        var faceWidth = image.Width / 3;
        var faceHeight = image.Height / 3;
        var centerX = image.Width / 2;
        var centerY = image.Height / 3;

        return new BoundingBoxDto
        {
            X = centerX - faceWidth / 2,
            Y = centerY - faceHeight / 2,
            Width = faceWidth,
            Height = faceHeight
        };
    }

    private float EstimateSharpness(Image<Rgb24> image)
    {
        // Simplified sharpness estimation using edge detection
        var edgeCount = 0;
        var sampleSize = Math.Min(1000, image.Width * image.Height / 100);
        var random = new Random();

        for (int i = 0; i < sampleSize; i++)
        {
            var x = random.Next(1, image.Width - 1);
            var y = random.Next(1, image.Height - 1);

            var pixel = image[x, y];
            var leftPixel = image[x - 1, y];
            var diff = Math.Abs(pixel.R - leftPixel.R) + Math.Abs(pixel.G - leftPixel.G) + Math.Abs(pixel.B - leftPixel.B);

            if (diff > 30)
            {
                edgeCount++;
            }
        }

        return (float)edgeCount / sampleSize;
    }

    private float EstimateBrightness(Image<Rgb24> image)
    {
        var totalBrightness = 0f;
        var sampleSize = Math.Min(1000, image.Width * image.Height / 100);
        var random = new Random();

        for (int i = 0; i < sampleSize; i++)
        {
            var x = random.Next(0, image.Width);
            var y = random.Next(0, image.Height);
            var pixel = image[x, y];

            totalBrightness += (pixel.R + pixel.G + pixel.B) / 3.0f;
        }

        return totalBrightness / sampleSize / 255.0f; // Normalize to [0, 1]
    }

    private float EstimateContrast(Image<Rgb24> image)
    {
        var brightnesses = new List<float>();
        var sampleSize = Math.Min(500, image.Width * image.Height / 200);
        var random = new Random();

        for (int i = 0; i < sampleSize; i++)
        {
            var x = random.Next(0, image.Width);
            var y = random.Next(0, image.Height);
            var pixel = image[x, y];

            brightnesses.Add((pixel.R + pixel.G + pixel.B) / 3.0f);
        }

        if (brightnesses.Count == 0) return 0f;

        var mean = brightnesses.Average();
        var variance = brightnesses.Select(b => Math.Pow(b - mean, 2)).Average();
        var stdDev = (float)Math.Sqrt(variance);

        return Math.Min(stdDev / 128.0f, 1.0f); // Normalize to [0, 1]
    }
}

public class FaceDetectionDetails
{
    public bool FaceDetected { get; set; }
    public BoundingBoxDto? BoundingBox { get; set; }
    public float Confidence { get; set; }
    public FaceQualityMetrics? QualityMetrics { get; set; }
    public string? ErrorMessage { get; set; }
}

public class FaceQualityMetrics
{
    public float Sharpness { get; set; }
    public float Brightness { get; set; }
    public float Contrast { get; set; }
}