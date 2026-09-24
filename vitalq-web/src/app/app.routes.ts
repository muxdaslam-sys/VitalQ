import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },

  // Admin Portal (Full Section)
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

  // Doctor Dashboard Template
  {
    path: 'doctor',
    loadComponent: () => import('./features/doctor/doctor-dashboard/doctor-dashboard.component').then(m => m.DoctorDashboardComponent),
    canActivate: [authGuard(['Doctor', 'Admin'])]
  },

  // Nurse Station Template
  {
    path: 'nurse',
    loadComponent: () => import('./features/nurse/nurse-dashboard/nurse-dashboard.component').then(m => m.NurseDashboardComponent),
    canActivate: [authGuard(['Nurse', 'Admin'])]
  },

  // Patient Mobile Dashboard Template
  {
    path: 'patient',
    loadComponent: () => import('./features/patient/patient-dashboard/patient-dashboard.component').then(m => m.PatientDashboardComponent),
    canActivate: [authGuard(['Patient', 'Admin'])]
  },

  { path: '**', redirectTo: 'login' }
];
