import { Routes } from '@angular/router';
import { LoginComponent } from './features/login/login.component';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () => import('./features/app-shell/app-shell.component').then(m => m.AppShellComponent),
    children: [
      {
        path: 'log',
        loadComponent: () => import('./features/fillups/fillups.component').then(m => m.FillupsComponent)
      },
      {
        path: 'autos',
        loadComponent: () => import('./features/autos/autos.component').then(m => m.AutosComponent)
      },
      {
        path: 'maintenance',
        loadComponent: () => import('./features/maintenance/maintenance.component').then(m => m.MaintenanceComponent)
      }
    ]
  },
  {
    path: '',
    redirectTo: '/app/log',
    pathMatch: 'full'
  }
];
