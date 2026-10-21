import { Component, OnInit, OnDestroy, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AdminService } from '../../../core/services/admin.service';
import { SignalRService } from '../../../core/services/signalr.service';
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
export class DashboardComponent implements OnInit, OnDestroy {
  private adminService = inject(AdminService);
  private signalR = inject(SignalRService);
  private subs: Subscription[] = [];

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
    this.initSignalR();
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
    this.signalR.leaveAdminGroup();
  }

  // #endregion

  // #region Data Fetching & Real-Time Sync

  /**
   * Fetches the aggregated dashboard KPIs.
   * If silent is true, refreshes KPIs without showing loading spinner.
   */
  loadHospitalMetrics(silent = false): void {
    if (!silent) {
      this.isLoading.set(true);
      this.hasError.set(false);
    }

    this.adminService.getDashboardStats().subscribe({
      next: (data) => {
        this.stats.set(data);
        if (!silent) this.isLoading.set(false);
      },
      error: () => {
        if (!silent) {
          this.hasError.set(true);
          this.isLoading.set(false);
        }
      }
    });
  }

  private initSignalR(): void {
    this.signalR.startConnection().then(() => {
      this.signalR.joinAdminGroup();

      const docSub = this.signalR.onDoctorStatusChanged$.subscribe(() => {
        this.loadHospitalMetrics(true);
      });

      const bookSub = this.signalR.onTokenBooked$.subscribe(() => {
        this.loadHospitalMetrics(true);
      });

      const queueSub = this.signalR.onQueueUpdated$.subscribe(() => {
        this.loadHospitalMetrics(true);
      });

      this.subs.push(docSub, bookSub, queueSub);
    });
  }

  // #endregion
}
