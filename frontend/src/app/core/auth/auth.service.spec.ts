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

  it('registers an adult and returns the one-time family code', () => {
    auth
      .registerAdult({
        householdName: 'Familjen Test',
        email: 'alex@example.se',
        password: 'Secret12',
      })
      .subscribe((result) => expect(result.familyCode).toBe('FAMILY123456'));
    const request = http.expectOne('/api/auth/register');
    expect(request.request.method).toBe('POST');
    request.flush({
      householdId: 12,
      email: 'alex@example.se',
      role: 'Adult',
      familyCode: 'FAMILY123456',
    });
  });

  it('creates a household invitation without client-owned household fields', () => {
    auth.createHouseholdInvitation().subscribe((result) => expect(result.code).toBe('ABCD-EFGH'));
    const request = http.expectOne('/api/household/invitations');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    request.flush({ code: 'ABCD-EFGH', expiresAt: '2026-08-27T12:00:00Z' });
  });

  it('registers an invited adult with only code and credentials', () => {
    auth
      .registerInvitedAdult({
        invitationCode: 'ABCD-EFGH',
        email: 'invited@example.se',
        password: 'Secret12',
      })
      .subscribe((result) => expect(result.role).toBe('Adult'));
    const request = http.expectOne('/api/auth/register/invited');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      invitationCode: 'ABCD-EFGH',
      email: 'invited@example.se',
      password: 'Secret12',
    });
    request.flush({ email: 'invited@example.se', role: 'Adult', householdId: 12 });
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
