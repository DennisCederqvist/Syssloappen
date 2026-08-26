import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ChildChoresService } from './child-chores.service';

describe('ChildChoresService', () => {
  let service: ChildChoresService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ChildChoresService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads assignments for the authenticated child without query parameters', () => {
    service.getAssignments().subscribe();
    const request = http.expectOne('/api/child/chore-assignments');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.keys()).toEqual([]);
  });

  it('loads the authenticated child points', () => {
    service.getPoints().subscribe();
    expect(http.expectOne('/api/child/points').request.method).toBe('GET');
  });

  it('submits only the assignment route id and an empty body', () => {
    service.submitAssignment(17).subscribe();
    const request = http.expectOne('/api/child/chore-assignments/17/submit');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
  });
});
