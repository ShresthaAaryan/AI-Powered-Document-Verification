using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocumentVerification.API.Configuration;

public class AIModelsHealthCheck : IHealthCheck
{
    private readonly ILogger<AIModelsHealthCheck> _logger;
    private readonly IConfiguration _configuration;

    public AIModelsHealthCheck(ILogger<AIModelsHealthCheck> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var issues = new List<string>();

            // Check Tesseract data path
            var tesseractDataPath = _configuration["Tesseract:DataPath"];
            if (string.IsNullOrEmpty(tesseractDataPath) || !Directory.Exists(tesseractDataPath))
            {
                issues.Add("Tesseract data path not found or inaccessible");
            }

            // Check ONNX model path
            var onnxModelPath = _configuration["ONNX:ArcFaceModelPath"];
            if (string.IsNullOrEmpty(onnxModelPath) || !File.Exists(onnxModelPath))
            {
                issues.Add("ArcFace ONNX model not found or inaccessible");
            }

            // Check upload directory
            var uploadPath = _configuration["FileStorage:BasePath"];
            if (!string.IsNullOrEmpty(uploadPath))
            {
                try
                {
                    Directory.CreateDirectory(uploadPath);
                }
                catch
                {
                    issues.Add("Cannot create or access upload directory");
                }
            }

            if (issues.Count == 0)
            {
                return HealthCheckResult.Healthy("All AI models and resources are available");
            }

            return HealthCheckResult.Degraded(
                description: "Some AI resources are unavailable",
                data: new Dictionary<string, object>
                {
                    ["issues"] = string.Join(", ", issues)
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI models health check");
            return HealthCheckResult.Unhealthy("AI models health check failed", ex);
        }
    }
}