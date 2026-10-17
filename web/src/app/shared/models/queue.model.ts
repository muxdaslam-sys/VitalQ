/**
 * ============================================================================
 * MODEL DEFINITIONS: Queue & Appointment Token Domain Entities
 * ============================================================================
 * 
 * PURPOSE:
 * Strongly-typed data models representing outpatient consultation tokens,
 * clinical departments, doctors on duty, and status transitions.
 * ============================================================================
 */

//#region 1. Department & Doctor Models

/**
 * Clinical Outpatient Department (e.g., Cardiology, Pediatrics, General Medicine).
 */
export interface Department {
  /** Unique GUID identifier of the department */
  id: string;
  /** Display name of the department */
  name: string;
  /** Unique short code prefix for tokens (e.g., 'CARD', 'PED') */
  code: string;
  /** Hospital building floor location (e.g., 'Floor 1', 'Ground Floor') */
  locationFloor: string;
  /** Monotonically incrementing daily token sequence counter */
  lastTokenNumber: number;
  /** Whether the department is actively accepting patients */
  isActive: boolean;
}

/**
 * Consulting Physician profile with schedule and room assignment.
 */
export interface Doctor {
  /** Unique GUID identifier of the doctor */
  id: string;
  /** User account GUID linked to this doctor */
  userId: string;
  /** Full clinical name (e.g., 'Dr. Sarah Connor') */
  doctorName: string;
  /** Login username / email */
  username: string;
  /** Assigned primary department GUID */
  departmentId: string;
  /** Department name */
  departmentName: string;
  /** Clinical medical specialty */
  specialization: string;
  /** Consultation room or chamber number (e.g., 'Room 101') */
  roomNumber: string;
  /** Current duty availability: 'Available', 'InConsultation', 'OnBreak', 'OffDuty' */
  status: string;
  /** Average consultation duration in minutes used for wait time estimates */
  avgConsultationMinutes: number;
}

/**
 * Shift performance metrics for the consulting doctor's room console.
 */
export interface DoctorDashboardStats {
  doctorId: string;
  doctorName: string;
  roomNumber: string;
  departmentName: string;
  status: string; // 'Available' | 'Busy' | 'OnLeave'
  totalAssignedToday: number;
  completedToday: number;
  waitingCount: number;
  skippedCount: number;
  avgConsultationMinutes: number;
  currentCalledTokenNumber?: string | null;
}

//#endregion

//#region 2. Queue Token & Navigation Models

/**
 * Live Outpatient Consultation Token slip with queue positioning and wayfinding guidance.
 */
export interface QueueToken {
  /** Unique GUID identifier of the token */
  id: string;
  /** Formatted daily token number (e.g., 'CARD-104') */
  tokenNumber: string;
  /** Patient chart GUID */
  patientId: string;
  /** Full name of the patient */
  patientName: string;
  /** Primary contact phone number */
  patientPhone: string;
  /** Assigned department GUID */
  departmentId: string;
  /** Department display name */
  departmentName: string;
  /** Department prefix code */
  departmentCode: string;
  /** Assigned consulting doctor GUID */
  doctorId: string;
  /** Consulting doctor's full name */
  doctorName: string;
  /** Consultation room number */
  roomNumber: string;
  /**
   * Lifecycle stage of the token:
   * - 'Booked': Token issued; awaiting nurse vitals intake.
   * - 'Waiting': Triaged and queued; dynamically gaining priority.
   * - 'Called': Doctor has summoned the patient to the consultation room.
   * - 'Skipped': Patient absent when called; held temporarily on hold.
   * - 'Completed': Consultation finished and closed by physician.
   * - 'Cancelled': Appointment cancelled by patient or hospital staff.
   * - 'Expired': Patient did not arrive before end-of-day cutoff.
   */
  status: 'Booked' | 'Waiting' | 'Called' | 'Skipped' | 'Completed' | 'Cancelled' | 'Expired';
  /** Dynamically computed score (BaseWeight + Waiting Minutes * Rate) */
  priorityScore: number;
  /** Clinical urgency weight assigned during nurse triage (0 to 100) */
  baseWeight: number;
  /** Emergency severity level: 'Red' (Immediate), 'Yellow' (Urgent), 'Green' (Standard) */
  triageLevel?: string;
  /** Real-time number of patients queued ahead for the same doctor */
  patientsAhead: number;
  /** Dynamic estimated wait time in minutes */
  estimatedWaitMinutes: number;
  /** Cumulative number of times patient was skipped (0, 1, 2, or 3) */
  skipCount?: number;
  /** UTC timestamp when the appointment was originally booked */
  bookedAtUtc: string;
  /** UTC timestamp when nurse recorded vitals */
  triagedAtUtc?: string;
  /** UTC timestamp when doctor called the token */
  calledAtUtc?: string;
  /** UTC timestamp when consultation completed */
  completedAtUtc?: string;
  /** Physician's clinical notes, advice, or cancellation reason */
  consultationNotes?: string;
  /** ID of the nursing station assigned for triage vitals */
  nursingStationId?: string;
  /** Display name of the nursing station (e.g., 'Triage Desk A') */
  nursingStationName?: string;
  /** Floor location for physical patient wayfinding */
  locationFloor?: string;
  /** Human-readable wayfinding instruction based on current queue stage */
  nextStepInstruction?: string;
}

//#endregion

//#region 3. Request Payloads

/**
 * Payload for booking a new outpatient consultation slot.
 */
export interface BookTokenRequest {
  patientId: string;
  departmentId: string;
  doctorId: string;
}

/**
 * Payload for completing an active consultation with physician notes.
 */
export interface CompleteConsultationRequest {
  consultationNotes?: string;
}

/**
 * Generic request for manual token status transitions.
 */
export interface UpdateTokenStatusRequest {
  status: string;
  notes?: string;
}

/**
 * Request payload for toggling physician duty availability.
 */
export interface UpdateDoctorStatusRequest {
  status: string;
}

//#endregion
