// #region 1. Clinical Triage Intake Request

/**
 * Payload submitted by nurse at the triage desk when recording patient vital signs.
 */
export interface TriageRequest {
  nursingStationId?: string | null;
  spO2?: number | null;
  systolicBp?: number | null;
  diastolicBp?: number | null;
  heartRate?: number | null;
  temperature?: number | null;
  painScale?: number | null;
  isManualOverride: boolean;
  overrideTriageLevel?: string | null;
  overrideReason?: string | null;
  nurseNotes?: string | null;
}

// #endregion

// #region 2. Clinical Triage Assessment Response

/**
 * Recorded clinical assessment entity including assigned nurse and CTAS urgency score.
 */
export interface TriageAssessment {
  id: string;
  queueTokenId: string;
  nurseUserId: string;
  nurseName: string;
  nursingStationId?: string;
  stationName?: string;
  spO2?: number | null;
  systolicBp?: number | null;
  diastolicBp?: number | null;
  heartRate?: number | null;
  temperature?: number | null;
  painScale?: number | null;
  triageLevel: 'Red' | 'Yellow' | 'Green';
  isManualOverride: boolean;
  overrideReason?: string;
  nurseNotes?: string;
  assessedAtUtc: string;
}

// #endregion

// #region 3. Intake Patient Search Result

/**
 * Search result item returned when looking up patients by Token, MRN, Name, or Phone.
 */
export interface PatientSearchResult {
  patientId: string;
  medicalRecordNumber: string;
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string;
  gender: string;
  activeTokenId?: string;
  activeTokenNumber?: string;
  activeTokenStatus?: string;
}

// #endregion
