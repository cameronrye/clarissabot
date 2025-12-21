import { useState, useRef, useCallback, useEffect } from 'react';
import type { ChatMessage as ChatMessageType, VehicleContext, ToolCallInfo } from './types/chat';
import { streamChatMessage } from './services/chatService';
import { ChatMessage } from './components/ChatMessage';
import { ChatInput } from './components/ChatInput';
import { InfoOverlay } from './components/InfoOverlay';
import { ThemeToggle } from './components/ThemeToggle';
import { VehicleContextBadge } from './components/VehicleContextBadge';
import { useTheme } from './hooks/useTheme';
import { ClarissaLogo, Info, Bell, Star, FileText, AlertTriangle, GradientHeart } from './components/Icons';
import './App.css';

// Throttle interval for streaming updates (ms) - reduces re-renders during fast streaming
const STREAM_THROTTLE_MS = 32; // ~30fps, smooth enough for text

function App() {
  const [messages, setMessages] = useState<ChatMessageType[]>([]);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showInfo, setShowInfo] = useState(false);
  const [showClearConfirm, setShowClearConfirm] = useState(false);
  const [vehicleContext, setVehicleContext] = useState<VehicleContext | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const { theme, setTheme } = useTheme();

  // Streaming buffer refs for throttled updates
  const chunkBufferRef = useRef<string>('');
  const flushTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const lastFlushRef = useRef<number>(0);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, scrollToBottom]);

  const handleSendMessage = useCallback(async (content: string) => {
    setError(null);
    
    // Add user message
    const userMessage: ChatMessageType = {
      id: Date.now().toString(),
      role: 'user',
      content,
      timestamp: new Date(),
    };
    
    setMessages(prev => [...prev, userMessage]);
    
    // Add placeholder assistant message
    const assistantId = (Date.now() + 1).toString();
    const assistantMessage: ChatMessageType = {
      id: assistantId,
      role: 'assistant',
      content: '',
      timestamp: new Date(),
      isStreaming: true,
    };
    
    setMessages(prev => [...prev, assistantMessage]);
    setIsStreaming(true);
    
    abortControllerRef.current = new AbortController();
    
    // Reset buffer for new stream
    chunkBufferRef.current = '';
    lastFlushRef.current = Date.now();

    // Helper to flush buffered chunks to state
    const flushBuffer = () => {
      if (chunkBufferRef.current) {
        const bufferedContent = chunkBufferRef.current;
        chunkBufferRef.current = '';
        setMessages(prev => prev.map(msg =>
          msg.id === assistantId
            ? { ...msg, content: msg.content + bufferedContent, currentTool: undefined }
            : msg
        ));
      }
      lastFlushRef.current = Date.now();
    };

    try {
      await streamChatMessage(
        content,
        conversationId,
        {
          onConversationId: (id) => setConversationId(id),
          onChunk: (chunk) => {
            // Buffer chunks and throttle updates to reduce re-renders
            chunkBufferRef.current += chunk;
            const now = Date.now();
            const elapsed = now - lastFlushRef.current;

            if (elapsed >= STREAM_THROTTLE_MS) {
              // Enough time has passed, flush immediately
              if (flushTimeoutRef.current) {
                clearTimeout(flushTimeoutRef.current);
                flushTimeoutRef.current = null;
              }
              flushBuffer();
            } else if (!flushTimeoutRef.current) {
              // Schedule a flush for the remaining time
              flushTimeoutRef.current = setTimeout(() => {
                flushTimeoutRef.current = null;
                flushBuffer();
              }, STREAM_THROTTLE_MS - elapsed);
            }
          },
          onToolCall: (toolCall: ToolCallInfo) => {
            // Update message to show which tool is being called
            setMessages(prev => prev.map(msg =>
              msg.id === assistantId
                ? { ...msg, currentTool: toolCall }
                : msg
            ));
          },
          onToolResult: () => {
            // Clear the current tool when result is received
            setMessages(prev => prev.map(msg =>
              msg.id === assistantId
                ? { ...msg, currentTool: undefined }
                : msg
            ));
          },
          onVehicleContext: (context) => {
            setVehicleContext(context);
          },
          onDone: () => {
            // Flush any remaining buffered content
            if (flushTimeoutRef.current) {
              clearTimeout(flushTimeoutRef.current);
              flushTimeoutRef.current = null;
            }
            const remainingContent = chunkBufferRef.current;
            chunkBufferRef.current = '';

            setMessages(prev => prev.map(msg =>
              msg.id === assistantId
                ? { ...msg, content: msg.content + remainingContent, isStreaming: false, currentTool: undefined }
                : msg
            ));
            setIsStreaming(false);
          },
          onError: (message) => {
            setError(message);
            setIsStreaming(false);
          },
        },
        abortControllerRef.current.signal
      );
    } catch (err) {
      // Clean up buffer and timeout on error
      if (flushTimeoutRef.current) {
        clearTimeout(flushTimeoutRef.current);
        flushTimeoutRef.current = null;
      }
      const remainingContent = chunkBufferRef.current;
      chunkBufferRef.current = '';

      if (err instanceof Error && err.name === 'AbortError') {
        // User cancelled - include any buffered content
        setMessages(prev => prev.map(msg =>
          msg.id === assistantId
            ? { ...msg, isStreaming: false, currentTool: undefined, content: msg.content + remainingContent + ' [Cancelled]' }
            : msg
        ));
      } else {
        setError(err instanceof Error ? err.message : 'An error occurred');
        // Remove the empty assistant message on error
        setMessages(prev => prev.filter(msg => msg.id !== assistantId || msg.content));
      }
      setIsStreaming(false);
    }
  }, [conversationId]);

  const handleCancel = useCallback(() => {
    abortControllerRef.current?.abort();
    setIsStreaming(false);
  }, []);

  const handleClearChat = useCallback(async () => {
    // Clear server-side conversation if we have a conversation ID
    if (conversationId) {
      try {
        await fetch(`/api/chat/${conversationId}`, { method: 'DELETE' });
      } catch (err) {
        console.error('Failed to clear server conversation:', err);
        // Continue with local clear even if server clear fails
      }
    }
    setMessages([]);
    setConversationId(null);
    setVehicleContext(null);
    setError(null);
    setShowClearConfirm(false);
  }, [conversationId]);

  return (
    <div className="app">
      <header className="app-header">
        <div className="header-brand">
          <ClarissaLogo size={28} />
          <h1>Clarissa</h1>
        </div>
        <div className="header-subtitle">
          <p>NHTSA Vehicle Safety Assistant</p>
          {vehicleContext && <VehicleContextBadge vehicle={vehicleContext} />}
        </div>
        <div className="header-actions">
          {messages.length > 0 && (
            <button onClick={() => setShowClearConfirm(true)} className="clear-button">
              Clear Chat
            </button>
          )}
          <ThemeToggle theme={theme} setTheme={setTheme} />
          <button onClick={() => setShowInfo(true)} className="info-button" aria-label="About">
            <Info size={18} />
          </button>
        </div>
      </header>

      {showClearConfirm && (
        <div className="confirm-overlay" onClick={() => setShowClearConfirm(false)}>
          <div className="confirm-dialog" onClick={e => e.stopPropagation()}>
            <p>Are you sure you want to clear the chat?</p>
            <div className="confirm-actions">
              <button onClick={() => setShowClearConfirm(false)} className="confirm-cancel">
                Cancel
              </button>
              <button onClick={handleClearChat} className="confirm-clear">
                Clear
              </button>
            </div>
          </div>
        </div>
      )}

      <InfoOverlay isOpen={showInfo} onClose={() => setShowInfo(false)} />

      <main className="chat-container">
        {messages.length === 0 ? (
          <div className="welcome-message">
            <h2>Welcome</h2>
            <p>I can help you with vehicle safety information from NHTSA:</p>
            <ul>
              <li><Bell size={18} /> <strong>Recalls</strong> <span>- Check for safety recalls on any vehicle</span></li>
              <li><Star size={18} /> <strong>Safety Ratings</strong> <span>- Get NCAP crash test ratings</span></li>
              <li><FileText size={18} /> <strong>Complaints</strong> <span>- View consumer complaints</span></li>
            </ul>
            <p className="example-label">Try asking:</p>
            <div className="example-queries">
              <button
                className="example-query"
                onClick={() => handleSendMessage("Any recalls on the 2020 Tesla Model 3?")}
                disabled={isStreaming}
              >
                🚨 "Any recalls on the 2020 Tesla Model 3?"
              </button>
              <button
                className="example-query"
                onClick={() => handleSendMessage("What's the safety rating for the 2024 Toyota Camry?")}
                disabled={isStreaming}
              >
                ⭐ "What's the safety rating for the 2024 Toyota Camry?"
              </button>
              <button
                className="example-query"
                onClick={() => handleSendMessage("Show me complaints for the 2022 Honda Accord")}
                disabled={isStreaming}
              >
                💬 "Show me complaints for the 2022 Honda Accord"
              </button>
            </div>
          </div>
        ) : (
          <div className="messages">
            {messages.map(msg => (
              <ChatMessage key={msg.id} message={msg} />
            ))}
            <div ref={messagesEndRef} />
          </div>
        )}

        {error && (
          <div className="error-banner">
            <AlertTriangle size={18} />
            <span>{error}</span>
            <button onClick={() => setError(null)}>Dismiss</button>
          </div>
        )}
      </main>

      <footer className="chat-footer">
        <ChatInput
          onSend={handleSendMessage}
          disabled={isStreaming}
          isStreaming={isStreaming}
          onCancel={handleCancel}
        />
        <p className="attribution">
          Made with <span className="heart"><GradientHeart size={14} /></span> by <a href="https://rye.dev" target="_blank" rel="noopener noreferrer">Cameron Rye</a>
        </p>
      </footer>
    </div>
  );
}

export default App;

