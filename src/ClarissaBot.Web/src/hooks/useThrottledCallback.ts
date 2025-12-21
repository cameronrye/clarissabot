import { useCallback, useRef } from 'react';

/**
 * Returns a throttled version of the callback that will only execute
 * at most once per specified interval. Useful for high-frequency updates
 * like streaming where we want to reduce re-renders.
 */
export function useThrottledCallback<T extends (...args: unknown[]) => void>(
  callback: T,
  intervalMs: number
): T {
  const lastCallRef = useRef<number>(0);
  const pendingArgsRef = useRef<Parameters<T> | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  return useCallback(
    ((...args: Parameters<T>) => {
      const now = Date.now();
      const elapsed = now - lastCallRef.current;

      if (elapsed >= intervalMs) {
        // Enough time has passed, execute immediately
        lastCallRef.current = now;
        callback(...args);
      } else {
        // Store the latest args and schedule execution
        pendingArgsRef.current = args;

        if (!timeoutRef.current) {
          const remaining = intervalMs - elapsed;
          timeoutRef.current = setTimeout(() => {
            lastCallRef.current = Date.now();
            if (pendingArgsRef.current) {
              callback(...pendingArgsRef.current);
              pendingArgsRef.current = null;
            }
            timeoutRef.current = null;
          }, remaining);
        }
      }
    }) as T,
    [callback, intervalMs]
  );
}

