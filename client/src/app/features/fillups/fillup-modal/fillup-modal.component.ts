import { Component, Input, Output, EventEmitter, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';

@Component({
  selector: 'app-fillup-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    @if (isOpen) {
      <div class="modal-backdrop" (click)="onClose()">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h2>{{ mode === 'add' ? 'Add Fillup' : 'Edit Fillup' }}</h2>
            <button (click)="onClose()" class="close-btn">×</button>
          </div>
          <form [formGroup]="form" (ngSubmit)="onSubmit()" class="modal-body">
            <div class="form-group">
              <label for="filledAt">Date &amp; Time</label>
              <input type="datetime-local" id="filledAt" formControlName="filledAt" required>
            </div>
            <div class="form-group">
              <label for="fuelType">Fuel Type</label>
              <select id="fuelType" formControlName="fuelType" required>
                <option value="">Select fuel type</option>
                <option value="0">Regular</option>
                <option value="1">Mid-grade</option>
                <option value="2">Premium</option>
                <option value="3">Diesel</option>
                <option value="4">E85</option>
              </select>
            </div>
            <div class="form-group">
              <label for="gallons">Gallons</label>
              <input type="number" id="gallons" formControlName="gallons" placeholder="12.5" step="0.01" required>
            </div>
            <div class="form-group">
              <label for="pricePerGallon">Price per Gallon (\$)</label>
              <input type="number" id="pricePerGallon" formControlName="pricePerGallon" placeholder="3.50" step="0.01" required>
            </div>
            <div class="form-group">
              <label for="odometer">Odometer (miles)</label>
              <input type="number" id="odometer" formControlName="odometer" placeholder="50000" required>
            </div>
            <div class="form-group checkbox">
              <input type="checkbox" id="isPartialFill" formControlName="isPartialFill">
              <label for="isPartialFill">Partial fill (don't calculate MPG)</label>
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
      background: #fff;
      border-radius: 8px;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
      width: 90%;
      max-width: 500px;
      max-height: 90vh;
      overflow-y: auto;
    }

    .modal-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1.5rem;
      border-bottom: 1px solid #e0e0e0;
    }

    .modal-header h2 {
      margin: 0;
      font-size: 1.3rem;
      color: #111;
    }

    .close-btn {
      background: none;
      border: none;
      font-size: 1.5rem;
      cursor: pointer;
      color: #666;
    }

    .close-btn:hover {
      color: #111;
    }

    .modal-body {
      padding: 1.5rem;
    }

    .form-group {
      margin-bottom: 1rem;
    }

    .form-group.checkbox {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    .form-group.checkbox input {
      width: auto;
      margin: 0;
    }

    label {
      display: block;
      font-size: 0.875rem;
      font-weight: 500;
      color: #333;
      margin-bottom: 0.375rem;
    }

    .form-group.checkbox label {
      margin: 0;
      display: inline;
      font-weight: normal;
    }

    input,
    select {
      width: 100%;
      padding: 0.5rem 0.75rem;
      border: 1px solid #ccc;
      border-radius: 5px;
      font-size: 1rem;
      outline: none;
      transition: border-color 0.15s;
    }

    input:focus,
    select:focus {
      border-color: #ec7004;
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
      transition: background 0.15s;
    }

    .btn-cancel {
      background: #f0f0f0;
      color: #111;
    }

    .btn-cancel:hover {
      background: #e0e0e0;
    }

    .btn-save {
      background: #ec7004;
      color: #fff;
    }

    .btn-save:hover:not(:disabled) {
      background: #d86500;
    }

    .btn-save:disabled {
      background: #ffe4cd;
      cursor: default;
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
export class FillupModalComponent {
  @Input() isOpen = false;
  @Input() mode: 'add' | 'edit' = 'add';
  @Input() fillup: any = null;
  @Input() autoId: number | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<any>();

  form: FormGroup;

  constructor(private fb: FormBuilder) {
    this.form = this.fb.group({
      filledAt: ['', Validators.required],
      fuelType: ['', Validators.required],
      gallons: ['', Validators.required],
      pricePerGallon: ['', Validators.required],
      odometer: ['', Validators.required],
      isPartialFill: [false]
    });

    effect(() => {
      if (this.fillup && this.mode === 'edit') {
        const filledAt = new Date(this.fillup.filledAt).toISOString().slice(0, 16);
        this.form.patchValue({
          filledAt,
          fuelType: String(this.fillup.fuelType),
          gallons: this.fillup.gallons,
          pricePerGallon: this.fillup.pricePerGallon,
          odometer: this.fillup.odometer,
          isPartialFill: this.fillup.isPartialFill
        });
      } else if (this.mode === 'add') {
        const now = new Date().toISOString().slice(0, 16);
        this.form.patchValue({
          filledAt: now,
          fuelType: '0',
          isPartialFill: false
        });
        this.form.reset({
          filledAt: now,
          fuelType: '0',
          gallons: '',
          pricePerGallon: '',
          odometer: '',
          isPartialFill: false
        });
      }
    });
  }

  onClose() {
    this.close.emit();
  }

  onSubmit() {
    if (this.form.valid) {
      const value = this.form.value;
      this.save.emit({
        filledAt: new Date(value.filledAt).toISOString(),
        fuelType: parseInt(value.fuelType, 10),
        gallons: parseFloat(value.gallons),
        pricePerGallon: parseFloat(value.pricePerGallon),
        odometer: parseInt(value.odometer, 10),
        isPartialFill: value.isPartialFill
      });
    }
  }
}
