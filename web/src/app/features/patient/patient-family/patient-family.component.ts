/**
 * ============================================================================
 * COMPONENT: PatientFamilyComponent (Family Members & Dependents Directory)
 * ============================================================================
 * 
 * PURPOSE:
 * Allows patients to view, register, and manage dependent profiles (children,
 * spouse, elderly parents) sharing their mobile account. Enables one-click
 * appointment booking for any family member.
 * ============================================================================
 */

import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PatientService } from '../../../core/services/patient.service';
import { ToastService } from '../../../core/services/toast.service';
import { PatientProfile } from '../../../shared/models/patient.model';
import { TeleportToBodyDirective } from '../../../shared/directives/teleport.directive';

@Component({
  selector: 'app-patient-family',
  standalone: true,
  imports: [CommonModule, FormsModule, TeleportToBodyDirective],
  templateUrl: './patient-family.component.html',
  styleUrls: ['./patient-family.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PatientFamilyComponent implements OnInit {
  //#region 1. Dependencies & Services

  private patientService = inject(PatientService);
  private toast = inject(ToastService);
  private router = inject(Router);

  //#endregion

  //#region 2. Reactive State Signals

  /** List of patient profiles registered under the user's phone number */
  familyMembers = signal<PatientProfile[]>([]);

  /** Loading indicator during initial family directory fetch */
  loading = signal<boolean>(true);

  /** Add dependent modal visibility flag */
  isAddModalOpen = signal<boolean>(false);

  /** Modal form input state */
  newName = signal<string>('');
  newDob = signal<string>('');
  newGender = signal<string>('Male');

  /** Submission state flags */
  submitting = signal<boolean>(false);
  errorMessage = signal<string>('');

  //#endregion

  //#region 3. Lifecycle & Data Fetching

  ngOnInit(): void {
    this.loadFamily();
  }

  /**
   * Fetches all registered family members from the backend API.
   */
  loadFamily(): void {
    this.loading.set(true);
    this.patientService.getMyFamily().subscribe({
      next: (members) => {
        this.familyMembers.set(members);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  //#endregion

  //#region 4. Modal Handlers & Submission

  /**
   * Opens the registration modal and resets all form inputs.
   */
  openAddModal(): void {
    this.newName.set('');
    this.newDob.set('');
    this.newGender.set('Male');
    this.errorMessage.set('');
    this.isAddModalOpen.set(true);
  }

  /**
   * Closes the registration modal.
   */
  closeAddModal(): void {
    this.isAddModalOpen.set(false);
  }

  /**
   * Validates inputs and submits dependent registration to the backend.
   */
  submitAddFamily(): void {
    if (this.submitting()) return;

    if (!this.newName().trim()) {
      this.errorMessage.set('Please enter a full name.');
      return;
    }
    if (!this.newDob()) {
      this.errorMessage.set('Please select date of birth.');
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    this.patientService.addFamilyMember({
      fullName: this.newName().trim(),
      dateOfBirth: this.newDob(),
      gender: this.newGender()
    }).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.closeAddModal();
        this.toast.success(`Profile for ${created.fullName} added successfully.`);
        this.loadFamily();
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err.error?.message || 'Failed to add family member.';
        this.errorMessage.set(msg);
      }
    });
  }

  //#endregion

  //#region 5. Navigation & UI Helpers

  /**
   * Generates a 2-character uppercase avatar string from a person's full name.
   */
  getInitials(name: string): string {
    const parts = (name || '').trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return (parts[0] || 'P').substring(0, 2).toUpperCase();
  }

  /**
   * Deep-links directly to the appointment booking wizard with this member pre-selected.
   */
  bookForMember(memberId: string): void {
    this.router.navigate(['/patient/book'], { queryParams: { patientId: memberId } });
  }

  //#endregion
}
