-- ====================================================================================
-- VitalQ: Complete 100 Patients & Cardiology Testing Data Seed Script
-- Database: VitalQ on DESKTOP-S6AF6OE\SQLEXPRESS
-- ====================================================================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>> Checking Cardiology Department, Doctor, and Nursing Station...';

    -- 1. Identify or Create Cardiology Department
    DECLARE @DeptId UNIQUEIDENTIFIER;
    SELECT TOP 1 @DeptId = Id FROM Departments WHERE Code = 'CARD';

    IF @DeptId IS NULL
    BEGIN
        SET @DeptId = NEWID();
        INSERT INTO Departments (Id, Name, Code, LocationFloor, IsActive, CreatedAtUtc)
        VALUES (@DeptId, 'Cardiology', 'CARD', 'Floor 2, Cardio Wing', 1, GETUTCDATE());
        PRINT 'Created Cardiology Department.';
    END
    ELSE
    BEGIN
        PRINT 'Found existing Cardiology Department.';
    END

    -- 2. Identify or Create Nursing Station (Desk A - Cardiology)
    DECLARE @StationId UNIQUEIDENTIFIER;
    SELECT TOP 1 @StationId = Id FROM NursingStations WHERE DepartmentId = @DeptId AND StationName LIKE '%Desk A%';

    IF @StationId IS NULL
    BEGIN
        SET @StationId = NEWID();
        INSERT INTO NursingStations (Id, DepartmentId, StationName, LocationFloor, IsActive)
        VALUES (@StationId, @DeptId, 'Desk A - Cardiology', 'Floor 2, Cardio Wing', 1);
        PRINT 'Created Nursing Station: Desk A - Cardiology.';
    END
    ELSE
    BEGIN
        PRINT 'Found existing Nursing Station: Desk A - Cardiology.';
    END

    -- 3. Identify or Create Cardiology Doctor
    DECLARE @DoctorId UNIQUEIDENTIFIER;
    DECLARE @DoctorUserId UNIQUEIDENTIFIER;

    SELECT TOP 1 @DoctorId = Id, @DoctorUserId = UserId 
    FROM Doctors 
    WHERE DepartmentId = @DeptId;

    IF @DoctorId IS NULL
    BEGIN
        SET @DoctorUserId = NEWID();
        INSERT INTO Users (Id, Username, Password, FullName, Role, PhoneNumber, IsActive, CreatedAtUtc)
        VALUES (@DoctorUserId, 'dr.sharma', 'Doctor@123', 'Dr. Rajesh Sharma, MD', 'Doctor', '9876543201', 1, GETUTCDATE());

        SET @DoctorId = NEWID();
        INSERT INTO Doctors (Id, UserId, DepartmentId, RoomNumber, Specialization, AvgConsultationMinutes, Status, CreatedAtUtc)
        VALUES (@DoctorId, @DoctorUserId, @DeptId, 'Room 204', 'Senior Interventional Cardiologist', 12, 'Available', GETUTCDATE());
        PRINT 'Created Cardiology Doctor: Dr. Rajesh Sharma (Room 204).';
    END
    ELSE
    BEGIN
        UPDATE Doctors SET Status = 'Available' WHERE Id = @DoctorId;
        PRINT 'Found existing Cardiology Doctor. Set status to Available.';
    END

    -- 4. Identify or Create Nurse Account Assigned to Desk A
    DECLARE @NurseUserId UNIQUEIDENTIFIER;
    SELECT TOP 1 @NurseUserId = Id FROM Users WHERE Role = 'Nurse' AND NursingStationId = @StationId;

    IF @NurseUserId IS NULL
    BEGIN
        SET @NurseUserId = NEWID();
        INSERT INTO Users (Id, Username, Password, FullName, Role, PhoneNumber, NursingStationId, IsActive, CreatedAtUtc)
        VALUES (@NurseUserId, 'nurse.cardio', 'Nurse@123', 'Nurse Sarah Jenkins, RN', 'Nurse', '9876543210', @StationId, 1, GETUTCDATE());
        PRINT 'Created Cardiology Triage Nurse: nurse.cardio (Password: Nurse@123).';
    END
    ELSE
    BEGIN
        PRINT 'Found existing Cardiology Nurse assigned to Desk A.';
    END

    -- ================================================================================
    -- 5. Seed 100 Patient Accounts (Users + Patients)
    -- ================================================================================
    PRINT '>> Seeding 100 Patient accounts (9800000001 to 9800000100)...';

    DECLARE @FirstNames TABLE (Id INT IDENTITY(1,1), Name NVARCHAR(50), Gender NVARCHAR(10));
    INSERT INTO @FirstNames (Name, Gender) VALUES
    ('Aarav', 'Male'), ('Aditi', 'Female'), ('Rohan', 'Male'), ('Priya', 'Female'),
    ('Rahul', 'Male'), ('Ananya', 'Female'), ('Vikram', 'Male'), ('Deepika', 'Female'),
    ('Arjun', 'Male'), ('Pooja', 'Female'), ('Karan', 'Male'), ('Sneha', 'Female'),
    ('Siddharth', 'Male'), ('Kavya', 'Female'), ('Manish', 'Male'), ('Meera', 'Female'),
    ('Naveen', 'Male'), ('Divya', 'Female'), ('Suresh', 'Male'), ('Sunita', 'Female');

    DECLARE @LastNames TABLE (Id INT IDENTITY(1,1), Name NVARCHAR(50));
    INSERT INTO @LastNames (Name) VALUES
    ('Nair'), ('Menon'), ('Kurian'), ('Pillai'), ('Varma'), ('Mathew'), ('Sharma'),
    ('Verma'), ('Patel'), ('Iyer'), ('Rao'), ('Gupta'), ('Reddy'), ('Das'), ('Chopra');

    DECLARE @i INT = 1;
    DECLARE @PatientUserId UNIQUEIDENTIFIER;
    DECLARE @PatientId UNIQUEIDENTIFIER;
    DECLARE @Phone NVARCHAR(20);
    DECLARE @FullName NVARCHAR(100);
    DECLARE @Gender NVARCHAR(10);
    DECLARE @Mrn NVARCHAR(30);
    DECLARE @Dob DATE = '1995-01-01';

    DECLARE @CreatedPatientIds TABLE (Seq INT, PatientId UNIQUEIDENTIFIER, FullName NVARCHAR(100), Phone NVARCHAR(20), MRN NVARCHAR(30));

    WHILE @i <= 100
    BEGIN
        SET @PatientUserId = NULL;
        SET @PatientId = NULL;
        SET @Phone = '98' + RIGHT('00000000' + CAST(@i AS VARCHAR(8)), 8); -- 9800000001 .. 9800000100
        SET @Mrn = 'MRN-261015-' + RIGHT('0000' + CAST(@i AS VARCHAR(4)), 4);
        
        DECLARE @fn NVARCHAR(50), @gen NVARCHAR(10), @ln NVARCHAR(50);
        SELECT @fn = Name, @gen = Gender FROM @FirstNames WHERE Id = ((@i % 20) + 1);
        SELECT @ln = Name FROM @LastNames WHERE Id = ((@i % 15) + 1);
        SET @FullName = @fn + ' ' + @ln;
        SET @Gender = @gen;

        -- Check if User already exists
        SELECT @PatientUserId = Id FROM Users WHERE Username = @Phone;
        IF @PatientUserId IS NULL
        BEGIN
            SET @PatientUserId = NEWID();
            INSERT INTO Users (Id, Username, Password, FullName, Role, PhoneNumber, IsActive, CreatedAtUtc)
            VALUES (@PatientUserId, @Phone, '01011995', @FullName, 'Patient', @Phone, 1, GETUTCDATE());
        END

        -- Check if Patient already exists
        SELECT @PatientId = Id FROM Patients WHERE PhoneNumber = @Phone;
        IF @PatientId IS NULL
        BEGIN
            SET @PatientId = NEWID();
            INSERT INTO Patients (Id, UserId, MedicalRecordNumber, FullName, PhoneNumber, DateOfBirth, Gender, CreatedAtUtc)
            VALUES (@PatientId, @PatientUserId, @Mrn, @FullName, @Phone, @Dob, @Gender, GETUTCDATE());
        END

        INSERT INTO @CreatedPatientIds (Seq, PatientId, FullName, Phone, MRN)
        VALUES (@i, @PatientId, @FullName, @Phone, @Mrn);

        SET @i = @i + 1;
    END

    PRINT 'Successfully verified/inserted 100 Patient accounts.';

    -- ================================================================================
    -- 6. Clean and Seed 25 Today's Appointments for Cardiology
    -- ================================================================================
    PRINT '>> Seeding 25 Cardiology Consultation Tokens for Today...';

    DECLARE @TodayUtc DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @TomorrowUtc DATE = DATEADD(DAY, 1, @TodayUtc);

    -- Remove any old test tokens for these 100 patients today to keep clean
    DELETE FROM TriageAssessments 
    WHERE QueueTokenId IN (
        SELECT Id FROM QueueTokens 
        WHERE PatientId IN (SELECT PatientId FROM @CreatedPatientIds) 
          AND BookedAtUtc >= @TodayUtc AND BookedAtUtc < @TomorrowUtc
    );

    DELETE FROM QueueTokens 
    WHERE PatientId IN (SELECT PatientId FROM @CreatedPatientIds)
      AND BookedAtUtc >= @TodayUtc AND BookedAtUtc < @TomorrowUtc;

    -- Update department sequence number
    UPDATE Departments 
    SET LastTokenNumber = 25, LastTokenDate = @TodayUtc 
    WHERE Id = @DeptId;

    DECLARE @tokenSeq INT = 1;
    DECLARE @curPatientId UNIQUEIDENTIFIER;
    DECLARE @tokenNumber NVARCHAR(20);
    DECLARE @tokenId UNIQUEIDENTIFIER;
    DECLARE @status NVARCHAR(20);
    DECLARE @baseWeight INT;
    DECLARE @priorityScore INT;
    DECLARE @triagedAt DATETIME2;
    DECLARE @calledAt DATETIME2;
    DECLARE @completedAt DATETIME2;
    DECLARE @notes NVARCHAR(MAX);

    WHILE @tokenSeq <= 25
    BEGIN
        SELECT @curPatientId = PatientId FROM @CreatedPatientIds WHERE Seq = @tokenSeq;
        SET @tokenNumber = 'CARD-261015-' + RIGHT('000' + CAST(@tokenSeq AS VARCHAR(3)), 3);
        SET @tokenId = NEWID();

        -- Group 1: Token 1 (Called - in room right now)
        IF @tokenSeq = 1
        BEGIN
            SET @status = 'Called';
            SET @baseWeight = 500;
            SET @priorityScore = 520;
            SET @triagedAt = DATEADD(MINUTE, -40, GETUTCDATE());
            SET @calledAt = DATEADD(MINUTE, -10, GETUTCDATE());
            SET @completedAt = NULL;
            SET @notes = 'Chief Complaint: Chronic angina pectoris on exertion.';

            INSERT INTO QueueTokens (Id, TokenNumber, PatientId, DepartmentId, DoctorId, Status, BaseWeight, PriorityScore, BookedAtUtc, TriagedAtUtc, CalledAtUtc, CompletedAtUtc, ConsultationNotes)
            VALUES (@tokenId, @tokenNumber, @curPatientId, @DeptId, @DoctorId, @status, @baseWeight, @priorityScore, DATEADD(MINUTE, -60, GETUTCDATE()), @triagedAt, @calledAt, @completedAt, @notes);

            INSERT INTO TriageAssessments (Id, QueueTokenId, NurseUserId, NursingStationId, SpO2, SystolicBP, DiastolicBP, HeartRate, Temperature, PainScale, TriageLevel, IsManualOverride, OverrideReason, NurseNotes, AssessedAtUtc)
            VALUES (NEWID(), @tokenId, @NurseUserId, @StationId, 93.5, 142, 88, 86, 37.1, 4, 'Yellow', 0, NULL, 'Patient reports mild tightness after climbing stairs.', @triagedAt);
        END
        -- Group 2: Tokens 2-8 (Waiting - already triaged, sitting in Doctor Priority Queue)
        ELSE IF @tokenSeq >= 2 AND @tokenSeq <= 8
        BEGIN
            SET @status = 'Waiting';
            SET @triagedAt = DATEADD(MINUTE, -(45 - @tokenSeq * 3), GETUTCDATE());
            SET @calledAt = NULL;
            SET @completedAt = NULL;

            -- Create variety of CTAS levels:
            -- Token 2: Red Emergency
            IF @tokenSeq = 2
            BEGIN
                SET @baseWeight = 1000;
                SET @priorityScore = 1050;
                SET @notes = 'Severe acute substernal chest pressure with diaphoresis.';

                INSERT INTO QueueTokens (Id, TokenNumber, PatientId, DepartmentId, DoctorId, Status, BaseWeight, PriorityScore, BookedAtUtc, TriagedAtUtc, CalledAtUtc, CompletedAtUtc, ConsultationNotes)
                VALUES (@tokenId, @tokenNumber, @curPatientId, @DeptId, @DoctorId, @status, @baseWeight, @priorityScore, DATEADD(MINUTE, -50, GETUTCDATE()), @triagedAt, @calledAt, @completedAt, @notes);

                INSERT INTO TriageAssessments (Id, QueueTokenId, NurseUserId, NursingStationId, SpO2, SystolicBP, DiastolicBP, HeartRate, Temperature, PainScale, TriageLevel, IsManualOverride, OverrideReason, NurseNotes, AssessedAtUtc)
                VALUES (NEWID(), @tokenId, @NurseUserId, @StationId, 88.0, 75, 50, 155, 37.4, 9, 'Red', 0, NULL, '🚨 Acute myocardial distress detected. Code-Red priority jump.', @triagedAt);
            END
            -- Token 3, 4, 5: Yellow Urgent
            ELSE IF @tokenSeq <= 5
            BEGIN
                SET @baseWeight = 500;
                SET @priorityScore = 500 + (@tokenSeq * 6);
                SET @notes = 'Hypertensive urgency with palpitations.';

                INSERT INTO QueueTokens (Id, TokenNumber, PatientId, DepartmentId, DoctorId, Status, BaseWeight, PriorityScore, BookedAtUtc, TriagedAtUtc, CalledAtUtc, CompletedAtUtc, ConsultationNotes)
                VALUES (@tokenId, @tokenNumber, @curPatientId, @DeptId, @DoctorId, @status, @baseWeight, @priorityScore, DATEADD(MINUTE, -45, GETUTCDATE()), @triagedAt, @calledAt, @completedAt, @notes);

                INSERT INTO TriageAssessments (Id, QueueTokenId, NurseUserId, NursingStationId, SpO2, SystolicBP, DiastolicBP, HeartRate, Temperature, PainScale, TriageLevel, IsManualOverride, OverrideReason, NurseNotes, AssessedAtUtc)
                VALUES (NEWID(), @tokenId, @NurseUserId, @StationId, 92.0, 168, 102, 110, 37.0, 5, 'Yellow', 0, NULL, 'Elevated blood pressure, monitor during consultation.', @triagedAt);
            END
            -- Token 6, 7, 8: Green Routine
            ELSE
            BEGIN
                SET @baseWeight = 100;
                SET @priorityScore = 100 + (@tokenSeq * 4);
                SET @notes = 'Routine 3-month lipid panel review & medication refill.';

                INSERT INTO QueueTokens (Id, TokenNumber, PatientId, DepartmentId, DoctorId, Status, BaseWeight, PriorityScore, BookedAtUtc, TriagedAtUtc, CalledAtUtc, CompletedAtUtc, ConsultationNotes)
                VALUES (@tokenId, @tokenNumber, @curPatientId, @DeptId, @DoctorId, @status, @baseWeight, @priorityScore, DATEADD(MINUTE, -30, GETUTCDATE()), @triagedAt, @calledAt, @completedAt, @notes);

                INSERT INTO TriageAssessments (Id, QueueTokenId, NurseUserId, NursingStationId, SpO2, SystolicBP, DiastolicBP, HeartRate, Temperature, PainScale, TriageLevel, IsManualOverride, OverrideReason, NurseNotes, AssessedAtUtc)
                VALUES (NEWID(), @tokenId, @NurseUserId, @StationId, 99.0, 120, 80, 72, 36.8, 0, 'Green', 0, NULL, 'Vitals stable. Standard outpatient visit.', @triagedAt);
            END
        END
        -- Group 3: Tokens 9-23 (15 Booked tokens - sitting at Nurse Desk awaiting vitals!)
        ELSE IF @tokenSeq >= 9 AND @tokenSeq <= 23
        BEGIN
            SET @status = 'Booked';
            SET @baseWeight = 0;
            SET @priorityScore = 0;
            SET @triagedAt = NULL;
            SET @calledAt = NULL;
            SET @completedAt = NULL;
            SET @notes = 'Online appointment booking for Cardiology consultation.';

            INSERT INTO QueueTokens (Id, TokenNumber, PatientId, DepartmentId, DoctorId, Status, BaseWeight, PriorityScore, BookedAtUtc, TriagedAtUtc, CalledAtUtc, CompletedAtUtc, ConsultationNotes)
            VALUES (@tokenId, @tokenNumber, @curPatientId, @DeptId, @DoctorId, @status, @baseWeight, @priorityScore, DATEADD(MINUTE, -(25 - @tokenSeq), GETUTCDATE()), @triagedAt, @calledAt, @completedAt, @notes);
        END
        -- Group 4: Tokens 24-25 (Completed tokens - for checking past consultation history)
        ELSE
        BEGIN
            SET @status = 'Completed';
            SET @baseWeight = 500;
            SET @priorityScore = 550;
            SET @triagedAt = DATEADD(HOUR, -3, GETUTCDATE());
            SET @calledAt = DATEADD(HOUR, -2, GETUTCDATE());
            SET @completedAt = DATEADD(HOUR, -2, DATEADD(MINUTE, 15, GETUTCDATE()));
            SET @notes = 'Consultation Completed. Prescribed Atorvastatin 20mg & Ecosprin 75mg. Follow-up in 4 weeks.';

            INSERT INTO QueueTokens (Id, TokenNumber, PatientId, DepartmentId, DoctorId, Status, BaseWeight, PriorityScore, BookedAtUtc, TriagedAtUtc, CalledAtUtc, CompletedAtUtc, ConsultationNotes)
            VALUES (@tokenId, @tokenNumber, @curPatientId, @DeptId, @DoctorId, @status, @baseWeight, @priorityScore, DATEADD(HOUR, -4, GETUTCDATE()), @triagedAt, @calledAt, @completedAt, @notes);

            INSERT INTO TriageAssessments (Id, QueueTokenId, NurseUserId, NursingStationId, SpO2, SystolicBP, DiastolicBP, HeartRate, Temperature, PainScale, TriageLevel, IsManualOverride, OverrideReason, NurseNotes, AssessedAtUtc)
            VALUES (NEWID(), @tokenId, @NurseUserId, @StationId, 97.0, 130, 85, 78, 36.9, 2, 'Green', 0, NULL, 'Past visit completed smoothly.', @triagedAt);
        END

        SET @tokenSeq = @tokenSeq + 1;
    END

    PRINT '>> Seeding Completed Successfully! All 25 Cardiology tokens generated.';

    COMMIT TRANSACTION;

    -- Return summary
    SELECT 'SUMMARY: 100 Patients created (Phone: 9800000001 - 9800000100, Pass: 01011995).' AS [Result];
    SELECT 'Cardiology Tokens: 1 Called (CARD-001), 7 Waiting in Doctor Queue (CARD-002 to CARD-008), 15 Awaiting Vitals at Nurse Desk (CARD-009 to CARD-023), 2 Completed (CARD-024 to CARD-025).' AS [TokensSummary];
    SELECT 'Nurse Login: nurse.cardio / Nurse@123 (Locked to Desk A - Cardiology).' AS [NurseLogin];
    SELECT 'Doctor Login: dr.sharma / Doctor@123 (Room 204 - Available).' AS [DoctorLogin];

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'ERROR ENCOUNTERED: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
