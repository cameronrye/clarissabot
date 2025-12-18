export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
  isStreaming?: boolean;
}

export interface StreamEvent {
  type: 'conversationId' | 'chunk' | 'usage' | 'done' | 'error';
  conversationId?: string;
  content?: string;
  duration?: number;
  message?: string;
}

