import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AdultAssignment,
  Chore,
  CreateAssignmentRequest,
  CreateChoreRequest,
  CreatedAssignment,
} from './chores.models';

@Injectable({ providedIn: 'root' })
export class ChoresService {
  private readonly http = inject(HttpClient);

  getChores(): Observable<Chore[]> {
    return this.http.get<Chore[]>('/api/chores');
  }

  createChore(request: CreateChoreRequest): Observable<Chore> {
    return this.http.post<Chore>('/api/chores', request);
  }

  getAssignments(): Observable<AdultAssignment[]> {
    return this.http.get<AdultAssignment[]>('/api/chore-assignments');
  }

  createAssignment(request: CreateAssignmentRequest): Observable<CreatedAssignment> {
    return this.http.post<CreatedAssignment>('/api/chore-assignments', request);
  }
}
