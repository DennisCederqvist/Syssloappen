import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ChoresService } from './chores.service';

describe('ChoresService', () => {
  let service: ChoresService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ChoresService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads household chores', () => {
    service.getChores().subscribe();
    expect(http.expectOne('/api/chores').request.method).toBe('GET');
  });

  it('creates a chore without owner or household fields', () => {
    service.createChore({ title: 'Mata katten', description: null, points: 10 }).subscribe();
    const request = http.expectOne('/api/chores');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ title: 'Mata katten', description: null, points: 10 });
    request.flush({ id: 1, title: 'Mata katten', description: null, points: 10, createdAt: '' });
  });

  it('loads adult assignments', () => {
    service.getAssignments().subscribe();
    expect(http.expectOne('/api/chore-assignments').request.method).toBe('GET');
  });

  it('updates only editable chore fields', () => {
    const body = { title: 'Mata katten nu', description: 'På morgonen', points: 15 };
    service.updateChore(3, body).subscribe();
    const request = http.expectOne('/api/chores/3');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(body);
    request.flush({ id: 3, ...body, createdAt: '' });
  });

  it('deactivates a chore through the backend', () => {
    service.deactivateChore(3).subscribe();
    expect(http.expectOne('/api/chores/3').request.method).toBe('DELETE');
  });

  it('assigns a chore, child and date', () => {
    service.createAssignment({ choreId: 3, childId: 7, dueDate: '2026-08-27' }).subscribe();
    const request = http.expectOne('/api/chore-assignments');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ choreId: 3, childId: 7, dueDate: '2026-08-27' });
    request.flush({ id: 9, choreId: 3, childId: 7, points: 10, assignedAt: '' });
  });

  it('cancels an assignment through the backend', () => {
    service.cancelAssignment(9).subscribe();
    expect(http.expectOne('/api/chore-assignments/9').request.method).toBe('DELETE');
  });

  it('approves with only the optional review comment', () => {
    service.approveAssignment(9, { comment: 'Bra jobbat!' }).subscribe();
    const request = http.expectOne('/api/chore-assignments/9/approve');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ comment: 'Bra jobbat!' });
  });

  it('rejects with only a nullable review comment', () => {
    service.rejectAssignment(9, { comment: null }).subscribe();
    const request = http.expectOne('/api/chore-assignments/9/reject');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ comment: null });
  });
});
