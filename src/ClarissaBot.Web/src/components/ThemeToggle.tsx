import { useState, useRef, useEffect } from 'react';
import { Sun, Moon, Monitor } from 'lucide-react';
import type { Theme } from '../hooks/useTheme';
import './ThemeToggle.css';

interface ThemeToggleProps {
  theme: Theme;
  setTheme: (theme: Theme) => void;
}

const themeOptions: { value: Theme; label: string; icon: typeof Sun }[] = [
  { value: 'light', label: 'Light', icon: Sun },
  { value: 'dark', label: 'Dark', icon: Moon },
  { value: 'system', label: 'System', icon: Monitor },
];

export function ThemeToggle({ theme, setTheme }: ThemeToggleProps) {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const currentOption = themeOptions.find(opt => opt.value === theme) || themeOptions[2];
  const CurrentIcon = currentOption.icon;

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };

    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setIsOpen(false);
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      document.addEventListener('keydown', handleEscape);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [isOpen]);

  return (
    <div className="theme-toggle" ref={dropdownRef}>
      <button
        className="theme-toggle-button"
        onClick={() => setIsOpen(!isOpen)}
        aria-label={`Theme: ${currentOption.label}`}
        aria-expanded={isOpen}
        aria-haspopup="listbox"
      >
        <CurrentIcon size={18} />
      </button>

      {isOpen && (
        <div className="theme-dropdown" role="listbox">
          {themeOptions.map(option => {
            const Icon = option.icon;
            return (
              <button
                key={option.value}
                className={`theme-option ${theme === option.value ? 'active' : ''}`}
                onClick={() => {
                  setTheme(option.value);
                  setIsOpen(false);
                }}
                role="option"
                aria-selected={theme === option.value}
              >
                <Icon size={16} />
                <span>{option.label}</span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}

