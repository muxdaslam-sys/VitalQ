/**
 * DEPRECATED / REDIRECT SHIM
 * This view has been consolidated into the high-performance All-in-One Patient Smart Hub:
 * file:///d:/Projects/VitalQ/vitalq-web/src/app/features/patient/patient-dashboard/patient-dashboard.component.ts
 * 
 * Automatically forwards any direct route requests to the Smart Hub with Express Booking action.
 */
import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-patient-book',
  standalone: true,
  template: `
    <div class="py-20 text-center space-y-3">
      <div class="w-8 h-8 border-4 border-teal-500 border-t-transparent rounded-full animate-spin mx-auto"></div>
      <p class="text-xs font-semibold text-slate-500">Opening Express Booking in Patient Smart Hub...</p>
    </div>
  `
})
export class PatientBookComponent implements OnInit {
  private router = inject(Router);

  ngOnInit(): void {
    this.router.navigate(['/patient/dashboard'], { queryParams: { action: 'book' }, replaceUrl: true });
  }
}
