using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Common;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

/// <summary>
/// Implements nurse station services: patient lookup, walk-in registration, counter token issuance, and clinical triage.
/// </summary>
public class NurseService : INurseService
{
    private readonly VitalQDbContext _context;

    public NurseService(VitalQDbContext context)
    {
        _context = context;
    }

    #region 1. Patient Lookup & Identification

    /// <summary>
    /// Fast B-Tree index routed search for reception desk check-in.
    /// Returns today's active token state if an appointment is already booked.
    /// </summary>
    public async Task<IEnumerable<PatientSearchResult>> SearchPatientsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Enumerable.Empty<PatientSearchResult>();
        }

        var q = query.Trim();
        if (q.Length < 2)
        {
            return Enumerable.Empty<PatientSearchResult>();
        }

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var patientQuery = _context.Patients
            .AsNoTracking()
            .AsQueryable();

        var mrnClean = Regex.Replace(q, @"^mrn[\s\-_]*", "", RegexOptions.IgnoreCase).Trim();

        var digitsOnly = Regex.Replace(q, @"[^\d]", "");

        // 1. If searching by Token Number (must start with department code letters then dash, e.g. "CARD-")
        if (Regex.IsMatch(q, @"^[A-Za-z]+-") && !q.StartsWith("MRN", StringComparison.OrdinalIgnoreCase))
        {
            var matchedPatientIds = await _context.QueueTokens
                .AsNoTracking()
                .Where(t => t.TokenNumber.StartsWith(q) && t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc)
                .Select(t => t.PatientId)
                .Distinct()
                .Take(15)
                .ToListAsync();

            patientQuery = patientQuery.Where(p => matchedPatientIds.Contains(p.Id));
        }
        else if (q.StartsWith("MRN", StringComparison.OrdinalIgnoreCase))
        {
            patientQuery = patientQuery.Where(p => p.MedicalRecordNumber.StartsWith(q) 
                                                || p.MedicalRecordNumber.Contains(mrnClean));
        }
        else if (!string.IsNullOrEmpty(digitsOnly) && digitsOnly.Length >= 3 && !Regex.IsMatch(q, @"[a-zA-Z]"))
        {
            // Phone number search (handles 123-456, (123) 456, +1 1234, etc.)
            patientQuery = patientQuery.Where(p => p.PhoneNumber.Contains(digitsOnly) 
                                                || p.MedicalRecordNumber.Contains(q));
        }
        else
        {
            patientQuery = patientQuery.Where(p => p.FullName.Contains(q) 
                                                || p.MedicalRecordNumber.Contains(q) 
                                                || p.MedicalRecordNumber.Contains(mrnClean));
        }

        var results = await patientQuery
            .Take(15)
            .Select(p => new
            {
                p.Id,
                p.MedicalRecordNumber,
                p.FullName,
                p.PhoneNumber,
                p.DateOfBirth,
                p.Gender,
                ActiveToken = p.QueueTokens
                    .Where(t => t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc && t.Status != "Completed" && t.Status != "Cancelled")
                    .OrderByDescending(t => t.BookedAtUtc)
                    .Select(t => new
                    {
                        t.Id,
                        t.TokenNumber,
                        t.Status,
                        DoctorName = t.Doctor.User.FullName,
                        DepartmentName = t.Department.Name
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return results.Select(p => new PatientSearchResult
        {
            PatientId = p.Id,
            MedicalRecordNumber = p.MedicalRecordNumber,
            FullName = p.FullName,
            PhoneNumber = p.PhoneNumber,
            DateOfBirth = p.DateOfBirth,
            Gender = p.Gender,
            ActiveTokenId = p.ActiveToken != null ? (Guid?)p.ActiveToken.Id : null,
            ActiveTokenNumber = p.ActiveToken?.TokenNumber,
            ActiveTokenStatus = p.ActiveToken?.Status,
            ActiveDoctorName = p.ActiveToken?.DoctorName,
            ActiveDepartmentName = p.ActiveToken?.DepartmentName
        }).ToList();
    }

    #endregion

    #region 2. Clinical Triage & Vital Signs Intake

    /// <summary>
    /// Lists patients booked today awaiting vital signs intake.
    /// Supports filtering by Department and specific physical Nursing Station.
    /// </summary>
    public async Task<IEnumerable<QueueTokenResponse>> GetPendingTriageQueueAsync(Guid? departmentId = null, Guid? nursingStationId = null)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);
        var query = _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Department).ThenInclude(d => d.NursingStations)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department)
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NursingStation)
            .Where(t => (t.Status == "Booked" || t.Status == "Skipped" || (t.Status == "Waiting" && t.BaseWeight >= 1000)) && t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc);

        if (departmentId.HasValue && departmentId.Value != Guid.Empty)
        {
            query = query.Where(t => t.DepartmentId == departmentId.Value);
        }

        if (nursingStationId.HasValue && nursingStationId.Value != Guid.Empty)
        {
            var stationDeptId = await _context.NursingStations
                .AsNoTracking()
                .Where(s => s.Id == nursingStationId.Value)
                .Select(s => s.DepartmentId)
                .FirstOrDefaultAsync();

            if (stationDeptId.HasValue)
            {
                query = query.Where(t => t.DepartmentId == stationDeptId.Value);
            }
        }

        var tokens = await query.OrderBy(t => t.BookedAtUtc).ToListAsync();
        return tokens.Select(t => MapToTokenResponse(t, t.Patient, t.Doctor));
    }

    /// <summary>
    /// Records 5 vital signs, validates and stores NursingStationId, computes CTAS level,
    /// and activates token into the Doctor's waiting queue with base priority weight.
    /// </summary>
    public async Task<QueueTokenResponse> RecordVitalsAndTriageAsync(Guid tokenId, Guid nurseUserId, TriageRequest request)
    {
        var token = await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department).ThenInclude(dept => dept.NursingStations)
            .Include(t => t.Department).ThenInclude(dept => dept.NursingStations)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.Id == tokenId);

        if (token == null)
        {
            throw new KeyNotFoundException($"Queue token with ID {tokenId} was not found.");
        }

        if (token.Status == "Completed" || token.Status == "Cancelled")
        {
            throw new InvalidOperationException($"Cannot triage a token that is already {token.Status}.");
        }

        // 1. Validate NursingStationId if provided
        if (request.NursingStationId.HasValue)
        {
            var stationExists = await _context.NursingStations
                .AsNoTracking()
                .AnyAsync(s => s.Id == request.NursingStationId.Value && s.IsActive);

            if (!stationExists)
            {
                throw new ArgumentException($"Nursing station with ID '{request.NursingStationId.Value}' was not found or is inactive.");
            }
        }

        // 2. Determine clinical triage level (Automated CTAS vs Manual Override)
        string triageLevel;
        if (request.IsManualOverride && !string.IsNullOrWhiteSpace(request.OverrideTriageLevel))
        {
            var cleanOverride = request.OverrideTriageLevel.Trim();
            if (cleanOverride != "Red" && cleanOverride != "Yellow" && cleanOverride != "Green")
            {
                throw new ArgumentException("Manual override triage level must be 'Red', 'Yellow', or 'Green'.");
            }

            if (string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                throw new ArgumentException("A clinical override reason is mandatory when manually setting triage level.");
            }

            triageLevel = cleanOverride;
        }
        else
        {
            triageLevel = CalculateAutomatedTriageLevel(
                request.SpO2,
                request.SystolicBp,
                request.DiastolicBp,
                request.HeartRate,
                request.Temperature,
                request.PainScale);
        }

        var baseWeight = CalculateBaseWeight(triageLevel);

        // 3. Upsert Triage Assessment (Persisting NursingStationId & NurseUserId)
        var assessment = await _context.TriageAssessments.FirstOrDefaultAsync(a => a.QueueTokenId == tokenId);
        if (assessment == null)
        {
            assessment = new TriageAssessment
            {
                Id = Guid.NewGuid(),
                QueueTokenId = tokenId,
                NurseUserId = nurseUserId,
                AssessedAtUtc = DateTime.UtcNow
            };
            await _context.TriageAssessments.AddAsync(assessment);
        }

        // Bind all vitals and station audit info
        assessment.NursingStationId = request.NursingStationId;
        assessment.NurseUserId = nurseUserId;
        assessment.SpO2 = request.SpO2 ?? 0;
        assessment.SystolicBp = request.SystolicBp ?? 0;
        assessment.DiastolicBp = request.DiastolicBp ?? 0;
        assessment.HeartRate = request.HeartRate ?? 0;
        assessment.Temperature = request.Temperature ?? 0;
        assessment.PainScale = request.PainScale ?? 0;
        assessment.TriageLevel = triageLevel;
        assessment.IsManualOverride = request.IsManualOverride;
        assessment.OverrideReason = request.OverrideReason?.Trim();
        assessment.NurseNotes = request.NurseNotes?.Trim();
        assessment.AssessedAtUtc = DateTime.UtcNow;

        // 4. Transition token to 'Waiting' in doctor's queue
        token.Status = "Waiting";
        token.BaseWeight = baseWeight;
        token.PriorityScore = baseWeight;
        token.TriagedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // 5. Calculate position ahead
        var patientsAhead = await _context.QueueTokens
            .AsNoTracking()
            .Where(t => t.DoctorId == token.DoctorId &&
                        t.Status == "Waiting" &&
                        t.Id != token.Id &&
                        t.PriorityScore > token.PriorityScore)
            .CountAsync();

        var response = MapToTokenResponse(token, token.Patient, token.Doctor);
        response.PatientsAhead = patientsAhead;
        response.EstimatedWaitMinutes = patientsAhead * (token.Doctor?.AvgConsultationMinutes ?? 10);

        return response;
    }

    /// <summary>
    /// Retrieves full clinical triage details and vital signs for a consultation token.
    /// </summary>
    public async Task<TriageAssessmentResponse?> GetTriageAssessmentAsync(Guid tokenId)
    {
        var assessment = await _context.TriageAssessments
            .AsNoTracking()
            .Include(a => a.NurseUser)
            .Include(a => a.NursingStation)
            .FirstOrDefaultAsync(a => a.QueueTokenId == tokenId);

        return assessment != null ? MapToTriageResponse(assessment) : null;
    }

    #endregion

    #region 3. Dashboard KPIs & Live Doctor Queues

    /// <summary>
    /// Aggregates today's clinic metrics and live doctor roster with single-trip SQL aggregation.
    /// </summary>
    public async Task<NurseDashboardStatsResponse> GetDashboardStatsAsync(Guid? nursingStationId)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        Guid? deptId = null;
        if (nursingStationId.HasValue && nursingStationId.Value != Guid.Empty)
        {
            deptId = await _context.NursingStations
                .AsNoTracking()
                .Where(s => s.Id == nursingStationId.Value)
                .Select(s => s.DepartmentId)
                .FirstOrDefaultAsync();
        }

        // 1. Single aggregated query for today's tokens
        var tokenQuery = _context.QueueTokens
            .AsNoTracking()
            .Where(t => t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc);

        if (deptId.HasValue)
        {
            tokenQuery = tokenQuery.Where(t => t.DepartmentId == deptId.Value);
        }

        var tokenStats = await tokenQuery
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalBookings = tokenStats.Sum(x => x.Count);
        var totalCompleted = tokenStats.Where(x => x.Status == "Completed").Sum(x => x.Count);
        var totalTriaged = tokenStats.Where(x => x.Status != "Booked" && x.Status != "Cancelled").Sum(x => x.Count);
        var totalSkipped = tokenStats.Where(x => x.Status == "Skipped").Sum(x => x.Count);

        // 2. Query doctors for the station's department
        var doctorQuery = _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.User.IsActive);

        if (deptId.HasValue)
        {
            doctorQuery = doctorQuery.Where(d => d.DepartmentId == deptId.Value);
        }

        var doctors = await doctorQuery
            .OrderBy(d => d.User.FullName)
            .ToListAsync();

        var doctorsPresent = doctors.Count(d => d.Status == "Available" || d.Status == "Busy");
        var doctorsAbsent = doctors.Count(d => d.Status == "OnLeave");

        var doctorIds = doctors.Select(d => d.Id).ToList();

        // 3. Single-trip fetch of active tokens by doctor to eliminate N+1 queries
        var activeTokensByDoc = await _context.QueueTokens
            .AsNoTracking()
            .Where(t => t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc && doctorIds.Contains(t.DoctorId))
            .Select(t => new { t.DoctorId, t.TokenNumber, t.Status, t.CalledAtUtc })
            .ToListAsync();

        var doctorSummaries = doctors.Select(d =>
        {
            var docTokens = activeTokensByDoc.Where(t => t.DoctorId == d.Id).ToList();
            var currentCalling = docTokens
                .Where(t => t.Status == "Called")
                .OrderByDescending(t => t.CalledAtUtc)
                .Select(t => t.TokenNumber)
                .FirstOrDefault();

            return new DoctorQueueSummaryDto
            {
                DoctorId = d.Id,
                DoctorName = d.User.FullName,
                Specialization = d.Specialization,
                RoomNumber = d.RoomNumber,
                Status = d.Status,
                CurrentCallingToken = currentCalling,
                WaitingCount = docTokens.Count(t => t.Status == "Waiting"),
                TotalAssignedToday = docTokens.Count,
                SkippedCount = docTokens.Count(t => t.Status == "Skipped")
            };
        }).ToList();

        return new NurseDashboardStatsResponse
        {
            TotalBookingsToday = totalBookings,
            TotalTriagedToday = totalTriaged,
            TotalCompletedToday = totalCompleted,
            TotalSkippedToday = totalSkipped,
            DoctorsPresent = doctorsPresent,
            DoctorsAbsent = doctorsAbsent,
            Doctors = doctorSummaries
        };
    }

    /// <summary>
    /// Retrieves full live queue for a specific doctor with priority scores and triage vitals.
    /// </summary>
    public async Task<DoctorDetailedQueueResponse> GetDoctorQueueAsync(Guid doctorId)
    {
        var doctor = await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == doctorId);

        if (doctor == null)
        {
            throw new KeyNotFoundException($"Doctor with ID '{doctorId}' was not found.");
        }

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var tokens = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NurseUser)
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NursingStation)
            .Where(t => t.DoctorId == doctorId && t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc && t.Status != "Cancelled")
            .OrderByDescending(t => t.Status == "Called")
            .ThenByDescending(t => t.Status == "Waiting")
            .ThenByDescending(t => t.PriorityScore)
            .ThenBy(t => t.BookedAtUtc)
            .ToListAsync();

        var queueItems = tokens.Select(t =>
        {
            int skipCount = 0;
            if (!string.IsNullOrEmpty(t.ConsultationNotes))
            {
                if (t.ConsultationNotes.Contains("Call 3 of 3") || t.ConsultationNotes.Contains("Absent after 3 calls")) skipCount = 3;
                else if (t.ConsultationNotes.Contains("Call 2 of 3")) skipCount = 2;
                else if (t.ConsultationNotes.Contains("Call 1 of 3")) skipCount = 1;
            }

            return new DoctorQueueItemDto
            {
                TokenId = t.Id,
                TokenNumber = t.TokenNumber,
                PatientId = t.PatientId,
                PatientName = t.Patient.FullName,
                PhoneNumber = t.Patient.PhoneNumber,
                Status = t.Status,
                PriorityScore = t.PriorityScore,
                TriageLevel = t.TriageAssessment?.TriageLevel,
                SkipCount = skipCount,
                BookedAtUtc = t.BookedAtUtc,
                TriagedAtUtc = t.TriagedAtUtc,
                CalledAtUtc = t.CalledAtUtc,
                TriageAssessment = t.TriageAssessment != null ? MapToTriageResponse(t.TriageAssessment) : null
            };
        }).ToList();

        return new DoctorDetailedQueueResponse
        {
            DoctorId = doctor.Id,
            DoctorName = doctor.User.FullName,
            Specialization = doctor.Specialization,
            RoomNumber = doctor.RoomNumber,
            Status = doctor.Status,
            Queue = queueItems
        };
    }

    /// <summary>
    /// Retrieves all active tokens booked today for a patient.
    /// </summary>
    public async Task<IEnumerable<QueueTokenResponse>> GetPatientActiveTokensAsync(Guid patientId)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var tokens = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.Doctor).ThenInclude(d => d.Department).ThenInclude(dept => dept.NursingStations)
            .Include(t => t.Department).ThenInclude(dept => dept.NursingStations)
            .Include(t => t.TriageAssessment).ThenInclude(a => a!.NursingStation)
            .Where(t => t.PatientId == patientId && t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc && t.Status != "Cancelled")
            .OrderByDescending(t => t.BookedAtUtc)
            .ToListAsync();

        return tokens.Select(t => MapToTokenResponse(t, t.Patient, t.Doctor));
    }

    /// <summary>
    /// Retrieves complete patient medical profile and visit history for the Full Details modal.
    /// </summary>
    public async Task<PatientDetailResponse> GetPatientDetailsAsync(Guid patientId)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .Where(p => p.Id == patientId)
            .Select(p => new PatientDetailResponse
            {
                Id = p.Id,
                MedicalRecordNumber = p.MedicalRecordNumber,
                FullName = p.FullName,
                PhoneNumber = p.PhoneNumber,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                IsRegisteredAppUser = p.UserId.HasValue,
                CreatedAtUtc = p.CreatedAtUtc,
                TotalCompletedBookings = p.QueueTokens.Count(t => t.Status == "Completed"),
                Visits = p.QueueTokens
                    .OrderByDescending(t => t.BookedAtUtc)
                    .Select(t => new PatientVisitHistoryDto
                    {
                        TokenId = t.Id,
                        TokenNumber = t.TokenNumber,
                        PatientId = t.PatientId,
                        PatientName = p.FullName,
                        DepartmentName = t.Department.Name,
                        DoctorName = t.Doctor.User.FullName,
                        Status = t.Status,
                        TriageLevel = t.TriageAssessment != null ? t.TriageAssessment.TriageLevel : null,
                        BookedAtUtc = t.BookedAtUtc,
                        TriagedAtUtc = t.TriagedAtUtc,
                        CalledAtUtc = t.CalledAtUtc,
                        CompletedAtUtc = t.CompletedAtUtc,
                        ConsultationNotes = t.ConsultationNotes
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (patient == null)
        {
            throw new KeyNotFoundException($"Patient with ID '{patientId}' was not found.");
        }

        return patient;
    }

    /// <summary>
    /// Computes real-time line position, wait estimates, and doctor queue visualizer for a patient token.
    /// </summary>
    public async Task<PatientQueuePositionResponse> GetPatientQueuePositionAsync(Guid tokenId)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var token = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.Doctor).ThenInclude(d => d.User)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.Id == tokenId);

        if (token == null)
        {
            throw new KeyNotFoundException($"Token with ID '{tokenId}' was not found.");
        }

        // Fetch all active tokens for this doctor today
        var docTokens = await _context.QueueTokens
            .AsNoTracking()
            .Include(t => t.Patient)
            .Include(t => t.TriageAssessment)
            .Where(t => t.DoctorId == token.DoctorId && t.BookedAtUtc >= todayUtc && t.BookedAtUtc < tomorrowUtc && t.Status != "Cancelled")
            .OrderByDescending(t => t.Status == "Called")
            .ThenByDescending(t => t.Status == "Waiting")
            .ThenByDescending(t => t.PriorityScore)
            .ThenBy(t => t.BookedAtUtc)
            .ToListAsync();

        var queueItems = docTokens.Select(t => new DoctorQueueItemDto
        {
            TokenId = t.Id,
            TokenNumber = t.TokenNumber,
            PatientId = t.PatientId,
            PatientName = t.Patient.FullName,
            PhoneNumber = t.Patient.PhoneNumber,
            Status = t.Status,
            PriorityScore = t.PriorityScore,
            TriageLevel = t.TriageAssessment?.TriageLevel,
            SkipCount = t.Status == "Skipped" ? 1 : 0,
            BookedAtUtc = t.BookedAtUtc,
            TriagedAtUtc = t.TriagedAtUtc,
            CalledAtUtc = t.CalledAtUtc,
            TriageAssessment = t.TriageAssessment != null ? MapToTriageResponse(t.TriageAssessment) : null
        }).ToList();

        // Calculate position among Waiting tokens
        var waitingTokens = docTokens.Where(t => t.Status == "Waiting").ToList();
        var indexInWaiting = waitingTokens.FindIndex(t => t.Id == tokenId);

        int queuePosition = 0;
        int patientsAhead = 0;

        if (token.Status == "Called")
        {
            queuePosition = 1;
            patientsAhead = 0;
        }
        else if (indexInWaiting >= 0)
        {
            var isSomeoneCalled = docTokens.Any(t => t.Status == "Called");
            patientsAhead = indexInWaiting + (isSomeoneCalled ? 1 : 0);
            queuePosition = patientsAhead + 1;
        }

        var currentlyCalling = docTokens
            .Where(t => t.Status == "Called")
            .OrderByDescending(t => t.CalledAtUtc)
            .Select(t => t.TokenNumber)
            .FirstOrDefault();

        var avgMinutes = token.Doctor?.AvgConsultationMinutes ?? 15;
        var estWait = patientsAhead * avgMinutes;

        return new PatientQueuePositionResponse
        {
            TokenId = token.Id,
            TokenNumber = token.TokenNumber,
            PatientId = token.PatientId,
            PatientName = token.Patient.FullName,
            Status = token.Status,
            QueuePosition = queuePosition,
            PatientsAhead = patientsAhead,
            EstimatedWaitMinutes = estWait,
            DoctorId = token.DoctorId,
            DoctorName = token.Doctor?.User?.FullName ?? "Doctor",
            Specialization = token.Doctor?.Specialization ?? "General",
            RoomNumber = token.Doctor?.RoomNumber ?? "Room",
            CurrentlyCallingToken = currentlyCalling,
            TriageLevel = token.TriageAssessment?.TriageLevel,
            PriorityScore = token.PriorityScore,
            DoctorLiveQueue = queueItems
        };
    }

    #endregion

    #region 4. Emergency Walk-In & Fast-Track Intake

    /// <summary>
    /// Registers or resolves existing patient, generates walk-in token, and optionally executes Code-Red emergency bypass.
    /// </summary>
    public async Task<QueueTokenResponse> ProcessEmergencyWalkInAsync(EmergencyWalkInBookingRequest request, Guid nurseUserId, Guid? nursingStationId)
    {
        ValidateDateOfBirth(request.DateOfBirth);
        var phone = NormalizePhone(request.PhoneNumber);

        // 1. Resolve or create Patient
        var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PhoneNumber == phone);
        if (patient == null)
        {
            var dobPassword = request.DateOfBirth.ToString("ddMMyyyy");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == phone);
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = phone,
                    Password = dobPassword,
                    FullName = request.FullName.Trim(),
                    PhoneNumber = phone,
                    Role = "Patient",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                await _context.Users.AddAsync(user);
            }

            patient = new Patient
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                MedicalRecordNumber = MrnGenerator.Generate(),
                FullName = request.FullName.Trim(),
                PhoneNumber = phone,
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };
            await _context.Patients.AddAsync(patient);
            await _context.SaveChangesAsync();
        }

        return await CreateAndIssueTokenAsync(patient, request.DoctorId, request.IsEmergencyCodeRed, request.ClinicalNotes, nurseUserId, nursingStationId);
    }

    /// <summary>
    /// Issues a token for an existing registered patient with optional Code-Red emergency bypass.
    /// Eliminates re-entering demographics for already registered patients.
    /// </summary>
    public async Task<QueueTokenResponse> BookTokenForExistingPatientAsync(Guid patientId, ExistingPatientTokenBookingRequest request, Guid nurseUserId, Guid? nursingStationId)
    {
        var patient = await _context.Patients
            .FirstOrDefaultAsync(p => p.Id == patientId)
            ?? throw new KeyNotFoundException($"Patient with ID '{patientId}' was not found.");

        return await CreateAndIssueTokenAsync(patient, request.DoctorId, request.IsEmergencyCodeRed, request.ClinicalNotes, nurseUserId, nursingStationId);
    }

    /// <summary>
    /// Core centralized token issuance: validates physician duty, blocks duplicate active visits today,
    /// assigns daily atomic token number, and activates Code-Red bypass or routine intake.
    /// </summary>
    private async Task<QueueTokenResponse> CreateAndIssueTokenAsync(
        Patient patient,
        Guid doctorId,
        bool isEmergencyCodeRed,
        string? clinicalNotes,
        Guid nurseUserId,
        Guid? nursingStationId)
    {
        var doctor = await _context.Doctors
            .Include(d => d.Department).ThenInclude(dept => dept.NursingStations)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == doctorId)
            ?? throw new KeyNotFoundException("Doctor was not found.");

        if (!doctor.User.IsActive || string.Equals(doctor.Status, "OnLeave", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Dr. {doctor.User.FullName} is currently on leave or inactive and cannot accept patient appointments.");
        }

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var existingActiveToken = await _context.QueueTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PatientId == patient.Id
                                   && t.DoctorId == doctor.Id
                                   && t.BookedAtUtc >= today
                                   && t.BookedAtUtc < tomorrow
                                   && (t.Status == "Booked" || t.Status == "Waiting" || t.Status == "Called"));

        if (existingActiveToken != null)
        {
            throw new InvalidOperationException($"Patient '{patient.FullName}' already has an active appointment with Dr. {doctor.User.FullName} today (Token #{existingActiveToken.TokenNumber}, Status: {existingActiveToken.Status}).");
        }

        var tokenNumber = await TokenGenerator.GenerateAsync(_context, doctor.DepartmentId, doctor.Department.Code);

        if (isEmergencyCodeRed)
        {
            var token = new QueueToken
            {
                Id = Guid.NewGuid(),
                TokenNumber = tokenNumber,
                PatientId = patient.Id,
                DepartmentId = doctor.DepartmentId,
                DoctorId = doctor.Id,
                Status = "Waiting",
                BaseWeight = PriorityScoreCalculator.BaseWeightRed, // 1000: Guaranteed top priority
                PriorityScore = PriorityScoreCalculator.BaseWeightRed, // 1000: Uncompromised Code-Red score
                BookedAtUtc = DateTime.UtcNow,
                TriagedAtUtc = DateTime.UtcNow,
                ConsultationNotes = null
            };

            var emergencyReason = string.IsNullOrWhiteSpace(clinicalNotes)
                ? "Immediate Emergency Walk-In Bypass (Acute Life Threat)"
                : $"Immediate Emergency Bypass: {clinicalNotes.Trim()}";

            var assessment = new TriageAssessment
            {
                Id = Guid.NewGuid(),
                QueueTokenId = token.Id,
                NurseUserId = nurseUserId,
                NursingStationId = nursingStationId,
                SpO2 = 0,
                SystolicBp = 0,
                DiastolicBp = 0,
                HeartRate = 0,
                Temperature = 0,
                PainScale = 10,
                TriageLevel = "Red",
                IsManualOverride = true,
                OverrideReason = emergencyReason,
                NurseNotes = clinicalNotes?.Trim(),
                AssessedAtUtc = DateTime.UtcNow
            };

            await _context.QueueTokens.AddAsync(token);
            await _context.TriageAssessments.AddAsync(assessment);
            await _context.SaveChangesAsync();

            token.TriageAssessment = assessment;
            return MapToTokenResponse(token, patient, doctor);
        }
        else
        {
            var token = new QueueToken
            {
                Id = Guid.NewGuid(),
                TokenNumber = tokenNumber,
                PatientId = patient.Id,
                DepartmentId = doctor.DepartmentId,
                DoctorId = doctor.Id,
                Status = "Booked",
                BaseWeight = 0,
                PriorityScore = 0,
                BookedAtUtc = DateTime.UtcNow,
                ConsultationNotes = null
            };

            await _context.QueueTokens.AddAsync(token);
            await _context.SaveChangesAsync();

            return MapToTokenResponse(token, patient, doctor);
        }
    }

    /// <summary>
    /// Cancels an untriaged walk-in token if patient departed or checked in mistakenly.
        /// </summary>
        public async Task<QueueTokenResponse> CancelUntriagedTokenAsync(Guid tokenId, Guid nurseUserId)
        {
            var token = await _context.QueueTokens
                .Include(t => t.Patient)
                .Include(t => t.Doctor).ThenInclude(d => d.User)
                .Include(t => t.Department)
                .FirstOrDefaultAsync(t => t.Id == tokenId)
                ?? throw new KeyNotFoundException($"Token with ID '{tokenId}' was not found.");

            if (token.Status != "Booked")
            {
                throw new InvalidOperationException($"Cannot cancel a token with status '{token.Status}'. Only untriaged 'Booked' tokens can be cancelled at the nursing desk.");
            }

            token.Status = "Cancelled";
            token.ConsultationNotes = "Cancelled at Nurse Desk - Patient departed before vitals intake.";
            await _context.SaveChangesAsync();

            return MapToTokenResponse(token, token.Patient, token.Doctor);
        }

    #endregion

    #region 5. Clinical Decision Support & Entity Mapping

    /// <summary>
    /// Evaluates 5 core vital signs against Canadian Triage and Acuity Scale (CTAS) threshold rules:
    /// - Red (Immediate Resuscitation): SpO2 &lt; 90%, BP extreme, HR &gt; 140 or &lt; 40, Temp &gt; 40.0C, Pain 10/10.
    /// - Yellow (Urgent Assessment): SpO2 90-94%, borderline vitals, severe fever, Pain &gt;= 6/10.
    /// - Green (Standard Ambulatory): Hemodynamically stable parameters.
    /// </summary>
    private static string CalculateAutomatedTriageLevel(
        decimal? spo2, 
        int? systolicBp, 
        int? diastolicBp, 
        int? heartRate, 
        decimal? temperature, 
        int? painScale)
    {
        // 1. Red (Resuscitation / Immediate Life Threat)
        if ((spo2.HasValue && spo2.Value > 0 && spo2.Value < 90m) ||
            (systolicBp.HasValue && systolicBp.Value > 0 && (systolicBp.Value < 90 || systolicBp.Value > 200)) ||
            (diastolicBp.HasValue && diastolicBp.Value > 0 && diastolicBp.Value >= 130) ||
            (heartRate.HasValue && heartRate.Value > 0 && (heartRate.Value > 140 || heartRate.Value < 40)) ||
            (temperature.HasValue && temperature.Value > 0 && (temperature.Value > 40.0m || temperature.Value < 32.0m)) ||
            (painScale.HasValue && painScale.Value == 10))
        {
            return "Red";
        }

        // 2. Yellow (Urgent / Serious Medical Concern)
        if ((spo2.HasValue && spo2.Value >= 90m && spo2.Value <= 94m) ||
            (systolicBp.HasValue && ((systolicBp.Value >= 90 && systolicBp.Value <= 99) || (systolicBp.Value >= 160 && systolicBp.Value <= 200))) ||
            (diastolicBp.HasValue && diastolicBp.Value >= 105 && diastolicBp.Value < 130) ||
            (heartRate.HasValue && ((heartRate.Value >= 110 && heartRate.Value <= 140) || (heartRate.Value >= 40 && heartRate.Value < 50))) ||
            (temperature.HasValue && ((temperature.Value >= 38.5m && temperature.Value <= 40.0m) || (temperature.Value >= 32.0m && temperature.Value < 35.0m))) ||
            (painScale.HasValue && painScale.Value >= 6))
        {
            return "Yellow";
        }

        // 3. Green (Routine / Standard Ambulatory)
        return "Green";
    }

    /// <summary>
    /// Translates clinical triage level into base queue urgency weights for the priority sorting engine.
    /// </summary>
    private static int CalculateBaseWeight(string triageLevel) => triageLevel switch
    {
        "Red" => 1000,
        "Yellow" => 500,
        "Green" => 100,
        _ => 100
    };

    /// <summary>
    /// Strips non-digit formatting characters from telephone input and validates digit length.
    /// </summary>
    private static string NormalizePhone(string rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
            throw new ArgumentException("Phone number is required.");
        var cleaned = Regex.Replace(rawPhone.Trim(), @"[^\d]", "");
        if (cleaned.Length < 7 || cleaned.Length > 15)
            throw new ArgumentException("Please enter a valid phone number (7 to 15 digits).");
        return cleaned;
    }

    /// <summary>
    /// Verifies date of birth is logically within the past century and not in the future.
    /// </summary>
    private static void ValidateDateOfBirth(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dob >= today)
            throw new ArgumentException("Date of birth must be in the past.");
        if (dob < today.AddYears(-130))
            throw new ArgumentException("Please provide a valid date of birth.");
    }

    /// <summary>
    /// Projects QueueToken entity into QueueTokenResponse with resolved nursing station and live wayfinding guidance.
    /// </summary>
    private static TriageAssessmentResponse MapToTriageResponse(TriageAssessment a)
    {
        return new TriageAssessmentResponse
        {
            Id = a.Id,
            QueueTokenId = a.QueueTokenId,
            NurseUserId = a.NurseUserId,
            NurseName = a.NurseUser?.FullName ?? string.Empty,
            NursingStationId = a.NursingStationId,
            StationName = a.NursingStation?.StationName,
            SpO2 = a.SpO2 > 0 ? a.SpO2 : null,
            SystolicBp = a.SystolicBp > 0 ? a.SystolicBp : null,
            DiastolicBp = a.DiastolicBp > 0 ? a.DiastolicBp : null,
            HeartRate = a.HeartRate > 0 ? a.HeartRate : null,
            Temperature = a.Temperature > 0 ? a.Temperature : null,
            PainScale = a.PainScale,
            TriageLevel = a.TriageLevel,
            IsManualOverride = a.IsManualOverride,
            OverrideReason = a.OverrideReason,
            NurseNotes = a.NurseNotes,
            AssessedAtUtc = a.AssessedAtUtc
        };
    }

    private static QueueTokenResponse MapToTokenResponse(QueueToken t, Patient? p, Doctor? d)
    {
        // Resolve Nursing Station from TriageAssessment (if triaged) or from Department (if awaiting triage)
        var station = t.TriageAssessment?.NursingStation 
            ?? d?.Department?.NursingStations?.FirstOrDefault(s => s.IsActive)
            ?? t.Department?.NursingStations?.FirstOrDefault(s => s.IsActive);

        var stationName = station?.StationName ?? "Nursing Triage Desk";
        var stationFloor = station?.LocationFloor ?? d?.Department?.LocationFloor ?? t.Department?.LocationFloor ?? "Floor 1";
        var room = d?.RoomNumber ?? "Consultation Room";
        var doctorName = d?.User?.FullName ?? "Doctor";

        // Generate contextual wayfinding instruction based on current queue stage
        var nextStepInstruction = t.Status switch
        {
            "Booked" => $"Please report to {stationName} ({stationFloor}) for Vital Signs Intake.",
            "Waiting" => $"Vitals recorded ({t.TriageAssessment?.TriageLevel ?? "Green"}). Please wait in the lounge outside Room {room} ({stationFloor}).",
            "Called" => $"Please enter Room {room} for your consultation with Dr. {doctorName}.",
            "Completed" => "Consultation completed. Thank you for visiting VitalQ.",
            "Cancelled" => "Appointment was cancelled.",
            _ => $"Please proceed to {stationFloor}."
        };

        return new QueueTokenResponse
        {
            Id = t.Id,
            TokenNumber = t.TokenNumber,
            PatientId = t.PatientId,
            PatientName = p?.FullName ?? string.Empty,
            PatientPhone = p?.PhoneNumber ?? string.Empty,
            DepartmentId = t.DepartmentId,
            DepartmentName = d?.Department?.Name ?? t.Department?.Name ?? string.Empty,
            DepartmentCode = d?.Department?.Code ?? t.Department?.Code ?? string.Empty,
            DoctorId = t.DoctorId,
            DoctorName = doctorName,
            RoomNumber = room,
            Status = t.Status,
            PriorityScore = t.PriorityScore,
            BaseWeight = t.BaseWeight,
            TriageLevel = t.TriageAssessment?.TriageLevel,
            SpO2 = t.TriageAssessment?.SpO2,
            SystolicBp = t.TriageAssessment?.SystolicBp,
            DiastolicBp = t.TriageAssessment?.DiastolicBp,
            HeartRate = t.TriageAssessment?.HeartRate,
            Temperature = t.TriageAssessment?.Temperature,
            PainScale = t.TriageAssessment?.PainScale,
            NurseNotes = t.TriageAssessment?.NurseNotes,
            NurseName = t.TriageAssessment?.NurseUser?.FullName,
            BookedAtUtc = t.BookedAtUtc,
            TriagedAtUtc = t.TriagedAtUtc,
            CalledAtUtc = t.CalledAtUtc,
            CompletedAtUtc = t.CompletedAtUtc,
            ConsultationNotes = t.ConsultationNotes,
            RowVersion = t.RowVersion ?? Array.Empty<byte>(),
            NursingStationId = station?.Id,
            NursingStationName = stationName,
            LocationFloor = stationFloor,
            NextStepInstruction = nextStepInstruction
        };
    }

    #endregion
}

