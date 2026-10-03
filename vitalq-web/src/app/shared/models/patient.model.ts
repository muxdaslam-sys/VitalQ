export interface PatientProfile {
  id: string;
  userId?: string;
  medicalRecordNumber: string;
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string;
  gender: string;
  createdAtUtc: string;
}

export interface AddFamilyMemberRequest {
  fullName: string;
  dateOfBirth: string;
  gender: string;
}

export interface PatientVisitHistory {
  tokenId: string;
  tokenNumber: string;
  patientId: string;
  patientName: string;
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
