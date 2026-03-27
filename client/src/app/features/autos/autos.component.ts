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
        <h2>My Vehicles</h2>
        <button (click)="openAddModal()" class="btn-add">+ Add Vehicle</button>
      </div>

      @if (autosService.autos().length === 0) {
        <p class="empty-state">No vehicles yet. Add one to get started.</p>
      } @else {
        <div class="autos-grid">
          @for (auto of autosService.autos(); track auto.id) {
            <div class="auto-card">
              <div class="auto-info">
                <h3>{{ auto.brand }} {{ auto.model }}</h3>
                <p class="plate">{{ auto.plate }}</p>
                <p class="odometer">{{ auto.odometer | number }} mi</p>
              </div>
              <div class="auto-actions">
                <button (click)="openEditModal(auto)" class="btn-icon" title="Edit">✏️</button>
                <button (click)="deleteAuto(auto.id)" class="btn-icon btn-danger" title="Delete">🗑️</button>
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
      padding: 1rem;
    }

    .autos-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
    }

    .autos-header h2 {
      margin: 0;
      font-size: 1.5rem;
      color: #111;
    }

    .btn-add {
      padding: 0.6rem 1rem;
      background: #ec7004;
      color: #fff;
      border: none;
      border-radius: 5px;
      cursor: pointer;
      font-weight: 500;
      transition: background 0.15s;
    }

    .btn-add:hover {
      background: #d86500;
    }

    .empty-state {
      text-align: center;
      color: #666;
      padding: 2rem;
    }

    .autos-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
      gap: 1rem;
    }

    .auto-card {
      background: #fff;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      padding: 1rem;
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
    }

    .auto-info {
      flex: 1;
    }

    .auto-info h3 {
      margin: 0 0 0.25rem 0;
      font-size: 1.1rem;
      color: #111;
    }

    .plate {
      margin: 0.25rem 0;
      font-size: 0.9rem;
      color: #666;
      font-weight: 500;
    }

    .odometer {
      margin: 0.25rem 0 0 0;
      font-size: 0.85rem;
      color: #999;
    }

    .auto-actions {
      display: flex;
      gap: 0.5rem;
    }

    .btn-icon {
      padding: 0.4rem;
      background: #f0f0f0;
      border: 1px solid #ddd;
      border-radius: 5px;
      cursor: pointer;
      font-size: 1rem;
      transition: background 0.15s;
    }

    .btn-icon:hover {
      background: #e0e0e0;
    }

    .btn-icon.btn-danger:hover {
      background: #ffcccc;
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
        this.toastService.show('Vehicle added successfully', 'success');
      } else {
        await this.autosService.updateAuto(this.selectedAuto().id, data);
        this.toastService.show('Vehicle updated successfully', 'success');
      }
      this.closeModal();
    } catch (err) {
      this.toastService.show('Failed to save vehicle', 'error');
    }
  }

  async deleteAuto(id: number) {
    if (confirm('Are you sure you want to delete this vehicle?')) {
      try {
        await this.autosService.deleteAuto(id);
        this.toastService.show('Vehicle deleted successfully', 'success');
      } catch (err) {
        this.toastService.show('Failed to delete vehicle', 'error');
      }
    }
  }
}
