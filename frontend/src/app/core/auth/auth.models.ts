export type UserRole = 'Adult' | 'Child';

export interface CurrentUser {
  userId?: string;
  email?: string | null;
  childId?: number;
  name?: string;
  userName?: string;
  role: UserRole;
  householdId: number;
}

export interface AdultLoginRequest {
  email: string;
  password: string;
}
export interface RegisterAdultRequest {
  householdName: string;
  email: string;
  password: string;
}
export interface RegisterInvitedAdultRequest {
  invitationCode: string;
  email: string;
  password: string;
}
export interface RegisterInvitedAdultResponse {
  email: string;
  role: 'Adult';
  householdId: number;
}
export interface HouseholdInvitation {
  code: string;
  expiresAt: string;
}
export interface RegisterAdultResponse {
  householdId: number;
  email: string;
  role: 'Adult';
  familyCode: string;
}
export interface ChildLoginRequest {
  familyCode: string;
  userName: string;
  password: string;
}
export interface ChildPairingRequest {
  code: string;
}
