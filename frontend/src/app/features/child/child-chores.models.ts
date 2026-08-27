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

export interface ChildReward { id: number; name: string; description: string | null; pointsCost: number; }
export interface ChildRewards { availablePoints: number; rewards: ChildReward[]; }
export type RewardRedemptionStatus = 'Requested' | 'Approved' | 'Cancelled' | 'Delivered';

export interface RewardRedemption {
  id: number;
  rewardId: number;
  rewardName: string;
  pointsCost: number;
  status: RewardRedemptionStatus;
  requestedAt: string;
  reviewedAt: string | null;
  deliveredAt: string | null;
  comment: string | null;
  availablePoints: number;
}
