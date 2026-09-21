import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ChildDeviceSession,
  ChildPairingCode,
  ChildSummary,
  ChildWithPoints,
  CreateChildRequest,
  CreatedChild,
  UpdateChildRequest,
} from './children.models';

@Injectable({ providedIn: 'root' })
export class ChildrenService {
  private readonly http = inject(HttpClient);

  getActiveChildren(): Observable<ChildWithPoints[]> {
    return this.http.get<ChildWithPoints[]>('/api/children');
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

  updateChild(childId: number, request: UpdateChildRequest): Observable<ChildSummary> {
    return this.http.put<ChildSummary>(`/api/children/${childId}`, request);
  }

  deactivateChild(childId: number): Observable<void> {
    return this.http.delete<void>(`/api/children/${childId}`);
  }

  uploadPhoto(childId: number, file: File): Observable<ChildSummary> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ChildSummary>(`/api/children/${childId}/photo`, formData);
  }

  deletePhoto(childId: number): Observable<ChildSummary> {
    return this.http.delete<ChildSummary>(`/api/children/${childId}/photo`);
  }
}
