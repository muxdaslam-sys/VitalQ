/**
 * ============================================================================
 * SERVICE: QueueService (Doctor Priority Queue & Consultation Engine)
 * ============================================================================
 * 
 * PURPOSE:
 * Manages the live clinical consultation queue for attending physicians.
 * Powers the doctor's priority list, patient calling, skipping, re-queueing,
 * and consultation completion.
 * 
 * CONCURRENCY & CLINICAL LOGIC:
 * - Dynamic Priority Sorting: Patients are ordered by calculated PriorityScore
 *   (Base CTAS Urgency Weight + Aging Minutes).
 * - Concurrency Protection: `callNext` utilizes SQL RowVersion optimistic locking
 *   to ensure two staff members cannot call or mutate the same token simultaneously.
 * - Broadcasts: Every mutation triggers real-time SignalR broadcasts updating
 *   the patient's mobile slip and waiting room displays.
 * ============================================================================
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { QueueToken, CompleteConsultationRequest } from '../../shared/models/queue.model';

export type { QueueToken, CompleteConsultationRequest };

@Injectable({ providedIn: 'root' })
export class QueueService {
  /** Injected Angular HTTP client */
  private http = inject(HttpClient);

  /** Base API endpoint URL (e.g. http://localhost:5089/api) */
  private api = environment.apiUrl;

  /**
   * Retrieves the live, priority-sorted queue for a specific physician.
   * Endpoint: GET /api/queue/doctor/{doctorId}
   * Sorted descending by PriorityScore (CTAS Red > Yellow > Green + Aging).
   * 
   * @param doctorId GUID of the doctor.
   */
  getDoctorQueue(doctorId: string): Observable<QueueToken[]> {
    return this.http.get<QueueToken[]>(`${this.api}/queue/doctor/${doctorId}`);
  }

  /**
   * Retrieves the patient currently inside the physician's chamber (Status = 'Called').
   * Endpoint: GET /api/queue/doctor/{doctorId}/current
   * 
   * @param doctorId GUID of the doctor.
   */
  getCurrentCalled(doctorId: string): Observable<QueueToken | null> {
    return this.http.get<QueueToken | null>(`${this.api}/queue/doctor/${doctorId}/current`);
  }

  /**
   * Calls the highest-priority waiting patient into the consultation chamber.
   * Endpoint: POST /api/queue/doctor/{doctorId}/call-next
   * Updates token status to 'Called', broadcasts SignalR PatientCalled event,
   * and triggers the 3-tone melodic chime on the patient's phone.
   * 
   * @param doctorId GUID of the doctor calling the patient.
   */
  callNext(doctorId: string): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.api}/queue/doctor/${doctorId}/call-next`, {});
  }

  /**
   * Places an absent or unresponsive patient's slip on hold.
   * Endpoint: POST /api/tokens/{tokenId}/skip
   * Updates status to 'Skipped' so the doctor can call the next patient without delay.
   * 
   * @param tokenId GUID of the token to skip.
   */
  skipPatient(tokenId: string): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.api}/tokens/${tokenId}/skip`, {});
  }

  /**
   * Restores an absent patient back into the active queue when they return to the clinic.
   * Endpoint: POST /api/tokens/{tokenId}/requeue
   * Resets status to 'Waiting' and recalculates priority.
   * 
   * @param tokenId GUID of the skipped token.
   */
  requeuePatient(tokenId: string): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.api}/tokens/${tokenId}/requeue`, {});
  }

  /**
   * Concludes the consultation, archives the diagnosis advice, and releases the chamber.
   * Endpoint: POST /api/tokens/{tokenId}/complete
   * Saves consultationNotes into the permanent medical record and notifies the queue hub.
   * 
   * @param tokenId GUID of the token being completed.
   * @param consultationNotes Physician's advice, prescription summary, and follow-up plan.
   */
  completeConsultation(tokenId: string, consultationNotes?: string): Observable<QueueToken> {
    const payload: CompleteConsultationRequest = {
      consultationNotes: consultationNotes || ''
    };
    return this.http.post<QueueToken>(`${this.api}/tokens/${tokenId}/complete`, payload);
  }
}
