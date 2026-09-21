using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NsiTransfer.Contract.Models.Enums;
using NsiTransfer.DAL.Db.Entities;
using System.ComponentModel;
using System.Reflection;

namespace NsiTransfer.DAL.Db.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Sending> Sendings { get; set; }

    public DbSet<TargetReferenceNode> TargetReferenceNodes { get; set; }

    public DbSet<SendingStatus> SendingStatuses { get; set; }

    public DbSet<Message> Messages { get; set; }

    public DbSet<MessageObject> MessageObjects { get; set; }

    public DbSet<PolynomObject> PolynomObjects { get; set; }

    public DbSet<PolynomObjectFailure> PolynomObjectFailures { get; set; }

    public DbSet<ClassificationGroupCodeMax> ClassificationGroupCodeMaxes { get; set; }

    public DbSet<MessageFailure> MessageFailures { get; set; }
    
    public DbSet<MessagePublishingResult> PublishingResults { get; set; }

    public DbSet<EmailMessage> EmailMessages { get; set; }

    public DbSet<EmailFailure> EmailFailures { get; set; }
    
    public DbSet<EmailRecipient> EmailRecipients { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sending>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(s => s.ActiveMarker).IsUnique();

            entity.Property(e => e.InitiatedAt).HasColumnType("timestamp with time zone").HasPrecision(3);
            entity.Property(e => e.EndedAt).HasColumnType("timestamp with time zone").HasPrecision(3).IsRequired(false);
            entity.Property(e => e.InitiatorName).HasColumnType("varchar(256)");

            entity.HasOne(e => e.TargetReferenceNode).WithMany().HasForeignKey(e => e.TargetReferenceNodeId).OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<TargetReferenceNode>(entity =>
        {
            entity.HasKey(e => e.Id);
        });


        modelBuilder.Entity<SendingStatus>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasConversion<int>().ValueGeneratedNever();
            entity.Property(e => e.Name).HasColumnType("varchar(256)");
            entity.Property(e => e.Description).HasColumnType("varchar(1024)");

            entity.HasData(GetSendingStatuses());
        });

        
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StartedCollectionFromPolynomAt).HasColumnType("timestamp with time zone").HasPrecision(3);
            entity.Property(e => e.FinishedCollectionFromPolynomAt).HasColumnType("timestamp with time zone").HasPrecision(3).IsRequired(false);
            entity.Property(e => e.SentAtQueue).HasColumnType("timestamp with time zone").HasPrecision(3).IsRequired(false);
            entity.Property(e => e.SerializedMessage)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => string.IsNullOrEmpty(v) ? "{}" : v,
                    v => v ?? "{}")
                .IsRequired(false);

            entity.HasMany(e => e.PolynomObjects).WithOne(o => o.Message).HasForeignKey(o => o.MessageId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.MessageObjects).WithOne(o => o.Message).HasForeignKey(o => o.MessageId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.MessageFailure).WithOne(e => e.Message).HasForeignKey<MessageFailure>(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MessageObject>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasColumnType("varchar(256)");
            entity.Property(e => e.SerializedObject)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => string.IsNullOrEmpty(v) ? "{}" : v,
                    v => v ?? "{}");

            entity.HasIndex(e => e.MessageId).IsUnique(false);
            entity.HasIndex(e => new { e.PolynomObjectId, e.PolynomTypeId }).IsUnique(false);
            entity.HasIndex(e => e.Name).IsUnique(false);
        });

        modelBuilder.Entity<PolynomObject>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasColumnType("varchar(256)");
            entity.Property(e => e.ClassificationCode).HasColumnType("varchar(64)").IsRequired(false);

            entity.HasIndex(e => e.Name).IsUnique(false);
            entity.HasIndex(e => e.ClassificationCode).IsUnique(false);
        });

        modelBuilder.Entity<PolynomObjectFailure>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FailureType).HasConversion<string>();
            entity.Property(e => e.FailedAt).HasColumnType("timestamp with time zone").HasPrecision(3);
            entity.Property(e => e.ObjectName).HasColumnType("varchar(256)");

            entity.HasOne(e => e.Message).WithMany().HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClassificationGroupCodeMax>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LastMaxCode).HasColumnType("varchar(64)").IsRequired(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone").HasPrecision(3);

            entity.HasIndex(e => new { e.GroupObjectId, e.GroupTypeId }).IsUnique();
        });

        modelBuilder.Entity<MessageFailure>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FailedAt).HasColumnType("timestamp with time zone").HasPrecision(3);
            entity.Property(e => e.FailureDescription).HasColumnType("text");
            entity.Property(e => e.FailureReasonTitle).HasColumnType("varchar(1024)");
        });

        modelBuilder.Entity<MessagePublishingResult>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasConversion<int>().ValueGeneratedNever();
            entity.Property(e => e.Name).HasColumnType("varchar(256)");
            entity.Property(e => e.Description).HasColumnType("varchar(1024)");

            entity.HasData(GetPublishingResults());
        });

        modelBuilder.Entity<EmailMessage>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SentAt).HasColumnType("timestamp with time zone").HasPrecision(3);
            entity.Property(e => e.Subject).HasColumnType("varchar(998)"); // RFC 5322: максимальная длина строки заголовка
            entity.Property(e => e.Body).HasColumnType("text");
            entity.Property(e => e.Status).HasConversion<string>(); // хранит "Sent" / "Failed" вместо 0 / 1

            entity.HasMany(e => e.Recipients)
                .WithMany(e => e.Messages)
                .UsingEntity<Dictionary<string, object>>(
                    "EmailMessagesToEmailRecipients",
                    j => j.HasOne<EmailRecipient>().WithMany()
                          .HasForeignKey("RecipientId")
                          .OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<EmailMessage>().WithMany()
                          .HasForeignKey("MessageId")
                          .OnDelete(DeleteBehavior.Cascade));

            entity.HasOne(e => e.Failure).WithOne(e => e.EmailMessage).HasForeignKey<EmailFailure>(e => e.EmailMessageId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailRecipient>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EmailAddress).HasColumnType("varchar(256)");
        });

        modelBuilder.Entity<EmailFailure>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ErrorMessage).HasColumnType("text");
        });

        base.OnModelCreating(modelBuilder);
    }

    private static MessagePublishingResult[] GetPublishingResults()
    {
        return Enum.GetValues<RabbitMqPublishingResultEnum>()
            .Select(value => new MessagePublishingResult
            {
                Id = value,
                Name = value.ToString(),
                Description = GetEnumDescription(value)
            })
            .ToArray();
    }

    public static SendingStatus[] GetSendingStatuses()
    {
        return Enum.GetValues<SendingStatusEnum>()
            .Select(s => new SendingStatus
            {
                Id = s,
                Name = s.ToString(),
                Description = GetEnumDescription(s)
            })
            .ToArray();
    }

    private static string GetEnumDescription<T>(T value) where T : Enum
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
}
