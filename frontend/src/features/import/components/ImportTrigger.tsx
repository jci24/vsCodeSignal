import { Upload } from 'lucide-react'

import { Button } from '@/shared/ui/button'

import styles from './ImportTrigger.module.scss'

interface ImportTriggerProps {
  isOpen: boolean
  lastSuccessfulImportSummary: string | null
  onToggle: () => void
}

export function ImportTrigger({
  isOpen,
  lastSuccessfulImportSummary,
  onToggle,
}: ImportTriggerProps) {
  return (
    <div>
      <Button
        aria-expanded={isOpen}
        className={styles.trigger}
        onClick={onToggle}
        type="button"
      >
        <Upload className="size-4" />
        Import
      </Button>
      {lastSuccessfulImportSummary ? (
        <p className={styles.summary}>{lastSuccessfulImportSummary}</p>
      ) : null}
    </div>
  )
}
