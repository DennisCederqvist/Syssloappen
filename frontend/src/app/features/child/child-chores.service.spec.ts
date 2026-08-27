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

  it('loads rewards and sends a UUID idempotency key when requesting one', () => {
    service.getRewards().subscribe();
    expect(http.expectOne('/api/child/rewards').request.method).toBe('GET');
    service.requestReward(17, '85e3d637-9016-4401-a4c1-0e974844b027').subscribe();
    const request = http.expectOne('/api/child/reward-redemptions');
    expect(request.request.body).toEqual({ rewardId: 17 });
    expect(request.request.headers.get('Idempotency-Key')).toBe('85e3d637-9016-4401-a4c1-0e974844b027');
  });
});
