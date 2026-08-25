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
    path: 'vuxen',
    canActivate: [adultGuard],
    loadComponent: () => import('./features/adult/adult-home-page').then((c) => c.AdultHomePage),
  },
  {
    path: 'barn',
    canActivate: [childGuard],
    loadComponent: () => import('./features/child/child-home-page').then((c) => c.ChildHomePage),
  },
  { path: '**', redirectTo: '' },
];
