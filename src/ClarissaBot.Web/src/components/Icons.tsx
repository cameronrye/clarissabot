import {
  Car,
  User,
  Info,
  Hand,
  Bell,
  Star,
  FileText,
  AlertTriangle,
  Heart,
  X,
  Wrench,
  UserCircle,
  Github,
  BookOpen,
  ExternalLink,
} from 'lucide-react';

// Re-export all icons for easy importing
export {
  Car,
  User,
  Info,
  Hand,
  Bell,
  Star,
  FileText,
  AlertTriangle,
  Heart,
  X,
  Wrench,
  UserCircle,
  Github,
  BookOpen,
  ExternalLink,
};

// Clarissa Logo - Uses the favicon SVG
export function ClarissaLogo({ size = 32, className = '' }: { size?: number; className?: string }) {
  return (
    <img
      src="/favicon.svg"
      alt="Clarissa"
      width={size}
      height={size}
      className={className}
      style={{ display: 'block' }}
    />
  );
}

// Avatar version of the logo for chat messages
export function ClarissaAvatar({ size = 24 }: { size?: number }) {
  return <ClarissaLogo size={size} />;
}

// User avatar with consistent styling
export function UserAvatar({ size = 24 }: { size?: number }) {
  return <User size={size} strokeWidth={2} />;
}

// Heart icon matching the Clarissa brand pink color
export function GradientHeart({ size = 14 }: { size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
    >
      <path
        d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"
        fill="#EC4899"
      />
    </svg>
  );
}

