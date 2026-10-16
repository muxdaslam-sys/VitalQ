/**
 * ============================================================================
 * MODEL DEFINITIONS: Authentication & User Identity Domain Models
 * ============================================================================
 * 
 * PURPOSE:
 * Strongly-typed data models representing user session payloads, credentials,
 * and authenticated user profiles across clinical and patient portals.
 * ============================================================================
 */

//#region 1. User Profile & Account Data

/**
 * Sanitized user identity profile returned in responses. Never includes password.
 */
export interface UserResponse {
  /** Unique GUID identifier of the user */
  id: string;
  /** Login username or registered mobile number */
  username: string;
  /** Full clinical or legal name */
  fullName: string;
  /** Contact phone number */
  phoneNumber: string;
  /** Hospital access role */
  role: 'Admin' | 'Doctor' | 'Nurse' | 'Patient';
  /** Assigned nursing station GUID (mandatory for Nurse role) */
  nursingStationId?: string | null;
  /** Display name of the assigned nursing station */
  nursingStationName?: string | null;
  /** Account active status flag */
  isActive: boolean;
  /** UTC timestamp of account creation */
  createdAtUtc: string;
}

//#endregion

//#region 2. Session Tokens & Authentication Payloads

/**
 * Authentication response payload returned on successful login or silent token rotation.
 */
export interface AuthResponse {
  /** Signed JWT Bearer token used for API Authorization headers */
  accessToken: string;
  /** Expiration timestamp in UTC */
  expiresAtUtc: string;
  /** Optional refresh token (persisted in secure HttpOnly cookie) */
  refreshToken?: string;
  /** Authenticated user profile */
  user: UserResponse;
}

/**
 * Login credentials sent from client login forms.
 */
export interface LoginRequest {
  /** Username or registered phone number */
  username: string;
  /** Account password or DOB PIN */
  password: string;
}

//#endregion
