import { Component, Input, Output, EventEmitter, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MaintenanceRecord } from '../../../core/services/maintenance.service';

const MAINTENANCE_TYPES = [
  { value: 'OilChange', label: 'Oil Change' },
  { value: 'TireRotation', label: 'Tire Rotation' },
  { value: 'BrakeInspection', label: 'Brake Inspection' },
  { value: 'AirFilter', label: 'Air Filter' },
  { value: 'CabinFilter', label: 'Cabin Filter' },
  { value: 'WiperBlades', label: 'Wiper Blades' },
  { value: 'TireReplacement', label: 'Tire Replacement' },
  { value: 'BatteryReplacement', label: 'Battery Replacement' },
  { value: 'Other', label: 'Other' },
];

@Component({
  selector: 'app-maintenance-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    @if (isOpen) {
      <div class="modal-backdrop" (click)="onClose()">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h2>{{ mode === 'add' ? 'Add Maintenance Record' : 'Edit Maintenance Record' }}</h2>
            <button (click)="onClose()" class="close-btn">×</button>
          </div>
          <form [formGroup]="form" (ngSubmit)="onSubmit()" class="modal-body">
            <div class="form-group">
              <label for="type">Service Type</label>
              <select id="type" formControlName="type" required>
                <option value="">Select service type</option>
                @for (t of maintenanceTypes; track t.value) {
                  <option [value]="t.value">{{ t.label }}</option>
                }
              </select>
            </div>
            <div class="form-group">
              <label for="performedAt">Date</label>
              <input type="date" id="performedAt" formControlName="performedAt" required>
            </div>
            <div class="form-group">
              <label for="odometer">Odometer (miles)</label>
              <input type="number" id="odometer" formControlName="odometer" placeholder="50000" required>
            </div>
            <div class="form-group">
              <label for="cost">Cost (\$)</label>
              <input type="number" id="cost" formControlName="cost" placeholder="49.99" step="0.01" required>
            </div>
            <div class="form-group">
              <label for="notes">Notes <span class="optional">(optional)</span></label>
              <textarea id="notes" formControlName="notes" rows="3" placeholder="Shop name, technician, or other details"></textarea>
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
      max-height: 90vh;
      overflow-y: auto;
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

    .optional {
      font-weight: normal;
      color: var(--text-secondary);
      font-size: 0.8rem;
    }

    input,
    select,
    textarea {
      width: 100%;
      padding: 0.5rem 0.75rem;
      border: 1px solid var(--border-color);
      border-radius: 5px;
      font-size: 1rem;
      color: var(--text-primary);
      background: var(--bg-card);
      outline: none;
      transition: border-color 0.15s, background-color 0.15s, color 0.15s;
      box-sizing: border-box;
    }

    textarea {
      resize: vertical;
      font-family: inherit;
    }

    input:focus,
    select:focus,
    textarea:focus {
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
      transition: background 0.15s, color 0.15s;
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
export class MaintenanceModalComponent {
  @Input() isOpen = false;
  @Input() mode: 'add' | 'edit' = 'add';
  @Input() record: MaintenanceRecord | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<any>();

  maintenanceTypes = MAINTENANCE_TYPES;
  form: FormGroup;

  constructor(private fb: FormBuilder) {
    this.form = this.fb.group({
      type: ['', Validators.required],
      performedAt: ['', Validators.required],
      odometer: ['', Validators.required],
      cost: ['', Validators.required],
      notes: ['']
    });

    effect(() => {
      if (this.record && this.mode === 'edit') {
        this.form.patchValue({
          type: this.record.type,
          performedAt: this.toDateValue(this.record.performedAt),
          odometer: this.record.odometer,
          cost: this.record.cost,
          notes: this.record.notes ?? ''
        });
      } else if (this.mode === 'add') {
        this.form.reset({
          type: '',
          performedAt: this.toDateValue(new Date().toISOString()),
          odometer: '',
          cost: '',
          notes: ''
        });
      }
    });
  }

  private toDateValue(utcString: string): string {
    const d = new Date(utcString);
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }

  onClose() {
    this.close.emit();
  }

  onSubmit() {
    if (this.form.valid) {
      const value = this.form.value;
      this.save.emit({
        type: value.type,
        performedAt: new Date(value.performedAt + 'T00:00:00Z').toISOString(),
        odometer: parseFloat(value.odometer),
        cost: parseFloat(value.cost),
        notes: value.notes || null
      });
    }
  }
}
