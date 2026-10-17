import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Doctor, DoctorDashboardStats, QueueToken, CompleteConsultationRequest, UpdateDoctorStatusRequest } from '../../shared/models/queue.model';
import { PatientVisitHistory } from '../../shared/models/patient.model';

/**
 * Service powering the Doctor Consultation Room Console.
 * Directly communicates with backend /api/doctor endpoints for queue management,
 * patient summoning, clinical notes recording, and shift analytics.
 */
@Injectable({ providedIn: 'root' })
export class DoctorService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/doctor`;

  // #region 1. Doctor Profile & Duty Status

  /**
   * Retrieves the doctor profile linked to the authenticated session.
   * Endpoint: GET /api/doctor/me
   */
  getDoctorProfile(): Observable<Doctor> {
    return this.http.get<Doctor>(`${this.baseUrl}/me`);
  }

  /**
   * Aggregates real-time shift metrics for the doctor room header (completed, waiting, skipped, avg time).
   * Endpoint: GET /api/doctor/dashboard/stats
   */
  getDashboardStats(doctorId?: string): Observable<DoctorDashboardStats> {
    const url = doctorId ? `${this.baseUrl}/dashboard/stats?doctorId=${doctorId}` : `${this.baseUrl}/dashboard/stats`;
    return this.http.get<DoctorDashboardStats>(url);
  }

  /**
   * Updates physician availability status (Available, Busy, OnLeave).
   * Endpoint: PUT /api/doctor/status
   */
  updateDoctorStatus(status: string, doctorId?: string): Observable<Doctor> {
    const url = doctorId ? `${this.baseUrl}/status?doctorId=${doctorId}` : `${this.baseUrl}/status`;
    const payload: UpdateDoctorStatusRequest = { status };
    return this.http.put<Doctor>(url, payload);
  }

  // #endregion

  // #region 2. Live Consultation Queue & Summoning

  /**
   * Retrieves the live consultation queue for a doctor, partitioned into Called, Waiting, and Skipped.
   * Endpoint: GET /api/doctor/queue/{doctorId}
   */
  getDoctorQueue(doctorId: string): Observable<QueueToken[]> {
    return this.http.get<QueueToken[]>(`${this.baseUrl}/queue/${doctorId}`);
  }

  /**
   * Retrieves the patient currently called into the doctor's consultation chamber.
   * Endpoint: GET /api/doctor/current/{doctorId}
   */
  getCurrentCalled(doctorId: string): Observable<QueueToken | null> {
    return this.http.get<QueueToken | null>(`${this.baseUrl}/current/${doctorId}`);
  }

  /**
   * Summons the next highest-priority waiting patient into the consultation chamber.
   * Advances queue, plays audio chime, and alerts patient screen.
   * Endpoint: POST /api/doctor/call-next/{doctorId}
   */
  callNext(doctorId: string): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.baseUrl}/call-next/${doctorId}`, {});
  }

  /**
   * Re-broadcasts the melodic summon chime and visual call notification for an already-called patient.
   * Endpoint: POST /api/doctor/tokens/{id}/summon
   */
  summonPatient(tokenId: string): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.baseUrl}/tokens/${tokenId}/summon`, {});
  }

  // #endregion

  // #region 3. Queue Advancement & State Transitions

  /**
   * Marks a called patient as skipped if they do not enter the room.
   * Enforces 3-strike rule: Strike 1 & 2 move to Temporary Hold; Strike 3 auto-expires.
   * Endpoint: POST /api/doctor/tokens/{id}/skip
   */
  skipPatient(tokenId: string): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.baseUrl}/tokens/${tokenId}/skip`, {});
  }

  /**
   * Restores an absent patient back into the active waiting queue.
   * Applies CTAS-aware 3-slot fallback for routine patients and immediate Position #1 for Red emergencies.
   * Endpoint: POST /api/doctor/tokens/{id}/requeue
   */
  requeuePatient(tokenId: string): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.baseUrl}/tokens/${tokenId}/requeue`, {});
  }

  /**
   * Concludes the consultation, saves clinical notes and diagnosis, and archives encounter.
   * Endpoint: POST /api/doctor/tokens/{id}/complete
   */
  completeConsultation(tokenId: string, consultationNotes?: string): Observable<QueueToken> {
    const payload: CompleteConsultationRequest = {
      consultationNotes: consultationNotes || ''
    };
    return this.http.post<QueueToken>(`${this.baseUrl}/tokens/${tokenId}/complete`, payload);
  }

  // #endregion

  // #region 4. Encounter History & Patient Dossier

  /**
   * Retrieves consultations completed today by the specified doctor in chronological order.
   * Endpoint: GET /api/doctor/history
   */
  getCompletedHistoryToday(doctorId?: string): Observable<QueueToken[]> {
    const url = doctorId ? `${this.baseUrl}/history?doctorId=${doctorId}` : `${this.baseUrl}/history`;
    return this.http.get<QueueToken[]>(url);
  }

  /**
   * Retrieves past hospital encounters and consultation notes for a specific patient.
   * Endpoint: GET /api/doctor/patients/{patientId}/history
   */
  getPatientPastEncounters(patientId: string): Observable<PatientVisitHistory[]> {
    return this.http.get<PatientVisitHistory[]>(`${this.baseUrl}/patients/${patientId}/history`);
  }

  // #endregion
}
