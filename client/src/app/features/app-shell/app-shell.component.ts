import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
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
      <!-- Top nav -->
      <nav class="navbar">
        <div class="nav-content">
          <h1 class="nav-title">GASOHOLIC</h1>
          <div class="nav-actions">
            <button (click)="toggleTheme()" class="theme-btn" title="Toggle theme">
              {{ themeService.theme() === 'light' ? '🌙' : '☀️' }}
            </button>
            <button (click)="logout()" class="logout-btn">Logout</button>
          </div>
        </div>
      </nav>

      <!-- Main content -->
      <div class="main-content">
        <!-- Auto selector -->
        <div class="auto-selector-panel" *ngIf="autosService.autos().length > 0">
          <select id="autoSelector" [(ngModel)]="selectedAutoId" (change)="onAutoChange()">
            <option value="">Select an auto</option>
            @for (auto of autosService.autos(); track auto.id) {
              <option [value]="auto.id">{{ auto.brand }} {{ auto.model }} ({{ auto.plate }})</option>
            }
          </select>
        </div>

        <!-- Tab navigation -->
        <div class="tabs">
          <a routerLink="/app/log" routerLinkActive="active" data-testid="tab-log">Fillup Log</a>
          <a routerLink="/app/autos" routerLinkActive="active" data-testid="tab-autos">Autos</a>
        </div>

        <!-- Router outlet for page content -->
        <div id="fillupContent">
          <router-outlet></router-outlet>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .app-container {
      display: flex;
      flex-direction: column;
      height: 100dvh;
      width: 100dvw;
      background: #f5f5f5;
    }

    .navbar {
      background: #fff;
      border-bottom: 1px solid #e0e0e0;
      padding: 1rem;
    }

    .nav-content {
      display: flex;
      justify-content: space-between;
      align-items: center;
      max-width: 100%;
    }

    .nav-title {
      font-family: 'Contrail One', system-ui, sans-serif;
      font-size: 1.3rem;
      font-weight: 400;
      color: #111;
      margin: 0;
    }

    .nav-actions {
      display: flex;
      gap: 0.5rem;
    }

    .theme-btn, .logout-btn {
      padding: 0.5rem 1rem;
      border: 1px solid #ccc;
      border-radius: 5px;
      background: #fff;
      cursor: pointer;
      font-size: 0.9rem;
      transition: background 0.15s;
    }

    .theme-btn:hover, .logout-btn:hover {
      background: #f0f0f0;
    }

    .main-content {
      flex: 1;
      overflow-y: auto;
      padding: 1rem;
    }

    .auto-selector-panel {
      margin-bottom: 1rem;
    }

    select {
      width: 100%;
      max-width: 300px;
      padding: 0.5rem;
      border: 1px solid #ccc;
      border-radius: 5px;
      font-size: 0.9rem;
    }

    .tabs {
      display: flex;
      gap: 1rem;
      margin-bottom: 1.5rem;
      border-bottom: 1px solid #e0e0e0;
    }

    .tabs a {
      padding: 0.75rem 1rem;
      text-decoration: none;
      color: #666;
      border-bottom: 2px solid transparent;
      transition: color 0.15s, border-color 0.15s;
    }

    .tabs a:hover {
      color: #111;
    }

    .tabs a.active {
      color: #ec7004;
      border-bottom-color: #ec7004;
    }

    #fillupContent {
      flex: 1;
    }

    @media (max-width: 640px) {
      .navbar {
        padding: 0.75rem;
      }

      .nav-title {
        font-size: 1.1rem;
      }

      .main-content {
        padding: 0.75rem;
      }

      .nav-actions {
        gap: 0.25rem;
      }

      .theme-btn, .logout-btn {
        padding: 0.4rem 0.75rem;
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
      // Router will redirect to login due to authGuard
    } catch {
      this.toastService.show('Logout failed', 'error');
    }
  }
}
