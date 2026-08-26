import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, tap } from 'rxjs';
import {
  AdultLoginRequest,
  ChildLoginRequest,
  ChildPairingRequest,
  CurrentUser,
  HouseholdInvitation,
  RegisterAdultRequest,
  RegisterInvitedAdultRequest,
  RegisterInvitedAdultResponse,
  RegisterAdultResponse,
  UserRole,
} from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly userState = signal<CurrentUser | null>(null);
  private sessionChecked = false;

  readonly user = this.userState.asReadonly();
  readonly role = computed<UserRole | null>(() => this.userState()?.role ?? null);
  readonly isAuthenticated = computed(() => this.userState() !== null);
  readonly isCheckingSession = signal(false);

  restoreSession(): Observable<CurrentUser | null> {
    if (this.sessionChecked) return of(this.userState());

    this.isCheckingSession.set(true);
    return this.http.get<CurrentUser>('/api/auth/me').pipe(
      tap((user) => this.userState.set(user)),
      map((user) => user),
      catchError(() => {
        this.userState.set(null);
        return of(null);
      }),
      finalize(() => {
        this.sessionChecked = true;
        this.isCheckingSession.set(false);
      }),
    );
  }

  loginAdult(request: AdultLoginRequest): Observable<CurrentUser> {
    return this.http
      .post<CurrentUser>('/api/auth/login', request)
      .pipe(tap((user) => this.rememberUser(user)));
  }

  registerAdult(request: RegisterAdultRequest): Observable<RegisterAdultResponse> {
    return this.http.post<RegisterAdultResponse>('/api/auth/register', request);
  }

  createHouseholdInvitation(): Observable<HouseholdInvitation> {
    return this.http.post<HouseholdInvitation>('/api/household/invitations', {});
  }

  registerInvitedAdult(request: RegisterInvitedAdultRequest): Observable<RegisterInvitedAdultResponse> {
    return this.http.post<RegisterInvitedAdultResponse>('/api/auth/register/invited', request);
  }

  loginChild(request: ChildLoginRequest): Observable<CurrentUser> {
    return this.http
      .post<CurrentUser>('/api/auth/child/login', request)
      .pipe(tap((user) => this.rememberUser(user)));
  }

  pairChildDevice(request: ChildPairingRequest): Observable<CurrentUser> {
    return this.http
      .post<CurrentUser>('/api/auth/child/pair', request)
      .pipe(tap((user) => this.rememberUser(user)));
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', {}).pipe(
      finalize(() => {
        this.userState.set(null);
        this.sessionChecked = true;
      }),
    );
  }

  homeFor(role: UserRole): string {
    return role === 'Adult' ? '/vuxen' : '/barn';
  }

  private rememberUser(user: CurrentUser): void {
    this.userState.set(user);
    this.sessionChecked = true;
  }
}
