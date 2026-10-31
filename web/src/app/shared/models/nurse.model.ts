import { TriageAssessment } from './triage.model';

// #region 1. Dashboard Metrics & Doctor Queue Roster

/**
 * Summary card representation of a physician's live consultation queue.
 */
export interface DoctorQueueSummaryDto {
  doctorId: string;
  doctorName: string;
  specialization: string;
  roomNumber: string;
  status: 'Available' | 'Busy' | 'OnLeave' | string;
  currentCallingToken?: string | null;
  waitingCount: number;
  totalAssignedToday: number;
  skippedCount: number;
}

/**
 * Top-level response returned by GET /api/nurse/dashboard/stats for station command center.
 */
export interface NurseDashboardStatsResponse {
  totalBookingsToday: number;
  totalTriagedToday: number;
  totalCompletedToday: number;
  totalSkippedToday: number;
  doctorsPresent: number;
  doctorsAbsent: number;
  doctors: DoctorQueueSummaryDto[];
}

/**
 * Token queue entry inside a doctor's active consultation line.
 */
export interface DoctorQueueItemDto {
  tokenId: string;
  tokenNumber: string;
  patientId: string;
  patientName: string;
  phoneNumber: string;
  status: string;
  priorityScore: number;
  triageLevel?: 'Red' | 'Yellow' | 'Green' | null;
  skipCount?: number;
  bookedAtUtc: string;
  triagedAtUtc?: string | null;
  calledAtUtc?: string | null;
  triageAssessment?: TriageAssessment | null;
}

/**
 * Detailed modal response with full ordered queue for a clicked doctor.
 */
export interface DoctorDetailedQueueResponse {
  doctorId: string;
  doctorName: string;
  specialization: string;
  roomNumber: string;
  status: string;
  queue: DoctorQueueItemDto[];
}

// #endregion

// #region 2. Patient Real-Time Queue Position & Visualizer

/**
 * Detailed real-time tracking data for a patient's position in the clinic line.
 */
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

// #endregion

// #region 3. Emergency Walk-in & Existing Patient Token Booking Payloads

/**
 * Payload sent to register a new/returning walk-in patient at the station desk.
 */
export interface EmergencyWalkInBookingRequest {
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string;
  gender: string;
  doctorId: string;
  isEmergencyCodeRed: boolean;
  clinicalNotes?: string;
}

/**
 * Streamlined booking payload for registered patients without demographic re-entry.
 */
export interface ExistingPatientTokenBookingRequest {
  doctorId: string;
  isEmergencyCodeRed: boolean;
  clinicalNotes?: string;
}

// #endregion


