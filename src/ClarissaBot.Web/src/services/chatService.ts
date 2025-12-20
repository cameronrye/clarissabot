import type { StreamEvent, ToolCallInfo, VehicleContext } from '../types/chat';

/**
 * Get the API key from environment or config.
 * In production, this should be injected at build time or runtime.
 */
function getApiKey(): string | undefined {
  // Vite environment variable (set at build time)
  return import.meta.env.VITE_API_KEY;
}

/**
 * Get common headers for API requests.
 */
function getApiHeaders(): Record<string, string> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };

  const apiKey = getApiKey();
  if (apiKey) {
    headers['X-API-Key'] = apiKey;
  }

  return headers;
}

/**
 * Parse a single SSE line into an event object.
 */
function parseSseLine(line: string): StreamEvent | null {
  if (!line.startsWith('data: ')) return null;

  try {
    const data = JSON.parse(line.slice(6));
    return data as StreamEvent;
  } catch {
    return null;
  }
}

/**
 * Split SSE buffer into complete lines and remaining buffer.
 */
function splitSseBuffer(buffer: string): [string[], string] {
  const parts = buffer.split('\n\n');
  const remaining = parts.pop() || '';
  return [parts, remaining];
}

export interface ChatStreamCallbacks {
  onConversationId?: (conversationId: string) => void;
  onChunk?: (content: string) => void;
  onToolCall?: (toolCall: ToolCallInfo) => void;
  onToolResult?: (toolName: string, success: boolean) => void;
  onVehicleContext?: (context: VehicleContext) => void;
  onUsage?: (duration: number) => void;
  onDone?: () => void;
  onError?: (message: string) => void;
}

/**
 * Send a chat message and stream the response.
 */
export async function streamChatMessage(
  message: string,
  conversationId: string | null,
  callbacks: ChatStreamCallbacks,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetch('/api/chat/stream', {
    method: 'POST',
    headers: getApiHeaders(),
    body: JSON.stringify({
      message,
      conversationId,
    }),
    signal,
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || `HTTP ${response.status}`);
  }

  const reader = response.body?.getReader();
  const decoder = new TextDecoder();

  if (!reader) {
    throw new Error('Response body is not readable');
  }

  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      const chunk = decoder.decode(value, { stream: true });
      buffer += chunk;

      const [lines, remaining] = splitSseBuffer(buffer);
      buffer = remaining;

      for (const line of lines) {
        const event = parseSseLine(line);
        if (!event) continue;

        switch (event.type) {
          case 'conversationId':
            callbacks.onConversationId?.(event.conversationId!);
            break;
          case 'chunk':
            callbacks.onChunk?.(event.content!);
            break;
          case 'toolCall':
            callbacks.onToolCall?.({
              toolName: event.toolName!,
              description: event.description!,
              vehicleInfo: event.vehicleInfo,
            });
            break;
          case 'toolResult':
            callbacks.onToolResult?.(event.toolName!, event.success!);
            break;
          case 'vehicleContext':
            callbacks.onVehicleContext?.({
              year: event.year!,
              make: event.make!,
              model: event.model!,
              display: event.display!,
            });
            break;
          case 'usage':
            callbacks.onUsage?.(event.duration!);
            break;
          case 'done':
            callbacks.onDone?.();
            return;
          case 'error':
            callbacks.onError?.(event.message!);
            throw new Error(event.message);
        }
      }
    }
  } finally {
    try {
      reader.releaseLock();
    } catch {
      // Reader may already be released
    }
  }
}

