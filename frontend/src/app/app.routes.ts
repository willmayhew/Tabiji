import { Routes } from '@angular/router';

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
    path: 'trips/:id',
    loadComponent: () => import('./features/trip-detail/trip-detail').then((m) => m.TripDetail),
    title: 'Tabiji — Trip',
  },
  { path: '**', redirectTo: '' },
];
