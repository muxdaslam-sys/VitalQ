/**
 * QueueService: Backward-compatibility facade delegating to DoctorService.
 * Re-routes calls to the canonical /api/doctor endpoints.
 */

import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DoctorService } from './doctor.service';
import { QueueToken, CompleteConsultationRequest } from '../../shared/models/queue.model';

export type { QueueToken, CompleteConsultationRequest };

@Injectable({ providedIn: 'root' })
export class QueueService {
  private doctorService = inject(DoctorService);

  getDoctorQueue(doctorId: string): Observable<QueueToken[]> {
    return this.doctorService.getDoctorQueue(doctorId);
  }

  getCurrentCalled(doctorId: string): Observable<QueueToken | null> {
    return this.doctorService.getCurrentCalled(doctorId);
  }

  callNext(doctorId: string): Observable<QueueToken> {
    return this.doctorService.callNext(doctorId);
  }

  skipPatient(tokenId: string): Observable<QueueToken> {
    return this.doctorService.skipPatient(tokenId);
  }

  requeuePatient(tokenId: string): Observable<QueueToken> {
    return this.doctorService.requeuePatient(tokenId);
  }

  completeConsultation(tokenId: string, consultationNotes?: string): Observable<QueueToken> {
    return this.doctorService.completeConsultation(tokenId, consultationNotes);
  }
}
