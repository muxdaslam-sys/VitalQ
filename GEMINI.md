# VitalQ - System Architecture & Context Memory

> **Last Updated**: 2026-11-26  
> **Repository Root**: `d:\Projects\VitalQ`  
> **Monorepo Structure**: `api/` (.NET 8 Web API & Test Suite) & `web/` (Angular 19 SPA)  
> **Backend Status**: 100% Completed, Symmetrical, Hardened & Verified (.NET 8 Web API & 76 xUnit/Integration Tests)  
> **Frontend Status**: 100% Completed, Standalone, OnPush & Verified (Angular 19 & 21 Jasmine Tests)  
> **Database**: Microsoft SQL Server Express (Local) & Azure SQL Serverless `sql-vitalq-prod.database.windows.net` (`sqldb-vitalq-prod` in `centralindia`, Pune)  
> **Cloud Status**: Modules 2, 3, 4, 5, 6, 7 & 8 Completed: Live on Azure Container Apps with Serverless Azure SQL Database (Managed Identity), Redis Distributed Cache, Application Insights APM, Azure Key Vault, Azure Bicep IaC, Azure Monitor Metric Alerting, 24/7 Availability Ping Tests, and Trivy CVE Scanners  
> **Live Web Client**: [ca-vitalq-web](https://ca-vitalq-web.politeisland-88851c55.centralindia.azurecontainerapps.io)  
> **Live API Service**: [ca-vitalq-api](https://ca-vitalq-api.politeisland-88851c55.centralindia.azurecontainerapps.io)  
> **Live Redis Service**: `ca-vitalq-redis` (Internal TCP Port 6379 in `cae-vitalq-prod`)  
> **Live Telemetry**: `appi-vitalq-prod` (Azure Application Insights linked to `log-vitalq-prod`)  
> **Live Key Vault**: `kv-vitalq-prod` (Azure Key Vault in `centralindia`)  
> **Live Health Endpoint**: [ca-vitalq-api/health/live](https://ca-vitalq-api.politeisland-88851c55.centralindia.azurecontainerapps.io/health/live) (HTTP 200 Healthy)  
> **Real-Time Engine**: ASP.NET Core SignalR WebSockets (`/hubs/queue`) with Redis Scale-Out Backplane  
> **Test Coverage**: 97 Unit, Stress & Integration Tests (76 Backend xUnit + 21 Frontend Jasmine, 100% Pass Rate)  
> **Active Branch**: `feat/idempotent-db-migrations`  
> **Upcoming Planned Phase**: Phase 22 Safe Idempotent Database Evolution Completed & Phase 23 Next

---

## 1. Master Documentation & Knowledge Index

This central index provides direct links to all specialized documentation files across the repository:

| Document | File Path | Scope & Primary Purpose |
| :--- | :--- | :--- |
| **Project Master State** | [`PROJECT_STATE.md`](file:///d:/Projects/VitalQ/PROJECT_STATE.md) | Exhaustive historical log of all 11 completed development phases, physical database schema, 23 B-Tree indexes, and detailed architectural audit records. |
| **Public README** | [`README.md`](file:///d:/Projects/VitalQ/README.md) | Public repository overview, badges, clinical role feature summaries, architecture diagrams, and test scorecards. |
| **Frontend Architecture** | [`web/ARCHITECTURE.md`](file:///d:/Projects/VitalQ/web/ARCHITECTURE.md) | Deep technical guide for Angular 19: Signals reactive state, OnPush change detection, Web Audio harmonic chime synthesizer ($C_5 \rightarrow E_5 \rightarrow G_5$), and modal teleportation. |
| **Web Client Guide** | [`web/README.md`](file:///d:/Projects/VitalQ/web/README.md) | Angular developer guide, npm commands, proxy configuration (`proxy.conf.json`), and build targets. |
| **Azure Cloud Roadmap** | [`AZURE_MASTER_ROADMAP.md`](file:///d:/Projects/VitalQ/AZURE_MASTER_ROADMAP.md) | The 6-module cloud roadmap (Docker, Azure Container Apps, Azure SQL, Redis distributed caching, GitHub Actions CI/CD, Application Insights) and Infopark Kochi technical interview questions. |
| **Bicep IaC Guide** | [`infra/README.md`](file:///d:/Projects/VitalQ/infra/README.md) | Declarative Azure Bicep Infrastructure as Code deployment runbook, pre-flight validation, and What-If analysis commands (Internal Runbook). |

---

## 2. Symmetrical 1:1 Architecture (Services ↔ Controllers)

VitalQ strictly enforces a 1:1 symmetrical pattern between domain services and API controllers:

```
BUSINESS LOGIC SERVICES                    API CONTROLLERS
────────────────────────                    ───────────────
1. DoctorService              ──────►       1. DoctorController  (/api/doctor)
2. NurseService               ──────►       2. NurseController   (/api/nurse)
3. PatientService             ──────►       3. PatientController (/api/patient)
4. PublicService              ──────►       4. PublicController  (/api/public)
5. AdminService               ──────►       5. AdminController   (/api/admin)
6. AuthService                ──────►       6. AuthController    (/api/auth)
```

### Solution Directory Structure
```
VitalQ/
├── .github/workflows/ci.yml            # Parallel CI (Backend, Frontend, Gated Docker Builds)
├── .dockerignore                       # Docker build context exclusions
├── docker-compose.yml                  # Root multi-container orchestrator (API + Web + SQL)
├── api/                                # ASP.NET Core 8 Web API Solution
│   ├── VitalQ.sln
│   ├── VitalQ.API/
│   │   ├── Dockerfile                  # Multi-stage .NET 8 chiseled production image (~120MB)
│   │   ├── Controllers/                # Role controllers (Admin, Auth, Doctor, Nurse, Patient, Public)
│   │   ├── Health/                     # DatabaseHealthCheck.cs (/health & /health/live)
│   │   ├── Hubs/                       # QueueHub.cs (SignalR WebSocket hub)
│   │   ├── Services/                   # QueueNotificationService.cs (SignalR broadcaster)
│   │   ├── BackgroundServices/         # AgingWorker.cs (Background priority loop runs every 60s)
│   │   └── Program.cs                  # Pipeline, rate limiting, CORS, pooled DbContext warmup
│   │
│   ├── VitalQ.BusinessLogic/
│   │   ├── Admin/                      # IAdminService.cs & AdminService.cs
│   │   ├── Auth/                       # IAuthService.cs & AuthService.cs
│   │   ├── Common/                     # TokenGenerator, MrnGenerator, PriorityScoreCalculator
│   │   ├── Doctor/                     # IDoctorService.cs & DoctorService.cs
│   │   ├── Notifications/              # IQueueNotificationService.cs
│   │   ├── Nurse/                      # INurseService.cs & NurseService.cs
│   │   ├── Patient/                    # IPatientService.cs & PatientService.cs
│   │   └── Public/                     # IPublicService.cs & PublicService.cs
│   │
│   ├── VitalQ.DataAccess/
│   │   ├── VitalQDbContext.cs          # EF Core DbContext with model configurations
│   │   ├── Models/                     # Entities (User, Patient, QueueToken, NursingStation, etc.)
│   │   └── DTOs/                       # Data Transfer Objects
│   │
│   └── VitalQ.Tests/                   # 50 xUnit Automated Unit & Concurrency Tests
│       ├── Algorithms/                 # MrnGeneratorTests.cs & PriorityScoreCalculatorTests.cs
│       ├── Auth/                       # AuthServiceTests.cs
│       ├── Concurrency/                # ConcurrencyStressTests.cs (Atomic token & MRN stress tests)
│       ├── Doctor/                     # DoctorServiceSafetyTests.cs
│       ├── Health/                     # DatabaseHealthCheckTests.cs
│       ├── Nurse/                      # NurseServiceTriageTests.cs
│       ├── Patient/                    # PatientServiceTests.cs
│       ├── Public/                     # PublicServiceTests.cs
│       └── Helpers/                    # TestDbContextFactory.cs (InMemory EF Core Provider)
│
└── web/                                # Angular 19 SPA (Tailwind CSS 3.4)
    ├── Dockerfile                      # Multi-stage Angular 19 + Nginx Alpine image (<25MB)
    ├── nginx.conf                      # Production Nginx SPA routing & SignalR proxy
    ├── package.json
    ├── angular.json
    ├── karma.conf.js                   # Karma test runner with ChromeHeadlessNoSandbox
    └── src/
        ├── app/
        │   ├── core/                   # Guards, Interceptors, Global Services
        │   ├── shared/                 # Models, Directives, Toast System
        │   └── features/
        │       ├── auth/               # /login - Dual-mode staff & patient login
        │       ├── patient/            # /patient - OPD dashboard, booking wizard, family, history
        │       ├── doctor/             # /doctor - Room queue console & consultation notes
        │       ├── nurse/              # /nurse - Station-filtered intake & vitals triage
        │       └── admin/              # /admin - Multi-page hospital management suite
        └── environments/               # API endpoints & environment configurations
```

---

## 3. Core Business Logic & Clinical Engines

### A. Canadian Triage and Acuity Scale (CTAS) Urgency Rules
- **Red (Resuscitation / Emergency)**: Base Weight = **1000** (Uncapped ceiling).  
  Criteria: SpO2 < 90%, Systolic BP < 80, HR > 150, or Temp > 40.5 C.
- **Yellow (Urgent)**: Base Weight = **500** (Max ceiling = 999).  
  Criteria: SpO2 90-94%, Systolic BP 80-89, HR 121-150 or < 45, Temp 39.0-40.5 C, or Pain >= 7/10.
- **Green (Routine / Non-Urgent)**: Base Weight = **100** (Max ceiling = 499). Default category.
- **Nurse Clinical Override**: The triage nurse can manually set any triage level with a mandatory clinical reason.

### B. Anti-Starvation Priority Aging Loop & 60-Minute Outpatient SLA
- **Class**: `PriorityScoreCalculator.cs` and `AgingWorker.cs`
- Every 60 seconds, `AgingWorker` calls `IDoctorService.RecalculateQueueScoresAsync()`.
- **Dynamic Formula**: `PriorityScore = BaseWeight + (ElapsedWaitingMinutes * 2)`.
- **60-Minute Outpatient Anti-Starvation SLA**: When a Green patient waits >= 60 minutes, their base weight auto-promotes from `100` to `500` (Yellow equivalent). Combined with their accrued waiting timestamp, they immediately jump ahead of newly arrived Yellow patients.
- **Red Emergency Firewall**: Yellow and SLA-escalated Green scores are strictly capped at `999`, guaranteeing that emergency Red patients (base weight `1000`) can never be bypassed under any circumstances.

### C. Collision-Proof Sequential MRN Engine (`MrnGenerator.cs`)
- Generates sequential format: `MRN-000001` (minimum 6 digits).
- Dynamically scales past 1 Million (`MRN-1000000`) without truncating within `NVARCHAR(30)`.
- Length-aware B-Tree indexing with optimistic database collision retry loops (`SaveChangesWithMrnRetryAsync`).
- Shared identically across `PatientService` (self-registration) and `NurseService` (clinic desk intake).

### D. Concurrency-Safe Atomic Token Generator (`TokenGenerator.cs`)
- Direct ADO.NET SQL update executed against the `Departments` table.
- Generates collision-free tokens: `DEPT-YYMMDD-NNN` (e.g. `CARD-261114-001`). Zero memory locks, immune to multi-threaded race conditions.

### E. SignalR Reconnection Resilience (`signalr.service.ts`)
- Client tracks `activeTokenGroupIds = new Set<string>()`.
- Upon network reconnect (`onreconnected`), automatically re-joins all active `patient-{tokenId}` rooms so mobile users never lose live position updates.

### F. Waiting Lounge Emergency Casualty Transfer & Exclusive Nurse Cancellation
- When a patient collapses or requires immediate resuscitation in the outpatient lounge, waiting for the 60-minute automatic expiry loop is clinically unacceptable.
- The Triage Nurse holds exclusive authority to cancel or transfer active tokens (`Booked`, `Waiting`, `Skipped`) with clinical notes (e.g. `"Transferred to Casualty / Emergency Ward"`).
- Doctors operate inside examination rooms on tight consultation blocks and are not burdened with administrative queue management.
- Upon cancellation, SignalR immediately pushes a `QueueUpdated` event to the assigned doctor, automatically removing the patient and sliding the queue forward in real time.

---

## 4. Complete API Route Contract Table

| Controller | Method | HTTP | Modern Route | Payload / Query | Response DTO | Real-Time SignalR Event Pushed |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`Public`** | `GetDepartments` | `GET` | `/api/public/departments` | - | `DepartmentResponse[]` | - |
| **`Public`** | `GetDoctorsByDept` | `GET` | `/api/public/departments/{id}/doctors` | `id: Guid` | `DoctorResponse[]` | - |
| **`Public`** | `TrackToken` | `GET` | `/api/public/track/{tokenNumber}` | `tokenNumber: string` | `QueueTokenResponse` | - |
| **`Auth`** | `Login` | `POST` | `/api/auth/login` | `{ username, password }` | `AuthResponse` | - |
| **`Auth`** | `Refresh` | `POST` | `/api/auth/refresh` | Cookie / RefreshToken | `AuthResponse` | - |
| **`Auth`** | `Logout` | `POST` | `/api/auth/logout` | - | `200 OK` | - |
| **`Patient`** | `SelfRegister` | `POST` | `/api/patient/self-register` | `SelfRegisterRequest` | `AuthResponse` | - |
| **`Patient`** | `GetMyFamily` | `GET` | `/api/patient/family` | JWT claims | `PatientResponse[]` | - |
| **`Patient`** | `AddFamilyMember` | `POST` | `/api/patient/family` | `AddFamilyMemberRequest` | `PatientResponse` | - |
| **`Patient`** | `GetMyVisitHistory`| `GET` | `/api/patient/history` | JWT claims | `PatientVisitHistoryDto[]` | - |
| **`Patient`** | `GetVisitHistory` | `GET` | `/api/patient/history/{id}` | `id: Guid` | `PatientVisitHistoryDto[]` | - |
| **`Patient`** | `BookAppointment` | `POST` | `/api/patient/bookings` | `BookTokenRequest` | `QueueTokenResponse` | `TokenBooked` -> `doctor-{id}`, `nurse-station` |
| **`Patient`** | `GetActiveBookings`| `GET` | `/api/patient/bookings/active` | JWT claims | `QueueTokenResponse[]` | - |
| **`Patient`** | `GetPatientActive` | `GET` | `/api/patient/bookings/active/{id}` | `id: Guid` | `QueueTokenResponse` | - |
| **`Patient`** | `CancelBooking` | `POST` | `/api/patient/bookings/{id}/cancel` | `{ notes?: string }` | `QueueTokenResponse` | `QueueUpdated` -> `doctor-{id}` |
| **`Patient`** | `GetProfile` | `GET` | `/api/patient/profile/{id}` | `id: Guid` | `PatientResponse` | - |
| **`Patient`** | `UpdateProfile` | `PUT` | `/api/patient/profile/{id}` | `UpdatePatientRequest` | `PatientResponse` | - |
| **`Nurse`** | `SearchPatients` | `GET` | `/api/nurse/patients/search` | `?query={search}` | `PatientSearchResult[]` | - |
| **`Nurse`** | `GetPendingTriage`| `GET` | `/api/nurse/triage/pending` | `?departmentId={guid}&nursingStationId={guid}` | `QueueTokenResponse[]` | - |
| **`Nurse`** | `SubmitTriage` | `POST` | `/api/nurse/triage/{id}` | `TriageRequest` | `QueueTokenResponse` | `TriageCompleted` -> `doctor-{id}`, `nurse-station` |
| **`Nurse`** | `GetTriageAssessment`|`GET`| `/api/nurse/triage/{id}` | `id: Guid` | `TriageAssessmentResponse` | - |
| **`Nurse`** | `GetDashboardStats` | `GET` | `/api/nurse/dashboard/stats` | - | `NurseDashboardStatsDto` | - |
| **`Nurse`** | `GetDoctorQueue` | `GET` | `/api/nurse/doctors/{doctorId}/queue` | `doctorId: Guid` | `QueueTokenResponse[]` | - |
| **`Nurse`** | `GetPatientActiveTokens`|`GET`| `/api/nurse/patients/{patientId}/tokens/today` | `patientId: Guid` | `QueueTokenResponse[]` | - |
| **`Nurse`** | `GetPatientDetails`| `GET` | `/api/nurse/patients/{patientId}/details` | `patientId: Guid` | `PatientDetailResponse` | - |
| **`Nurse`** | `GetQueuePosition`| `GET` | `/api/nurse/tokens/{tokenId}/queue-position` | `tokenId: Guid` | `PatientQueuePositionDto` | - |
| **`Nurse`** | `ProcessEmergencyWalkIn`|`POST`| `/api/nurse/tokens/walk-in-emergency` | `EmergencyWalkInBookingRequest` | `QueueTokenResponse` | `TokenBooked` -> `doctor-{id}`, `nurse-station` |
| **`Nurse`** | `BookTokenForExisting`| `POST` | `/api/nurse/patients/{patientId}/book-token` | `ExistingPatientTokenBookingRequest` | `QueueTokenResponse` | `TokenBooked` -> `doctor-{id}`, `nurse-station` |
| **`Nurse`** | `CancelToken` | `POST` | `/api/nurse/tokens/{id}/cancel` | `id: Guid, [FromBody] CancelTokenRequest? { notes?: string }` | `QueueTokenResponse` | `QueueUpdated` -> `doctor-{id}` |
| **`Doctor`** | `GetCurrentDoctor` | `GET` | `/api/doctor/me` | JWT claims | `DoctorResponse` | - |
| **`Doctor`** | `UpdateStatus` | `PUT` | `/api/doctor/status` | `UpdateDoctorStatusRequest` | `DoctorResponse` | `DoctorStatusChanged` -> All clients |
| **`Doctor`** | `GetQueue` | `GET` | `/api/doctor/queue/{id}` | `id: Guid` | `QueueTokenResponse[]` | - |
| **`Doctor`** | `GetCurrentCalled` | `GET` | `/api/doctor/current/{id}` | `id: Guid` | `QueueTokenResponse` | - |
| **`Doctor`** | `CallNext` | `POST` | `/api/doctor/call-next/{id}` | `id: Guid` | `QueueTokenResponse` | `PatientCalled` -> `patient-{id}`, `doctor-{id}` |
| **`Doctor`** | `SkipPatient` | `POST` | `/api/doctor/tokens/{id}/skip` | `id: Guid` | `QueueTokenResponse` | `QueueUpdated` -> `doctor-{id}` |
| **`Doctor`** | `RequeuePatient` | `POST` | `/api/doctor/tokens/{id}/requeue` | `id: Guid` | `QueueTokenResponse` | `QueueUpdated` -> `doctor-{id}` |
| **`Doctor`** | `CompleteConsultation`|`POST`| `/api/doctor/tokens/{id}/complete` | `CompleteConsultationRequest` | `QueueTokenResponse` | `QueueUpdated` -> `doctor-{id}` |
| **`Admin`** | `GetDashboardStats`| `GET` | `/api/admin/dashboard/stats` | - | `AdminDashboardStatsDto` | - |
| **`Admin`** | `GetStaffUsers` | `GET` | `/api/admin/users` | - | `StaffUserResponse[]` | - |
| **`Admin`** | `CreateAdminUser` | `POST` | `/api/admin/users` | `CreateUserRequest` | `StaffUserResponse` | - |
| **`Admin`** | `UpdateUser` | `PUT` | `/api/admin/users/{id}` | `UpdateUserRequest` | `StaffUserResponse` | - |
| **`Admin`** | `GetDoctors` | `GET` | `/api/admin/doctors` | - | `DoctorResponse[]` | - |
| **`Admin`** | `CreateDoctor` | `POST` | `/api/admin/doctors` | `CreateDoctorRequest` | `DoctorResponse` | - |
| **`Admin`** | `ToggleDoctorStatus`| `PATCH`| `/api/admin/doctors/{id}/status` | `UpdateDoctorStatusRequest` | `DoctorResponse` | `DoctorStatusChanged` -> All clients |
| **`Admin`** | `GetPatients` | `GET` | `/api/admin/patients` | `?search={query}` | `PatientDetailResponse[]` | - |
| **`Admin`** | `GetPatientById` | `GET` | `/api/admin/patients/{id}` | `id: Guid` | `PatientDetailResponse` | - |
| **`Health`** | `LivenessProbe` | `GET` | `/health/live` | - | `200 OK` (`Healthy`) | - |
| **`Health`** | `ReadinessProbe` | `GET` | `/health` | - | JSON HealthReport | - |

---

## 5. Strict Engineering & Typography Rules (AI Agent Guardrails)

Whenever modifying or analyzing this codebase, every AI agent must strictly follow these rules:

1. **Zero Em/En Dashes**: Never use em dashes or en dashes anywhere in text, documentation, comments, commit messages, or code. Use standard hyphens `-` or colons `:`.
2. **Preserve 100% Test Pass Rate**: All 97 automated tests (76 backend and 21 frontend) must remain green on every modification. Run tests before declaring any task complete:
   - Backend: `dotnet test api/VitalQ.Tests/VitalQ.Tests.csproj --no-restore`
   - Frontend: `npm --prefix web test -- --watch=false`
3. **Angular 19 Standalone Standard**:
   - Use Standalone Components only (`imports: [...]`). Never create or introduce `NgModule`.
   - Use Angular Signals (`signal()`, `computed()`, `effect()`) for reactive state management.
   - Enforce `ChangeDetectionStrategy.OnPush` on all components.
   - Clean up RxJS subscriptions using `takeUntilDestroyed()`.
4. **Pure SVGs & Zero Emojis**: All UI components must use clean inline SVGs. Zero raw platform emojis (`⚡`, `🚨`, `✓`, `✕`).
5. **Session Security & HIPAA Compliance**:
   - Store user session data in `sessionStorage` (auto-lock on browser shutdown).
   - Use `IdleTimeoutService` to automatically terminate sessions after 15 minutes of inactivity.
   - Use HttpOnly cookies for refresh tokens.
6. **Clickable Links**: Reference all files with the `file:///` URI scheme using forward slashes (e.g. `[Program.cs](file:///d:/Projects/VitalQ/api/VitalQ.API/Program.cs)`).
7. **Cloud & Containerization Scope**: Multi-stage Dockerfiles, Nginx configuration, Docker Compose, and GitHub Actions CI test/build workflows are implemented. Cloud hosting (Azure Container Apps, Azure SQL, Redis, ACR) is documented in [`AZURE_MASTER_ROADMAP.md`](file:///d:/Projects/VitalQ/AZURE_MASTER_ROADMAP.md) as the next live deployment phase.
8. **Explain First & Await User Review**: Always provide a detailed explanation of proposed code or architecture changes first and allow the user to review before proceeding with execution.
9. **Automatic Documentation Updates on Completion**: Automatically update master documentation and memory files (`GEMINI.md`, `PROJECT_STATE.md`, `README.md`) as soon as any task or phase completes without requiring separate user prompting.

---

## 6. DevOps & Cloud CI/CD Mastery (Phase 11 & Phase 12 Deployments)

### A. Phase 11 Achievements: Multi-Stage Containers & Parallel CI
1. **Multi-Stage Container Architecture**:
   - **Backend**: Built .NET 8 SDK build stage and runtime packaging on `mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled`. Image size reduced to ~120MB with non-root security compliance.
   - **Frontend**: Built Node.js 20 Alpine stage with production `ng build` and lightweight `nginx:alpine` runtime (<25MB) with custom SPA fallback routing and WebSocket reverse proxying for SignalR.
   - **Local Orchestration**: Authored `docker-compose.yml` linking SQL Server 2022 (`vitalq-db`), Web API (`vitalq-api`), and Angular SPA (`vitalq-web`) over an isolated bridge network (`vitalq-network`).
2. **Industry-Standard Parallel CI Pipelines**:
   - Converted sequential scripts into a 3-job parallel pipeline in `.github/workflows/ci.yml`:
     - `Backend Build (.NET 8)`: Restores, compiles, and runs 51 xUnit tests.
     - `Frontend Build (Angular 19)`: Installs dependencies, runs 13 headless Jasmine tests with Chrome, and builds production bundle.
     - `Verify Docker Container Builds`: Gated on both test jobs passing via `needs: [backend-ci, frontend-ci]`. Avoids building images when unit tests fail.

### B. Phase 12 Achievements: Azure Cloud Infrastructure & Container Apps (Module 2)
1. **Cloud Resource Group & Governance**:
   - Provisioned `rg-vitalq-prod-centralindia` in Pune (`centralindia`) on Pay-As-You-Go subscription.
   - Configured $10.00/month budget guardrail with proactive alert thresholds (50%, 75%, 90%, 100%).
2. **Azure Container Registry (ACR)**:
   - Provisioned private registry `crvitalqprod.azurecr.io` (Basic SKU).
   - Built production container images in the cloud via ACR Tasks (`az acr build`) without local Docker daemon dependencies.
3. **Azure Container Apps Serverless Microservices**:
   - Provisioned managed Container Apps Environment `cae-vitalq-prod` backed by Log Analytics (`log-vitalq-prod`).
   - Deployed Backend API (`ca-vitalq-api`): Running on .NET 8 chiseled runtime with HTTP 200 liveness probes.
   - Deployed Frontend SPA (`ca-vitalq-web`): Running on Nginx Alpine with dynamic DNS resolution via Azure recursive resolver (`168.63.129.16`) and runtime environment variable substitution (`BACKEND_URL`).
   - Reverse-proxied TLS traffic with SNI pass-through (`proxy_ssl_server_name on`). Verified live at `https://ca-vitalq-web.politeisland-88851c55.centralindia.azurecontainerapps.io`.

### C. Phase 13 Achievements: Azure SQL Database & Cloud Persistence (Module 3)
1. **Azure SQL Server & Serverless Database**:
   - Provisioned logical Azure SQL Server `sql-vitalq-prod.database.windows.net` in `centralindia` (Pune).
   - Deployed serverless database `sqldb-vitalq-prod` with 60-minute auto-pause and Azure monthly free-tier limits.
2. **Cloud Security & Firewall**:
   - Configured firewall rule `AllowAllWindowsAzureIps` (`0.0.0.0`) permitting Container Apps communication over port 1433 with TLS encryption.
   - Whitelisted client developer IP for administrative migration tasks.
   - Encrypted connection string stored in Azure Container Apps Secret Store (`sql-connection-string`).
3. **Full Data & Schema Migration**:
   - Exported and migrated all 7 tables and 23 B-Tree performance indexes via `vitalq_cloud_seed.sql`.
   - Imported all 392 active clinical records (8 departments, 8 nursing stations, 121 users, 16 doctors, 103 patients, 90 tokens, 46 triage assessments) into Azure SQL.
4. **Runtime ICU Globalization Hardening**:
   - Upgraded .NET 8 runtime to `mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled-extra` to supply ICU globalization libraries required by SQL Server collation.
   - Built and deployed via ACR Tasks (`cu5`). Verified live `/health` returning HTTP 200 `Healthy` with ~90ms SQL roundtrip latency.

### D. Phase 14 Achievements: Module 4 Redis Distributed Caching & Scale-Out
1. **Serverless Redis Deployment**:
   - Deployed internal Container App `ca-vitalq-redis` on TCP port 6379 in `cae-vitalq-prod`.
   - Zero-cost architecture sharing existing Container Apps Environment quota.
2. **Multi-Tier L1/L2 Caching Engine**:
   - Implemented in `PublicService.cs`: L1 Memory (sub-millisecond) + L2 Redis (5-min shared cache across all replicas) + L3 Azure SQL fallback.
   - Implemented targeted cache invalidation in `AdminService.cs` and `DoctorService.cs`.
3. **SignalR WebSocket Redis Backplane**:
   - Configured `AddStackExchangeRedis` on SignalR builder with channel prefix `VitalQ_SignalR`.
   - Broadcasts real-time events (`PatientCalled`, `QueueUpdated`, `TokenBooked`) across multi-replica container instances.
4. **Cloud API Revision Deployment**:
   - Built and pushed `vitalq-api:redis-v1` to ACR (`crvitalqprod.azurecr.io`).
   - Deployed revision `ca-vitalq-api--0000005` with `ConnectionStrings__RedisConnection=ca-vitalq-redis:6379`.
   - Verified live `/health` returning HTTP 200 `Healthy`.
5. **Test Coverage**:
   - Added 2 new unit tests in `PublicServiceTests.cs`.
   - Total test suite at 66 tests (53 backend + 13 frontend, 100% pass rate).

### E. Phase 15 Achievements: Module 6 Zero-Secret Architecture & EF Core Migrations
1. **Azure Key Vault & Managed Identity Integration**:
   - Provisioned Azure Key Vault `kv-vitalq-prod` in `centralindia` (Pune).
   - Configured System-Assigned Managed Identity on `ca-vitalq-api`.
   - Stored `JwtKey` in Key Vault; linked via Container Apps Secret Store (`keyvaultref:https://kv-vitalq-prod.vault.azure.net/secrets/JwtKey,identityref:system`).
   - Configured Passwordless Azure SQL connection string with Active Directory Default authentication.
2. **Automated In-App EF Core Migrations**:
   - Baselined initial database schema with `20261121100154_InitialBaseline`.
   - Configured in-app startup database migration in `Program.cs` (`db.Database.MigrateAsync()`), executing with native Managed Identity privileges inside Azure Container Apps.
3. **Repository Security Sanitization**:
   - Removed cleartext credentials from public `README.md` and added Role-Based Access Control matrix.
   - Redacted internal Azure resource identifiers from public documentation.
   - Sanitized local PC hostnames and fallback signing secrets from `appsettings.json` and HTTP test files.
   - Verified 100% test pass rate across all 66 automated tests.

### F. Phase 16 Achievements: Module 7 Declarative Infrastructure as Code (Azure Bicep)
1. **Modular Infrastructure as Code Architecture**:
   - Authored master deployment orchestrator [`infra/main.bicep`](file:///d:/Projects/VitalQ/infra/main.bicep).
   - Authored specialized Bicep modules in `infra/modules/`:
     - `logAnalytics.bicep`: Log Analytics Workspace (`log-vitalq-prod`).
     - `appInsights.bicep`: Application Insights APM component (`appi-vitalq-prod`).
     - `keyVault.bicep`: Azure Key Vault with soft-delete & RBAC permissions (`kv-vitalq-prod`).
     - `sqlServer.bicep`: Azure SQL Server and Serverless Database (`sqldb-vitalq-prod`).
     - `containerApps.bicep`: Managed Environment (`cae-vitalq-prod`), Redis, Web client, and .NET API with System-Assigned Managed Identity.
   - Parameterized configurations cleanly in [`infra/main.parameters.json`](file:///d:/Projects/VitalQ/infra/main.parameters.json).
2. **Compiler & Linter Verification**:
   - Compiled with Azure Bicep CLI (`az bicep build --file infra/main.bicep`).
   - Achieved 100% clean compilation with 0 errors and 0 linter warnings.
3. **Deployment Runbook**:
   - Authored comprehensive documentation in [`infra/README.md`](file:///d:/Projects/VitalQ/infra/README.md) for template pre-flight validation, What-If simulation, and group deployments.

### G. Phase 17 Achievements: Module 8 Production Metric Alerting & Action Groups
1. **Automated Azure Monitor Action Group**:
   - Authored [`infra/modules/alerts.bicep`](file:///d:/Projects/VitalQ/infra/modules/alerts.bicep) provisioning `ag-vitalq-prod-devops`.
   - Parameterized email notifications with Common Alert Schema enabled.
2. **Four Critical Metric Alert Rules**:
   - **HTTP 5xx Server Error Spike (`alert-vitalq-api-5xx-errors`)**: Severity 1 error triggering on > 5 failed requests in 5 minutes via Application Insights.
   - **Proactive Memory Warning (`alert-vitalq-api-high-memory`)**: Severity 2 warning triggering when API container working set exceeds 800 MiB (80% ceiling) before OOM termination.
   - **Container CPU Surge (`alert-vitalq-api-high-cpu`)**: Severity 2 warning triggering when API CPU exceeds 80% allocation for 5 minutes.
   - **Azure SQL Serverless Compute Alert (`alert-vitalq-sql-high-compute`)**: Severity 2 warning triggering when database CPU exceeds 85% for 10 minutes.
3. **Master Orchestrator Integration**:
   - Integrated `alerts.bicep` directly into [`infra/main.bicep`](file:///d:/Projects/VitalQ/infra/main.bicep).
   - Validated entire 7-module Bicep suite with zero compilation errors and zero warnings.

### H. Phase 18 Achievements: Enterprise Production Workflow & Security Auditing
1. **Automated Bicep Infrastructure Deployment in CD**:
   - Integrated `az deployment group create` directly into [`.github/workflows/cd.yml`](file:///d:/Projects/VitalQ/.github/workflows/cd.yml).
   - Added parallel Bicep syntax validation gate in [`.github/workflows/ci.yml`](file:///d:/Projects/VitalQ/.github/workflows/ci.yml) (`bicep-ci`), eliminating manual infrastructure deployments.
2. **Zero-Downtime Blue/Green Revisions with Automated Rollback**:
   - Deploys container updates using unique Git commit revision suffixes (`${{ github.sha }}`).
   - Automated synthetic smoke test probe verifies endpoint health before traffic shift.
   - Integrated emergency auto-rollback guard that reverts 100% traffic to previous healthy revision if any probe fails, guaranteeing zero patient downtime.
3. **Container Vulnerability & CVE Scanning (Aquasec Trivy)**:
   - Added automated Trivy vulnerability scanner in `ci.yml` scanning both .NET 8 Chiseled API and Angular 19 Nginx Alpine images for High/Critical CVEs before registry push.
4. **24/7 Automated Availability Ping Web Tests**:
   - Authored `Microsoft.Insights/webtests` in [`infra/modules/appInsights.bicep`](file:///d:/Projects/VitalQ/infra/modules/appInsights.bicep) pinging web client every 5 minutes from Singapore, Dublin, and Virginia.
5. **HIPAA & SOC2 Diagnostic Audit Settings**:
   - Added `Microsoft.Insights/diagnosticSettings` in [`infra/modules/keyVault.bicep`](file:///d:/Projects/VitalQ/infra/modules/keyVault.bicep) streaming `AuditEvent` and `AllMetrics` continuously to Log Analytics.

### I. The 100% Complete Enterprise SDLC Roadmap & Architecture Blueprints

To achieve 100% Fortune-500 Enterprise SDLC completeness for VitalQ without introducing cloud infrastructure costs ($0.00 extra cost), the following 5 engineering phases define the complete development roadmap:

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                             100% ENTERPRISE SDLC COMPLETENESS ROADMAP                            │
├───────────────────────┬─────────────────────────────────┬────────────────────────────────────────┤
│ Phase                 │ Scope & Focus                   │ Tooling & Safety Guarantees            │
├───────────────────────┼─────────────────────────────────┼────────────────────────────────────────┤
│ Phase 19 (Current)    │ API Integration Test Suite      │ WebApplicationFactory, Real HTTP 429   │
│ Phase 20              │ Automated Code Coverage Gate    │ Coverlet, Karma Lcov, 80%+ CI Threshold│
│ Phase 21              │ DevSecOps Static Analysis       │ GitHub CodeQL SAST (OWASP Top 10)      │
│ Phase 22              │ Idempotent Zero-Data-Loss DB    │ EF Core Idempotent SQL, Schema Safety  │
│ Phase 23              │ End-to-End Clinical Journey     │ Microsoft Playwright, WebSockets E2E   │
└───────────────────────┴─────────────────────────────────┴────────────────────────────────────────┘
```

#### 1. Phase 19: ASP.NET Core Integration Testing (WebApplicationFactory<Program>)
* **Status**: Completed on feature branch `feat/api-integration-tests` (63/63 backend and 13/13 frontend passing, 100% pass rate).
* **Objective**: Move beyond unit tests with mocked services to verify the real ASP.NET Core HTTP hosting pipeline, routing table, dependency injection container, JWT authentication handler, role authorization policies, and anti-brute-force rate limiting middleware.
* **Component Implementation**:
  - `VitalQWebApplicationFactory.cs`: Custom factory inheriting from `WebApplicationFactory<Program>`. Replaces SQL Server DbContextPool with an isolated in-memory database per test run (`UseInMemoryDatabase`), configures test environments, and automatically seeds realistic hospital test data (Cardiology department, Admin user, Doctor entity and user, Nurse user, Patient entity and user, and active QueueToken). Provides helper `CreateAuthenticatedClientAsync(username, password)`.
  - `AuthIntegrationTests.cs`:
    - `POST /api/auth/login` with valid credentials returns HTTP 200 OK with valid JWT bearer token and role claim.
    - `POST /api/auth/login` with invalid password returns HTTP 401 Unauthorized.
    - Unauthenticated access to `GET /api/admin/users` returns HTTP 401 Unauthorized.
    - Role authorization check: Nurse JWT accessing `GET /api/admin/users` returns HTTP 403 Forbidden.
    - Doctor role check: Doctor JWT accessing `GET /api/doctor/me` returns HTTP 200 OK with doctor profile.
  - `RateLimitingIntegrationTests.cs`:
    - Simulates rapid login requests to `POST /api/auth/login` using client IP header `X-Forwarded-For`.
    - First 10 requests within 1 minute succeed/reach controller (PermitLimit = 10).
    - Request #11 is throttled by ASP.NET Core rate limiting middleware with HTTP 429 Too Many Requests.
    - Requests from an alternate IP header are not throttled, verifying IP partition isolation.
  - `DoctorQueueIntegrationTests.cs`:
    - Doctor calls `POST /api/doctor/call-next/{doctorId}`: summons waiting patient, returns HTTP 200 OK, transitions token status to "Called".
    - Clinical safety guard: `POST /api/doctor/tokens/{id}/complete` with consultation notes under 5 characters returns HTTP 400 Bad Request.
    - Consultation conclusion: `POST /api/doctor/tokens/{id}/complete` with valid clinical advice returns HTTP 200 OK, marks token "Completed", and timestamps CompletedAtUtc.
  - `PublicEndpointsIntegrationTests.cs`:
    - Probes `/health/live` liveness endpoint, asserting HTTP 200 OK.
    - Probes `/health` readiness endpoint, asserting HTTP 200 OK with database connection health check report.
    - Probes `GET /api/public/departments`, asserting HTTP 200 OK with active department list.
* **Cost**: $0.00 (Executes completely in-memory during build/test).

#### 2. Phase 20: Automated Code Coverage Quality Gate (Coverlet + Lcov + ReportGenerator)
* **Status**: Completed on feature branch `feat/code-coverage-gate`.
* **Objective**: Establish automated line and branch coverage gates in the GitHub Actions CI pipeline, preventing any code regression or untested business logic from merging into `main`.
* **Technical Blueprint**:
  - **Backend Coverage (.NET 8)**:
    - Utilize `coverlet.collector` (already in `VitalQ.Tests.csproj`) with XPlat Code Coverage format.
    - Command: `dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage`.
    - Enforce line coverage and branch coverage thresholds (80%+ target) across `VitalQ.BusinessLogic` and `VitalQ.API`.
  - **Frontend Coverage (Angular 19)**:
    - Configure Karma (`karma.conf.js`) with `karma-coverage` reporter producing `lcov.info`.
    - Command: `npm test -- --watch=false --code-coverage`.
  - **Unified CI Reporting**:
    - Add GitHub Actions step using `danielpalme/ReportGenerator-GitHub-Action` to generate a unified markdown coverage summary.
    - Post coverage delta scorecard directly as a PR comment.
    - Pipeline fails if overall test coverage drops below 80%.
* **Cost**: $0.00 (Standard GitHub Actions compute).

#### 3. Phase 21: DevSecOps Static Code Analysis (GitHub CodeQL SAST)
* **Status**: Completed on feature branch `feat/devsecops-codeql`.
* **Objective**: Automate continuous application security testing to detect and remediate vulnerabilities, injection flaws, and security weaknesses before deployment to Azure.
* **Technical Blueprint**:
  - **Workflow**: Authored [`.github/workflows/codeql.yml`](file:///d:/Projects/VitalQ/.github/workflows/codeql.yml).
  - **Languages Analyzed**: `csharp` (.NET 8 Web API) and `javascript-typescript` (Angular 19 SPA).
  - **Scanning Scope**:
    - OWASP Top 10 vulnerabilities (SQL Injection, Cross-Site Scripting, Broken Authentication, Sensitive Data Exposure).
    - CWE/SANS Top 25 most dangerous software errors.
    - Cryptographic key strength and JWT signing verification.
    - Mass assignment and authorization bypass detection.
  - **Quality Gate**: Blocks pull requests if any High or Critical severity security alert is detected.
* **Cost**: $0.00 (Included free for all public GitHub repositories).

#### 4. Phase 22: Safe Idempotent Database Evolution & Zero Data Loss Migration Strategy
* **Status**: Completed on feature branch `feat/idempotent-db-migrations`.
* **Objective**: Guarantee that all future schema updates (e.g. adding new tables like `Pharmacy`, adding columns, indexes, or modifying relationships) execute in Azure SQL automatically without deleting, truncating, or corrupting existing live clinical data.
* **Database Evolution Principles & Guarantees**:
  - **Idempotent Migration Scripts**:
    - Generate idempotent SQL scripts using `dotnet ef migrations script --idempotent --output migrations.sql`.
    - In SQL Server, every schema statement is wrapped in an existence check against the `[__EFMigrationsHistory]` table:
      ```sql
      IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261201_AddPharmacyTable')
      BEGIN
          CREATE TABLE [dbo].[PharmacyPrescriptions] (...);
          INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261201_AddPharmacyTable', '8.0.10');
      END;
      ```
    - Running an idempotent script 1 time or 100 times produces identical safe results. Existing patient records, active queue tokens, and doctor rosters are completely untouched.
  - **Safe Seed Data Separation (`DbInitializer.cs`)**:
    - Seed data must never run destructive commands (`DROP`, `TRUNCATE`, or blind `INSERT`).
    - Every seed operation is guarded by existence checks (`if (!await context.Departments.AnyAsync())`).
  - **Fail-Fast CI/CD Pipeline Stop-Gates**:
    - Pinned `dotnet-ef` to `--version 8.0.10` in CI to eliminate .NET 10 runtime mismatches.
    - Removed error masking (`|| true`) across all verification steps.
    - Gated `coverage-gate` on all CI jobs: `needs: [backend-ci, frontend-ci, docker-ci, bicep-ci]`.
    - Added `verify-build-and-tests` gate in `cd.yml` so production Azure Container Apps deployment stops immediately if any test or migration fails.
  - **Enterprise Code Quality & Security Hardening**:
    - Authored root [`.editorconfig`](file:///d:/Projects/VitalQ/.editorconfig) enforcing UTF-8 without BOM, LF line endings, 4-space C# indent, and 2-space web indent.
    - Automated Supply-Chain Dependency Patching ([`.github/dependabot.yml`](file:///d:/Projects/VitalQ/.github/dependabot.yml)): Daily scans and weekly automated patch PRs across NuGet, npm, and GitHub Actions.
    - Automated Secret Leak Prevention: Configured Gitleaks scanning in CI with [`.gitleaks.toml`](file:///d:/Projects/VitalQ/.gitleaks.toml) allowlisting test fixtures.
    - Two-Tier Native Rate Limiting ([`Program.cs`](file:///d:/Projects/VitalQ/api/VitalQ.API/Program.cs)): Tier 1 Global sliding-window shield (100 req/min/IP) + Tier 2 Login shield (10 req/min/IP) with health check bypass.
    - Multi-Replica Startup Race Defense ([`DbInitializer.cs`](file:///d:/Projects/VitalQ/api/VitalQ.DataAccess/DbInitializer.cs)): Optimistic concurrency with `DbUpdateException` absorption preventing container crash-loops.
    - Zero Compiler Warning Quality Gate: Enforced `--warnaserror` across .NET solution builds in CI/CD pipelines.
    - Production Nginx HTTP Security Headers ([`web/nginx.conf`](file:///d:/Projects/VitalQ/web/nginx.conf)): Injected `Content-Security-Policy` (CSP), `X-Frame-Options: SAMEORIGIN`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `Permissions-Policy`, and `Strict-Transport-Security` (HSTS).
    - Enforced 7 required GitHub Actions status checks, linear history, conversation resolution, and administrator compliance on `main`.
* **Cost**: $0.00 (Native EF Core, GitHub Actions, and Azure capability).

#### 5. Phase 23: End-to-End Clinical Journey Testing (Microsoft Playwright)
* **Status**: Planned Phase 23.
* **Objective**: Validate the complete real-time clinical workflow across multiple distinct browser sessions (Patient, Nurse, Doctor) interacting concurrently over SignalR WebSockets.
* **Technical Blueprint**:
  - **Tooling**: Microsoft Playwright (`@playwright/test`) running in GitHub Actions with headless Chromium.
  - **Clinical Scenario Flow**:
    1. **Context 1 (Patient Mobile)**: Patient logs in, selects Cardiology, and books an appointment. Receives token `CARD-001` in "Booked" status.
    2. **Context 2 (Nurse Desktop)**: Triage workstation receives real-time `TokenBooked` SignalR event. Nurse enters vital signs (SpO2 92%, BP 135/88, HR 98). Dynamic CTAS engine calculates Yellow urgency. Token promotes to "Waiting".
    3. **Context 3 (Doctor PC)**: Doctor console shows patient at top of queue. Doctor clicks "Call Next". Web Audio melodic chime plays ($C_5 \rightarrow E_5 \rightarrow G_5$). Token transitions to "Called".
    4. **Context 1 Verification**: Patient mobile screen updates in real time to "Called into Room 201".
    5. **Context 3 Finalization**: Doctor attempts to call next patient without notes (blocked by safety guard), enters clinical advice ("Normal sinus rhythm. Follow-up in 3 months."), and completes consultation.
    6. **System Verification**: Token transitions to "Completed", queue slides forward for subsequent patients.
  - **CI Integration**: Executes as an automated end-to-end verification gate in GitHub Actions.
* **Cost**: $0.00 (Headless Chromium on GitHub-hosted runners).
