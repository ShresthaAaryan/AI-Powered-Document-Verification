using DocumentVerification.API.Data;
using DocumentVerification.API.Models.DTOs.Verification;
using DocumentVerification.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DocumentVerification.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VerificationController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IWorkflowService _workflowService;
    private readonly DocumentVerificationDbContext _context;
    private readonly ILogger<VerificationController> _logger;

    public VerificationController(
        IDocumentService documentService,
        IWorkflowService workflowService,
        DocumentVerificationDbContext context,
        ILogger<VerificationController> logger)
    {
        _documentService = documentService;
        _workflowService = workflowService;
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<VerificationDto>> CreateVerification([FromForm] CreateVerificationRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var verification = await _documentService.CreateVerificationAsync(request, userId);

            // Automatically start processing the verification
            try
            {
                await _workflowService.StartVerificationAsync(verification.Id);
                // Refresh verification to get updated status
                verification = await _documentService.GetVerificationByIdAsync(verification.Id) ?? verification;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-start verification: {VerificationId}", verification.Id);
                // Continue even if auto-start fails - verification is still created
            }

            return CreatedAtAction(nameof(GetVerification), new { id = verification.Id }, verification);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Verification creation failed");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during verification creation");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VerificationDto>> GetVerification(Guid id)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var verification = await _documentService.GetVerificationByIdAsync(id);
            if (verification == null)
            {
                return NotFound(new { error = "Verification not found" });
            }

            // Users can only access their own verifications unless they're admins
            if (userRole != "Admin" && verification.SubmittedBy != userId)
            {
                return Forbid();
            }

            return Ok(verification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting verification: {VerificationId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("my-verifications")]
    public async Task<ActionResult<IEnumerable<VerificationDto>>> GetMyVerifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var verifications = await _documentService.GetUserVerificationsAsync(userId, page, pageSize);
            return Ok(verifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user verifications");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Admin,VerificationOfficer")]
    public async Task<ActionResult<IEnumerable<VerificationDto>>> GetAllVerifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        try
        {
            var verifications = await _documentService.GetAllVerificationsAsync(page, pageSize, status);
            return Ok(verifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all verifications");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,VerificationOfficer")]
    public async Task<ActionResult<VerificationDto>> UpdateVerificationStatus(
        Guid id,
        [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var verification = await _documentService.UpdateVerificationStatusAsync(
                id,
                request.Status,
                request.Reason);

            return Ok(verification);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Status update failed for verification: {VerificationId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating verification status: {VerificationId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("{id}/document/{documentId}")]
    public async Task<ActionResult> GetDocument(Guid id, Guid documentId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var verification = await _documentService.GetVerificationByIdAsync(id);
            if (verification == null)
            {
                return NotFound(new { error = "Verification not found" });
            }

            // Users can only access their own documents unless they're admins
            if (userRole != "Admin" && verification.SubmittedBy != userId)
            {
                return Forbid();
            }

            var fileBytes = await _documentService.GetDocumentFileAsync(documentId);
            var document = verification.Documents.FirstOrDefault(d => d.Id == documentId);
            if (document == null)
            {
                return NotFound(new { error = "Document not found" });
            }

            return File(fileBytes, document.MimeType, document.OriginalFileName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Document access failed: {DocumentId}", documentId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document: {DocumentId}", documentId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteVerification(Guid id)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var isAdmin = userRole == "Admin" || userRole == "VerificationOfficer";

            // Check if user has permission - query entity directly to get UserId
            var verificationEntity = await _context.Verifications.FindAsync(id);
            if (verificationEntity == null)
            {
                return NotFound(new { error = "Verification not found" });
            }

            // Users can only delete their own verifications unless they're admins
            // Check both UserId and SubmittedBy to handle different scenarios
            var isOwner = !string.IsNullOrEmpty(userId) && 
                         (verificationEntity.SubmittedBy == userId || verificationEntity.UserId == userId);
            
            if (!isAdmin && !isOwner)
            {
                _logger.LogWarning("User {UserId} attempted to delete verification {VerificationId} owned by {OwnerId}", 
                    userId, id, verificationEntity.SubmittedBy ?? verificationEntity.UserId);
                return Forbid();
            }

            var success = await _documentService.DeleteVerificationAsync(id);
            if (!success)
            {
                return NotFound(new { error = "Verification not found" });
            }

            return Ok(new { message = "Verification deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting verification: {VerificationId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}