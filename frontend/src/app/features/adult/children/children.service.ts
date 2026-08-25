import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ChildDeviceSession,
  ChildPairingCode,
  ChildSummary,
  CreateChildRequest,
  CreatedChild,
} from './children.models';

@Injectable({ providedIn: 'root' })
export class ChildrenService {
  private readonly http = inject(HttpClient);

  getActiveChildren(): Observable<ChildSummary[]> {
    return this.http.get<ChildSummary[]>('/api/children');
  }

  createChild(request: CreateChildRequest): Observable<CreatedChild> {
    return this.http.post<CreatedChild>('/api/children', request);
  }

  createPairingCode(childId: number): Observable<ChildPairingCode> {
    return this.http.post<ChildPairingCode>(`/api/children/${childId}/pairing-codes`, {});
  }

  getDeviceSessions(childId: number): Observable<ChildDeviceSession[]> {
    return this.http.get<ChildDeviceSession[]>(`/api/children/${childId}/device-sessions`);
  }

  revokeDeviceSession(childId: number, sessionId: string): Observable<void> {
    return this.http.delete<void>(`/api/children/${childId}/device-sessions/${sessionId}`);
  }
}
