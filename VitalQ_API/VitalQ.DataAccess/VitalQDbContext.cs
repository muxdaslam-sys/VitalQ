using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using VitalQ.Entities.Models;

namespace VitalQ.DataAccess;

public partial class VitalQDbContext : DbContext
{
    public VitalQDbContext(DbContextOptions<VitalQDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Doctor> Doctors { get; set; }

    public virtual DbSet<NursingStation> NursingStations { get; set; }

    public virtual DbSet<Patient> Patients { get; set; }

    public virtual DbSet<QueueToken> QueueTokens { get; set; }

    public virtual DbSet<TokenAuditLog> TokenAuditLogs { get; set; }

    public virtual DbSet<TriageAssessment> TriageAssessments { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Departme__3214EC07C171BD8C");

            entity.HasIndex(e => e.Code, "UQ__Departme__A25C5AA73BEFC9B1").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Code).HasMaxLength(10);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LocationFloor).HasMaxLength(20);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Doctors__3214EC07F7077CCD");

            entity.HasIndex(e => e.UserId, "UQ__Doctors__1788CC4D6EEFBA27").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AvgConsultationMinutes).HasDefaultValue(10);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RoomNumber).HasMaxLength(20);
            entity.Property(e => e.Specialization).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Available");

            entity.HasOne(d => d.Department).WithMany(p => p.Doctors)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Doctors__Departm__4BAC3F29");

            entity.HasOne(d => d.User).WithOne(p => p.Doctor)
                .HasForeignKey<Doctor>(d => d.UserId)
                .HasConstraintName("FK__Doctors__UserId__4AB81AF0");
        });

        modelBuilder.Entity<NursingStation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__NursingS__3214EC07A8171AB6");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LocationFloor).HasMaxLength(20);
            entity.Property(e => e.StationName).HasMaxLength(100);

            entity.HasOne(d => d.Department).WithMany(p => p.NursingStations)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__NursingSt__Depar__44FF419A");
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Patients__3214EC07FC77F5AF");

            entity.HasIndex(e => e.UserId, "UQ__Patients__1788CC4DDB52AD00").IsUnique();

            entity.HasIndex(e => e.PhoneNumber, "UQ__Patients__85FB4E38C4051772").IsUnique();

            entity.HasIndex(e => e.MedicalRecordNumber, "UQ__Patients__8E549ED0439BE140").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.MedicalRecordNumber).HasMaxLength(30);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);

            entity.HasOne(d => d.User).WithOne(p => p.Patient)
                .HasForeignKey<Patient>(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__Patients__UserId__5535A963");
        });

        modelBuilder.Entity<QueueToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__QueueTok__3214EC0750657CDB");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.BookedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Booked");
            entity.Property(e => e.TokenNumber).HasMaxLength(20);

            entity.HasOne(d => d.Department).WithMany(p => p.QueueTokens)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QueueToke__Depar__5AEE82B9");

            entity.HasOne(d => d.Doctor).WithMany(p => p.QueueTokens)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QueueToke__Docto__5BE2A6F2");

            entity.HasOne(d => d.Patient).WithMany(p => p.QueueTokens)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QueueToke__Patie__59FA5E80");
        });

        modelBuilder.Entity<TokenAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TokenAud__3214EC070A7592BB");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ChangeSource)
                .HasMaxLength(20)
                .HasDefaultValue("User");
            entity.Property(e => e.ChangedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.NewStatus).HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(255);
            entity.Property(e => e.PreviousStatus).HasMaxLength(20);

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.TokenAuditLogs)
                .HasForeignKey(d => d.ChangedByUserId)
                .HasConstraintName("FK__TokenAudi__Chang__6D0D32F4");

            entity.HasOne(d => d.QueueToken).WithMany(p => p.TokenAuditLogs)
                .HasForeignKey(d => d.QueueTokenId)
                .HasConstraintName("FK__TokenAudi__Queue__6C190EBB");
        });

        modelBuilder.Entity<TriageAssessment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TriageAs__3214EC075D0B9242");

            entity.HasIndex(e => e.QueueTokenId, "UQ__TriageAs__DD7E97C8B81C306A").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AssessedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DiastolicBp).HasColumnName("DiastolicBP");
            entity.Property(e => e.OverrideReason).HasMaxLength(255);
            entity.Property(e => e.SpO2).HasColumnType("decimal(4, 1)");
            entity.Property(e => e.SystolicBp).HasColumnName("SystolicBP");
            entity.Property(e => e.Temperature).HasColumnType("decimal(4, 1)");
            entity.Property(e => e.TriageLevel).HasMaxLength(10);

            entity.HasOne(d => d.NurseUser).WithMany(p => p.TriageAssessments)
                .HasForeignKey(d => d.NurseUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TriageAss__Nurse__656C112C");

            entity.HasOne(d => d.NursingStation).WithMany(p => p.TriageAssessments)
                .HasForeignKey(d => d.NursingStationId)
                .HasConstraintName("FK__TriageAss__Nursi__66603565");

            entity.HasOne(d => d.QueueToken).WithOne(p => p.TriageAssessment)
                .HasForeignKey<TriageAssessment>(d => d.QueueTokenId)
                .HasConstraintName("FK__TriageAss__Queue__6477ECF3");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC073427C0CC");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E4E322DABA").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
