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
    private const string FaceModelInputName = "data";

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

            // Compare using multiple candidate crops and take the best similarity.
            // This improves reliability when the ID portrait is small (or surrounded by text) and when selfies are soft/blurred.
            var colorBest = await CompareWithCandidatesAsync(idDocumentPath, selfiePath, useGrayscale: false);
            var grayBest = await CompareWithCandidatesAsync(idDocumentPath, selfiePath, useGrayscale: true);

            if (colorBest.idEmbedding == null || colorBest.selfieEmbedding == null)
            {
                return CreateFailedResult(verificationId, idFaceDetected, selfieFaceDetected, stopwatch.ElapsedMilliseconds, "EmbeddingGenerationFailed");
            }

            var useGray = grayBest.similarity > colorBest.similarity;
            var similarityScore = useGray ? grayBest.similarity : colorBest.similarity;
            var idEmbedding = useGray ? grayBest.idEmbedding : colorBest.idEmbedding;
            var selfieEmbedding = useGray ? grayBest.selfieEmbedding : colorBest.selfieEmbedding;

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

    public Task<float[]?> GenerateFaceEmbeddingAsync(string imagePath)
    {
        return GenerateFaceEmbeddingAsync(imagePath, useGrayscale: false);
    }

    public async Task<float[]?> GenerateFaceEmbeddingAsync(string imagePath, bool useGrayscale = false)
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
            if (useGrayscale)
            {
                ConvertToGrayscaleInPlace(image);
            }

            // Enhance image quality for better face recognition (soft ID photos, uneven lighting, low contrast)
            EnhanceImageForFaceRecognition(image);

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
                NamedOnnxValue.CreateFromTensor(FaceModelInputName, inputTensor)
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

    private async Task<(float similarity, float[]? idEmbedding, float[]? selfieEmbedding)> CompareWithCandidatesAsync(
        string idDocumentPath,
        string selfiePath,
        bool useGrayscale)
    {
        var idEmbeddings = await GenerateCandidateEmbeddingsAsync(idDocumentPath, useGrayscale);
        var selfieEmbeddings = await GenerateCandidateEmbeddingsAsync(selfiePath, useGrayscale);

        if (idEmbeddings.Count == 0 || selfieEmbeddings.Count == 0)
        {
            // Fallback to existing single-embedding path (keeps behavior for edge cases).
            var idOne = await GenerateFaceEmbeddingAsync(idDocumentPath, useGrayscale);
            var selfieOne = await GenerateFaceEmbeddingAsync(selfiePath, useGrayscale);
            if (idOne == null || selfieOne == null) return (0f, null, null);
            var sim = await CalculateSimilarityScore(idOne, selfieOne);
            return (sim, idOne, selfieOne);
        }

        float best = 0f;
        float[]? bestId = null;
        float[]? bestSelfie = null;

        // Evaluate a small cartesian product of candidate crops.
        foreach (var idEmb in idEmbeddings)
        {
            foreach (var sEmb in selfieEmbeddings)
            {
                var sim = await CalculateSimilarityScore(idEmb, sEmb);
                if (sim > best)
                {
                    best = sim;
                    bestId = idEmb;
                    bestSelfie = sEmb;
                }
            }
        }

        return (best, bestId, bestSelfie);
    }

    private async Task<List<float[]>> GenerateCandidateEmbeddingsAsync(string imagePath, bool useGrayscale)
    {
        var results = new List<float[]>();

        if (_faceRecognitionSession == null) return results;

        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imagePath);

            if (useGrayscale)
            {
                ConvertToGrayscaleInPlace(image);
            }

            EnhanceImageForFaceRecognition(image);

            // Detect a face box on the original path (keeps existing logic consistent).
            var (_, faceBox) = await DetectFaceBoundingBoxAsync(imagePath);
            var candidateBoxes = GetCandidateFaceBoxes(image.Width, image.Height, faceBox);

            // Limit compute: at most 3 candidates per image.
            foreach (var box in candidateBoxes.Take(3))
            {
                using var clone = image.Clone();
                var processed = PreprocessFaceImage(clone, box);
                var embedding = RunFaceModel(processed);
                if (embedding != null)
                {
                    results.Add(embedding);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed generating candidate embeddings for {ImagePath}", imagePath);
        }

        return results;
    }

    private float[]? RunFaceModel(float[] processedImage)
    {
        try
        {
            if (_faceRecognitionSession == null) return null;

            var inputTensor = new DenseTensor<float>(processedImage, new[] { 1, 3, 112, 112 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(FaceModelInputName, inputTensor)
            };

            using var results = _faceRecognitionSession.Run(inputs);
            var embedding = results.First().AsEnumerable<float>().ToArray();
            return NormalizeEmbedding(embedding);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Face model inference failed");
            return null;
        }
    }

    private static List<Rectangle?> GetCandidateFaceBoxes(int imageWidth, int imageHeight, Rectangle? detected)
    {
        var list = new List<Rectangle?>();

        if (detected.HasValue)
        {
            // Base box + a slightly expanded version to tolerate detection errors.
            var b = detected.Value;
            list.Add(b);

            var expandX = (int)(b.Width * 0.18);
            var expandY = (int)(b.Height * 0.18);
            var rx = Math.Clamp(b.X - expandX, 0, imageWidth - 1);
            var ry = Math.Clamp(b.Y - expandY, 0, imageHeight - 1);
            var rw = Math.Clamp(b.Width + 2 * expandX, 10, imageWidth - rx);
            var rh = Math.Clamp(b.Height + 2 * expandY, 10, imageHeight - ry);
            list.Add(new Rectangle(rx, ry, rw, rh));
        }

        // Always add a center crop fallback (null means: rely on ResizeMode.Crop center).
        list.Add(null);

        // Document-style images often have the portrait on the left side.
        if (imageWidth > imageHeight * 1.2)
        {
            var w = (int)(imageWidth * 0.5);
            var h = (int)(imageHeight * 0.7);
            list.Add(new Rectangle(0, 0, Math.Clamp(w, 10, imageWidth), Math.Clamp(h, 10, imageHeight)));
        }

        // Deduplicate rectangles (by value) while preserving order.
        var seen = new HashSet<string>();
        var deduped = new List<Rectangle?>();
        foreach (var r in list)
        {
            var key = r.HasValue ? $"{r.Value.X},{r.Value.Y},{r.Value.Width},{r.Value.Height}" : "null";
            if (seen.Add(key)) deduped.Add(r);
        }
        return deduped;
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

    /// <summary>
    /// Enhances image quality for face recognition: sharpens soft/blurry photos (e.g. ID documents),
    /// improves contrast for flat lighting, and normalizes brightness for uneven lighting.
    /// </summary>
    private static void EnhanceImageForFaceRecognition(Image<Rgb24> image)
    {
        // Normalize brightness first (estimate before any changes)
        var brightness = EstimateBrightnessForEnhancement(image);
        image.Mutate(ctx =>
        {
            // Improve contrast so facial features and contours are more distinct (helps soft/flat ID photos)
            ctx.Contrast(1.2f);
            // Normalize brightness: boost if too dark, reduce if too bright (helps uneven passport lighting)
            if (brightness < 0.4f)
                ctx.Brightness(1.15f);
            else if (brightness > 0.65f)
                ctx.Brightness(0.92f);
            // Sharpen to recover detail in low-resolution or slightly blurry ID/selfie images
            ctx.GaussianSharpen(1.5f);
        });
    }

    private static float EstimateBrightnessForEnhancement(Image<Rgb24> image)
    {
        long sum = 0;
        int count = 0;
        int stepX = Math.Max(1, image.Width / 80);
        int stepY = Math.Max(1, image.Height / 80);
        for (int y = 0; y < image.Height; y += stepY)
        {
            for (int x = 0; x < image.Width; x += stepX)
            {
                var p = image[x, y];
                sum += (p.R + p.G + p.B) / 3;
                count++;
            }
        }
        return count == 0 ? 0.5f : (sum / (float)count / 255f);
    }

    /// <summary>
    /// Converts an RGB image to grayscale in place (R=G=B = luminance) for more consistent face comparison
    /// when ID photos are B&amp;W or lighting differs between ID and selfie.
    /// </summary>
    private static void ConvertToGrayscaleInPlace(Image<Rgb24> image)
    {
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var p = image[x, y];
                byte gray = (byte)Math.Clamp(0.299 * p.R + 0.587 * p.G + 0.114 * p.B, 0, 255);
                image[x, y] = new Rgb24(gray, gray, gray);
            }
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

            // Build a skin mask and pick the best connected component (instead of taking min/max over all skin pixels).
            // This prevents text/background from polluting the crop on ID documents.
            var mask = new bool[image.Width * image.Height];
            int idx = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    mask[idx++] = IsSkinLikely(image[x, y]);
                }
            }

            var best = FindBestConnectedSkinComponent(mask, image.Width, image.Height);
            if (!best.HasValue)
            {
                // Fallback: return a reasonable center-ish box; candidates logic will also try left-side crop for wide images.
                var fw = (int)(image.Width * 0.45);
                var fh = (int)(image.Height * 0.55);
                var fx = Math.Clamp((image.Width - fw) / 2, 0, Math.Max(0, image.Width - fw));
                var fy = Math.Clamp((image.Height - fh) / 4, 0, Math.Max(0, image.Height - fh));
                var fallback = new Rectangle(fx, fy, Math.Clamp(fw, 10, image.Width), Math.Clamp(fh, 10, image.Height));

                if (Math.Abs(scale - 1.0) > 0.0001)
                {
                    fallback = new Rectangle((int)(fallback.X / scale), (int)(fallback.Y / scale), (int)(fallback.Width / scale), (int)(fallback.Height / scale));
                }
                return (true, fallback);
            }

            var rectScaled = best.Value;

            // Convert back to original scale if we resized
            if (Math.Abs(scale - 1.0) > 0.0001)
            {
                rectScaled = new Rectangle(
                    (int)(rectScaled.X / scale),
                    (int)(rectScaled.Y / scale),
                    (int)(rectScaled.Width / scale),
                    (int)(rectScaled.Height / scale));
            }

            // Final sanity clamp
            rectScaled = ClampRect(rectScaled, (int)(image.Width / scale), (int)(image.Height / scale));
            return (true, rectScaled);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heuristic face box detection failed");
            return (false, null);
        }
    }

    private static Rectangle ClampRect(Rectangle r, int w, int h)
    {
        var x = Math.Clamp(r.X, 0, Math.Max(0, w - 1));
        var y = Math.Clamp(r.Y, 0, Math.Max(0, h - 1));
        var rw = Math.Clamp(r.Width, 10, Math.Max(10, w - x));
        var rh = Math.Clamp(r.Height, 10, Math.Max(10, h - y));
        return new Rectangle(x, y, rw, rh);
    }

    // Combined RGB + YCbCr-ish heuristic (more tolerant to lighting than the old RGB-only rule).
    private static bool IsSkinLikely(Rgb24 p)
    {
        var r = (int)p.R;
        var g = (int)p.G;
        var b = (int)p.B;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));

        // Classic RGB rule (kept, but slightly relaxed).
        bool rgbSkin = r > 75 && g > 30 && b > 15 &&
                       (max - min) > 10 &&
                       (r - g) > 10 &&
                       r > g && r > b;

        // Approximate YCbCr constraints.
        // Y  = 0.299R + 0.587G + 0.114B
        // Cb = 128 - 0.168736R - 0.331264G + 0.5B
        // Cr = 128 + 0.5R - 0.418688G - 0.081312B
        double cb = 128 - 0.168736 * r - 0.331264 * g + 0.5 * b;
        double cr = 128 + 0.5 * r - 0.418688 * g - 0.081312 * b;
        bool yccSkin = cb is >= 77 and <= 127 && cr is >= 133 and <= 173;

        return rgbSkin || yccSkin;
    }

    private static Rectangle? FindBestConnectedSkinComponent(bool[] mask, int width, int height)
    {
        var visited = new bool[mask.Length];

        Rectangle? bestRect = null;
        int bestArea = 0;
        double bestScore = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (visited[i] || !mask[i]) continue;

                // Flood fill (4-neighborhood)
                int minX = x, minY = y, maxX = x, maxY = y;
                int count = 0;

                var q = new Queue<int>();
                q.Enqueue(i);
                visited[i] = true;

                while (q.Count > 0)
                {
                    int cur = q.Dequeue();
                    int cy = cur / width;
                    int cx = cur - cy * width;

                    count++;
                    if (cx < minX) minX = cx;
                    if (cx > maxX) maxX = cx;
                    if (cy < minY) minY = cy;
                    if (cy > maxY) maxY = cy;

                    // neighbors
                    void Try(int nx, int ny)
                    {
                        if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) return;
                        int ni = ny * width + nx;
                        if (visited[ni] || !mask[ni]) return;
                        visited[ni] = true;
                        q.Enqueue(ni);
                    }

                    Try(cx - 1, cy);
                    Try(cx + 1, cy);
                    Try(cx, cy - 1);
                    Try(cx, cy + 1);
                }

                if (count < (width * height) * 0.01) continue; // too small to be a face region

                int rw = (maxX - minX + 1);
                int rh = (maxY - minY + 1);
                int rectArea = rw * rh;
                if (rectArea <= 0) continue;

                double aspect = rw / (double)rh;
                if (aspect < 0.45 || aspect > 1.9) continue;

                double fill = count / (double)rectArea; // how dense the component is in its bounding box
                if (fill < 0.12) continue;

                // Prefer components in upper/middle portion (common for faces in docs/selfies),
                // prefer larger, denser components.
                double centerY = (minY + maxY) / 2.0 / height;
                double yBonus = centerY < 0.75 ? 1.0 : 0.85;
                double score = rectArea * fill * yBonus;

                if (score > bestScore || (score == bestScore && rectArea > bestArea))
                {
                    bestScore = score;
                    bestArea = rectArea;
                    bestRect = new Rectangle(minX, minY, rw, rh);
                }
            }
        }

        if (!bestRect.HasValue) return null;

        // Reject if the best component is still too tiny vs full image.
        if (bestArea < (width * height) * 0.02) return null;

        return bestRect.Value;
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