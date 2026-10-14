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
  private patientService = inject(PatientService);
  private toast = inject(ToastService);
  private router = inject(Router);

  familyMembers = signal<PatientProfile[]>([]);
  loading = signal<boolean>(true);

  // Add Family Modal
  isAddModalOpen = signal<boolean>(false);
  newName = signal<string>('');
  newDob = signal<string>('');
  newGender = signal<string>('Male');
  submitting = signal<boolean>(false);
  errorMessage = signal<string>('');

  ngOnInit(): void {
    this.loadFamily();
  }

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

  openAddModal(): void {
    this.newName.set('');
    this.newDob.set('');
    this.newGender.set('Male');
    this.errorMessage.set('');
    this.isAddModalOpen.set(true);
  }

  closeAddModal(): void {
    this.isAddModalOpen.set(false);
  }

  getInitials(name: string): string {
    const parts = (name || '').trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return (parts[0] || 'P').substring(0, 2).toUpperCase();
  }

  submitAddFamily(): void {
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

  bookForMember(memberId: string): void {
    this.router.navigate(['/patient/book'], { queryParams: { patientId: memberId } });
  }
}
