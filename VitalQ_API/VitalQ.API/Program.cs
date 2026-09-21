using Microsoft.EntityFrameworkCore;
using VitalQ.API.BackgroundServices;
using VitalQ.API.Hubs;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.BusinessLogic.Services;
using VitalQ.DataAccess;
using VitalQ.DataAccess.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context
builder.Services.AddDbContext<VitalQDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Data Access Repositories (DAL)
builder.Services.AddScoped<IQueueTokenRepository, QueueTokenRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();

// 3. Business Logic Services (BLL)
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ITriageService, TriageService>();
builder.Services.AddScoped<IQueueService, QueueService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// 4. SignalR & Background Aging Worker
builder.Services.AddSignalR();
builder.Services.AddHostedService<AgingWorker>();

// 5. Controllers & JSON cycle handling
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();
app.MapHub<QueueHub>("/hubs/queue");

app.Run();
