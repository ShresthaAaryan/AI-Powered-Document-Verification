using DocumentVerification.API.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DocumentVerification.API.Data;

public class DocumentVerificationDbContext : IdentityDbContext<IdentityUser>
{
    public DocumentVerificationDbContext(DbContextOptions<DocumentVerificationDbContext> options) : base(options)
    {
    }

    public DbSet<Verification> Verifications { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<OcrResult> OcrResults { get; set; }
    public DbSet<AuthenticityScore> AuthenticityScores { get; set; }
    public DbSet<FaceMatchResult> FaceMatchResults { get; set; }
    public DbSet<VerificationLog> VerificationLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Verification entity
        builder.Entity<Verification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReferenceNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.DocumentType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Priority).HasMaxLength(10);
            entity.Property(e => e.FinalDecision).HasMaxLength(20);
            entity.Property(e => e.DecisionReason).HasMaxLength(1000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.UserActionRequired).HasMaxLength(1000);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.SubmittedBy);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.AssignedTo);
        });

        // Configure Document entity
        builder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DocumentType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.MimeType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.OriginalFileName).HasMaxLength(255);
            entity.Property(e => e.ChecksumMd5).HasMaxLength(32);
            entity.Property(e => e.ChecksumSha256).HasMaxLength(64);
            entity.HasOne(d => d.Verification)
                  .WithMany(v => v.Documents)
                  .HasForeignKey(d => d.VerificationId)
                  .HasPrincipalKey(v => v.Id)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure OcrResult entity
        builder.Entity<OcrResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConfidenceScore).HasPrecision(5, 4);
            entity.Property(e => e.LanguageDetected).HasMaxLength(10);
            entity.Property(e => e.TesseractVersion).HasMaxLength(20);
            entity.HasOne(e => e.Verification)
                  .WithMany(v => v.OcrResults)
                  .HasForeignKey(e => e.VerificationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AuthenticityScore entity
        builder.Entity<AuthenticityScore>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Classification).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ModelVersion).HasMaxLength(20);
            entity.HasOne(e => e.Verification)
                  .WithMany(v => v.AuthenticityScores)
                  .HasForeignKey(e => e.VerificationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure FaceMatchResult entity
        builder.Entity<FaceMatchResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SimilarityScore).HasPrecision(5, 4);
            entity.Property(e => e.ConfidenceThreshold).HasPrecision(5, 4);
            entity.Property(e => e.ModelVersion).HasMaxLength(20);
            entity.HasOne(e => e.Verification)
                  .WithMany(v => v.FaceMatchResults)
                  .HasForeignKey(e => e.VerificationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure VerificationLog entity
        builder.Entity<VerificationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ServiceName).HasMaxLength(50);
            entity.Property(e => e.PreviousStatus).HasMaxLength(20);
            entity.Property(e => e.NewStatus).HasMaxLength(20);
            entity.Property(e => e.UserAgent);
            entity.Property(e => e.ErrorMessage);
            entity.HasOne(log => log.Verification)
                  .WithMany(v => v.Logs)
                  .HasForeignKey(log => log.VerificationId)
                  .HasPrincipalKey(v => v.Id)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.UserId);
        });

        // Seed data
        SeedData(builder);
    }

    private void SeedData(ModelBuilder builder)
    {
        // Create default admin user
        var adminUser = new IdentityUser
        {
            Id = "1",
            UserName = "admin@docverify.com",
            Email = "admin@docverify.com",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        adminUser.PasswordHash = new PasswordHasher<IdentityUser>().HashPassword(adminUser, "Admin123!");

        builder.Entity<IdentityUser>().HasData(adminUser);

        // Create admin role
        builder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = "1",
                Name = "Admin",
                NormalizedName = "ADMIN"
            },
            new IdentityRole
            {
                Id = "2",
                Name = "VerificationOfficer",
                NormalizedName = "VERIFICATIONOFFICER"
            }
        );

        // Assign admin role to admin user
        builder.Entity<IdentityUserRole<string>>().HasData(
            new IdentityUserRole<string>
            {
                UserId = adminUser.Id,
                RoleId = "1"
            }
        );
    }
}