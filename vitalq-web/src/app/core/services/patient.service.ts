/**
 * ============================================================================
 * SERVICE: PatientService (Medical Profile & Family Management Engine)
 * ============================================================================
 * 
 * PURPOSE:
 * Manages patient demographic profiles, family dependent relationships, and
 * lifelong outpatient consultation history.
 * 
 * CLINICAL ARCHITECTURE:
 * - Shared Phone Number: A parent or guardian can register multiple family members
 *   under their primary mobile account for login convenience.
 * - Distinct Medical Record Numbers (MRN): Each dependent is assigned a unique,
 *   permanent MRN (e.g. MRN-20261003-ABCD) ensuring medical diagnoses, vitals,
 *   and prescriptions remain confidential and strictly segregated.
 * ============================================================================
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PatientProfile, AddFamilyMemberRequest, PatientVisitHistory } from '../../shared/models/patient.model';

export type { PatientProfile, AddFamilyMemberRequest, PatientVisitHistory };

@Injectable({ providedIn: 'root' })
export class PatientService {
  /** Injected Angular HTTP client for patient profile REST calls */
  private http = inject(HttpClient);

  /** Base API endpoint URL for patient services (e.g. http://localhost:5089/api/patients) */
  private api = `${environment.apiUrl}/patients`;

  /**
   * Retrieves all family member profiles linked to the authenticated user's mobile number.
   * Endpoint: GET /api/patients/family
   * Used to populate the family directory drawer and the booking patient selector.
   */
  getMyFamily(): Observable<PatientProfile[]> {
    return this.http.get<PatientProfile[]>(`${this.api}/family`);
  }

  /**
   * Registers a new child or elderly dependent profile under the user's primary account.
   * Endpoint: POST /api/patients/family
   * The backend generates an official Medical Record Number (MRN) and links the profile.
   * 
   * @param request Dependent details including FullName, DateOfBirth, and Gender.
   */
  addFamilyMember(request: AddFamilyMemberRequest): Observable<PatientProfile> {
    return this.http.post<PatientProfile>(`${this.api}/family`, request);
  }

  /**
   * Retrieves a specific patient's demographic profile by their unique ID.
   * Endpoint: GET /api/patients/{id}
   * 
   * @param id GUID identifier of the patient record.
   */
  getPatientById(id: string): Observable<PatientProfile> {
    return this.http.get<PatientProfile>(`${this.api}/${id}`);
  }

  /**
   * Retrieves the continuous OPD consultation history for the authenticated primary user.
   * Endpoint: GET /api/patients/my-history
   * Returns past completed visits, triage priority ratings, and attending doctor advice.
   */
  getMyHistory(): Observable<PatientVisitHistory[]> {
    return this.http.get<PatientVisitHistory[]>(`${this.api}/my-history`);
  }

  /**
   * Retrieves the consultation history for a specific dependent or family member.
   * Endpoint: GET /api/patients/{patientId}/history
   * 
   * @param patientId GUID identifier of the family member.
   */
  getPatientHistory(patientId: string): Observable<PatientVisitHistory[]> {
    return this.http.get<PatientVisitHistory[]>(`${this.api}/${patientId}/history`);
  }
}
