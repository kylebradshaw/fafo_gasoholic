import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'fuelType',
  standalone: true
})
export class FuelTypePipe implements PipeTransform {
  private fuelTypesByIndex = ['Regular', 'Mid-grade', 'Premium', 'Diesel', 'E85'];

  // API enum names → display labels (API returns "MidGrade", display wants "Mid-grade")
  private fuelTypesByName: Record<string, string> = {
    'Regular': 'Regular',
    'MidGrade': 'Mid-grade',
    'Premium': 'Premium',
    'Diesel': 'Diesel',
    'E85': 'E85',
  };

  transform(value: string | number | null | undefined): string {
    if (value === null || value === undefined) {
      return 'Unknown';
    }

    // API returns string enum name (e.g. "Regular", "MidGrade")
    if (typeof value === 'string') {
      return this.fuelTypesByName[value] ?? 'Unknown';
    }

    // Numeric index fallback (used by modal form before server roundtrip)
    if (value >= 0 && value < this.fuelTypesByIndex.length) {
      return this.fuelTypesByIndex[value];
    }

    return 'Unknown';
  }
}
