export interface Chore {
  id: number;
  title: string;
  description: string | null;
  points: 5 | 10 | 15 | 20;
  createdAt: string;
}

export interface CreateChoreRequest {
  title: string;
  description: string | null;
  points: number;
}

export type UpdateChoreRequest = CreateChoreRequest;

export interface CreateAssignmentRequest {
  choreId: number;
  childId: number;
  dueDate: string;
}

export interface CreatedAssignment {
  id: number;
  choreId: number;
  childId: number;
  points: number;
  assignedAt: string;
  dueDate: string;
}

export interface AdultAssignment {
  assignmentId: number;
  choreId: number;
  choreTitle: string;
  childId: number;
  childName: string;
  points: number;
  assignedAt: string;
  dueDate: string;
  status: 'Assigned' | 'PendingApproval' | 'NeedsRedo' | 'Approved' | 'Cancelled';
  submittedAt: string | null;
  reviewedByUserId: string | null;
  reviewedAt: string | null;
  reviewComment: string | null;
  cancelledByUserId: string | null;
  cancelledAt: string | null;
  adultArchivedAt: string | null;
}

export interface ReviewAssignmentRequest {
  comment: string | null;
}

export interface ReviewedAssignment {
  assignmentId: number;
  status: 'Approved' | 'NeedsRedo';
  reviewedAt: string;
  reviewComment: string | null;
  pointsAwarded: number | null;
}
