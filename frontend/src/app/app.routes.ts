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
    path: 'vuxen/barn/:childId',
    canActivate: [adultGuard],
    loadComponent: () => import('./features/adult/children/adult-child-profile-page').then((c) => c.AdultChildProfilePage),
  },
  {
    path: 'vuxen/installningar/barn',
    canActivate: [adultGuard],
    loadComponent: () =>
      import('./features/adult/children/adult-children-page').then((c) => c.AdultChildrenPage),
  },
  {
    path: 'vuxen/installningar',
    canActivate: [adultGuard],
    loadComponent: () =>
      import('./features/adult/settings/adult-settings-page').then((c) => c.AdultSettingsPage),
  },
  { path: 'vuxen/barn', pathMatch: 'full', redirectTo: 'vuxen/installningar/barn' },
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
    path: 'vuxen/onskningar',
    canActivate: [adultGuard],
    loadComponent: () =>
      import('./features/adult/rewards/adult-reward-redemptions-page').then(
        (c) => c.AdultRewardRedemptionsPage,
      ),
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
  {
    path: 'barn/beloningar',
    canActivate: [childGuard],
    loadComponent: () => import('./features/child/child-rewards-page').then((c) => c.ChildRewardsPage),
  },
  {
    path: 'barn/onskningar',
    canActivate: [childGuard],
    loadComponent: () => import('./features/child/child-redemptions-page').then((c) => c.ChildRedemptionsPage),
  },
  {
    path: 'barn/beloningar',
    canActivate: [childGuard],
    loadComponent: () => import('./features/child/child-rewards-page').then((c) => c.ChildRewardsPage),
  },
  {
    path: 'barn/onskningar',
    canActivate: [childGuard],
    loadComponent: () => import('./features/child/child-redemptions-page').then((c) => c.ChildRedemptionsPage),
  },
  { path: '**', redirectTo: '' },
];
