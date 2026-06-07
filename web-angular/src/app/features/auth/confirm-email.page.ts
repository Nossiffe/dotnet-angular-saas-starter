import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-confirm-email',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule],
  templateUrl: './confirm-email.page.html',
})
export class ConfirmEmailPage implements OnInit {
  private readonly auth = inject(AuthService);

  // Query params bound via withComponentInputBinding
  readonly userId = input<string>('');
  readonly token = input<string>('');

  readonly status = signal<'pending' | 'success' | 'error' | 'invalid'>('pending');

  ngOnInit(): void {
    const uid = this.userId();
    const tok = this.token();
    if (!uid || !tok) {
      this.status.set('invalid');
      return;
    }
    this.auth.confirmEmail(uid, tok).subscribe({
      next: () => this.status.set('success'),
      error: () => this.status.set('error'),
    });
  }
}
