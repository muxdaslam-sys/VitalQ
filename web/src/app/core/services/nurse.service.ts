import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { QueueToken } from '../../shared/models/queue.model';
import { TriageRequest, TriageAssessment, PatientSearchResult } from '../../shared/models/triage.model';
import {
  NurseDashboardStatsResponse,
  DoctorDetailedQueueResponse,
  PatientQueuePositionResponse,
  EmergencyWalkInBookingRequest,
  ExistingPatientTokenBookingRequest
} from '../../shared/models/nurse.model';
import { PatientDetailResponse } from '../../shared/models/admin.model';

@Injectable({ providedIn: 'root' })
export class NurseService {

  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/nurse`;

  // #region 1. Dashboard Metrics & Doctor Queues

  /**
   * Aggregates today's clinic metrics and live doctor roster.
   */
  getDashboardStats(): Observable<NurseDashboardStatsResponse> {
    return this.http.get<NurseDashboardStatsResponse>(`${this.baseUrl}/dashboard/stats`);
  }

  /**
   * Retrieves full live queue for a specific doctor with priority scores and triage vitals.
   */
  getDoctorQueue(doctorId: string): Observable<DoctorDetailedQueueResponse> {
    return this.http.get<DoctorDetailedQueueResponse>(`${this.baseUrl}/doctors/${doctorId}/queue`);
  }

  // #endregion

  // #region 2. Clinical Triage & Vital Signs Intake

  /**
   * Lists patients booked today awaiting vital signs intake.
   */
  getPendingTriage(departmentId?: string, nursingStationId?: string): Observable<QueueToken[]> {
    let url = `${this.baseUrl}/triage/pending`;
    const params: string[] = [];
    if (departmentId) params.push(`departmentId=${departmentId}`);
    if (nursingStationId) params.push(`nursingStationId=${nursingStationId}`);
    if (params.length) url += `?${params.join('&')}`;
    return this.http.get<QueueToken[]>(url);
  }

  /**
   * Records vitals and CTAS urgency score for a token.
   */
  submitTriage(tokenId: string, request: TriageRequest): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.baseUrl}/triage/${tokenId}`, request);
  }

  /**
   * Retrieves recorded triage assessment and vital signs for a token.
   */
  getTriageAssessment(tokenId: string): Observable<TriageAssessment> {
    return this.http.get<TriageAssessment>(`${this.baseUrl}/triage/${tokenId}`);
  }

  // #endregion

  // #region 3. Patient Lookup & Intake Modals

  /**
   * Searches patients by Phone, MRN, Name, or Token Number.
   */
  searchPatients(query: string): Observable<PatientSearchResult[]> {
    return this.http.get<PatientSearchResult[]>(`${this.baseUrl}/patients/search?query=${encodeURIComponent(query)}`);
  }

  /**
   * Retrieves active booked tokens today for a patient selected in search.
   */
  getPatientActiveTokens(patientId: string): Observable<QueueToken[]> {
    return this.http.get<QueueToken[]>(`${this.baseUrl}/patients/${patientId}/tokens/today`);
  }

  /**
   * Retrieves complete patient medical profile and visit history for the Full Details modal.
   */
  getPatientDetails(patientId: string): Observable<PatientDetailResponse> {
    return this.http.get<PatientDetailResponse>(`${this.baseUrl}/patients/${patientId}/details`);
  }

  /**
   * Computes real-time line position, wait estimates, and doctor queue visualizer for a token.
   */
  getPatientQueuePosition(tokenId: string): Observable<PatientQueuePositionResponse> {
    return this.http.get<PatientQueuePositionResponse>(`${this.baseUrl}/tokens/${tokenId}/queue-position`);
  }

  // #endregion

  // #region 4. Walk-in & Emergency Token Booking

  /**
   * Enhanced walk-in intake with automatic patient deduplication and Code-Red bypass option.
   */
  processEmergencyWalkIn(request: EmergencyWalkInBookingRequest): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.baseUrl}/tokens/walk-in-emergency`, request);
  }

  /**
   * Issues a token for an existing registered patient with optional Code-Red emergency bypass.
   * Eliminates re-entering demographics.
   */
  bookTokenForExistingPatient(patientId: string, request: ExistingPatientTokenBookingRequest): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.baseUrl}/patients/${patientId}/book-token`, request);
  }

  /**
   * Restores a skipped patient back into the doctor's active waiting queue from the Nurse Desk.
   * Places routine patients 3 slots back in line and emergency patients immediately at Position #1.
   */
  requeuePatient(tokenId: string): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.baseUrl}/tokens/${tokenId}/requeue`, {});
  }

  // #endregion

  // #region 5. Client-Side Clinical Decision Support

  /**
   * Client-side instant CTAS evaluation preview for triage form visual feedback.
   */
  evaluateLevel(spo2: number, sys: number, hr: number, temp: number, pain: number): 'Red' | 'Yellow' | 'Green' {
    if (spo2 < 90 || sys < 90 || sys > 200 || hr > 140 || hr < 40 || temp > 40.0 || pain === 10) {
      return 'Red';
    }
    if (
      (spo2 >= 90 && spo2 <= 94) ||
      (sys >= 90 && sys <= 99) ||
      (sys >= 160 && sys <= 200) ||
      (hr >= 110 && hr <= 140) ||
      (hr >= 40 && hr < 50) ||
      (temp >= 38.5 && temp <= 40.0) ||
      pain >= 6
    ) {
      return 'Yellow';
    }
    return 'Green';
  }

  // #endregion
}
