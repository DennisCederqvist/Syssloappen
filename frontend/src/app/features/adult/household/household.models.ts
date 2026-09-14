export interface HouseholdAdult {
  id: string;
  email: string;
  isOwner: boolean;
  displayName: string | null;
}

export interface UpdateAdultProfileRequest {
  firstName?: string | null;
  lastName?: string | null;
  nickname?: string | null;
}

export interface ChangeAdultPasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface FamilyCodeStatus {
  isConfigured: boolean;
  maskedCode: string | null;
  updatedAt: string | null;
}

export interface RotatedFamilyCode {
  familyCode: string;
  updatedAt: string;
}

export interface AccountDeletionStatus {
  deletionScheduledAt: string | null;
}

export interface ScheduleAccountDeletionRequest {
  password: string;
}
