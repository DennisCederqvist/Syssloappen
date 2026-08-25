import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ChildSummary, CreateChildRequest, CreatedChild } from './children.models';

@Injectable({ providedIn: 'root' })
export class ChildrenService {
  private readonly http = inject(HttpClient);

  getActiveChildren(): Observable<ChildSummary[]> {
    return this.http.get<ChildSummary[]>('/api/children');
  }

  createChild(request: CreateChildRequest): Observable<CreatedChild> {
    return this.http.post<CreatedChild>('/api/children', request);
  }
}
