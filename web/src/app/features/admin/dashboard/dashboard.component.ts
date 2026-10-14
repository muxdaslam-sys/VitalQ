/**
 * ============================================================================
 * COMPONENT: DashboardComponent (Essential Admin Operations Overview)
 * ============================================================================
 * 
 * PURPOSE:
 * Provides hospital administrators with an instantaneous, streamlined overview of:
 * 1. Total Doctors and count currently marked 'Available'.
 * 2. Total Administrative and Nursing staff accounts.
 * 3. Total Registered patient records.
 * 4. Fast navigation shortcuts to Doctor, Staff, Department, Nursing Station, and Patient modules.
 * ============================================================================
 */

import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { forkJoin, catchError, of } from 'rxjs';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent implements OnInit {
  private adminService = inject(AdminService);

  // --------------------------------------------------------------------------
  // CORE OPERATIONAL KPI SIGNALS
  // --------------------------------------------------------------------------
  
  /** Total registered physicians */
  totalDoctors = signal(0);

  /** Doctors currently available on shift */
  availableDoctors = signal(0);

  /** Total administrative and nursing staff accounts */
  totalStaff = signal(0);

  /** Total patient registrations */
  totalPatients = signal(0);

  ngOnInit(): void {
    this.loadHospitalMetrics();
  }

  /**
   * Concurrently loads doctors, staff accounts, and patient records.
   */
  loadHospitalMetrics(): void {
    forkJoin({
      doctors: this.adminService.getDoctors().pipe(catchError(() => of([]))),
      users: this.adminService.getUsers().pipe(catchError(() => of([]))),
      patients: this.adminService.getPatients().pipe(catchError(() => of([])))
    }).subscribe(({ doctors, users, patients }) => {
      this.totalDoctors.set(doctors.length);
      this.availableDoctors.set(doctors.filter(d => d.status === 'Available').length);
      this.totalStaff.set(users.length);
      this.totalPatients.set(patients.length);
    });
  }
}
