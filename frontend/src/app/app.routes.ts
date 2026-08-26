import { Routes } from '@angular/router';
import { adultGuard, childGuard, guestGuard, sessionGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/session/session-page').then((c) => c.SessionPage),
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/login/login-page').then((c) => c.LoginPage),
  },
  {
    path: 'acceptera-inbjudan',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/login/accept-invitation-page').then((c) => c.AcceptInvitationPage),
  },
  {
    path: 'vuxen',
    canActivate: [adultGuard],
    loadComponent: () => import('./features/adult/adult-home-page').then((c) => c.AdultHomePage),
  },
  {
    path: 'vuxen/bjud-in',
    canActivate: [adultGuard],
    loadComponent: () =>
      import('./features/adult/invitation/adult-invitation-page').then(
        (c) => c.AdultInvitationPage,
      ),
  },
  {
    path: 'vuxen/barn',
    canActivate: [adultGuard],
    loadComponent: () =>
      import('./features/adult/children/adult-children-page').then((c) => c.AdultChildrenPage),
  },
  {
    path: 'vuxen/sysslor',
    canActivate: [adultGuard],
    loadComponent: () =>
      import('./features/adult/chores/adult-chores-page').then((c) => c.AdultChoresPage),
  },
  {
    path: 'vuxen/belöningar',
    canActivate: [adultGuard],
    loadComponent: () =>
      import('./features/adult/rewards/adult-rewards-page').then((c) => c.AdultRewardsPage),
  },
  {
    path: 'vuxen/granska',
    canActivate: [adultGuard],
    loadComponent: () =>
      import('./features/adult/review/adult-review-page').then((c) => c.AdultReviewPage),
  },
  {
    path: 'barn',
    canActivate: [childGuard],
    loadComponent: () => import('./features/child/child-home-page').then((c) => c.ChildHomePage),
  },
  { path: '**', redirectTo: '' },
];
