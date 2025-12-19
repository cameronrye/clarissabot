import { useEffect, useRef } from 'react';
import './InfoOverlay.css';

interface InfoOverlayProps {
  isOpen: boolean;
  onClose: () => void;
}

export function InfoOverlay({ isOpen, onClose }: InfoOverlayProps) {
  const overlayRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };

    if (isOpen) {
      document.addEventListener('keydown', handleEscape);
      document.body.style.overflow = 'hidden';
    }

    return () => {
      document.removeEventListener('keydown', handleEscape);
      document.body.style.overflow = '';
    };
  }, [isOpen, onClose]);

  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === overlayRef.current) onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="info-overlay" ref={overlayRef} onClick={handleBackdropClick}>
      <div className="info-modal" role="dialog" aria-modal="true" aria-labelledby="info-title">
        <button className="info-close" onClick={onClose} aria-label="Close">
          ✕
        </button>

        <h2 id="info-title">🚗 About Clarissa</h2>
        <p className="info-description">
          Your AI-powered NHTSA vehicle safety assistant. Get instant information about 
          recalls, safety ratings, and consumer complaints for any vehicle.
        </p>

        <hr className="info-divider" />

        <h3>🛠️ Built With</h3>
        <div className="tech-stack">
          <div className="tech-category">
            <h4>Frontend</h4>
            <ul>
              <li>React 19</li>
              <li>TypeScript</li>
              <li>Vite 7</li>
            </ul>
          </div>
          <div className="tech-category">
            <h4>Backend</h4>
            <ul>
              <li>.NET 9 / C#</li>
              <li>ASP.NET Core Minimal APIs</li>
            </ul>
          </div>
          <div className="tech-category">
            <h4>AI & Cloud</h4>
            <ul>
              <li>Azure AI Foundry</li>
              <li>Azure OpenAI Service</li>
              <li>Azure Identity (DefaultAzureCredential)</li>
            </ul>
          </div>
          <div className="tech-category">
            <h4>Data</h4>
            <ul>
              <li>
                <a
                  href="https://www.nhtsa.gov/nhtsa-datasets-and-apis"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="tech-link"
                >
                  NHTSA Open Data APIs
                  <span className="learn-more">Learn more →</span>
                </a>
              </li>
            </ul>
          </div>
        </div>

        <hr className="info-divider" />

        <h3>👨‍💻 About the Creator</h3>
        <p className="creator-blurb">
          Hi! I'm <strong>Cameron Rye</strong>, a software developer passionate about 
          building useful applications with modern technologies. I'm currently 
          <strong> open to new opportunities</strong> — if you're hiring or want to 
          connect, I'd love to hear from you!
        </p>
        <div className="creator-links">
          <a href="https://rye.dev" target="_blank" rel="noopener noreferrer" className="creator-link">
            📖 Blog (rye.dev)
          </a>
          <a href="https://github.com/cameronrye" target="_blank" rel="noopener noreferrer" className="creator-link">
            🐙 GitHub Profile
          </a>
        </div>

        <hr className="info-divider" />

        <h3>⭐ Project Source</h3>
        <a 
          href="https://github.com/cameronrye/clarissabot" 
          target="_blank" 
          rel="noopener noreferrer"
          className="github-repo-link"
        >
          View on GitHub →
        </a>
      </div>
    </div>
  );
}

