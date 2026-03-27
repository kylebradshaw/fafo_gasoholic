import { Injectable, signal } from '@angular/core';

type Theme = 'light' | 'dark';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private themeSignal = signal<Theme>('light');
  theme = this.themeSignal.asReadonly();

  constructor() {
    const stored = localStorage.getItem('theme') as Theme | null;
    if (stored) {
      this.setTheme(stored);
    }
  }

  setTheme(theme: Theme) {
    this.themeSignal.set(theme);
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('theme', theme);
  }

  toggleTheme() {
    const current = this.themeSignal();
    const newTheme = current === 'light' ? 'dark' : 'light';
    this.setTheme(newTheme);
  }
}
