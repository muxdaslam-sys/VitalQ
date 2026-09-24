export interface UserResponse {
  id: string;
  username: string;
  fullName: string;
  phoneNumber: string;
  role: 'Admin' | 'Doctor' | 'Nurse' | 'Patient';
  isActive: boolean;
  createdAtUtc: string;
}

export interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  refreshToken?: string;
  user: UserResponse;
}

export interface LoginRequest {
  username: string;
  password: string;
}
