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

  it('assigns only a chore and child id', () => {
    service.createAssignment({ choreId: 3, childId: 7 }).subscribe();
    const request = http.expectOne('/api/chore-assignments');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ choreId: 3, childId: 7 });
    request.flush({ id: 9, choreId: 3, childId: 7, points: 10, assignedAt: '' });
  });
});
