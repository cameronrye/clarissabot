import { Car } from 'lucide-react';
import type { VehicleContext } from '../types/chat';
import './VehicleContextBadge.css';

interface VehicleContextBadgeProps {
  vehicle: VehicleContext;
}

export function VehicleContextBadge({ vehicle }: VehicleContextBadgeProps) {
  return (
    <div className="vehicle-context-badge" title={`Currently discussing: ${vehicle.display}`}>
      <Car size={14} />
      <span>{vehicle.display}</span>
    </div>
  );
}

