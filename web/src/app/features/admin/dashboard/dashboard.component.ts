import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { AdminDashboardStatsResponse } from '../../../shared/models/admin.model';

/**
 * ============================================================================
 * COMPONENT: DashboardComponent (Admin Command Center Overview)
 * ============================================================================
 * 
 * Provides hospital administrators with an instant real-time KPI overview:
 * 1. Total Registered Doctors & Active Available Doctors on duty
 * 2. Total Administrative and Triage Staff Accounts
 * 3. Master Patient Directory totals
 * 4. Clinical Departments & Nursing Stations totals
 * 5. Direct navigation shortcuts to all administration modules
 */
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

  // #region State Signals

  /** Real-time hospital metrics aggregated from the database */
  stats = signal<AdminDashboardStatsResponse>({
    totalDoctors: 0,
    availableDoctors: 0,
    totalStaff: 0,
    totalPatients: 0,
    totalDepartments: 0,
    totalNursingStations: 0
  });

  /** Loading state indicator */
  isLoading = signal(true);

  /** Failure state flag */
  hasError = signal(false);

  // #endregion

  // #region Lifecycle

  ngOnInit(): void {
    this.loadHospitalMetrics();
  }

  // #endregion

  // #region Data Fetching

  /**
   * Fetches the aggregated dashboard KPIs in a single lightweight API call.
   */
  loadHospitalMetrics(): void {
    this.isLoading.set(true);
    this.hasError.set(false);

    this.adminService.getDashboardStats().subscribe({
      next: (data) => {
        this.stats.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.hasError.set(true);
        this.isLoading.set(false);
      }
    });
  }

  // #endregion
}
