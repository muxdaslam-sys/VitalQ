/**
 * ============================================================================
 * MODEL DEFINITIONS: Patient Demographic, Family, and History Domain Models
 * ============================================================================
 * 
 * PURPOSE:
 * Strongly-typed data models representing patient demographics, dependent profiles,
 * registration requests, and clinical consultation history.
 * ============================================================================
 */

//#region 1. Patient Demographics & Dependents

/**
 * Patient demographic record with unique Medical Record Number (MRN).
 */
export interface PatientProfile {
  /** Unique GUID identifier of the patient chart */
  id: string;
  /** Linked User identity GUID (null for unlinked family dependents) */
  userId?: string;
  /** Official Hospital Medical Record Number (e.g., 'MRN-492019') */
  medicalRecordNumber: string;
  /** Full legal name of the patient */
  fullName: string;
  /** Primary contact phone number */
  phoneNumber: string;
  /** ISO 8601 Date of birth (YYYY-MM-DD) */
  dateOfBirth: string;
  /** Biological gender: 'Male', 'Female', 'Other' */
  gender: string;
  /** UTC timestamp of profile creation */
  createdAtUtc: string;
}

/**
 * Payload for adding a new family dependent under the logged-in user's account.
 */
export interface AddFamilyMemberRequest {
  /** Full legal name of the dependent */
  fullName: string;
  /** Date of birth (YYYY-MM-DD) */
  dateOfBirth: string;
  /** Biological gender */
  gender: string;
}

/**
 * Self-registration payload for new patients signing up online.
 */
export interface SelfRegisterRequest {
  /** Full legal name */
  fullName: string;
  /** Primary phone number (used as username) */
  phoneNumber: string;
  /** Date of birth (used as initial password in ddMMyyyy format) */
  dateOfBirth: string;
  /** Optional gender specification */
  gender?: string;
}

//#endregion

//#region 2. Clinical Encounter & Visit History

/**
 * Summary record of a past outpatient consultation encounter.
 */
export interface PatientVisitHistory {
  /** Consultation token GUID */
  tokenId: string;
  /** Daily sequence token number (e.g., 'CARD-104') */
  tokenNumber: string;
  /** Patient chart GUID */
  patientId: string;
  /** Full name of the patient */
  patientName: string;
  /** Department where consultation took place */
  departmentName: string;
  /** Consulting physician */
  doctorName: string;
  /** Final encounter status ('Completed', 'Cancelled', 'Expired', 'Skipped') */
  status: string;
  /** Nurse triage urgency level ('Red', 'Yellow', 'Green') */
  triageLevel?: string;
  /** UTC timestamp of appointment booking */
  bookedAtUtc: string;
  /** UTC timestamp of nurse vitals intake */
  triagedAtUtc?: string;
  /** UTC timestamp when doctor called the token */
  calledAtUtc?: string;
  /** UTC timestamp when encounter was finalized */
  completedAtUtc?: string;
  /** Doctor's clinical advice, diagnosis, or cancellation note */
  consultationNotes?: string;
}

//#endregion
