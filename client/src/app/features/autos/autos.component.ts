import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AutosService } from '../../core/services/autos.service';
import { ToastService } from '../../core/services/toast.service';
import { AutoModalComponent } from './auto-modal/auto-modal.component';

@Component({
  selector: 'app-autos',
  standalone: true,
  imports: [CommonModule, AutoModalComponent],
  template: `
    <div class="autos-container">
      <div class="autos-header">
        <h2>My Autos</h2>
        <button (click)="openAddModal()" class="btn-add">+ Add Auto</button>
      </div>

      @if (autosService.autos().length === 0) {
        <p class="empty-state">No autos yet. Add one to get started.</p>
      } @else {
        <div class="autos-grid">
          @for (auto of autosService.autos(); track auto.id) {
            <div class="auto-card">
              <div class="auto-info">
                <h3>{{ auto.brand }} {{ auto.model }}</h3>
                <p class="meta">{{ auto.plate }} &middot; {{ (auto.latestFillupOdometer ?? auto.odometer) | number }} mi</p>
              </div>
              <div class="actions">
                <button (click)="openEditModal(auto)" class="btn-secondary">Edit</button>
                <button (click)="deleteAuto(auto.id)" class="btn-danger">Delete</button>
              </div>
            </div>
          }
        </div>
      }

      <app-auto-modal
        [isOpen]="showModal()"
        [mode]="modalMode()"
        [auto]="selectedAuto()"
        (close)="closeModal()"
        (save)="onSave($event)"
      ></app-auto-modal>
    </div>
  `,
  styles: [`
    .autos-container {
      padding: 0;
    }

    .autos-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1rem;
    }

    .autos-header h2 {
      margin: 0;
      font-size: 1rem;
      font-weight: 600;
      color: var(--text-primary);
    }

    .btn-add {
      padding: 0.4rem 0.75rem;
      background: var(--primary-color);
      color: #fff;
      border: none;
      border-radius: 5px;
      cursor: pointer;
      font-size: 0.875rem;
      font-weight: 500;
      transition: opacity 0.15s;
    }

    .btn-add:hover {
      opacity: 0.9;
      filter: brightness(0.9);
    }

    .empty-state {
      text-align: center;
      color: var(--text-secondary);
      padding: 2rem;
    }

    .autos-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
      gap: 1rem;
    }

    .auto-card {
      background: var(--bg-card);
      border: 1px solid var(--border-color);
      border-radius: 8px;
      padding: 1rem;
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      transition: background-color 0.3s, border-color 0.3s;
    }

    .auto-info {
      flex: 1;
    }

    .auto-info h3 {
      margin: 0 0 0.25rem 0;
      font-size: 1rem;
      color: var(--text-primary);
    }

    .meta {
      margin: 0;
      font-size: 0.8rem;
      color: var(--text-secondary);
    }

    .actions {
      display: flex;
      gap: 0.5rem;
    }

    .btn-secondary {
      padding: 0.3rem 0.6rem;
      background: transparent;
      color: var(--text-primary);
      border: 1px solid var(--border-color);
      border-radius: 5px;
      cursor: pointer;
      font-size: 0.8rem;
      transition: background 0.15s, border-color 0.3s, color 0.3s;
    }

    .btn-secondary:hover {
      background: var(--bg-light);
    }

    .btn-danger {
      padding: 0.3rem 0.6rem;
      background: transparent;
      color: #dc2626;
      border: 1px solid #dc2626;
      border-radius: 5px;
      cursor: pointer;
      font-size: 0.8rem;
      transition: background 0.15s;
    }

    .btn-danger:hover {
      background: rgba(220, 38, 38, 0.08);
    }

    @media (max-width: 640px) {
      .autos-grid {
        grid-template-columns: 1fr;
      }

      .autos-header {
        flex-direction: column;
        gap: 1rem;
        align-items: stretch;
      }

      .btn-add {
        width: 100%;
      }
    }
  `]
})
export class AutosComponent {
  autosService = inject(AutosService);
  private toastService = inject(ToastService);

  showModal = signal(false);
  modalMode = signal<'add' | 'edit'>('add');
  selectedAuto = signal<any>(null);

  openAddModal() {
    this.modalMode.set('add');
    this.selectedAuto.set(null);
    this.showModal.set(true);
  }

  openEditModal(auto: any) {
    this.modalMode.set('edit');
    this.selectedAuto.set(auto);
    this.showModal.set(true);
  }

  closeModal() {
    this.showModal.set(false);
    this.selectedAuto.set(null);
  }

  async onSave(data: any) {
    try {
      if (this.modalMode() === 'add') {
        await this.autosService.createAuto(data);
        this.toastService.show('Auto added successfully', 'success');
      } else {
        await this.autosService.updateAuto(this.selectedAuto().id, data);
        this.toastService.show('Auto updated successfully', 'success');
      }
      this.closeModal();
    } catch (err) {
      this.toastService.show('Failed to save auto', 'error');
    }
  }

  async deleteAuto(id: number) {
    if (confirm('Are you sure you want to delete this auto?')) {
      try {
        await this.autosService.deleteAuto(id);
        this.toastService.show('Auto deleted successfully', 'success');
      } catch (err) {
        this.toastService.show('Failed to delete auto', 'error');
      }
    }
  }
}
