# VitalQ — Real-Time Hospital OPD Queue Orchestration & CTAS Triage Platform

[![Status: Production Ready](https://img.shields.io/badge/Status-Production--Ready-success?logo=checkmarx&logoColor=white)](https://github.com/muxdaslam-sys/VitalQ)
[![Version](https://img.shields.io/badge/Version-1.0.0-blue)](https://github.com/muxdaslam-sys/VitalQ)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular 19](https://img.shields.io/badge/Angular-19-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time-blue?logo=signalr&logoColor=white)](https://learn.microsoft.com/aspnet/core/signalr/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-3.4-38B2AC?logo=tailwind-css&logoColor=white)](https://tailwindcss.com/)
[![SQL Server](https://img.shields.io/badge/SQL_Server-Express-CC292B?logo=microsoft-sql-server&logoColor=white)](https://www.microsoft.com/sql-server)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**VitalQ** is a high-concurrency, enterprise-grade outpatient department (OPD) queue orchestration platform designed for modern hospitals and healthcare networks. Architected with a **.NET 8 Web API** and an **Angular 19 Standalone SPA**, VitalQ synchronizes queue positions, physician chamber pacing, and Canadian Triage and Acuity Scale (CTAS) vital signs intake in real time across the entire hospital via WebSockets.

---

## 🏥 Architecture & Symmetrical 1:1 Design Pattern

```
                       ┌─────────────────────────────────────────┐
                       │          SignalR Real-Time Hub          │
                       │             (/hubs/queue)               │
                       └───────────────────▲─────────────────────┘
                                           │ WebSocket Sync
┌──────────────────────────────────────────┴──────────────────────────────────────────┐
│                                 .NET 8 Web API Core                                 │
│                                                                                     │
│  [AdminController]   [DoctorController]   [NurseController]   [PatientController]   │
│         ▲                    ▲                   ▲                    ▲             │
│         │                    │                   │                    │             │
│  [AdminService]      [DoctorService]      [NurseService]      [PatientService]      │
│         │                    │                   │                    │             │
│         ▼                    ▼                   ▼                    ▼             │
│  [AsNoTracking Reads] [Aging Worker (60s)] [CTAS Evaluator]  [Atomic Token Counter] │
└──────────────────────────────────────────┬──────────────────────────────────────────┘
                                           │ Entity Framework Core 8 (RowVersion Concurrency)
                       ┌───────────────────▼─────────────────────┐
                       │   SQL Server Express (B-Tree Indexed)   │
                       └─────────────────────────────────────────┘
```

---

## ✨ Core Features by Clinical Role

### 1. 🩺 Doctor Consultation Console (`/doctor`)
* **Real-Time Priority Queue**: High-urgency (Red/Yellow) patients dynamically prioritized ahead of routine visits using the CTAS algorithm + waiting aging formula (+2 pts/min).
* **Audio Summon & Web Audio Chimes**: One-click "Call Next" triggers harmonic auditory chimes and sends SignalR broadcasts to patient devices and waiting room monitors.
* **Mandatory Consultation Notes & Discharge Guard**: Physicians must record structured clinical advice/prescriptions (5–2000 chars) to finalize consultations; safety guard prevents summoning a new patient while an active consultation is in progress without finalized notes.
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

* **Workstation Auto-Lock on Shutdown**: User credentials and tokens stored exclusively in `sessionStorage`—terminating sessions automatically upon browser close or workstation shutdown.
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

## 🧪 Default Test Credentials

| Role | Username / Mobile | Password | Authorized Portal |
| :--- | :--- | :--- | :--- |
| **System Admin** | `admin` | `Admin@123` | `/admin/dashboard` |
| **Consulting Physician** | `dr.sarah` | `Doctor@123` | `/doctor` |
| **Triage Nurse** | `nurse.emily` | `Nurse@123` | `/nurse` |
| **Registered Patient** | `9876543210` | `Patient@123` | `/patient` |

---

## 🏁 Project State & Production Readiness

- **Release Version**: `v1.0.0 (Production-Ready)`
- **Backend Quality**: 0 Warnings, 0 Errors (`dotnet build api/VitalQ.sln`)
- **Frontend Quality**: 0 Errors (`ng build`, Angular 19 AOT Ahead-Of-Time compilation)
- **Clinical Alignment**: Fully verified CTAS triage algorithms, SLA priority aging, 3-strike absentee enforcement, and mandatory consultation discharge documentation.

---

## 📄 License
This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
