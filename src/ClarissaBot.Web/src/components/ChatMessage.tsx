import type { ChatMessage as ChatMessageType } from '../types/chat';
import ReactMarkdown from 'react-markdown';
import { ClarissaAvatar, UserAvatar } from './Icons';
import './ChatMessage.css';

interface ChatMessageProps {
  message: ChatMessageType;
}

export function ChatMessage({ message }: ChatMessageProps) {
  const isUser = message.role === 'user';

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
        </div>
        <div className="message-text">
          {isUser ? (
            message.content
          ) : (
            <ReactMarkdown>{message.content}</ReactMarkdown>
          )}
          {message.isStreaming && <span className="cursor">▊</span>}
        </div>
      </div>
    </div>
  );
}
