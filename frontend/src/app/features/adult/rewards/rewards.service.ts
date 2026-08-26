import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Reward, RewardRequest } from './rewards.models';

@Injectable({ providedIn: 'root' })
export class RewardsService {
  private readonly http = inject(HttpClient);

  getRewards(): Observable<Reward[]> {
    return this.http.get<Reward[]>('/api/rewards');
  }
  createReward(request: RewardRequest): Observable<Reward> {
    return this.http.post<Reward>('/api/rewards', request);
  }
  updateReward(id: number, request: RewardRequest): Observable<Reward> {
    return this.http.put<Reward>(`/api/rewards/${id}`, request);
  }
  deactivateReward(id: number): Observable<void> {
    return this.http.delete<void>(`/api/rewards/${id}`);
  }
}
