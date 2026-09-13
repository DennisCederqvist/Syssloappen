import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ChoreRecurrence, CreateChoreRecurrenceRequest } from './chore-recurrence.models';
import {
  AdultAssignment,
  Chore,
  CreateAssignmentRequest,
  CreateChoreRequest,
  CreatedAssignment,
  ReviewAssignmentRequest,
  ReviewedAssignment,
  UpdateChoreRequest,
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

  updateChore(choreId: number, request: UpdateChoreRequest): Observable<Chore> {
    return this.http.put<Chore>(`/api/chores/${choreId}`, request);
  }

  deactivateChore(choreId: number): Observable<void> {
    return this.http.delete<void>(`/api/chores/${choreId}`);
  }

  getAssignments(includeCancelled = false): Observable<AdultAssignment[]> {
    return this.http.get<AdultAssignment[]>(
      `/api/chore-assignments${includeCancelled ? '?includeCancelled=true' : ''}`,
    );
  }

  createAssignment(request: CreateAssignmentRequest): Observable<CreatedAssignment> {
    return this.http.post<CreatedAssignment>('/api/chore-assignments', request);
  }

  cancelAssignment(assignmentId: number): Observable<void> {
    return this.http.delete<void>(`/api/chore-assignments/${assignmentId}`);
  }

  archiveAssignment(assignmentId: number): Observable<void> {
    return this.http.post<void>(`/api/chore-assignments/${assignmentId}/archive`, null);
  }

  restoreAssignment(assignmentId: number): Observable<void> {
    return this.http.post<void>(`/api/chore-assignments/${assignmentId}/restore`, null);
  }

  approveAssignment(
    assignmentId: number,
    request: ReviewAssignmentRequest,
  ): Observable<ReviewedAssignment> {
    return this.http.post<ReviewedAssignment>(
      `/api/chore-assignments/${assignmentId}/approve`,
      request,
    );
  }

  rejectAssignment(
    assignmentId: number,
    request: ReviewAssignmentRequest,
  ): Observable<ReviewedAssignment> {
    return this.http.post<ReviewedAssignment>(
      `/api/chore-assignments/${assignmentId}/reject`,
      request,
    );
  }

  getRecurrences(): Observable<ChoreRecurrence[]> {
    return this.http.get<ChoreRecurrence[]>('/api/chore-recurrences');
  }

  createRecurrence(request: CreateChoreRecurrenceRequest): Observable<ChoreRecurrence> {
    return this.http.post<ChoreRecurrence>('/api/chore-recurrences', request);
  }

  deleteRecurrence(recurrenceId: number): Observable<void> {
    return this.http.delete<void>(`/api/chore-recurrences/${recurrenceId}`);
  }
}
