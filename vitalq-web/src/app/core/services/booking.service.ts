/**
 * ============================================================================
 * SERVICE: BookingService (OPD Appointment & Sequence Slip Engine)
 * ============================================================================
 * 
 * PURPOSE:
 * Manages the patient outpatient booking lifecycle, connecting the frontend to
 * the high-concurrency booking endpoints in the ASP.NET Core 8 Web API.
 * 
 * CORE RESPONSIBILITIES:
 * 1. Fetching available clinical departments and their floor locations.
 * 2. Querying on-duty specialists for a selected department.
 * 3. Booking daily consultation slips with atomic sequence numbers (e.g., CARD-1003-001).
 * 4. Retrieving today's active tokens for live tracking.
 * 5. In-app appointment cancellations with ownership validation and audit trail.
 * ============================================================================
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Department, Doctor, QueueToken, BookTokenRequest, UpdateTokenStatusRequest } from '../../shared/models/queue.model';

export type { Department, Doctor, QueueToken, BookTokenRequest, UpdateTokenStatusRequest };

@Injectable({ providedIn: 'root' })
export class BookingService {
  /** Injected Angular HTTP client for making REST API calls */
  private http = inject(HttpClient);

  /** Base API endpoint URL from environment configuration (e.g. http://localhost:5089/api) */
  private api = environment.apiUrl;

  /**
   * Retrieves all active clinical departments in the hospital.
   * Endpoint: GET /api/public/departments
   * Used in the booking wizard to populate the specialty picker (e.g. Cardiology, Pediatrics).
   */
  getDepartments(): Observable<Department[]> {
    return this.http.get<Department[]>(`${this.api}/public/departments`);
  }

  /**
   * Retrieves all doctors assigned to a specific department.
   * Endpoint: GET /api/public/departments/{deptId}/doctors
   * Used to display on-duty specialists, their room numbers, and average consultation times.
   * 
   * @param deptId Unique GUID identifier of the clinical department.
   */
  getDoctorsByDepartment(deptId: string): Observable<Doctor[]> {
    return this.http.get<Doctor[]>(`${this.api}/public/departments/${deptId}/doctors`);
  }

  /**
   * Books a new consultation slot and generates a collision-free daily sequence slip.
   * Endpoint: POST /api/patient/bookings
   * The backend row-locks the department counter to prevent duplicate token numbers.
   * 
   * @param request Booking payload containing PatientId, DepartmentId, and DoctorId.
   */
  bookToken(request: BookTokenRequest): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.api}/patient/bookings`, request);
  }

  /**
   * Retrieves the active token for a specific patient.
   * Endpoint: GET /api/patient/bookings/active/{patientId}
   * 
   * @param patientId Optional GUID of a dependent profile; if omitted, fetches first active token.
   */
  getMyActiveToken(patientId?: string): Observable<QueueToken> {
    const url = patientId 
      ? `${this.api}/patient/bookings/active/${patientId}` 
      : `${this.api}/patient/bookings/active`;
    return this.http.get<QueueToken>(url);
  }

  /**
   * Retrieves all consultation slips registered under the logged-in user's phone for today.
   * Endpoint: GET /api/patient/bookings/active
   * Enables multi-slip tracking on the Patient Dashboard when a parent books for multiple children.
   */
  getMyTokens(): Observable<QueueToken[]> {
    return this.http.get<QueueToken[]>(`${this.api}/patient/bookings/active`);
  }

  /**
   * Cancels an active consultation slip and releases the queue spot to waiting patients.
   * Endpoint: POST /api/patient/bookings/{tokenId}/cancel
   * The backend validates patient ownership and triggers real-time queue position recalculation.
   * 
   * @param tokenId Unique GUID identifier of the QueueToken to cancel.
   * @param reason Optional patient-provided explanation for cancelling.
   */
  cancelToken(tokenId: string, reason?: string): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.api}/patient/bookings/${tokenId}/cancel`, {
      notes: reason?.trim() || ''
    });
  }

  /**
   * Public anonymous tracker for walk-in patients holding a physical paper slip.
   * Endpoint: GET /api/public/track/{tokenNumber}
   * Returns current queue position, doctor, room number, and wait estimation without login.
   * 
   * @param tokenNumber Serial code printed on physical slip (e.g. 'CARD-104')
   */
  trackToken(tokenNumber: string): Observable<QueueToken> {
    const cleanToken = encodeURIComponent(tokenNumber.trim().toUpperCase());
    return this.http.get<QueueToken>(`${this.api}/public/track/${cleanToken}`);
  }
}
