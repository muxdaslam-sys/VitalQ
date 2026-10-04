export interface Department {
  id: string;
  name: string;
  code: string;
  locationFloor: string;
  lastTokenNumber: number;
  isActive: boolean;
}

export interface Doctor {
  id: string;
  userId: string;
  doctorName: string;
  username: string;
  departmentId: string;
  departmentName: string;
  specialization: string;
  roomNumber: string;
  status: string;
  avgConsultationMinutes: number;
}

export interface QueueToken {
  id: string;
  tokenNumber: string;
  patientId: string;
  patientName: string;
  patientPhone: string;
  departmentId: string;
  departmentName: string;
  departmentCode: string;
  doctorId: string;
  doctorName: string;
  roomNumber: string;
  status: 'Booked' | 'Waiting' | 'Called' | 'Skipped' | 'Completed' | 'Cancelled';
  priorityScore: number;
  baseWeight: number;
  triageLevel?: string;
  patientsAhead: number;
  estimatedWaitMinutes: number;
  bookedAtUtc: string;
  triagedAtUtc?: string;
  calledAtUtc?: string;
  completedAtUtc?: string;
  consultationNotes?: string;
}

export interface BookTokenRequest {
  patientId: string;
  departmentId: string;
  doctorId: string;
}

export interface CompleteConsultationRequest {
  consultationNotes?: string;
}

export interface UpdateTokenStatusRequest {
  status: string;
  notes?: string;
}
