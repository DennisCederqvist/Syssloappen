import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { FamilyCodeStatus, RotatedFamilyCode } from './household.models';

@Injectable({ providedIn: 'root' })
export class FamilyCodeService {
  private readonly http = inject(HttpClient);

  getStatus(): Observable<FamilyCodeStatus> {
    return this.http.get<FamilyCodeStatus>('/api/household/family-code');
  }

  rotate(): Observable<RotatedFamilyCode> {
    return this.http.post<RotatedFamilyCode>('/api/household/family-code/rotate', null);
  }
}
