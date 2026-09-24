/**
 * ============================================================================
 * COMPONENT: NursingStationsComponent (Triage Desks & Clinical Intake)
 * ============================================================================
 * 
 * PURPOSE:
 * Allows Hospital Administrators to:
 * 1. View all physical nursing triage desks and their assigned department.
 * 2. Search desks by name or floor location.
 * 3. Filter by department assignment and operational status (Active vs Inactive).
 * 4. Register new triage desks across hospital wings.
 * 5. Reassign desks to different departments or floor locations.
 * 
 * ARCHITECTURAL DESIGN:
 * - Angular 19 Signals for clean, high-performance reactivity.
 * - Reactive computed filtering pipeline.
 * - ToastService alerts replacing browser alerts.
 * - Submission safety (isSubmitting) to guard against duplicate API calls.
 * ============================================================================
 */

import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { ToastService } from '../../../core/services/toast.service';
import { NursingStationResponse, DepartmentResponse, CreateNursingStationRequest, UpdateNursingStationRequest } from '../../../shared/models/admin.model';

@Component({
  selector: 'app-nursing-stations',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './nursing-stations.component.html',
  styleUrl: './nursing-stations.component.css'
})
export class NursingStationsComponent implements OnInit {
  // --------------------------------------------------------------------------
  // 1. DEPENDENCY INJECTION
  // --------------------------------------------------------------------------
  private adminService = inject(AdminService);
  private toast = inject(ToastService);

  // --------------------------------------------------------------------------
  // 2. COMPONENT STATE (SIGNALS)
  // --------------------------------------------------------------------------

  /** Master list of nursing stations loaded from backend */
  stations = signal<NursingStationResponse[]>([]);

  /** Available departments for station mapping */
  departments = signal<DepartmentResponse[]>([]);

  /** Controls display of the Add Nursing Station Desk modal */
  showModal = signal(false);

  /** ID of station currently being edited (null = closed) */
  editingStationId = signal<string | null>(null);

  /** Submission flag to prevent button multi-clicks */
  isSubmitting = signal(false);

  /** Search query string */
  searchQuery = signal('');

  /** Department filter ('all' = all departments) */
  selectedDepartmentId = signal('all');

  /** Status filter ('all', 'active', or 'inactive') */
  selectedStatus = signal<'all' | 'active' | 'inactive'>('all');

  /** Form state model for creating a new nursing station */
  newStation: CreateNursingStationRequest = { stationName: '', locationFloor: '' };

  /** Form state model for updating an existing nursing station */
  editStation: UpdateNursingStationRequest = { stationName: '', departmentId: undefined, locationFloor: '', isActive: true };

  // --------------------------------------------------------------------------
  // 3. COMPUTED REACTIVE FILTER
  // --------------------------------------------------------------------------
  filteredStations = computed(() => {
    let list = this.stations();
    const query = this.searchQuery().trim().toLowerCase();
    const dept = this.selectedDepartmentId();
    const status = this.selectedStatus();

    // 1. Text Search Filter (station name, location, or mapped department)
    if (query) {
      list = list.filter(s =>
        s.stationName.toLowerCase().includes(query) ||
        s.locationFloor.toLowerCase().includes(query) ||
        (s.departmentName && s.departmentName.toLowerCase().includes(query))
      );
    }

    // 2. Department Category Filter
    if (dept !== 'all') {
      list = list.filter(s => s.departmentId === dept);
    }

    // 3. Status Filter
    if (status === 'active') {
      list = list.filter(s => s.isActive);
    } else if (status === 'inactive') {
      list = list.filter(s => !s.isActive);
    }

    return list;
  });

  // --------------------------------------------------------------------------
  // 4. LIFECYCLE HOOKS & DATA LOADING
  // --------------------------------------------------------------------------
  ngOnInit(): void {
    this.loadData();
  }

  /**
   * Fetches latest nursing stations and departments from backend.
   */
  loadData(): void {
    this.adminService.getNursingStations().subscribe({
      next: s => this.stations.set(s),
      error: () => {
        this.stations.set([]);
        this.toast.error('Failed to load nursing stations.');
      }
    });

    this.adminService.getDepartments().subscribe({
      next: d => this.departments.set(d),
      error: () => this.departments.set([])
    });
  }

  // --------------------------------------------------------------------------
  // 5. USER ACTIONS & MODAL CONTROLS
  // --------------------------------------------------------------------------

  /**
   * Resets form state and opens Add Station modal.
   */
  openModal(): void {
    this.newStation = { stationName: '', locationFloor: '' };
    this.showModal.set(true);
  }

  /**
   * Submits new nursing station desk to backend.
   */
  saveStation(): void {
    if (!this.newStation.stationName || !this.newStation.locationFloor) {
      this.toast.warning('Please enter Station Name and Floor Location.');
      return;
    }

    this.isSubmitting.set(true);
    this.adminService.createNursingStation(this.newStation).subscribe({
      next: (s) => {
        this.isSubmitting.set(false);
        this.showModal.set(false);
        this.toast.success(`Triage station "${s.stationName}" created successfully.`);
 this.loadData();
 },
 error: (err) => {
 this.isSubmitting.set(false);
 this.toast.error(err.error?.message || 'Failed to save nursing station.');
 }
 });
 }

 /**
 * Pre-populates edit form with selected station data and opens edit modal.
 */
 openEditModal(station: NursingStationResponse): void {
 this.editingStationId.set(station.id);
 this.editStation = {
 stationName: station.stationName,
 departmentId: station.departmentId,
 locationFloor: station.locationFloor,
 isActive: station.isActive
 };
 }

 /**
 * Submits station modifications to backend.
 */
 updateStation(): void {
 const id = this.editingStationId();
 if (!id) return;

 this.isSubmitting.set(true);
 this.adminService.updateNursingStation(id, this.editStation).subscribe({
 next: () => {
 this.isSubmitting.set(false);
 this.editingStationId.set(null);
 this.toast.success('Nursing station desk updated successfully.');
 this.loadData();
 },
 error: (err) => {
 this.isSubmitting.set(false);
 this.toast.error(err.error?.message || 'Failed to update nursing station.');
 }
 });
 }
}
