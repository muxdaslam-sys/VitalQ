import { TriageAssessment } from './triage.model';

export interface DoctorQueueSummaryDto {
  doctorId: string;
  doctorName: string;
  specialization: string;
  roomNumber: string;
  status: 'Available' | 'Busy' | 'OnLeave' | string;
  currentCallingToken?: string | null;
  waitingCount: number;
  totalAssignedToday: number;
}

export interface NurseDashboardStatsResponse {
  totalBookingsToday: number;
  totalTriagedToday: number;
  totalCompletedToday: number;
  doctorsPresent: number;
  doctorsAbsent: number;
  doctors: DoctorQueueSummaryDto[];
}

export interface DoctorQueueItemDto {
  tokenId: string;
  tokenNumber: string;
  patientId: string;
  patientName: string;
  phoneNumber: string;
  status: string;
  priorityScore: number;
  triageLevel?: 'Red' | 'Yellow' | 'Green' | null;
  bookedAtUtc: string;
  triagedAtUtc?: string | null;
  calledAtUtc?: string | null;
  triageAssessment?: TriageAssessment | null;
}

export interface DoctorDetailedQueueResponse {
  doctorId: string;
  doctorName: string;
  specialization: string;
  roomNumber: string;
  status: string;
  queue: DoctorQueueItemDto[];
}

export interface PatientQueuePositionResponse {
  tokenId: string;
  tokenNumber: string;
  patientId: string;
  patientName: string;
  status: string;
  queuePosition: number;
  patientsAhead: number;
  estimatedWaitMinutes: number;
  doctorId: string;
  doctorName: string;
  specialization: string;
  roomNumber: string;
  currentlyCallingToken?: string | null;
  triageLevel?: string | null;
  priorityScore: number;
  doctorLiveQueue: DoctorQueueItemDto[];
}

export interface EmergencyWalkInBookingRequest {
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string;
  gender: string;
  doctorId: string;
  isEmergencyCodeRed: boolean;
  clinicalNotes?: string;
}

export interface ExistingPatientTokenBookingRequest {
  doctorId: string;
  isEmergencyCodeRed: boolean;
  clinicalNotes?: string;
}


