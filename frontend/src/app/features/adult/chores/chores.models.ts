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

export interface CreateAssignmentRequest {
  choreId: number;
  childId: number;
}

export interface CreatedAssignment {
  id: number;
  choreId: number;
  childId: number;
  points: number;
  assignedAt: string;
}

export interface AdultAssignment {
  assignmentId: number;
  choreId: number;
  choreTitle: string;
  childId: number;
  childName: string;
  points: number;
  assignedAt: string;
  status: 'Assigned' | 'PendingApproval' | 'NeedsRedo' | 'Approved';
  submittedAt: string | null;
  reviewedByUserId: string | null;
  reviewedAt: string | null;
  reviewComment: string | null;
}
