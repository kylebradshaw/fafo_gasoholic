import { Component, Input, Output, EventEmitter, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';

@Component({
  selector: 'app-auto-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    @if (isOpen) {
      <div class="modal-backdrop" (click)="onClose()">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h2>{{ mode === 'add' ? 'Add Vehicle' : 'Edit Vehicle' }}</h2>
            <button (click)="onClose()" class="close-btn">×</button>
          </div>
          <form [formGroup]="form" (ngSubmit)="onSubmit()" class="modal-body">
            <div class="form-group">
              <label for="brand">Brand</label>
              <input type="text" id="brand" formControlName="brand" placeholder="Toyota" required>
            </div>
            <div class="form-group">
              <label for="model">Model</label>
              <input type="text" id="model" formControlName="model" placeholder="Camry" required>
            </div>
            <div class="form-group">
              <label for="plate">License Plate</label>
              <input type="text" id="plate" formControlName="plate" placeholder="ABC123" required>
            </div>
            <div class="form-group">
              <label for="odometer">Current Odometer (miles)</label>
              <input type="number" id="odometer" formControlName="odometer" placeholder="10000" required>
            </div>
            <div class="form-actions">
              <button type="button" (click)="onClose()" class="btn-cancel">Cancel</button>
              <button type="submit" [disabled]="!form.valid" class="btn-save">Save</button>
            </div>
          </form>
        </div>
      </div>
    }
  `,
  styles: [`
    .modal-backdrop {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.5);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
    }

    .modal {
      background: var(--bg-card);
      border-radius: 8px;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
      width: 90%;
      max-width: 500px;
      transition: background-color 0.3s, color 0.3s;
    }

    .modal-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1.5rem;
      border-bottom: 1px solid var(--border-color);
    }

    .modal-header h2 {
      margin: 0;
      font-size: 1.3rem;
      color: var(--text-primary);
    }

    .close-btn {
      background: none;
      border: none;
      font-size: 1.5rem;
      cursor: pointer;
      color: var(--text-secondary);
      transition: color 0.15s;
    }

    .close-btn:hover {
      color: var(--text-primary);
    }

    .modal-body {
      padding: 1.5rem;
    }

    .form-group {
      margin-bottom: 1rem;
    }

    label {
      display: block;
      font-size: 0.875rem;
      font-weight: 500;
      color: var(--text-primary);
      margin-bottom: 0.375rem;
    }

    input {
      width: 100%;
      padding: 0.5rem 0.75rem;
      border: 1px solid var(--border-color);
      border-radius: 5px;
      font-size: 1rem;
      color: var(--text-primary);
      background: var(--bg-card);
      outline: none;
      transition: border-color 0.15s, background-color 0.15s, color 0.15s;
    }

    input:focus {
      border-color: var(--primary-color);
    }

    .form-actions {
      display: flex;
      gap: 1rem;
      margin-top: 1.5rem;
    }

    .btn-cancel, .btn-save {
      flex: 1;
      padding: 0.6rem;
      border: none;
      border-radius: 5px;
      font-size: 1rem;
      font-weight: 500;
      cursor: pointer;
      transition: opacity 0.15s;
    }

    .btn-cancel {
      background: var(--bg-light);
      color: var(--text-primary);
      border: 1px solid var(--border-color);
    }

    .btn-cancel:hover {
      opacity: 0.8;
    }

    .btn-save {
      background: var(--primary-color);
      color: #fff;
    }

    .btn-save:hover:not(:disabled) {
      opacity: 0.9;
      filter: brightness(0.9);
    }

    .btn-save:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    @media (max-width: 640px) {
      .modal {
        width: 95%;
      }

      .modal-header,
      .modal-body {
        padding: 1rem;
      }
    }
  `]
})
export class AutoModalComponent {
  @Input() isOpen = false;
  @Input() mode: 'add' | 'edit' = 'add';
  @Input() auto: any = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<any>();

  form: FormGroup;

  constructor(private fb: FormBuilder) {
    this.form = this.fb.group({
      brand: ['', Validators.required],
      model: ['', Validators.required],
      plate: ['', Validators.required],
      odometer: ['', Validators.required]
    });

    effect(() => {
      if (this.auto && this.mode === 'edit') {
        this.form.patchValue(this.auto);
      } else {
        this.form.reset();
      }
    });
  }

  onClose() {
    this.close.emit();
  }

  onSubmit() {
    if (this.form.valid) {
      this.save.emit(this.form.value);
    }
  }
}
