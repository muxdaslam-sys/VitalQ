# VitalQ Web - Frontend Architecture & Developer Guide

Welcome to the **VitalQ Web** frontend (`vitalq-web`). This guide enables any software engineer to understand the architecture, data flows, and design conventions within **2 minutes**.

---

## 1. High-Level Architecture Overview

VitalQ Web is built using **Angular 19 Standalone Components**, **Tailwind CSS**, and **Angular Signals**. It consumes the ASP.NET Core 8 Web API via REST and real-time WebSockets via **Microsoft SignalR**.

```
vitalq-web/src/app/
│
├── core/                  # SINGLETON INFRASTRUCTURE (Always eager, imported once)
│   ├── guards/            # Route security (authGuard with role checking)
│   ├── interceptors/      # HTTP middleware (jwtInterceptor with token refresh)
│   └── services/          # Singleton business logic (Auth, Booking, Patient, SignalR, Toast)
│
├── shared/                # REUSABLE CONTRACTS & DUMB COMPONENTS
│   ├── models/            # 1:1 Mirror of C# Backend DTOs & Domain Contracts
│   └── components/        # Presentational UI (e.g., ToastContainerComponent)
│
└── features/              # DOMAIN MODULES (Role-based features)
    ├── auth/              # Login & authentication screen
    ├── admin/             # Hospital operations management (Doctors, Staff, Departments, etc.)
    ├── patient/           # All-in-One Patient Smart Hub (Tokens, Live Wait, Express Booking, Family)
    ├── doctor/            # Doctor Consultation Console (Hold)
    └── nurse/             # Nurse Triage Station (Hold)
```

---

## 2. Directory & Coding Standards

### A. The Core Layer (`src/app/core/`)
- Contains **only singleton services**, guards, and interceptors.
- Exported via barrel `src/app/core/index.ts` (aliased as `@core`).
- **Never** imports from `features/`.

### B. The Shared Layer (`src/app/shared/`)
- **Models (`@shared/models`)**: Defines all TypeScript interfaces representing backend DTOs.
  - `admin.model.ts`: Admin management responses & requests.
  - `auth.model.ts`: Login requests, tokens, and user credentials.
  - `queue.model.ts`: `QueueToken`, `Doctor`, `Department`, booking requests.
  - `patient.model.ts`: `PatientProfile`, family members, visit history.
  - `triage.model.ts`: `TriageRequest`, vitals assessment, search results.
  - Exported cleanly through `index.ts`.
- **Components (`@shared/components`)**: Stateless, presentation-only components.

### C. The Feature Layer (`src/app/features/`)
- Grouped strictly by **Hospital Role**:
  - `/admin`: Master Admin Layout + operational tabs.
  - `/patient`: Patient Layout + **All-in-One Patient Smart Hub**.
  - `/doctor`: Clinical doctor consultation.
  - `/nurse`: Nurse vital signs triage.

---

## 3. The All-in-One Patient Smart Hub Pattern

Hospital patients on mobile Wi-Fi suffer from cognitive overload if forced to navigate across multiple pages to book a slot, check a token, manage a child profile, or view history.

VitalQ solves this with the **Single-Screen Smart Hub** located at `src/app/features/patient/patient-dashboard/`:

1. **Hero Active Pass**: Apple Wallet / Boarding Pass card showing live token number, dynamic "Patients Ahead" countdown, estimated wait minutes, chamber room number, and CTAS urgency badge.
2. **Multi-Slip Switcher**: Patients managing tokens for multiple family members can switch slips with a single tap.
3. **Express Booking Drawer**: Slide-over 3-step drawer (Person $\rightarrow$ Department $\rightarrow$ Doctor $\rightarrow$ Confirm) that creates a slip instantly with **zero page hops**.
4. **Family Directory Drawer**: Slide-over drawer to register dependents and view profiles inline.
5. **Consultation Records**: Expandable accordion displaying past doctor notes and prescriptions.
6. **Smart Route Synchronization**:
   - `/patient/dashboard`: Normal view.
   - `/patient/book` or `?action=book`: Opens the Express Booking drawer.
   - `/patient/family` or `?action=family`: Opens the Family Directory drawer.
   - `/patient/history` or `?action=history`: Opens Consultation Records.

### Section-by-Section UI & Clinical Guide

| Section | UI Purpose | Clinical / Hospital Logic |
| :--- | :--- | :--- |
| **Top Hub Header** | Welcome banner with live WebSocket connectivity badge | Reassures patient that their connection to the hospital server is active and updating live in real-time. |
| **Multi-Slip Switcher** | Horizontal chips of today's tokens | Allows parents or guardians to manage 2–4 family members' queues from one device with single-tap switching. |
| **Active Pass Header** | Hospital department, room number, token number, status | Acts as the official digital sequence slip to be shown at reception and doctor chambers. |
| **Stage 1 (`Booked`)** | Warning banner directing patient to Nursing Desk | Enforces hospital policy that vitals (BP, SpO2, HR, Temp) must be evaluated before queue position is activated. |
| **Stage 2 (`Waiting`)** | "Patients Ahead" & "Estimated Wait" live counters + CTAS tier | Dynamic estimate based on doctor's consultation pace. Explains anti-starvation aging (score increases every minute). |
| **Stage 3 (`Called`)** | Full-screen glowing green summon alert | Doctor clicked "Call Next". Native Web Audio 3-tone chime plays. Directs patient to chamber room immediately. |
| **Stage 4 (`Skipped`)** | Soft rose alert with instructions on re-entry | Number called 3 times without answer. Explains that token is preserved on hold and how to notify nurse to re-queue. |
| **Quick Actions** | 2 large prominent buttons for Booking & Family | Express pathways with clear subtitles eliminating cognitive ambiguity. |
| **Consultation Records** | History cards with doctor advice and past triage levels | Permanent record of past diagnoses and treatment advice written by the attending physician upon completion. |
| **Express Booking Drawer** | 3-step slide-over (Patient $\rightarrow$ Specialty $\rightarrow$ Specialist) | Issues a new token with zero full-page reloads. Only on-duty physicians are selectable. |
| **Family Drawer** | Directory + add dependent form | All profiles share the primary login phone number while maintaining distinct, confidential Medical Record Numbers (MRN). |
| **Cancel Modal** | Confirmation popup with reason input | Clearly states cancellation policy (token released to waiting patients, non-restorable) before submission. |

---

## 4. State Management: Signals + Real-Time SignalR

### Zero-Polling Philosophy
VitalQ does **not** hammer the backend API with `setInterval` polling loops. Instead, state updates are event-driven via **SignalR WebSockets**:

1. Patient connects to `http://localhost:5089/hubs/queue` on login.
2. When a doctor calls a patient, triage is recorded, or aging recalculates, the backend pushes an event.
3. `SignalRService` exposes reactive observables:
   - `onPatientCalled$`
   - `onQueueUpdated$`
   - `onScoresRecalculated$`
4. Components subscribe and update their **Angular Signals** silently in $<1\text{ms}$.

### Web Audio Summons Chime
When a patient's status transitions to `Called`, `PatientDashboardComponent.playHospitalChime()` synthesizes an ascending 3-tone chime ($C_5 \rightarrow E_5 \rightarrow G_5$) using native browser `AudioContext` without requiring external MP3 assets.

---

## 5. Non-Blocking Notifications (`ToastService`)
Browser `alert()` dialogs are strictly prohibited.
All operational alerts, confirmations, and validation errors use `ToastService`:
```typescript
private toast = inject(ToastService);

this.toast.success('Token booked successfully!');
this.toast.error('Could not cancel token.');
this.toast.warning('Please select a doctor.');
```
Rendered globally via `<app-toast-container></app-toast-container>`.

---

## 6. How to Add a New Feature (3 Steps)

1. **Define the Model Contract**:
   Add the TypeScript interface matching backend DTO in `src/app/shared/models/<domain>.model.ts` and export it in `src/app/shared/models/index.ts`.
2. **Expose the HTTP Service Method**:
   Add the API call to the relevant service in `src/app/core/services/` using `environment.apiUrl`.
3. **Build the Standalone UI**:
   Create or update the component in `src/app/features/<role>/` using Angular Signals for reactivity.
