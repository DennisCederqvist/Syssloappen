import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ChildChoreAssignment,
  ChildPoints,
  SubmittedChildChoreAssignment,
  ChildRewards,
  RewardRedemption,
} from './child-chores.models';

@Injectable({ providedIn: 'root' })
export class ChildChoresService {
  private readonly http = inject(HttpClient);

  getAssignments(): Observable<ChildChoreAssignment[]> {
    return this.http.get<ChildChoreAssignment[]>('/api/child/chore-assignments');
  }

  getRewards(): Observable<ChildRewards> { return this.http.get<ChildRewards>('/api/child/rewards'); }

  requestReward(rewardId: number, idempotencyKey: string): Observable<RewardRedemption> {
    return this.http.post<RewardRedemption>('/api/child/reward-redemptions', { rewardId }, { headers: { 'Idempotency-Key': idempotencyKey } });
  }

  getRewardRedemptions(): Observable<RewardRedemption[]> {
    return this.http.get<RewardRedemption[]>('/api/child/reward-redemptions');
  }

  getPoints(): Observable<ChildPoints> {
    return this.http.get<ChildPoints>('/api/child/points');
  }

  submitAssignment(assignmentId: number): Observable<SubmittedChildChoreAssignment> {
    // The assignment id belongs in the route. No ownership, status, points or timestamps
    // are sent because the authenticated Child session and backend choose those values.
    return this.http.post<SubmittedChildChoreAssignment>(
      `/api/child/chore-assignments/${assignmentId}/submit`,
      null,
    );
  }
}
