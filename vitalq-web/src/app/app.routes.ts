import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

/**
 * ============================================================================
 * APPLICATION ROUTING TABLE (VITALQ WEB)
 * ============================================================================
 * 
 * ARCHITECTURE PRINCIPLES:
 * 1. Role-First Clean URL Hierarchy:
 *    - /login   -> Public Dual-Mode Entry (Hospital Staff & Patients)
 *    - /patient -> Unified Patient OPD Smart Hub & Live Queue Tracker
 *    - /doctor  -> Doctor Consultation & Dynamic Priority Queue Console
 *    - /nurse   -> Nursing Station & Clinical Triage Intake Desk
 *    - /admin   -> Hospital Administration & System Configuration Suite
 * 
 * 2. Clean Single-Segment URLs:
 *    - /patient, /doctor, and /nurse have single-segment URLs (no redundant /dashboard nesting).
 *    - Modal drawers on the patient side use query parameters:
 *      e.g. /patient?action=book, /patient?action=family, /patient?token=CARD-104
 * 
 * 3. Multi-Page Structure for Admin:
 *    - /admin maintains distinct resource sub-routes with a persistent sidebar.
 * ============================================================================
 */
export const routes: Routes = [
  // Default entry route redirects to Unified Login
  { path: '', redirectTo: 'login', pathMatch: 'full' },

  // Public Dual-Mode Authentication (Staff & Patient)
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },

  // Public Walk-In Token Tracker (Anonymous Paper Slip Tracking)
  {
    path: 'track',
    loadComponent: () => import('./features/patient/public-tracker/public-tracker.component').then(m => m.PublicTrackerComponent)
  },

  // Patient OPD Portal (Dedicated Pages Architecture)
  {
    path: 'patient',
    loadComponent: () => import('./features/patient/patient-layout/patient-layout.component').then(m => m.PatientLayoutComponent),
    canActivate: [authGuard(['Patient', 'Admin'])],
    children: [
      {
        path: '',
        loadComponent: () => import('./features/patient/patient-dashboard/patient-dashboard.component').then(m => m.PatientDashboardComponent)
      },
      {
        path: 'book',
        loadComponent: () => import('./features/patient/patient-book/patient-book.component').then(m => m.PatientBookComponent)
      },
      {
        path: 'family',
        loadComponent: () => import('./features/patient/patient-family/patient-family.component').then(m => m.PatientFamilyComponent)
      },
      {
        path: 'history',
        loadComponent: () => import('./features/patient/patient-history/patient-history.component').then(m => m.PatientHistoryComponent)
      }
    ]
  },

  // Doctor Consultation & Queue Console (Clean Single URL: /doctor)
  {
    path: 'doctor',
    loadComponent: () => import('./features/doctor/doctor-dashboard/doctor-dashboard.component').then(m => m.DoctorDashboardComponent),
    canActivate: [authGuard(['Doctor', 'Admin'])]
  },

  // Nurse Clinical Vitals & Triage Station (Clean Single URL: /nurse)
  {
    path: 'nurse',
    loadComponent: () => import('./features/nurse/nurse-dashboard/nurse-dashboard.component').then(m => m.NurseDashboardComponent),
    canActivate: [authGuard(['Nurse', 'Admin'])]
  },

  // Hospital Administration Management Suite (Multi-Page Sidebar Layout)
  {
    path: 'admin',
    loadComponent: () => import('./features/admin/admin-layout/admin-layout.component').then(m => m.AdminLayoutComponent),
    canActivate: [authGuard(['Admin'])],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/admin/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'doctors',
        loadComponent: () => import('./features/admin/doctors/doctors.component').then(m => m.DoctorsComponent)
      },
      {
        path: 'staff',
        loadComponent: () => import('./features/admin/staff/staff.component').then(m => m.StaffComponent)
      },
      {
        path: 'departments',
        loadComponent: () => import('./features/admin/departments/departments.component').then(m => m.DepartmentsComponent)
      },
      {
        path: 'nursing-stations',
        loadComponent: () => import('./features/admin/nursing-stations/nursing-stations.component').then(m => m.NursingStationsComponent)
      },
      {
        path: 'patients',
        loadComponent: () => import('./features/admin/patients/patients.component').then(m => m.PatientsComponent)
      }
    ]
  },

  // Wildcard fallback: Re-route unrecognized paths to login
  { path: '**', redirectTo: 'login' }
];
