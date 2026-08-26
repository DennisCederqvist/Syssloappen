export type ChildChoreStatus = 'Assigned' | 'PendingApproval' | 'NeedsRedo' | 'Approved';

export interface ChildChoreAssignment {
  assignmentId: number;
  choreId: number;
  title: string;
  description: string | null;
  points: number;
  assignedAt: string;
  status: ChildChoreStatus;
  submittedAt: string | null;
  reviewComment: string | null;
}

export interface ChildPoints {
  totalPoints: number;
}

export interface SubmittedChildChoreAssignment {
  assignmentId: number;
  status: 'PendingApproval';
  submittedAt: string;
}
