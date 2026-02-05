namespace DocumentVerification.API.Models.DTOs.Verification;

public class WorkflowStatsDto
{
    public int totalVerifications { get; set; }
    public int pendingVerifications { get; set; }
    public int processingVerifications { get; set; }
    public int completedVerifications { get; set; }
    public int approvedVerifications { get; set; }
    public int rejectedVerifications { get; set; }
    public int reviewNeededVerifications { get; set; }
    public double averageProcessingTimeMinutes { get; set; }
    public int todayVerifications { get; set; }
}

