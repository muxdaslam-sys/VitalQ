# VitalQ - Real-Time Hospital OPD Queue Orchestration & CTAS Triage Platform

[![Status: Production Ready](https://img.shields.io/badge/Status-Production--Ready-success?logo=checkmarx&logoColor=white)](https://github.com/muxdaslam-sys/VitalQ)
[![Tests](https://img.shields.io/badge/Tests-76%2F76%20Passing-brightgreen?logo=checkmarx&logoColor=white)](https://github.com/muxdaslam-sys/VitalQ)
[![Version](https://img.shields.io/badge/Version-1.0.0-blue)](https://github.com/muxdaslam-sys/VitalQ)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular 19](https://img.shields.io/badge/Angular-19-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time-blue?logo=signalr&logoColor=white)](https://learn.microsoft.com/aspnet/core/signalr/)
[![Redis](https://img.shields.io/badge/Redis-Distributed%20Cache-DC382D?logo=redis&logoColor=white)](https://redis.io/)
[![Azure Container Apps](https://img.shields.io/badge/Azure-Container%20Apps-0078D4?logo=microsoft-azure&logoColor=white)](https://azure.microsoft.com/)
[![Azure App Insights](https://img.shields.io/badge/Azure-App%20Insights-68217A?logo=microsoft-azure&logoColor=white)](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview)
[![Azure Key Vault](https://img.shields.io/badge/Azure-Key%20Vault-0078D4?logo=microsoft-azure&logoColor=white)](https://azure.microsoft.com/)
[![Managed Identity](https://img.shields.io/badge/Security-Managed%20Identity-green?logo=azure-devops&logoColor=white)](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview)
[![Azure Bicep](https://img.shields.io/badge/Azure-Bicep%20IaC-0078D4?logo=microsoft-azure&logoColor=white)](https://learn.microsoft.com/azure/azure-resource-manager/bicep/overview)
[![CI/CD: GitHub Actions](https://img.shields.io/badge/CI%2FCD-GitHub%20Actions-2088FF?logo=github-actions&logoColor=white)](https://github.com/features/actions)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-3.4-38B2AC?logo=tailwind-css&logoColor=white)](https://tailwindcss.com/)
[![SQL Server](https://img.shields.io/badge/SQL_Server-Express%20%26%20Azure%20SQL-CC292B?logo=microsoft-sql-server&logoColor=white)](https://www.microsoft.com/sql-server)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**VitalQ** is a high-concurrency, enterprise-grade outpatient department (OPD) queue orchestration platform designed for modern hospitals and healthcare networks. Architected with a **.NET 8 Web API** and an **Angular 19 Standalone SPA**, VitalQ synchronizes queue positions, physician chamber pacing, and Canadian Triage and Acuity Scale (CTAS) vital signs intake in real time across the entire hospital via WebSockets with a **Redis Scale-Out Backplane** and **Application Insights Distributed Tracing**.

---

## 🌐 Live Production Cloud Endpoints

* **Live Web Client**: [ca-vitalq-web](https://ca-vitalq-web.politeisland-88851c55.centralindia.azurecontainerapps.io)
* **Live API Service**: [ca-vitalq-api](https://ca-vitalq-api.politeisland-88851c55.centralindia.azurecontainerapps.io)
* **Live Health Check**: [ca-vitalq-api/health/live](https://ca-vitalq-api.politeisland-88851c55.centralindia.azurecontainerapps.io/health/live) (HTTP 200 Healthy)
* **Cloud Infrastructure**: Azure Container Apps (Microservices Environment), Serverless Azure SQL Database (Managed Identity), Redis Distributed Cache, Application Insights APM, Azure Key Vault, Declarative Azure Bicep IaC, and 24/7 Global Availability Monitoring in Pune, India (`centralindia`).

---

## 🏥 Architecture & Symmetrical 1:1 Design Pattern

```
                       ┌─────────────────────────────────────────┐
                       │          SignalR Real-Time Hub          │
                       │             (/hubs/queue)               │
                       └───────────────────▲─────────────────────┘
                                           │ WebSocket Sync (Redis Backplane)
 ┌─────────────────────────────────────────┴─────────────────────────────────────────┐
 │                                .NET 8 Web API Core                                │
 │                                                                                   │
 │ [AdminController]   [DoctorController]   [NurseController]   [PatientController]  │
 │        ▲                    ▲                   ▲                    ▲            │
 │        │                    │                   │                    │            │
 │ [AdminService]      [DoctorService]      [NurseService]      [PatientService]     │
 │        │                    │                   │                    │            │
 │        ▼                    ▼                   ▼                    ▼            │
 │ [L1/L2 Cache]      [Aging Worker (60s)] [CTAS Evaluator]  [Atomic Token Counter]  │
 └────────┬─────────────────────────────────────────────────────────────┬────────────┘
          │ Redis Distributed Cache (ca-vitalq-redis)                   │ EF Core 8
          ▼                                                             ▼
 ┌───────────────────┐                         ┌─────────────────────────────────────┐
 │    Redis Cache    │                         │  Azure SQL Serverless / SQL Express │
 │  (0.01ms - 1.5ms) │                         │  (7 Tables, 23 B-Tree Indexes)      │
 └───────────────────┘                         └─────────────────────────────────────┘
```

---

## ✨ Core Features by Clinical Role

### 1. 🩺 Doctor Consultation Console (`/doctor`)
* **Real-Time Priority Queue**: High-urgency (Red/Yellow) patients dynamically prioritized ahead of routine visits using the CTAS algorithm + waiting aging formula (+2 pts/min).
* **Audio Summon & Web Audio Chimes**: One-click "Call Next" triggers harmonic auditory chimes and sends SignalR broadcasts to patient devices and waiting room monitors.
* **Mandatory Consultation Notes & Discharge Guard**: Physicians must record structured clinical advice/prescriptions (5-2000 chars) to finalize consultations; safety guard prevents summoning a new patient while an active consultation is in progress without finalized notes.
* **Physician Chamber Ownership**: Enforces strict room authorization so physicians can only discharge or skip patients assigned to their own examination room.
* **Longitudinal History Drawer**: Slide-over drawer providing immediate access to historical visits, past clinical notes, vitals taken, and previous doctor diagnoses.
* **3-Strike Absentee Policy**: Skipped/absent patients held on temporary hold with automated countdown timers before token expiration (`Call X of 3`).

### 2. 📋 Nurse Command Center & Triage Desk (`/nurse`)
* **Live CTAS Scoring Engine**: Real-time evaluation dynamically recalculates urgency levels (`Red`, `Yellow`, `Green`) on every vital sign keystroke (SpO2, BP, Heart Rate, Temperature, Pain Scale).
* **Dynamic SLA Aging Sync**: Instant priority re-ranking when the background `AgingWorker` promotes fair-wait waiting patients.
* **Dual Responsive Layout**: Touch-friendly healthcare cards on mobile viewports (<768px) with thumb-sized primary action buttons; high-density tabular view on desktop.
* **Emergency Walk-In & Fast-Track**: Immediate Code-Red bypass for acute emergencies pushes patients directly to Position #1 in the physician's chamber.
* **Same-Day Deduplication**: Enforces strict same-day booking deduplication per patient and doctor, preventing redundant tokens.

### 3. 👤 Patient Self-Service Portal (`/patient`)
* **Live Digital Boarding Pass**: Real-time waiting line visualizer displaying live queue position, estimated wait time, and room assignment across a 3-step visual journey (*Booked* → *Nurse Vitals* → *Doctor Room*).
* **Fair-Wait Anti-Starvation Meter**: Dynamic progress meter illustrating score aging (+2 pts/min) protecting routine Green patients from queue starvation.
* **Family Profile Management**: Primary account holders can manage appointments for dependents and elderly family members under a single login.
* **Past Visit History & Doctor Notes**: Patients can review past encounters, recorded vitals, and attending doctor advice/prescriptions.
* **Instant Online Self-Registration**: New patients register via mobile number and DOB with automatic MRN allocation.

### 4. 📊 Hospital Administration (`/admin`)
* **Real-Time KPI Command Center**: Live metrics for doctor duty availability, total bookings, and clinical station throughput without manual refresh.
* **Staff & Physician Provisioning**: Role-based access control (RBAC) with account activation, password resets, and room assignments.
* **Smart MRN & Directory Search**: Fuzzy search normalizing prefixes (`mrn - 444g`, `0005`, `CARD-001`, or patient names).
* **Master Patient Directory**: Searchable registry showing historical visits and total completed bookings per patient.

### 5. 📺 Public Slip Tracker (`/track/{tokenNumber}`)
* **Anonymous Guest Tracking**: Paper slip holders view real-time queue position and estimated wait time without logging in.
* **Hospital Wayfinding Navigation**: Contextual directions guiding patients to their designated Nursing Triage Desk, Floor, and Doctor Examination Room.
* **Completed Discharge Summary**: Once the doctor concludes the visit, the tracker displays the finalized consultation notes and prescriptions.

---

## 🔒 Security & HIPAA Alignment

* **Workstation Auto-Lock on Shutdown**: User credentials and tokens stored exclusively in `sessionStorage`, terminating sessions automatically upon browser close or workstation shutdown.
* **15-Minute HIPAA Idle Lock**: Native mouse and keyboard activity tracker terminates inactive sessions after 15 minutes of inactivity.
* **Brute-Force Shield**: .NET rate limiter (`10 requests/min/IP`) protects the authentication endpoints, returning HTTP `429 Too Many Requests`.
* **Silent Token Rotation**: Concurrency-safe RxJS mutex (`shareReplay`) deduplicates parallel 401 refresh calls and tags replayed requests with `X-VitalQ-Retry` to prevent infinite loops.
* **Optimistic Concurrency Control**: SQL Server `ROWVERSION` (`byte[] RowVersion`) protects against race conditions when multiple clinical staff interact with the same token simultaneously.

---

## 🚀 Getting Started

### Prerequisites
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [Node.js 20+ & npm](https://nodejs.org/)
* [Microsoft SQL Server](https://www.microsoft.com/sql-server) (LocalDB, Express, or Developer edition)

---

### Backend Setup (.NET 8 Web API)

1. **Clone the repository**:
   ```bash
   git clone https://github.com/muxdaslam-sys/VitalQ.git
   cd VitalQ
   ```

2. **Configure Connection String**:
   Update `api/VitalQ.API/appsettings.json` with your SQL Server connection:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=VitalQ;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
   }
   ```

3. **Restore & Build**:
   ```bash
   dotnet build api/VitalQ.sln
   ```

4. **Run the API**:
   ```bash
   dotnet run --project api/VitalQ.API/VitalQ.API.csproj
   ```
   *API will start on `http://localhost:5089` (Swagger documentation available at `/swagger`).*

---

### Frontend Setup (Angular 19 SPA)

1. **Navigate to the web workspace**:
   ```bash
   cd web
   ```

2. **Install Dependencies**:
   ```bash
   npm install
   ```

3. **Start the Development Server**:
   ```bash
   npm start
   ```
   *Navigate to `http://localhost:4200` in your browser.*

---

### Containerized Setup (Docker Compose)

Run the complete full-stack environment (.NET 8 Web API, Angular 19 SPA, and SQL Server 2022) in isolated containers with a single command:

```bash
docker compose up --build
```

* **Frontend SPA**: `http://localhost:4200`
* **Backend API**: `http://localhost:5089` (Swagger: `http://localhost:5089/swagger`)
* **SQL Server**: `localhost:1433`

---

## 🔐 Clinical Role-Based Portals & Access

VitalQ segments hospital operations by clinical role. User provisioning, role assignment, and credential administration are managed securely by the Hospital Administrator via the Admin Command Center:

| Clinical Role | Access Level | Workstation Portal | Primary Capabilities |
| :--- | :--- | :--- | :--- |
| **Hospital Administrator** | Full Clinical Administration | `/admin/dashboard` | Staff provisioning, physician chamber assignment, KPI analytics, and audit logging. |
| **Consulting Physician** | Doctor Chamber Queue | `/doctor` | Real-time CTAS priority queue, "Call Next" auditory chimes, and mandatory consultation discharge notes. |
| **Triage Nurse** | Station Intake & Triage | `/nurse` | Emergency walk-ins, station queue filtering, and real-time CTAS vital sign evaluations. |
| **Outpatient & Dependents** | Patient Self-Service | `/patient` | Live queue pass, wait time tracking, express appointment booking, and family profiles. |

---

## 🧪 Automated Testing & Concurrency Stress Verification

The solution includes an enterprise test suite of **76 automated tests across the entire stack (100% pass rate)**:
* **63 Backend xUnit & Integration Tests** (`api/VitalQ.Tests/`) covering algorithms, generators, clinical services, controllers, database health checks, high-concurrency stress simulations, and real ASP.NET Core HTTP hosting pipeline integration tests via `WebApplicationFactory`.
* **13 Frontend Jasmine Tests** (`web/src/app/`) covering session management, token decoding, consultation discharge validation, and strike calculations using headless Chrome (`ChromeHeadlessNoSandbox`).

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                               76 FULL-STACK TESTS (100% PASS)                          │
├───────────────────────────────┬────────────────────────────────┬───────────────────────┤
│ BACKEND UNIT & INTEGRATION(63)│ CONCURRENCY STRESS TESTS (4)   │ FRONTEND TESTS (13)   │
│ • AuthIntegrationTests (5)    │ • 30-Worker Race (0 Collision) │ • AuthService (4)     │
│ • RateLimitingTests (1)       │ • 100K SLA Benchmark (<500ms)  │ • CompleteModal (4)   │
│ • DoctorQueueTests (1)        │ • Past-1M Dynamic Scale (1)    │ • SkipModal (5)       │
│ • PublicEndpointsTests (3)    │ • Token Format Integrity (1)   │                       │
│ • MrnGeneratorTests (5)       │                                │                       │
│ • PriorityScoreTests (13)     │                                │                       │
│ • DoctorSafetyTests (9)       │                                │                       │
│ • NurseTriageTests (8)        │                                │                       │
│ • Patient & Public Tests (10) │                                │                       │
│ • DatabaseHealthCheckTest (1) │                                │                       │
│ • AuthServiceTests (3)        │                                │                       │
└───────────────────────────────┴────────────────────────────────┴───────────────────────┘
```

### High-Load Concurrency & Collision-Proofing Highlights
1. **Parallel MRN Registration Race**: 30 concurrent workers simultaneously hitting the shared database with retry loops achieved **100% unique sequence numbers with zero collisions**.
2. **100K-Record High-Throughput SLA Benchmark**: Processed 100,000 synthetic patient tokens in parallel in **< 500ms (~500,000+ evaluations/sec)**, mathematically verifying CTAS priority aging invariants and the inviolable Red Emergency Firewall (scores $\ge 1000$).
3. **Past-1M Dynamic Scaling**: Length-aware B-Tree indexing guarantees dynamic growth beyond 1 Million patients (`MRN-1000000` $\rightarrow$ `MRN-1000001`) comfortably fitting within the `NVARCHAR(30)` database limit.

### Running the Test Suite

```bash
# Run all 63 backend unit, stress, and ASP.NET Core integration tests
dotnet test api/VitalQ.Tests/VitalQ.Tests.csproj

# Run all 13 frontend Jasmine unit tests
cd web && npm test -- --watch=false
```

---

## 🔮 100% Enterprise SDLC Completeness Roadmap

VitalQ is completing a 5-phase Fortune-500 Enterprise SDLC hardening roadmap ($0.00 cloud cost):

| Phase | Engineering Initiative | Scope & Verification Highlights | Status |
| :--- | :--- | :--- | :--- |
| **Phase 19** | **ASP.NET Core Integration Tests** | `WebApplicationFactory<Program>` in-memory HTTP pipeline testing real status codes (200, 401, 403, 429), JWT Bearer auth, and anti-brute-force rate limiting. | **Completed** |
| **Phase 20** | **Automated Code Coverage Gate** | Coverlet XPlat collector (.NET) + Karma Lcov (Angular) in GitHub Actions CI with an 80%+ coverage threshold gating all PRs. | Planned |
| **Phase 21** | **DevSecOps Static Analysis (SAST)** | GitHub CodeQL scanning C# and TypeScript codebases continuously for OWASP Top 10 security vulnerabilities and injection flaws. | Planned |
| **Phase 22** | **Idempotent Zero-Data-Loss DB Evolution** | Decoupled EF Core idempotent SQL migration scripts (`--idempotent`) and guarded seeders (`DbInitializer.cs`) guaranteeing safe schema evolution without data loss. | Planned |
| **Phase 23** | **End-to-End Clinical Journey (Playwright)** | Headless Chromium browser automation simulating multi-role clinical interactions (Patient booking -> Nurse triage -> Doctor call & discharge) over SignalR WebSockets. | Planned |

---

## 🏁 Project State & Production Readiness

- **Release Version**: `v1.0.0 (Production-Ready)`
- **Test Coverage**: 76 Tests Across the Stack (63 .NET + 13 Angular, 100% Pass Rate)
- **Backend Quality**: 0 Warnings, 0 Errors (`dotnet build api/VitalQ.sln`)
- **Frontend Quality**: 0 Errors (`ng build`, Angular 19 AOT Ahead-Of-Time compilation)
- **Health & Monitoring**: Native ASP.NET Core database connectivity and readiness probe (`/health`, `/health/live`).
- **Collision-Proof Engines**: Atomic SQL sequence token generation + sequential collision-proof MRN engine.
- **Clinical Alignment**: Fully verified CTAS triage algorithms, SLA priority aging, 3-strike absentee enforcement, and mandatory consultation discharge documentation.
- **DevOps & Cloud Automation**: Zero-downtime blue/green revisions with emergency auto-rollback guard, declarative Azure Bicep IaC, and continuous 24/7 global availability ping web tests.
- **Security & Privacy**: HIPAA 15m idle auto-lock, native login rate limiting (10 req/min), and crawler shield (`robots.txt`).

---

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
