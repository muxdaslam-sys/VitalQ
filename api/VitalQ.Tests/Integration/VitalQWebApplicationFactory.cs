using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;
using VitalQ.Tests.Helpers;
using DoctorEntity = VitalQ.Entities.Models.Doctor;
using PatientEntity = VitalQ.Entities.Models.Patient;

namespace VitalQ.Tests.Integration;

public class VitalQWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "VitalQ_IntegrationDb_" + Guid.NewGuid();

    public Guid AdminUserId { get; private set; }
    public Guid DoctorUserId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid NurseUserId { get; private set; }
    public Guid PatientUserId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid WaitingTokenId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing EF Core registrations
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<VitalQDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(VitalQDbContext) ||
                (d.ServiceType.FullName != null && d.ServiceType.FullName.Contains("DbContextPool")) ||
                (d.ServiceType.FullName != null && d.ServiceType.FullName.Contains("ScopedDbContextLease"))
            ).ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Register VitalQDbContext with InMemory provider
            services.AddDbContext<VitalQDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.EnableSensitiveDataLogging();
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VitalQDbContext>();
        db.Database.EnsureCreated();
        SeedClinicalData(db);

        return host;
    }

    private void SeedClinicalData(VitalQDbContext db)
    {
        DepartmentId = Guid.NewGuid();
        var department = new Department
        {
            Id = DepartmentId,
            Name = "Cardiology",
            Code = "CARD",
            LocationFloor = "Floor 2",
            LastTokenNumber = 1,
            LastTokenDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Departments.Add(department);

        AdminUserId = Guid.NewGuid();
        var adminUser = new User
        {
            Id = AdminUserId,
            Username = "admin.test",
            Password = "AdminPass123!",
            Role = "Admin",
            FullName = "Hospital System Admin",
            PhoneNumber = "9999999991",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(adminUser);

        DoctorUserId = Guid.NewGuid();
        var doctorUser = new User
        {
            Id = DoctorUserId,
            Username = "dr.smith",
            Password = "DoctorPass123!",
            Role = "Doctor",
            FullName = "Dr. Sarah Smith",
            PhoneNumber = "9999999992",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(doctorUser);

        DoctorId = Guid.NewGuid();
        var doctor = new DoctorEntity
        {
            Id = DoctorId,
            UserId = DoctorUserId,
            DepartmentId = DepartmentId,
            Specialization = "Cardiologist",
            RoomNumber = "201",
            Status = "Available",
            AvgConsultationMinutes = 15,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Doctors.Add(doctor);

        NurseUserId = Guid.NewGuid();
        var nurseUser = new User
        {
            Id = NurseUserId,
            Username = "nurse.jane",
            Password = "NursePass123!",
            Role = "Nurse",
            FullName = "Nurse Jane Wilson",
            PhoneNumber = "9999999993",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(nurseUser);

        PatientUserId = Guid.NewGuid();
        var patientUser = new User
        {
            Id = PatientUserId,
            Username = "patient.john",
            Password = "PatientPass123!",
            Role = "Patient",
            FullName = "John Doe",
            PhoneNumber = "9999999994",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(patientUser);

        PatientId = Guid.NewGuid();
        var patient = new PatientEntity
        {
            Id = PatientId,
            UserId = PatientUserId,
            MedicalRecordNumber = "MRN-000001",
            FullName = "John Doe",
            PhoneNumber = "9999999994",
            Gender = "Male",
            DateOfBirth = new DateOnly(1985, 1, 1),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Patients.Add(patient);

        WaitingTokenId = Guid.NewGuid();
        var queueToken = new QueueToken
        {
            Id = WaitingTokenId,
            TokenNumber = "CARD-001",
            DepartmentId = DepartmentId,
            DoctorId = DoctorId,
            PatientId = PatientId,
            Status = "Waiting",
            PriorityScore = 150,
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[] { 1, 0, 0, 0 }
        };
        db.QueueTokens.Add(queueToken);

        db.SaveChanges();
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string username, string password)
    {
        var client = CreateClient();

        var loginPayload = new LoginRequest
        {
            Username = username,
            Password = password
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginPayload),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/auth/login", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<AuthResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (authResponse?.AccessToken != null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);
        }

        return client;
    }
}
