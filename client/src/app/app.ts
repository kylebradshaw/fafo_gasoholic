import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from './core/services/auth.service';
import { ToastComponent } from './shared/components/toast.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastComponent],
  template: `
    <router-outlet></router-outlet>
    <app-toast></app-toast>
  `,
  styles: []
})
export class App implements OnInit {
  private authService = inject(AuthService);

  ngOnInit() {
    // Non-blocking auth check on app start
    this.authService.checkAuth().catch(() => {});
  }
}
