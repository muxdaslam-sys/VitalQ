/**
 * ============================================================================
 * SERVICE: PatientService (Patient Demographics & Encounter History)
 * ============================================================================
 * 
 * PURPOSE:
 * Manages patient demographic profiles, family dependents, self-registration,
 * and comprehensive outpatient consultation history.
 * ============================================================================
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PatientProfile, AddFamilyMemberRequest, PatientVisitHistory, SelfRegisterRequest } from '../../shared/models/patient.model';

export type { PatientProfile, AddFamilyMemberRequest, PatientVisitHistory, SelfRegisterRequest };

@Injectable({ providedIn: 'root' })
export class PatientService {
  //#region Dependencies & API Configuration

  /** Injected Angular HTTP client for patient profile REST calls */
  private http = inject(HttpClient);

  /** Base API endpoint URL for patient services (e.g. http://localhost:5089/api/patient) */
  private api = `${environment.apiUrl}/patient`;

  //#endregion

  //#region 1. Family Members & Dependents

  /**
   * Retrieves all family member profiles linked to the authenticated user's mobile number.
   * Endpoint: GET /api/patient/family
   * Used to populate the family directory drawer and the booking patient selector.
   */
  getMyFamily(): Observable<PatientProfile[]> {
    return this.http.get<PatientProfile[]>(`${this.api}/family`);
  }

  /**
   * Registers a new child or elderly dependent profile under the user's primary account.
   * Endpoint: POST /api/patient/family
   * The backend generates an official Medical Record Number (MRN) and links the profile.
   * 
   * @param request Dependent details including FullName, DateOfBirth, and Gender.
   */
  addFamilyMember(request: AddFamilyMemberRequest): Observable<PatientProfile> {
    return this.http.post<PatientProfile>(`${this.api}/family`, request);
  }

  //#endregion

  //#region 2. Clinical History & Medical Records

  /**
   * BATCH HISTORY: Retrieves completed OPD consultation encounters for the authenticated user and their family.
   * Endpoint: GET /api/patient/history
   * Powered by a single indexed SQL query on the backend.
   */
  getMyHistory(): Observable<PatientVisitHistory[]> {
    return this.http.get<PatientVisitHistory[]>(`${this.api}/history`);
  }

  //#endregion
}
