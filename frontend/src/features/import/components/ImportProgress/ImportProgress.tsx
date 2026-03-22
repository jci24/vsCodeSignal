import type { JSX } from 'react'
import { LoaderCircle } from 'lucide-react'

import styles from './ImportProgress.module.scss'

export const ImportProgress = (): JSX.Element => {
  return (
    <div className={styles.root}>
      <div className={styles.icon}>
        <LoaderCircle className="size-5 animate-spin" />
      </div>
      <p className={styles.label}>Importing...</p>
    </div>
  )
}
