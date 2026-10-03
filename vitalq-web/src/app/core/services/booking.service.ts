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
   * Endpoint: GET /api/departments
   * Used in the booking wizard to populate the specialty picker (e.g. Cardiology, Pediatrics).
   */
  getDepartments(): Observable<Department[]> {
    return this.http.get<Department[]>(`${this.api}/departments`);
  }

  /**
   * Retrieves all doctors assigned to a specific department.
   * Endpoint: GET /api/departments/{deptId}/doctors
   * Used to display on-duty specialists, their room numbers, and average consultation times.
   * 
   * @param deptId Unique GUID identifier of the clinical department.
   */
  getDoctorsByDepartment(deptId: string): Observable<Doctor[]> {
    return this.http.get<Doctor[]>(`${this.api}/departments/${deptId}/doctors`);
  }

  /**
   * Books a new consultation slot and generates a collision-free daily sequence slip.
   * Endpoint: POST /api/bookings
   * The backend row-locks the department counter to prevent duplicate token numbers.
   * 
   * @param request Booking payload containing PatientId, DepartmentId, and DoctorId.
   */
  bookToken(request: BookTokenRequest): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.api}/bookings`, request);
  }

  /**
   * Retrieves the primary active token for a specific patient.
   * Endpoint: GET /api/bookings/my-token?patientId={patientId}
   * 
   * @param patientId Optional GUID of a dependent profile; if omitted, defaults to primary user.
   */
  getMyActiveToken(patientId?: string): Observable<QueueToken> {
    const url = patientId ? `${this.api}/bookings/my-token?patientId=${patientId}` : `${this.api}/bookings/my-token`;
    return this.http.get<QueueToken>(url);
  }

  /**
   * Retrieves all consultation slips registered under the logged-in user's phone for today.
   * Endpoint: GET /api/bookings/my-tokens
   * Enables the multi-slip switcher on the Patient Dashboard when a parent books for multiple children.
   */
  getMyTokens(): Observable<QueueToken[]> {
    return this.http.get<QueueToken[]>(`${this.api}/bookings/my-tokens`);
  }

  /**
   * Cancels an active consultation slip and releases the queue spot to waiting patients.
   * Endpoint: POST /api/bookings/{tokenId}/cancel
   * The backend validates patient ownership, writes a cancellation audit log, and triggers
   * a SignalR QueueUpdated broadcast so all other waiting patients' wait times decrease.
   * 
   * @param tokenId Unique GUID identifier of the QueueToken to cancel.
   * @param reason Optional patient-provided explanation for cancelling.
   */
  cancelToken(tokenId: string, reason?: string): Observable<QueueToken> {
    return this.http.post<QueueToken>(`${this.api}/bookings/${tokenId}/cancel`, {
      status: 'Cancelled',
      notes: reason?.trim() || ''
    });
  }

  /**
   * Public anonymous tracker for walk-in patients holding a physical paper slip.
   * Endpoint: GET /api/bookings/track/{tokenNumber}
   * Returns current queue position, doctor, room number, and wait estimation without login.
   * 
   * @param tokenNumber Serial code printed on physical slip (e.g. 'CARD-104')
   */
  trackToken(tokenNumber: string): Observable<QueueToken> {
    const cleanToken = encodeURIComponent(tokenNumber.trim().toUpperCase());
    return this.http.get<QueueToken>(`${this.api}/bookings/track/${cleanToken}`);
  }
}
