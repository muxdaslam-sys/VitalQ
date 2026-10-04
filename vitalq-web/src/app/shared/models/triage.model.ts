export interface TriageRequest {
  nursingStationId?: string | null;
  spO2: number;
  systolicBp: number;
  diastolicBp: number;
  heartRate: number;
  temperature: number;
  painScale: number;
  isManualOverride: boolean;
  overrideTriageLevel?: string | null;
  overrideReason?: string | null;
  nurseNotes?: string | null;
}

export interface TriageAssessment {
  id: string;
  queueTokenId: string;
  nurseUserId: string;
  nurseName: string;
  nursingStationId?: string;
  stationName?: string;
  spO2: number;
  systolicBp: number;
  diastolicBp: number;
  heartRate: number;
  temperature: number;
  painScale: number;
  triageLevel: 'Red' | 'Yellow' | 'Green';
  isManualOverride: boolean;
  overrideReason?: string;
  nurseNotes?: string;
  assessedAtUtc: string;
}

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
