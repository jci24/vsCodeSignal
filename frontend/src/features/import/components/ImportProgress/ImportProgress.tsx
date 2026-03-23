import type { JSX } from 'react'

import { LoadingSpinner } from '@/shared/ui/loading-spinner'

import styles from './ImportProgress.module.scss'

export const ImportProgress = (): JSX.Element => {
  return (
    <div className={styles.root}>
      <div className={styles.icon}>
        <LoadingSpinner label="Importing files" size="lg" />
      </div>
    </div>
  )
}
