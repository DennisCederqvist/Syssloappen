import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RewardsService } from './rewards.service';

describe('RewardsService', () => {
  let service: RewardsService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RewardsService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('sends only editable reward fields', () => {
    const body = { name: 'Godis', description: null, pointsCost: 25 };
    service.createReward(body).subscribe();
    const request = http.expectOne('/api/rewards');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush({ id: 1, ...body, createdAt: '' });
  });

  it('updates and deactivates through the reward API', () => {
    const body = { name: 'Filmkväll', description: 'På fredag', pointsCost: 50 };
    service.updateReward(3, body).subscribe();
    expect(http.expectOne('/api/rewards/3').request.body).toEqual(body);
    service.deactivateReward(3).subscribe();
    expect(http.expectOne('/api/rewards/3').request.method).toBe('DELETE');
  });
});
