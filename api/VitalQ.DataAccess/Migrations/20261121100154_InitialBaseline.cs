using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VitalQ.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LocationFloor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastTokenNumber = table.Column<int>(type: "int", nullable: false),
                    LastTokenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Departme__3214EC07C171BD8C", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NursingStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    StationName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationFloor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__NursingS__3214EC07A8171AB6", x => x.Id);
                    table.ForeignKey(
                        name: "FK__NursingSt__Depar__44FF419A",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    RefreshToken = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RefreshTokenExpiryTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NursingStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__3214EC073427C0CC", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_NursingStations_NursingStationId",
                        column: x => x.NursingStationId,
                        principalTable: "NursingStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Specialization = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Available"),
                    AvgConsultationMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Doctors__3214EC07F7077CCD", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Doctors__Departm__4BAC3F29",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Doctors__UserId__4AB81AF0",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MedicalRecordNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Patients__3214EC07FC77F5AF", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Patients__UserId__5535A963",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "QueueTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    TokenNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Booked"),
                    PriorityScore = table.Column<int>(type: "int", nullable: false),
                    BaseWeight = table.Column<int>(type: "int", nullable: false),
                    BookedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    TriagedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsultationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QueueTok__3214EC0750657CDB", x => x.Id);
                    table.ForeignKey(
                        name: "FK__QueueToke__Depar__5AEE82B9",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__QueueToke__Docto__5BE2A6F2",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__QueueToke__Patie__59FA5E80",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TriageAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    QueueTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NurseUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NursingStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriageLevel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SpO2 = table.Column<decimal>(type: "decimal(4,1)", nullable: false),
                    SystolicBP = table.Column<int>(type: "int", nullable: false),
                    DiastolicBP = table.Column<int>(type: "int", nullable: false),
                    HeartRate = table.Column<int>(type: "int", nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(4,1)", nullable: false),
                    PainScale = table.Column<int>(type: "int", nullable: false),
                    IsManualOverride = table.Column<bool>(type: "bit", nullable: false),
                    OverrideReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NurseNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__TriageAs__3214EC075D0B9242", x => x.Id);
                    table.ForeignKey(
                        name: "FK__TriageAss__Nurse__656C112C",
                        column: x => x.NurseUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__TriageAss__Nursi__66603565",
                        column: x => x.NursingStationId,
                        principalTable: "NursingStations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__TriageAss__Queue__6477ECF3",
                        column: x => x.QueueTokenId,
                        principalTable: "QueueTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ__Departme__A25C5AA73BEFC9B1",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_Dept_Status",
                table: "Doctors",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ__Doctors__1788CC4D6EEFBA27",
                table: "Doctors",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NursingStations_DepartmentId",
                table: "NursingStations",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_CreatedAtUtc",
                table: "Patients",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_FullName",
                table: "Patients",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PhoneNumber",
                table: "Patients",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "UQ__Patients__8E549ED0439BE140",
                table: "Patients",
                column: "MedicalRecordNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Patients_UserId",
                table: "Patients",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokens_Dept_Status",
                table: "QueueTokens",
                columns: new[] { "DepartmentId", "Status", "PriorityScore", "BookedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokens_Doctor_Status",
                table: "QueueTokens",
                columns: new[] { "DoctorId", "Status", "PriorityScore", "BookedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokens_Patient_Date",
                table: "QueueTokens",
                columns: new[] { "PatientId", "BookedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_TokenNumber",
                table: "QueueTokens",
                column: "TokenNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriageAssessments_NurseUserId",
                table: "TriageAssessments",
                column: "NurseUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TriageAssessments_NursingStationId",
                table: "TriageAssessments",
                column: "NursingStationId");

            migrationBuilder.CreateIndex(
                name: "UQ__TriageAs__DD7E97C8B81C306A",
                table: "TriageAssessments",
                column: "QueueTokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NursingStationId",
                table: "Users",
                column: "NursingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RefreshToken",
                table: "Users",
                column: "RefreshToken",
                unique: true,
                filter: "[RefreshToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ__Users__536C85E4E322DABA",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TriageAssessments");

            migrationBuilder.DropTable(
                name: "QueueTokens");

            migrationBuilder.DropTable(
                name: "Doctors");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "NursingStations");

            migrationBuilder.DropTable(
                name: "Departments");
        }
    }
}
