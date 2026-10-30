import { Routes } from '@angular/router';
import { unsavedChangesGuard } from './features/trip-detail/trip-detail.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/home/home').then((m) => m.Home),
    title: 'Tabiji — Overview',
  },
  {
    path: 'trips',
    loadComponent: () => import('./features/trips/trips').then((m) => m.Trips),
    title: 'Tabiji — Trips',
  },
  {
    path: 'trips/new',
    loadComponent: () => import('./features/trip-create/trip-create').then((m) => m.TripCreate),
    title: 'Tabiji — New trip',
  },
  {
    path: 'trips/:id/atlas',
    loadComponent: () => import('./features/trip-atlas/trip-atlas').then((m) => m.TripAtlas),
    title: 'Tabiji — Atlas',
  },
  {
    path: 'trips/:id',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./features/trip-detail/trip-detail').then((m) => m.TripDetail),
    title: 'Tabiji — Trip',
  },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
    title: 'Tabiji — Sign in',
  },
  {
    path: 'register',
    loadComponent: () => import('./features/register/register').then((m) => m.Register),
    title: 'Tabiji — Create an account',
  },
  {
    path: 'invite',
    loadComponent: () => import('./features/invitation/invitation').then((m) => m.Invitation),
    title: 'Tabiji — Invitation',
  },
  { path: '**', redirectTo: '' },
];
