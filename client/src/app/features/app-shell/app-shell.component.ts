import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { AutosService } from '../../core/services/autos.service';
import { ThemeService } from '../../core/services/theme.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="app-container">
      <!-- Top nav: brand + auto selector + logout (matches production) -->
      <nav class="navbar">
        <span class="brand">GASOHOLIC</span>
        @if (autosService.autos().length > 0) {
          <select id="autoSelector" [(ngModel)]="selectedAutoId" (change)="onAutoChange()">
            <option value="">&mdash; select auto &mdash;</option>
            @for (auto of autosService.autos(); track auto.id) {
              <option [value]="auto.id">{{ auto.brand }} {{ auto.model }} ({{ auto.plate }})</option>
            }
          </select>
        }
        <button (click)="logout()" class="logout-btn">Log out</button>
      </nav>

      <!-- Main content -->
      <div class="main-content">
        <!-- Tab navigation + theme toggle (matches production) -->
        <div class="tabs">
          <a routerLink="/app/log" routerLinkActive="active" data-testid="tab-log">Log</a>
          <a routerLink="/app/autos" routerLinkActive="active" data-testid="tab-autos">Autos</a>
          <button (click)="toggleTheme()" class="theme-toggle" title="Toggle theme">
            {{ themeService.theme() === 'light' ? '☽' : '☀' }}
          </button>
        </div>

        <!-- Router outlet for page content -->
        <div id="fillupContent">
          <router-outlet></router-outlet>
        </div>
      </div>
      <img id="pumpWatermark" src="assets/images/pump.webp" alt="" aria-hidden="true" style="position:fixed;bottom:0;left:0;width:180px;opacity:0.07;mix-blend-mode:multiply;pointer-events:none;user-select:none;z-index:0;">
    </div>
  `,
  styles: [`
    .app-container {
      display: flex;
      flex-direction: column;
      height: 100dvh;
      width: 100dvw;
      background: var(--bg-light);
      color: var(--text-primary);
      transition: background-color 0.3s, color 0.3s;
    }

    .navbar {
      background: var(--bg-card);
      border-bottom: 1px solid var(--border-color);
      padding: 0.6rem 1rem;
      display: flex;
      align-items: center;
      gap: 0.75rem;
      transition: background-color 0.3s, border-color 0.3s;
    }

    .brand {
      font-family: 'Contrail One', system-ui, sans-serif;
      font-size: 2.1rem;
      font-weight: 400;
      color: var(--primary-color);
      letter-spacing: 0.05em;
      display: block;
    }

    select {
      padding: 0.3rem 0.5rem;
      border: 1px solid var(--border-color);
      border-radius: 5px;
      font-size: 0.9rem;
      background: var(--bg-card);
      color: var(--text-primary);
      transition: background-color 0.3s, border-color 0.3s, color 0.3s;
    }

    .logout-btn {
      margin-left: auto;
      padding: 0.3rem 0.75rem;
      border: 1px solid var(--border-color);
      border-radius: 5px;
      background: var(--bg-card);
      color: var(--text-primary);
      cursor: pointer;
      font-size: 0.875rem;
      transition: background 0.15s, color 0.15s, border-color 0.3s;
    }

    .logout-btn:hover {
      background: var(--bg-light);
    }

    .main-content {
      flex: 1;
      overflow-y: auto;
      padding: 1rem;
    }

    .tabs {
      display: flex;
      border-bottom: 1px solid var(--border-color);
      margin-bottom: 1rem;
      transition: border-color 0.3s;
    }

    .tabs a {
      padding: 0.75rem 1rem;
      text-decoration: none;
      color: var(--text-secondary);
      border-bottom: 2px solid transparent;
      font-size: 0.9rem;
      font-weight: 400;
      transition: color 0.15s, border-color 0.15s;
    }

    .tabs a:hover {
      color: var(--text-primary);
    }

    .tabs a.active {
      color: var(--primary-color);
      border-bottom-color: var(--primary-color);
      font-weight: 500;
    }

    .theme-toggle {
      margin-left: auto;
      background: transparent;
      border: none;
      color: var(--text-secondary);
      font-size: 1rem;
      cursor: pointer;
      padding: 0.75rem 0.5rem;
      transition: color 0.15s;
    }

    .theme-toggle:hover {
      color: var(--text-primary);
    }

    #fillupContent {
      flex: 1;
    }

    @media (max-width: 640px) {
      .navbar {
        padding: 0.5rem 0.75rem;
        gap: 0.5rem;
      }

      .brand {
        font-size: 1.6rem;
      }

      .main-content {
        padding: 0.75rem;
      }

      .logout-btn {
        padding: 0.25rem 0.5rem;
        font-size: 0.8rem;
      }

      select {
        max-width: 160px;
        font-size: 0.8rem;
      }
    }
  `]
})
export class AppShellComponent implements OnInit {
  authService = inject(AuthService);
  autosService = inject(AutosService);
  themeService = inject(ThemeService);
  private toastService = inject(ToastService);
  private router = inject(Router);

  selectedAutoId = signal<number | string>('');

  async ngOnInit(): Promise<void> {
    await this.autosService.loadAutos();
    if (this.autosService.autos().length > 0) {
      this.selectedAutoId.set(this.autosService.currentAutoId() || '');
    }
  }

  onAutoChange() {
    const id = parseInt(String(this.selectedAutoId()), 10);
    if (!isNaN(id)) {
      this.autosService.setCurrentAuto(id);
    }
  }

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  async logout() {
    try {
      await this.authService.logout();
      this.router.navigate(['/login']);
    } catch {
      this.toastService.show('Logout failed', 'error');
    }
  }
}
