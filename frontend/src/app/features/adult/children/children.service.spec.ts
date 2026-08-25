import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ChildrenService } from './children.service';

describe('ChildrenService', () => {
  let service: ChildrenService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ChildrenService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the authenticated household active children', () => {
    service.getActiveChildren().subscribe((children) => expect(children).toHaveLength(2));
    const request = http.expectOne('/api/children');
    expect(request.request.method).toBe('GET');
    request.flush([
      { id: 1, name: 'Maja' },
      { id: 2, name: 'Leo' },
    ]);
  });

  it('creates a child without sending household or role fields', () => {
    service.createChild({ name: 'Maja', userName: 'maja', password: 'Secret12' }).subscribe();
    const request = http.expectOne('/api/children');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Maja',
      userName: 'maja',
      password: 'Secret12',
    });
    request.flush({ id: 1, name: 'Maja', userName: 'maja', role: 'Child' });
  });

  it('creates a pairing code for the selected child', () => {
    service.createPairingCode(42).subscribe((result) => expect(result.code).toBe('ABC234XY'));
    const request = http.expectOne('/api/children/42/pairing-codes');
    expect(request.request.method).toBe('POST');
    request.flush({ code: 'ABC234XY', expiresAt: '2026-08-25T20:10:00Z' });
  });
});
