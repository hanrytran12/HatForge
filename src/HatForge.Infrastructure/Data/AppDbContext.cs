using HatForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HatForge.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<HatModel> HatModels => Set<HatModel>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<BatchWorkshop> BatchWorkshops => Set<BatchWorkshop>();
    public DbSet<Work> Works => Set<Work>();
    public DbSet<WorkPhoto> WorkPhotos => Set<WorkPhoto>();
    public DbSet<TransferRequest> TransferRequests => Set<TransferRequest>();
    public DbSet<MaterialDelivery> MaterialDeliveries => Set<MaterialDelivery>();
    public DbSet<MaterialDeliveryItem> MaterialDeliveryItems => Set<MaterialDeliveryItem>();
    public DbSet<MaterialRequest> MaterialRequests => Set<MaterialRequest>();
    public DbSet<MaterialRequestItem> MaterialRequestItems => Set<MaterialRequestItem>();
    public DbSet<LeadTaskDelegationRequest> LeadTaskDelegationRequests => Set<LeadTaskDelegationRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.Property(u => u.Name).IsRequired().HasMaxLength(128);
            e.Property(u => u.PasswordHash).IsRequired();
            e.HasOne(u => u.Workshop)
                .WithMany(w => w.Users)
                .HasForeignKey(u => u.WorkshopId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Workshop>(e =>
        {
            e.Property(w => w.Name).IsRequired().HasMaxLength(128);
        });

        b.Entity<HatModel>(e =>
        {
            e.Property(h => h.Name).IsRequired().HasMaxLength(128);
        });

        b.Entity<Batch>(e =>
        {
            e.HasIndex(x => x.BatchNumber).IsUnique();
            e.Property(x => x.BatchNumber).IsRequired().HasMaxLength(64);
            e.HasOne(x => x.HatModel)
                .WithMany(h => h.Batches)
                .HasForeignKey(x => x.HatModelId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedToLead)
                .WithMany()
                .HasForeignKey(x => x.AssignedToLeadId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<BatchWorkshop>(e =>
        {
            e.HasIndex(x => new { x.BatchId, x.WorkshopId }).IsUnique();
            e.Property(x => x.InitialMaterialQty).HasPrecision(18, 2);
            e.Property(x => x.MaterialUsed).HasPrecision(18, 2);
            e.Property(x => x.EstimatedMetersPerUnit).HasPrecision(18, 4);
            e.HasOne(x => x.Batch)
                .WithMany(x => x.BatchWorkshops)
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Workshop)
                .WithMany(w => w.BatchWorkshops)
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Work>(e =>
        {
            e.Property(x => x.RejectionNotes).HasMaxLength(500);
            e.Property(x => x.ActualMaterialUsed).HasPrecision(18, 2);
            e.Property(x => x.EstimatedMaterialUsed).HasPrecision(18, 4);
            e.HasOne(x => x.Batch)
                .WithMany(x => x.Works)
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReviewedByQC)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByQCId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<WorkPhoto>(e =>
        {
            e.Property(x => x.PhotoUrl).IsRequired().HasMaxLength(512);
            e.HasOne(x => x.Work)
                .WithMany(x => x.Photos)
                .HasForeignKey(x => x.WorkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TransferRequest>(e =>
        {
            e.Property(x => x.ReceiptInspectionNotes).HasMaxLength(500);
            e.HasOne(x => x.Batch)
                .WithMany(x => x.TransferRequests)
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.FromWorkshop)
                .WithMany()
                .HasForeignKey(x => x.FromWorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ToWorkshop)
                .WithMany()
                .HasForeignKey(x => x.ToWorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ApprovedByLead)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByLeadId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CreatedByQC)
                .WithMany()
                .HasForeignKey(x => x.CreatedByQCId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ConfirmedByQC)
                .WithMany()
                .HasForeignKey(x => x.ConfirmedByQCId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<MaterialDelivery>(e =>
        {
            e.HasOne(x => x.Batch)
                .WithMany()
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<MaterialDeliveryItem>(e =>
        {
            e.Property(x => x.MaterialName).IsRequired().HasMaxLength(256);
            e.HasOne(x => x.MaterialDelivery)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.MaterialDeliveryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MaterialRequest>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasOne(x => x.OriginalDelivery)
                .WithMany()
                .HasForeignKey(x => x.OriginalDeliveryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Batch)
                .WithMany()
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByQC)
                .WithMany()
                .HasForeignKey(x => x.CreatedByQCId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ApprovedByLead)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByLeadId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.DeliveredByTransportQc)
                .WithMany()
                .HasForeignKey(x => x.DeliveredByTransportQcId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.FulfilledByQC)
                .WithMany()
                .HasForeignKey(x => x.FulfilledByQCId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<MaterialRequestItem>(e =>
        {
            e.Property(x => x.MaterialName).IsRequired().HasMaxLength(256);
            e.Property(x => x.Unit).IsRequired().HasMaxLength(32);
            e.HasOne(x => x.MaterialRequest)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.MaterialRequestId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<LeadTaskDelegationRequest>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.AdminNotes).HasMaxLength(500);
            e.HasIndex(x => new { x.Type, x.MaterialDeliveryId, x.Status });
            e.HasIndex(x => new { x.Type, x.TransferRequestId, x.Status });
            e.HasIndex(x => new { x.Type, x.MaterialRequestId, x.Status });
            e.HasIndex(x => new { x.Type, x.BatchId, x.Status });
            e.HasIndex(x => x.MaterialDeliveryId)
                .IsUnique()
                .HasDatabaseName("UX_LeadTaskDelegationRequests_ActiveMaterialDelivery")
                .HasFilter(@"""Type"" = 0 AND ""Status"" IN (0, 1) AND ""MaterialDeliveryId"" IS NOT NULL");
            e.HasIndex(x => x.TransferRequestId)
                .IsUnique()
                .HasDatabaseName("UX_LeadTaskDelegationRequests_ActiveTransferApproval")
                .HasFilter(@"""Type"" = 1 AND ""Status"" IN (0, 1) AND ""TransferRequestId"" IS NOT NULL");
            e.HasIndex(x => x.BatchId)
                .IsUnique()
                .HasDatabaseName("UX_LeadTaskDelegationRequests_ActiveFinalReview")
                .HasFilter(@"""Type"" = 2 AND ""Status"" IN (0, 1)");
            e.HasIndex(x => x.MaterialRequestId)
                .IsUnique()
                .HasDatabaseName("UX_LeadTaskDelegationRequests_ActiveMaterialRequestFulfillment")
                .HasFilter(@"""Type"" = 3 AND ""Status"" IN (0, 1, 3) AND ""MaterialRequestId"" IS NOT NULL");
            e.ToTable(t => t.HasCheckConstraint(
                    "CK_LeadTaskDelegationRequests_ExactlyOneTask",
                    @"(""Type"" = 0 AND ""MaterialDeliveryId"" IS NOT NULL AND ""TransferRequestId"" IS NULL AND ""MaterialRequestId"" IS NULL)
                      OR (""Type"" = 1 AND ""TransferRequestId"" IS NOT NULL AND ""MaterialDeliveryId"" IS NULL AND ""MaterialRequestId"" IS NULL)
                      OR (""Type"" = 2 AND ""MaterialDeliveryId"" IS NULL AND ""TransferRequestId"" IS NULL AND ""MaterialRequestId"" IS NULL)
                      OR (""Type"" = 3 AND ""MaterialRequestId"" IS NOT NULL AND ""MaterialDeliveryId"" IS NULL AND ""TransferRequestId"" IS NULL)"));
            e.HasOne(x => x.Batch)
                .WithMany()
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MaterialDelivery)
                .WithMany()
                .HasForeignKey(x => x.MaterialDeliveryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.TransferRequest)
                .WithMany()
                .HasForeignKey(x => x.TransferRequestId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.MaterialRequest)
                .WithMany()
                .HasForeignKey(x => x.MaterialRequestId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RequestedByLead)
                .WithMany()
                .HasForeignKey(x => x.RequestedByLeadId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedTransportQc)
                .WithMany()
                .HasForeignKey(x => x.AssignedTransportQcId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CompletedByTransportQc)
                .WithMany()
                .HasForeignKey(x => x.CompletedByTransportQcId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Notification>(e =>
        {
            e.Property(x => x.Type).IsRequired().HasMaxLength(64);
            e.Property(x => x.Title).IsRequired().HasMaxLength(256);
            e.Property(x => x.Message).IsRequired().HasMaxLength(1024);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.IsRead });
        });
    }
}
