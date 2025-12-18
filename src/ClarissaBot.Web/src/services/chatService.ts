import type { StreamEvent } from '../types/chat';

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
    headers: {
      'Content-Type': 'application/json',
    },
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

