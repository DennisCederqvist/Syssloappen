import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('restores an adult cookie session', () => {
    auth.restoreSession().subscribe();
    http.expectOne('/api/auth/me').flush({
      userId: 'adult-1',
      email: 'alex@example.se',
      role: 'Adult',
      householdId: 12,
    });

    expect(auth.role()).toBe('Adult');
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('treats a rejected session as signed out', () => {
    auth.restoreSession().subscribe((user) => expect(user).toBeNull());
    http.expectOne('/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isAuthenticated()).toBe(false);
  });

  it('uses the child fallback login endpoint', () => {
    auth
      .loginChild({ familyCode: 'FAMILJEKOD', userName: 'maja', password: 'Secret12' })
      .subscribe();
    const request = http.expectOne('/api/auth/child/login');
    expect(request.request.method).toBe('POST');
    request.flush({ childId: 4, name: 'Maja', userName: 'maja', role: 'Child', householdId: 12 });

    expect(auth.role()).toBe('Child');
    expect(auth.homeFor('Child')).toBe('/barn');
  });

  it('pairs a child device using only the one-time code', () => {
    auth.pairChildDevice({ code: 'ABC234XY' }).subscribe();
    const request = http.expectOne('/api/auth/child/pair');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ code: 'ABC234XY' });
    request.flush({
      childId: 4,
      name: 'Maja',
      userName: 'maja',
      role: 'Child',
      householdId: 12,
    });

    expect(auth.role()).toBe('Child');
  });
});
