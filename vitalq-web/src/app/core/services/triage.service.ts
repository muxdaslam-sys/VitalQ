/**
 * ============================================================================
 * SERVICE: TriageService (Automated CTAS Clinical Triage Engine)
 * ============================================================================
 * 
 * PURPOSE:
 * Bridges the nurse intake station with the backend CTAS clinical evaluation engine.
 * Records patient vital signs, applies Canadian Triage & Acuity Scale rules,
 * supports manual clinical overrides with audit logging, and tracks the pending
 * triage intake queue.
 * 
 * CTAS CLINICAL RULES & THRESHOLDS:
 * - Red (Emergency, Base Weight 100): Immediate life threat.
 *   SpO2 < 90% | Systolic BP < 90 or > 180 | Heart Rate > 120 or < 40 | Temp > 39.5°C | Pain = 10
 * - Yellow (Urgent, Base Weight 50): Target evaluation < 30 minutes.
 *   SpO2 <= 94% | Systolic BP >= 160 | Heart Rate >= 100 | Temp >= 38.0°C | Pain >= 6
 * - Green (Routine, Base Weight 10): Stable outpatient consultation.
 *   Normal vital signs within standard clinical ranges.
 * ============================================================================
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { QueueToken } from '../../shared/models/queue.model';
import { TriageRequest, TriageAssessment, PatientSearchResult } from '../../shared/models/triage.model';

export type { TriageRequest, TriageAssessment, PatientSearchResult };

@Injectable({ providedIn: 'root' })
export class TriageService {
  /** Injected Angular HTTP client */
  private http = inject(HttpClient);

  /** Base API endpoint URL (e.g. http://localhost:5089/api) */
  private api = environment.apiUrl;

  /**
   * Submits a vital signs assessment for a newly booked patient slip.
   * Endpoint: POST /api/tokens/{tokenId}/triage
   * Calculates CTAS tier, updates token status from 'Booked' -> 'Waiting',
   * and places the patient into the doctor's live queue.
   * 
   * @param tokenId GUID identifier of the QueueToken.
   * @param request Vital signs intake payload (SpO2, BP, HR, Temp, Pain, Overrides).
   */
  submitTriage(tokenId: string, request: TriageRequest): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.api}/tokens/${tokenId}/triage`, request);
  }

  /**
   * Retrieves all tokens in 'Booked' status awaiting nurse vitals triage for today.
   * Endpoint: GET /api/triage/pending
   * 
   * @param departmentId Optional filter for a specific department's triage desk.
   */
  getPendingTriage(departmentId?: string): Observable<QueueToken[]> {
    const url = departmentId
      ? `${this.api}/triage/pending?departmentId=${departmentId}`
      : `${this.api}/triage/pending`;
    return this.http.get<QueueToken[]>(url);
  }

  /**
   * Retrieves the recorded triage assessment and vital signs history for a token.
   * Endpoint: GET /api/tokens/{tokenId}/triage
   * 
   * @param tokenId GUID of the token.
   */
  getTriageAssessment(tokenId: string): Observable<TriageAssessment> {
    return this.http.get<TriageAssessment>(`${this.api}/tokens/${tokenId}/triage`);
  }

  /**
   * Client-side instant CTAS evaluation preview.
   * Allows the nurse intake UI to display real-time color badge feedback
   * (Red, Yellow, Green) while typing vitals before submitting to the backend.
   * 
   * @param spo2 Oxygen saturation percentage (%).
   * @param sys Systolic blood pressure (mmHg).
   * @param hr Heart rate (beats per minute).
   * @param temp Body temperature (Celsius).
   * @param pain Self-reported pain scale (1 to 10).
   */
  evaluateLevel(spo2: number, sys: number, hr: number, temp: number, pain: number): 'Red' | 'Yellow' | 'Green' {
    // Red (Emergency Priority): Acute distress requiring immediate physician summons
    if (spo2 < 90 || sys < 90 || sys > 180 || hr > 120 || hr < 40 || temp > 39.5 || pain === 10) {
      return 'Red';
    }

    // Yellow (Urgent Priority): Substantial distress, target evaluation < 30 mins
    if (spo2 <= 94 || sys >= 160 || hr >= 100 || temp >= 38.0 || pain >= 6) {
      return 'Yellow';
    }

    // Green (Routine Priority): Hemodynamically stable outpatient case
    return 'Green';
  }
}
