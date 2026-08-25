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

  it('loads device sessions for the selected child', () => {
    service.getDeviceSessions(42).subscribe((sessions) => expect(sessions).toHaveLength(1));
    const request = http.expectOne('/api/children/42/device-sessions');
    expect(request.request.method).toBe('GET');
    request.flush([
      {
        sessionId: '11111111-1111-1111-1111-111111111111',
        createdAt: '2026-08-25T10:00:00Z',
        lastSeenAt: '2026-08-25T12:00:00Z',
        expiresAt: '2026-09-01T12:00:00Z',
        absoluteExpiresAt: '2026-09-24T10:00:00Z',
        revokedAt: null,
      },
    ]);
  });

  it('revokes only the selected child device session', () => {
    service.revokeDeviceSession(42, '11111111-1111-1111-1111-111111111111').subscribe();
    const request = http.expectOne(
      '/api/children/42/device-sessions/11111111-1111-1111-1111-111111111111',
    );
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });
});
