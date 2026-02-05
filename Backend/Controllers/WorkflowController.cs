using DocumentVerification.API.Models.DTOs.Verification;
using DocumentVerification.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DocumentVerification.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _workflowService;
    private readonly IDocumentService _documentService;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IWorkflowService workflowService, 
        IDocumentService documentService,
        ILogger<WorkflowController> logger)
    {
        _workflowService = workflowService;
        _documentService = documentService;
        _logger = logger;
    }

    [HttpPost("{id}/start")]
    public async Task<ActionResult<VerificationDto>> StartVerification(Guid id)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Check if user has permission to start verification
            if (userRole != "Admin" && userRole != "VerificationOfficer")
            {
                // Users can only start their own verifications
                var verification = await _workflowService.UpdateStageAsync(id, "Check", "");
                if (verification.SubmittedBy != userId)
                {
                    return Forbid();
                }
            }

            var result = await _workflowService.StartVerificationAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to start verification: {VerificationId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting verification: {VerificationId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("{id}/process")]
    [Authorize(Roles = "Admin,VerificationOfficer")]
    public async Task<ActionResult<VerificationDto>> ProcessVerification(Guid id)
    {
        try
        {
            var result = await _workflowService.ProcessVerificationAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to process verification: {VerificationId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing verification: {VerificationId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("{id}/stage")]
    [Authorize(Roles = "Admin,VerificationOfficer")]
    public async Task<ActionResult<VerificationDto>> UpdateStage(Guid id, [FromBody] UpdateStageRequest request)
    {
        try
        {
            var result = await _workflowService.UpdateStageAsync(id, request.Stage, request.Status);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update stage: {VerificationId}, Stage: {Stage}", id, request.Stage);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stage: {VerificationId}, Stage: {Stage}", id, request.Stage);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("{id}/decision")]
    [Authorize(Roles = "Admin,VerificationOfficer")]
    public async Task<ActionResult<VerificationDto>> MakeFinalDecision(Guid id)
    {
        try
        {
            var result = await _workflowService.MakeFinalDecisionAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to make final decision: {VerificationId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error making final decision: {VerificationId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("{id}/needs-review")]
    public async Task<ActionResult<bool>> NeedsManualReview(Guid id)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Check permissions
            if (userRole != "Admin" && userRole != "VerificationOfficer")
            {
                // Users can check if their own verification needs review
                // This would require additional validation logic
                return Forbid();
            }

            var needsReview = await _workflowService.NeedsManualReviewAsync(id);
            return Ok(needsReview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if verification needs review: {VerificationId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("{id}/assign")]
    [Authorize(Roles = "Admin,VerificationOfficer")]
    public async Task<ActionResult<VerificationDto>> AssignToOfficer(Guid id, [FromBody] AssignOfficerRequest request)
    {
        try
        {
            var result = await _workflowService.AssignToOfficerAsync(id, request.OfficerId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to assign verification to officer: {VerificationId}, Officer: {OfficerId}", id, request.OfficerId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning verification to officer: {VerificationId}, Officer: {OfficerId}", id, request.OfficerId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("queue")]
    [Authorize(Roles = "Admin,VerificationOfficer")]
    public async Task<ActionResult<IEnumerable<VerificationDto>>> GetReviewQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? priority = null)
    {
        try
        {
            // Get verifications that need manual review (ReviewNeeded, Processing, or Pending status)
            var statuses = new[] { "ReviewNeeded", "Processing", "Pending" };
            var allVerifications = await _documentService.GetAllVerificationsAsync(page, pageSize, null);
            
            // Filter by statuses that need review
            var queue = allVerifications
                .Where(v => statuses.Contains(v.Status, StringComparer.OrdinalIgnoreCase))
                .ToList();
            
            // Filter by priority if specified
            if (!string.IsNullOrWhiteSpace(priority))
            {
                queue = queue.Where(v => 
                    v.Priority.Equals(priority, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            return Ok(queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting review queue");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("stats")]
    public async Task<ActionResult<WorkflowStatsDto>> GetWorkflowStats()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var isAdmin = userRole == "Admin" || userRole == "VerificationOfficer";

            var stats = await _workflowService.GetWorkflowStatsAsync(userId, isAdmin);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow stats");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("reset-processing")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ResetProcessingVerifications()
    {
        try
        {
            var count = await _workflowService.ResetProcessingVerificationsAsync();
            return Ok(new { message = $"Reset {count} verification(s) from Processing to Pending", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting processing verifications");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class UpdateStageRequest
{
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class AssignOfficerRequest
{
    public string OfficerId { get; set; } = string.Empty;
}