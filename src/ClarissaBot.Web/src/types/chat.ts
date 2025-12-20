export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
  isStreaming?: boolean;
  /** Current tool being executed (for loading state) */
  currentTool?: ToolCallInfo;
}

export interface ToolCallInfo {
  toolName: string;
  description: string;
  vehicleInfo?: string;
}

export interface VehicleContext {
  year: number;
  make: string;
  model: string;
  display: string;
}

export interface StreamEvent {
  type: 'conversationId' | 'chunk' | 'usage' | 'done' | 'error' | 'toolCall' | 'toolResult' | 'vehicleContext';
  conversationId?: string;
  content?: string;
  duration?: number;
  message?: string;
  // Tool call events
  toolName?: string;
  description?: string;
  vehicleInfo?: string;
  success?: boolean;
  // Vehicle context events
  year?: number;
  make?: string;
  model?: string;
  display?: string;
}

