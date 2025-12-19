import { useState, useRef, useCallback, useEffect } from 'react';
import type { ChatMessage as ChatMessageType } from './types/chat';
import { streamChatMessage } from './services/chatService';
import { ChatMessage } from './components/ChatMessage';
import { ChatInput } from './components/ChatInput';
import { InfoOverlay } from './components/InfoOverlay';
import './App.css';

function App() {
  const [messages, setMessages] = useState<ChatMessageType[]>([]);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showInfo, setShowInfo] = useState(false);
  const [showClearConfirm, setShowClearConfirm] = useState(false);
  const abortControllerRef = useRef<AbortController | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

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
    
    try {
      await streamChatMessage(
        content,
        conversationId,
        {
          onConversationId: (id) => setConversationId(id),
          onChunk: (chunk) => {
            setMessages(prev => prev.map(msg =>
              msg.id === assistantId
                ? { ...msg, content: msg.content + chunk }
                : msg
            ));
          },
          onDone: () => {
            setMessages(prev => prev.map(msg =>
              msg.id === assistantId
                ? { ...msg, isStreaming: false }
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
      if (err instanceof Error && err.name === 'AbortError') {
        // User cancelled
        setMessages(prev => prev.map(msg =>
          msg.id === assistantId
            ? { ...msg, isStreaming: false, content: msg.content + ' [Cancelled]' }
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

  const handleClearChat = useCallback(() => {
    setMessages([]);
    setConversationId(null);
    setError(null);
    setShowClearConfirm(false);
  }, []);

  return (
    <div className="app">
      <header className="app-header">
        <h1>🚗 Clarissa</h1>
        <p>NHTSA Vehicle Safety Assistant</p>
        <div className="header-actions">
          {messages.length > 0 && (
            <button onClick={() => setShowClearConfirm(true)} className="clear-button">
              Clear Chat
            </button>
          )}
          <button onClick={() => setShowInfo(true)} className="info-button" aria-label="About">
            ⓘ
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
            <h2>Welcome! 👋</h2>
            <p>I can help you with vehicle safety information from NHTSA:</p>
            <ul>
              <li>🔔 <strong>Recalls</strong> - Check for safety recalls on any vehicle</li>
              <li>⭐ <strong>Safety Ratings</strong> - Get NCAP crash test ratings</li>
              <li>📝 <strong>Complaints</strong> - View consumer complaints</li>
            </ul>
            <p className="example">Try asking: "Are there any recalls on the 2020 Tesla Model 3?"</p>
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
            ⚠️ {error}
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
          Made with <span className="heart">❤️</span> by <a href="https://rye.dev" target="_blank" rel="noopener noreferrer">Cameron Rye</a>
        </p>
      </footer>
    </div>
  );
}

export default App;

