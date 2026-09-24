import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  DoctorResponse, CreateDoctorRequest, UpdateDoctorRequest,
  DepartmentResponse, CreateDepartmentRequest, UpdateDepartmentRequest,
  NursingStationResponse, CreateNursingStationRequest, UpdateNursingStationRequest,
  PatientDetailResponse,
  UserResponse, CreateUserRequest, UpdateUserRequest
} from '../../shared/models/admin.model';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl + '/admin';

  // Staff & Admin Users
  getUsers(): Observable<UserResponse[]> {
    return this.http.get<UserResponse[]>(this.baseUrl + '/users');
  }

  createUser(req: CreateUserRequest): Observable<UserResponse> {
    return this.http.post<UserResponse>(this.baseUrl + '/users', req);
  }

  updateUser(id: string, req: UpdateUserRequest): Observable<UserResponse> {
    return this.http.put<UserResponse>(this.baseUrl + '/users/' + id, req);
  }

  // Doctors
  getDoctors(): Observable<DoctorResponse[]> {
    return this.http.get<DoctorResponse[]>(this.baseUrl + '/doctors');
  }

  createDoctor(req: CreateDoctorRequest): Observable<DoctorResponse> {
    return this.http.post<DoctorResponse>(this.baseUrl + '/doctors', req);
  }

  updateDoctor(id: string, req: UpdateDoctorRequest): Observable<DoctorResponse> {
    return this.http.put<DoctorResponse>(this.baseUrl + '/doctors/' + id, req);
  }

  toggleDoctorStatus(id: string, status: 'Available' | 'OnLeave' | 'Busy'): Observable<DoctorResponse> {
    return this.http.patch<DoctorResponse>(this.baseUrl + '/doctors/' + id + '/status', { status });
  }

  // Departments
  getDepartments(): Observable<DepartmentResponse[]> {
    return this.http.get<DepartmentResponse[]>(this.baseUrl + '/departments');
  }

  createDepartment(req: CreateDepartmentRequest): Observable<DepartmentResponse> {
    return this.http.post<DepartmentResponse>(this.baseUrl + '/departments', req);
  }

  updateDepartment(id: string, req: UpdateDepartmentRequest): Observable<DepartmentResponse> {
    return this.http.put<DepartmentResponse>(this.baseUrl + '/departments/' + id, req);
  }

  // Nursing Stations
  getNursingStations(departmentId?: string): Observable<NursingStationResponse[]> {
    const url = departmentId
      ? this.baseUrl + '/nursing-stations?departmentId=' + departmentId
      : this.baseUrl + '/nursing-stations';
    return this.http.get<NursingStationResponse[]>(url);
  }

  createNursingStation(req: CreateNursingStationRequest): Observable<NursingStationResponse> {
    return this.http.post<NursingStationResponse>(this.baseUrl + '/nursing-stations', req);
  }

  updateNursingStation(id: string, req: UpdateNursingStationRequest): Observable<NursingStationResponse> {
    return this.http.put<NursingStationResponse>(this.baseUrl + '/nursing-stations/' + id, req);
  }

  // Patient Directory
  getPatients(): Observable<PatientDetailResponse[]> {
    return this.http.get<PatientDetailResponse[]>(this.baseUrl + '/patients');
  }

  getPatientById(id: string): Observable<PatientDetailResponse> {
    return this.http.get<PatientDetailResponse>(this.baseUrl + '/patients/' + id);
  }
}
