import type { JSX } from 'react'
import { LoaderCircle } from 'lucide-react'

import { cn } from '@/shared/lib/cn'

import styles from './loading-spinner.module.scss'

interface LoadingSpinnerProps {
  className?: string
  label?: string
  size?: 'sm' | 'md' | 'lg'
}

export function LoadingSpinner({
  className,
  label = 'Loading',
  size = 'md',
}: LoadingSpinnerProps): JSX.Element {
  return (
    <div
      aria-label={label}
      aria-live="polite"
      className={cn(styles.root, className)}
      data-size={size}
      role="status"
    >
      <LoaderCircle aria-hidden="true" className={styles.icon} />
      <span className={styles.srOnly}>{label}</span>
    </div>
  )
}
