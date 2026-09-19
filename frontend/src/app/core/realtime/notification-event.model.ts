export type NotificationEventType =
  | 'ChoreAssigned'
  | 'ChoreApproved'
  | 'ChoreNeedsRedo'
  | 'ChoreSubmittedForReview'
  | 'RewardRequested'
  | 'RewardApproved'
  | 'ChoresChanged'
  | 'RewardsChanged';

export interface ChoreAssignedData {
  assignmentId: number;
  choreTitle: string;
  points: number;
}

export interface ChoreApprovedData {
  assignmentId: number;
  choreTitle: string;
  points: number;
}

export interface ChoreNeedsRedoData {
  assignmentId: number;
  choreTitle: string;
}

export interface ChoreSubmittedForReviewData {
  assignmentId: number;
  choreTitle: string;
  childName: string;
}

export interface RewardRequestedData {
  redemptionId: number;
  rewardName: string;
  childName: string;
}

export interface RewardApprovedData {
  redemptionId: number;
  rewardName: string;
}

export interface NotificationEvent {
  type: NotificationEventType;
  data: unknown;
}
