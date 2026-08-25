import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { UserRole } from './auth.models';
import { AuthService } from './auth.service';

const roleGuard =
  (expectedRole: UserRole): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth.restoreSession().pipe(
      map((user) => {
        if (!user) return router.createUrlTree(['/login']);
        return user.role === expectedRole ? true : router.createUrlTree([auth.homeFor(user.role)]);
      }),
    );
  };

export const adultGuard = roleGuard('Adult');
export const childGuard = roleGuard('Child');

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth
    .restoreSession()
    .pipe(map((user) => (user ? router.createUrlTree([auth.homeFor(user.role)]) : true)));
};

export const sessionGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth
    .restoreSession()
    .pipe(map((user) => router.createUrlTree([user ? auth.homeFor(user.role) : '/login'])));
};
