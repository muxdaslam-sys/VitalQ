/**
 * ============================================================================
 * COMPONENT: DepartmentsComponent (Clinical Departments & Token Prefixes)
 * ============================================================================
 * 
 * PURPOSE:
 * Allows Hospital Administrators to:
 * 1. View all clinical departments (Cardiology, Orthopedics, Pediatrics, etc.).
 * 2. Search by department name or token prefix code (e.g. CARD, ORTH).
 * 3. Filter by Active vs Inactive operational status.
 * 4. Register new clinical departments and designate unique prefix codes.
 * 5. Update department floor/wing locations and toggle active routing.
 * 
 * ARCHITECTURAL DESIGN:
 * - Angular 19 Signals for clean, high-performance reactivity.
 * - Reactive computed filtering for instantaneous searching without network latency.
 * - In-app ToastService alerts replacing browser alerts.
 * - Double-click protection with isSubmitting state.
 * ============================================================================
 */

import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { ToastService } from '../../../core/services/toast.service';
import { DepartmentResponse, CreateDepartmentRequest, UpdateDepartmentRequest } from '../../../shared/models/admin.model';

@Component({
  selector: 'app-departments',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './departments.component.html',
  styleUrl: './departments.component.css'
})
export class DepartmentsComponent implements OnInit {
  // --------------------------------------------------------------------------
  // 1. DEPENDENCY INJECTION
  // --------------------------------------------------------------------------
  private adminService = inject(AdminService);
  private toast = inject(ToastService);

  // --------------------------------------------------------------------------
  // 2. COMPONENT STATE (SIGNALS)
  // --------------------------------------------------------------------------

  /** Master list of departments fetched from the backend API */
  departments = signal<DepartmentResponse[]>([]);

  /** Controls display of the Add Department modal popup */
  showModal = signal(false);

  /** Stores the ID of the department currently being edited (null = closed) */
  editingDeptId = signal<string | null>(null);

  /** Submission flag to prevent duplicate submissions on button spam */
  isSubmitting = signal(false);

  /** Search query string */
  searchQuery = signal('');

  /** Filter by active status ('all', 'active', or 'inactive') */
  selectedStatus = signal<'all' | 'active' | 'inactive'>('all');

  /** Form state model for registering a new department */
  newDept: CreateDepartmentRequest = { name: '', code: '', locationFloor: '' };

  /** Form state model for editing an existing department */
  editDept: UpdateDepartmentRequest = { name: '', locationFloor: '', isActive: true };

  // --------------------------------------------------------------------------
  // 3. COMPUTED REACTIVE FILTER
  // --------------------------------------------------------------------------
  filteredDepartments = computed(() => {
    let list = this.departments();
    const query = this.searchQuery().trim().toLowerCase();
    const status = this.selectedStatus();

    // 1. Search by department name, prefix code, or floor location
    if (query) {
      list = list.filter(d =>
        d.name.toLowerCase().includes(query) ||
        d.code.toLowerCase().includes(query) ||
        d.locationFloor.toLowerCase().includes(query)
      );
    }

    // 2. Status Filter
    if (status === 'active') {
      list = list.filter(d => d.isActive);
    } else if (status === 'inactive') {
      list = list.filter(d => !d.isActive);
    }

    return list;
  });

  // --------------------------------------------------------------------------
  // 4. LIFECYCLE HOOKS & DATA LOADING
  // --------------------------------------------------------------------------
  ngOnInit(): void {
    this.loadDepartments();
  }

  /**
   * Fetches latest departments from the backend.
   */
  loadDepartments(): void {
    this.adminService.getDepartments().subscribe({
      next: depts => this.departments.set(depts),
      error: () => {
        this.departments.set([]);
        this.toast.error('Failed to load departments from server.');
      }
    });
  }

  // --------------------------------------------------------------------------
  // 5. USER ACTIONS & MODAL CONTROLS
  // --------------------------------------------------------------------------

  /**
   * Resets form state and opens Add Department modal.
   */
  openModal(): void {
    this.newDept = { name: '', code: '', locationFloor: '' };
    this.showModal.set(true);
  }

  /**
   * Validates and submits new department to backend.
   */
  saveDepartment(): void {
    if (!this.newDept.name || !this.newDept.code || !this.newDept.locationFloor) {
      this.toast.warning('Please enter Name, Prefix Code, and Floor Location.');
      return;
    }

    this.newDept.code = this.newDept.code.toUpperCase().trim();
    this.isSubmitting.set(true);

    this.adminService.createDepartment(this.newDept).subscribe({
      next: (dept) => {
        this.isSubmitting.set(false);
        this.showModal.set(false);
        this.toast.success(`Department "${dept.name}" (${dept.code}) created successfully.`);
 this.loadDepartments();
 },
 error: (err) => {
 this.isSubmitting.set(false);
 this.toast.error(err.error?.message || 'Failed to save department.');
 }
 });
 }

 /**
 * Populates edit form with selected department data and opens edit modal.
 */
 openEditModal(dept: DepartmentResponse): void {
 this.editingDeptId.set(dept.id);
 this.editDept = {
 name: dept.name,
 locationFloor: dept.locationFloor,
 isActive: dept.isActive
 };
 }

 /**
 * Submits department updates to backend.
 */
 updateDepartment(): void {
 const id = this.editingDeptId();
 if (!id) return;

 this.isSubmitting.set(true);
 this.adminService.updateDepartment(id, this.editDept).subscribe({
 next: () => {
 this.isSubmitting.set(false);
 this.editingDeptId.set(null);
 this.toast.success('Department configuration updated successfully.');
 this.loadDepartments();
 },
 error: (err) => {
 this.isSubmitting.set(false);
 this.toast.error(err.error?.message || 'Failed to update department.');
 }
 });
 }
}
