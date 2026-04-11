import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface User {
  email: string;
}

export type LoginStatus = 'ok' | 'pending_verification' | 'pending_reauth';

export interface LoginResult {
  status: LoginStatus;
  email?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);

  private userSignal = signal<User | null>(null);
  private loadingSignal = signal(false);

  user = this.userSignal.asReadonly();
  loading = this.loadingSignal.asReadonly();
  isAuthenticated = computed(() => this.userSignal() !== null);

  async checkAuth(): Promise<void> {
    this.loadingSignal.set(true);
    try {
      const response = await firstValueFrom(
        this.http.get<User>('/auth/me', { withCredentials: true })
      );
      this.userSignal.set(response);
    } catch {
      this.userSignal.set(null);
    } finally {
      this.loadingSignal.set(false);
    }
  }

  async login(email: string): Promise<LoginResult> {
    this.loadingSignal.set(true);
    try {
      // observe: 'response' so we can read the HTTP status (200 vs 202)
      const response = await firstValueFrom(
        this.http.post<{ status: LoginStatus; email?: string }>(
          '/auth/login',
          { email },
          { withCredentials: true, observe: 'response' }
        )
      );
      const body = response.body!;
      if (body.status === 'ok' && body.email) {
        this.userSignal.set({ email: body.email });
      }
      return body;
    } finally {
      this.loadingSignal.set(false);
    }
  }

  async logout(): Promise<void> {
    this.loadingSignal.set(true);
    try {
      await firstValueFrom(
        this.http.post('/auth/logout', {}, { withCredentials: true })
      );
      this.userSignal.set(null);
    } finally {
      this.loadingSignal.set(false);
    }
  }
}
