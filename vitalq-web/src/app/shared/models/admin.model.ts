export interface DoctorResponse {
  id: string;
  userId: string;
  doctorName: string;
  username: string;
  password: string;
  departmentId: string;
  departmentName: string;
  specialization: string;
  roomNumber: string;
  status: 'Available' | 'OnLeave' | 'Busy';
  avgConsultationMinutes: number;
  createdAtUtc: string;
}

export interface CreateDoctorRequest {
  username: string;
  password: string;
  fullName: string;
  phoneNumber: string;
  departmentId: string;
  specialization: string;
  roomNumber: string;
  avgConsultationMinutes: number;
}

export interface UpdateDoctorRequest {
  fullName?: string;
  phoneNumber?: string;
  password?: string;
  departmentId: string;
  specialization: string;
  roomNumber: string;
  status: 'Available' | 'OnLeave' | 'Busy';
  avgConsultationMinutes: number;
}

export interface UserResponse {
  id: string;
  username: string;
  password: string;
  fullName: string;
  phoneNumber: string;
  role: 'Admin' | 'Nurse';
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  fullName: string;
  phoneNumber: string;
  role: 'Admin' | 'Nurse';
}

export interface UpdateUserRequest {
  fullName: string;
  phoneNumber: string;
  role: 'Admin' | 'Nurse';
  password?: string;
  isActive: boolean;
}

export interface DepartmentResponse {
  id: string;
  name: string;
  code: string;
  locationFloor: string;
  lastTokenNumber: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreateDepartmentRequest {
  name: string;
  code: string;
  locationFloor: string;
}

export interface UpdateDepartmentRequest {
  name: string;
  locationFloor: string;
  isActive: boolean;
}

export interface NursingStationResponse {
  id: string;
  stationName: string;
  departmentId?: string;
  departmentName?: string;
  locationFloor: string;
  isActive: boolean;
}

export interface CreateNursingStationRequest {
  stationName: string;
  departmentId?: string;
  locationFloor: string;
}

export interface UpdateNursingStationRequest {
  stationName: string;
  departmentId?: string;
  locationFloor: string;
  isActive: boolean;
}

export interface PatientVisitHistoryDto {
  tokenId: string;
  tokenNumber: string;
  departmentName: string;
  doctorName: string;
  status: string;
  triageLevel?: string;
  bookedAtUtc: string;
  triagedAtUtc?: string;
  calledAtUtc?: string;
  completedAtUtc?: string;
  consultationNotes?: string;
}

export interface PatientDetailResponse {
  id: string;
  medicalRecordNumber: string;
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string;
  gender: string;
  isRegisteredAppUser: boolean;
  createdAtUtc: string;
  visits: PatientVisitHistoryDto[];
}
