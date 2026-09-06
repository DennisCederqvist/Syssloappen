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
