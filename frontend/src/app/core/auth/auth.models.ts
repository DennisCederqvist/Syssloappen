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
export interface ChildLoginRequest {
  familyCode: string;
  userName: string;
  password: string;
}
export interface ChildPairingRequest {
  code: string;
}
