/**
 * PatientService: Manages patient demographic profiles, family dependents,
 * and outpatient visit history.
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PatientProfile, AddFamilyMemberRequest, PatientVisitHistory, UpdatePatientRequest, SelfRegisterRequest } from '../../shared/models/patient.model';

export type { PatientProfile, AddFamilyMemberRequest, PatientVisitHistory, UpdatePatientRequest, SelfRegisterRequest };

@Injectable({ providedIn: 'root' })
export class PatientService {
  /** Injected Angular HTTP client for patient profile REST calls */
  private http = inject(HttpClient);

  /** Base API endpoint URL for patient services (e.g. http://localhost:5089/api/patient) */
  private api = `${environment.apiUrl}/patient`;

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

  /**
   * Retrieves a specific patient's demographic profile by their unique ID.
   * Endpoint: GET /api/patient/profile/{id}
   * 
   * @param id GUID identifier of the patient record.
   */
  getPatientById(id: string): Observable<PatientProfile> {
    return this.http.get<PatientProfile>(`${this.api}/profile/${id}`);
  }

  /**
   * Updates patient demographic details.
   * Endpoint: PUT /api/patient/profile/{id}
   * 
   * @param id GUID identifier of the patient record.
   * @param request Updated profile fields.
   */
  updateProfile(id: string, request: UpdatePatientRequest): Observable<PatientProfile> {
    return this.http.put<PatientProfile>(`${this.api}/profile/${id}`, request);
  }

  /**
   * BATCH HISTORY: Retrieves completed OPD consultation encounters for the authenticated user and their family.
   * Endpoint: GET /api/patient/history
   * Powered by a single indexed SQL query on the backend.
   */
  getMyHistory(): Observable<PatientVisitHistory[]> {
    return this.http.get<PatientVisitHistory[]>(`${this.api}/history`);
  }

  /**
   * Retrieves the consultation history for a specific individual patient ID.
   * Endpoint: GET /api/patient/history/{patientId}
   * 
   * @param patientId GUID identifier of the family member.
   */
  getPatientHistory(patientId: string): Observable<PatientVisitHistory[]> {
    return this.http.get<PatientVisitHistory[]>(`${this.api}/history/${patientId}`);
  }
}
