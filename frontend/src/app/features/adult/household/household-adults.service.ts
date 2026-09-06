import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ChangeAdultPasswordRequest,
  HouseholdAdult,
  UpdateAdultProfileRequest,
} from './household.models';

@Injectable({ providedIn: 'root' })
export class HouseholdAdultsService {
  private readonly http = inject(HttpClient);

  list(): Observable<HouseholdAdult[]> {
    return this.http.get<HouseholdAdult[]>('/api/household/adults');
  }

  disconnect(userId: string): Observable<void> {
    return this.http.delete<void>(`/api/household/adults/${userId}`);
  }

  updateOwnProfile(request: UpdateAdultProfileRequest): Observable<HouseholdAdult> {
    return this.http.put<HouseholdAdult>('/api/household/adults/me', request);
  }

  changeOwnPassword(request: ChangeAdultPasswordRequest): Observable<void> {
    return this.http.post<void>('/api/household/adults/me/change-password', request);
  }
}
