import { Component, OnInit, signal, inject } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card">
      <h1>GASOHOLIC</h1>

      <!-- Login form -->
      <div id="loginState" [style.display]="showLogin() ? 'block' : 'none'">
        <p class="subtitle">Enter your email to sign in or create an account.</p>
        <form id="loginForm" (ngSubmit)="onSubmit()">
          <label for="email">Email</label>
          <input
            type="email"
            id="email"
            name="email"
            placeholder="you@example.com"
            [(ngModel)]="email"
            (keydown.enter)="onSubmit()"
            required
            autofocus
          >
          <button type="submit" id="submitBtn" [disabled]="loading()">
            {{ submitBtnText() }}
          </button>
          <p class="error" id="error" [style.display]="errorMsg() ? 'block' : 'none'">
            {{ errorMsg() }}
          </p>
        </form>
      </div>

      <!-- Verification pending state -->
      <div id="pendingState" [style.display]="showLogin() ? 'none' : 'block'">
        <div class="pending-icon">✉️</div>
        <p class="subtitle">{{ pendingHeading() }}</p>
        <p class="pending-msg">
          We sent a sign-in link to <span class="pending-email" id="pendingEmail">{{ pendingEmail() }}</span>.
          Click the link in the email to continue — it expires in 24 hours.
        </p>
        <button class="btn-secondary" id="resendBtn" (click)="onResend()" [disabled]="loading()">
          Resend link
        </button>
        <p class="cooldown-msg" id="cooldownMsg" [style.display]="cooldownMsg() ? 'block' : 'none'">
          {{ cooldownMsg() }}
        </p>
        <span class="back-link" id="backLink" (click)="onBack()">Use a different email</span>
      </div>
    </div>
    <img src="assets/images/pump.webp" alt="" aria-hidden="true" class="pump-bg">
  `,
  styles: [`
    :host {
      display: flex;
      width: 100dvw;
      min-height: 100dvh;
      overflow-x: hidden;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      background: var(--bg-light);
      transition: background-color 0.3s;
    }

    .card {
      background: var(--bg-card);
      border: 1px solid var(--border-color);
      border-radius: 8px;
      padding: 2rem;
      width: 100%;
      max-width: 360px;
      margin-top: -5rem;
      transition: background-color 0.3s, border-color 0.3s;
    }

    h1 {
      font-family: 'Contrail One', system-ui, sans-serif;
      font-size: 1.4rem;
      font-weight: 400;
      margin-bottom: 0.25rem;
      color: var(--text-primary);
      letter-spacing: 0.05em;
      transition: color 0.3s;
    }

    .subtitle {
      font-size: 0.875rem;
      color: var(--text-secondary);
      margin-bottom: 1.5rem;
      transition: color 0.3s;
    }

    label {
      display: block;
      font-size: 0.875rem;
      font-weight: 500;
      color: var(--text-primary);
      margin-bottom: 0.375rem;
      transition: color 0.3s;
    }

    input[type="email"] {
      width: 100%;
      padding: 0.5rem 0.75rem;
      border: 1px solid var(--border-color);
      border-radius: 5px;
      font-size: 1rem;
      color: var(--text-primary);
      background: var(--bg-card);
      outline: none;
      transition: border-color 0.15s, background-color 0.3s, color 0.3s;
    }

    input[type="email"]:focus { border-color: var(--primary-color); }

    button[type="submit"],
    .btn-secondary {
      border-radius: 5px;
      font-size: 1rem;
      font-weight: 500;
      cursor: pointer;
      transition: opacity 0.15s;
    }

    button[type="submit"] {
      margin-top: 1rem;
      width: 100%;
      padding: 0.6rem;
      background: var(--primary-color);
      color: #fff;
      border: none;
    }

    button[type="submit"]:hover { opacity: 0.9; filter: brightness(0.9); }
    button[type="submit"]:disabled { opacity: 0.5; cursor: default; }

    .error {
      margin-top: 0.75rem;
      font-size: 0.875rem;
      color: #dc2626;
    }

    .pending-icon {
      font-size: 2.5rem;
      text-align: center;
      margin-bottom: 1rem;
    }

    .pending-email {
      font-weight: 600;
      color: var(--text-primary);
      transition: color 0.3s;
    }

    .pending-msg {
      font-size: 0.9rem;
      color: var(--text-secondary);
      margin: 0.75rem 0 1.25rem;
      line-height: 1.5;
      transition: color 0.3s;
    }

    .btn-secondary {
      width: 100%;
      padding: 0.55rem;
      background: transparent;
      color: #2563eb;
      border: 1px solid #2563eb;
      font-size: 0.9rem;
    }

    .btn-secondary:hover { background: #eff6ff; }
    .btn-secondary:disabled { opacity: 0.5; cursor: default; }

    .back-link {
      display: block;
      margin-top: 0.75rem;
      text-align: center;
      font-size: 0.85rem;
      color: var(--text-secondary);
      cursor: pointer;
      text-decoration: underline;
      transition: color 0.3s;
    }

    .cooldown-msg {
      margin-top: 0.5rem;
      font-size: 0.8rem;
      color: var(--text-secondary);
      text-align: center;
      transition: color 0.3s;
    }

    .pump-bg {
      position: fixed;
      bottom: 0;
      left: 0;
      width: 180px;
      opacity: 0.07;
      mix-blend-mode: multiply;
      pointer-events: none;
      user-select: none;
      z-index: 0;
    }
  `]
})
export class LoginComponent implements OnInit {
  private authService = inject(AuthService);
  private router = inject(Router);

  email = '';
  showLogin = signal(true);
  loading = signal(false);
  errorMsg = signal('');
  submitBtnText = signal('Sign In');
  pendingEmail = signal('');
  cooldownMsg = signal('');
  pendingHeading = signal('Check your inbox');

  ngOnInit() {
    // If already logged in, redirect to app
    if (this.authService.isAuthenticated()) {
      this.router.navigate(['/app/log']);
    }
  }

  async onSubmit() {
    const email = this.email.trim();
    if (!email) return;

    this.loading.set(true);
    this.submitBtnText.set('Signing in...');
    this.errorMsg.set('');

    try {
      const result = await this.authService.login(email);
      if (result.status === 'ok') {
        // Active session — straight to app
        this.router.navigate(['/app/log']);
        return;
      }
      // pending_verification or pending_reauth — show pending state with the right wording
      this.pendingEmail.set(email);
      this.pendingHeading.set(
        result.status === 'pending_reauth'
          ? 'Check your email to sign back in'
          : 'Check your email to verify your account'
      );
      this.showLogin.set(false);
      this.cooldownMsg.set('');
    } catch (err: any) {
      this.errorMsg.set('Sign in failed. Please try again.');
      this.loading.set(false);
      this.submitBtnText.set('Sign In');
    }
  }

  async onResend() {
    this.loading.set(true);
    this.cooldownMsg.set('');

    try {
      await fetch('/auth/resend', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'same-origin',
        body: JSON.stringify({ email: this.pendingEmail() })
      }).then(res => {
        if (res.status === 429) {
          this.cooldownMsg.set('Too many attempts. Please wait an hour before requesting another link.');
        } else {
          this.cooldownMsg.set('Link sent! Check your inbox (or spam folder).');
        }
      });
    } catch {
      this.cooldownMsg.set('Network error. Please try again.');
    } finally {
      this.loading.set(false);
      setTimeout(() => this.loading.set(false), 30000);
    }
  }

  onBack() {
    this.showLogin.set(true);
    this.errorMsg.set('');
    this.email = '';
    this.submitBtnText.set('Sign In');
    this.loading.set(false);
    this.pendingHeading.set('Check your inbox');
  }
}
