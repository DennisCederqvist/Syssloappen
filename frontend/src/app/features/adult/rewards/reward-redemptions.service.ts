import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface AdultRewardRedemption {
  id: number;
  childId: number;
  childName: string;
  rewardId: number;
  rewardName: string;
  pointsCost: number;
  status: 'Requested' | 'Approved' | 'Cancelled' | 'Delivered';
  requestedAt: string;
  reviewedAt: string | null;
  deliveredAt: string | null;
  comment: string | null;
}

@Injectable({ providedIn: 'root' })
export class RewardRedemptionsService {
  private readonly http = inject(HttpClient);

  get(): Observable<AdultRewardRedemption[]> {
    return this.http.get<AdultRewardRedemption[]>('/api/reward-redemptions');
  }

  change(id: number, action: 'approve' | 'cancel' | 'deliver', comment: string | null): Observable<AdultRewardRedemption> {
    return this.http.post<AdultRewardRedemption>(`/api/reward-redemptions/${id}/${action}`, { comment });
  }
}
