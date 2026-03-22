import type { JSX } from 'react'

import { cn } from '@/shared/lib/cn'

import styles from './SignalStudioLogo.module.scss'

interface SignalStudioLogoProps {
  compact?: boolean
  className?: string
}

export function SignalStudioLogo({
  className,
  compact = false,
}: SignalStudioLogoProps): JSX.Element {
  return (
    <div
      className={cn(
        styles.root,
        compact ? styles.compact : styles.default,
        className,
      )}
    >
      <div className={styles.mark} aria-hidden="true">
        <svg
          className={styles.markSvg}
          fill="none"
          viewBox="0 0 48 48"
          xmlns="http://www.w3.org/2000/svg"
        >
          <rect
            className={styles.markFrame}
            height="34"
            rx="11"
            width="34"
            x="7"
            y="7"
          />
          <path
            className={styles.markStroke}
            d="M16 28.5L21.5 22.75L26 27L32 19.5"
          />
          <path className={styles.markAccent} d="M16 18.5H21.25" />
          <path className={styles.markAccent} d="M27.5 31H32" />
        </svg>
      </div>

      {!compact ? (
        <div className={styles.wordmark}>
          <span className={styles.brand}>Signal Studio</span>
          <span className={styles.subline}>Signals workspace</span>
        </div>
      ) : null}
    </div>
  )
}
