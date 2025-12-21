import { useState, memo, lazy, Suspense, type ReactNode } from 'react';
import type { ChatMessage as ChatMessageType } from '../types/chat';
import { ClarissaAvatar, UserAvatar, Copy, Check, Bell, Star, MessageSquare, Search, Microscope, Loader2 } from './Icons';
import './ChatMessage.css';

// Lazy-load react-markdown to reduce initial bundle size (~40KB gzipped)
const ReactMarkdown = lazy(() => import('react-markdown'));

// Simple loading placeholder while markdown loads
function MarkdownLoading() {
  return <span className="markdown-loading">Loading...</span>;
}

interface ChatMessageProps {
  message: ChatMessageType;
  onSuggestionClick?: (suggestion: string) => void;
}

/**
 * Gets the appropriate icon component for a tool
 */
function getToolIcon(toolName: string): ReactNode {
  const iconProps = { size: 14, className: 'tool-icon' };
  switch (toolName) {
    case 'check_recalls': return <Bell {...iconProps} />;
    case 'get_complaints': return <MessageSquare {...iconProps} />;
    case 'get_safety_rating': return <Star {...iconProps} />;
    case 'decode_vin': return <Search {...iconProps} />;
    case 'check_investigations': return <Microscope {...iconProps} />;
    default: return <Loader2 {...iconProps} className="tool-icon spinning" />;
  }
}

// Memoize to prevent re-renders of completed messages when new streaming chunks arrive
export const ChatMessage = memo(function ChatMessage({ message }: ChatMessageProps) {
  const isUser = message.role === 'user';
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(message.content);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  };

  // Build loading state content with icon
  const renderLoadingState = () => {
    if (message.currentTool) {
      const vehicleInfo = message.currentTool.vehicleInfo
        ? ` for ${message.currentTool.vehicleInfo}`
        : '';
      return (
        <span className="thinking">
          {getToolIcon(message.currentTool.toolName)}
          <span className="thinking-text">{message.currentTool.description}{vehicleInfo}</span>
          <span className="thinking-dots">
            <span>.</span><span>.</span><span>.</span>
          </span>
        </span>
      );
    }
    return (
      <span className="thinking">
        <Loader2 size={14} className="tool-icon spinning" />
        <span className="thinking-text">Looking up vehicle data</span>
        <span className="thinking-dots">
          <span>.</span><span>.</span><span>.</span>
        </span>
      </span>
    );
  };

  return (
    <div className={`chat-message ${isUser ? 'user' : 'assistant'}`}>
      <div className={`message-avatar ${isUser ? 'user-avatar' : 'assistant-avatar'}`}>
        {isUser ? <UserAvatar size={22} /> : <ClarissaAvatar size={22} />}
      </div>
      <div className="message-content">
        <div className="message-header">
          <span className="message-role">{isUser ? 'You' : 'Clarissa'}</span>
          <span className="message-time">
            {message.timestamp.toLocaleTimeString()}
          </span>
          {!isUser && !message.isStreaming && message.content && (
            <button
              className={`copy-button ${copied ? 'copied' : ''}`}
              onClick={handleCopy}
              title={copied ? 'Copied!' : 'Copy response'}
              aria-label={copied ? 'Copied!' : 'Copy response'}
            >
              {copied ? <Check size={14} /> : <Copy size={14} />}
            </button>
          )}
        </div>
        <div className="message-text">
          {isUser ? (
            message.content
          ) : message.isStreaming && !message.content ? (
            renderLoadingState()
          ) : (
            <Suspense fallback={<MarkdownLoading />}>
              <ReactMarkdown>{message.content}</ReactMarkdown>
            </Suspense>
          )}
          {message.isStreaming && message.content && <span className="cursor">▊</span>}
        </div>
      </div>
    </div>
  );
}, (prevProps, nextProps) => {
  // Custom comparison for better memoization performance
  // Only re-render if message content/state actually changed
  const prev = prevProps.message;
  const next = nextProps.message;
  return (
    prev.id === next.id &&
    prev.content === next.content &&
    prev.isStreaming === next.isStreaming &&
    prev.currentTool?.toolName === next.currentTool?.toolName
  );
});
