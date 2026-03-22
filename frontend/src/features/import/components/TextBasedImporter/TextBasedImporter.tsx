import { useState, type DragEvent, type JSX, type KeyboardEvent } from 'react'
import { Upload } from 'lucide-react'

import type { IImportHistoryBatch, IImportRequest, IImportSelection } from '../../utils/types'

import { formatImportResultSummary } from '../../utils/formatImportSummary'
import { Button } from '@/shared/ui/button'

import styles from './TextBasedImporter.module.scss'

interface ITextBasedImporterProps {
  compactImportFormats: string
  errorMessage: string | null
  importHistory: IImportHistoryBatch[]
  importPathsPlaceholder: string
  lastSelection: IImportSelection | null
  onBrowserImport: (files: File[]) => void
  onImport: (request: IImportRequest) => void
  onOpenFileSelector: () => void
  onPathDraftChange: (value: string) => void
  pathDraft: string
}

export const TextBasedImporter = ({
  compactImportFormats,
  errorMessage,
  importHistory,
  importPathsPlaceholder,
  lastSelection,
  onBrowserImport,
  onImport,
  onOpenFileSelector,
  onPathDraftChange,
  pathDraft,
}: ITextBasedImporterProps): JSX.Element => {
  const [isDragActive, setIsDragActive] = useState(false)

  const handleDrop = async (event: DragEvent<HTMLDivElement>): Promise<void> => {
    event.preventDefault()
    event.stopPropagation()
    setIsDragActive(false)

    const files = await extractFilesFromDrop(event)

    if (files.length > 0) {
      onBrowserImport(files)
    }
  }

  const handleDropzoneKeyDown = (event: KeyboardEvent<HTMLDivElement>): void => {
    if (event.key !== 'Enter' && event.key !== ' ') {
      return
    }

    event.preventDefault()
    onOpenFileSelector()
  }

  const handleTextImport = (): void => {
    const filePaths = pathDraft
      .split(/\r?\n/g)
      .map((entry) => entry.trim())
      .filter(Boolean)

    onImport({ filePaths })
  }

  return (
    <div className={styles.root}>
      <div
        aria-label="Upload files or folders"
        className={styles.dropzone}
        data-drag-active={isDragActive}
        onClick={onOpenFileSelector}
        onDragEnter={(event) => {
          event.preventDefault()
          setIsDragActive(true)
        }}
        onDragLeave={(event) => {
          event.preventDefault()

          if (event.currentTarget.contains(event.relatedTarget as Node | null)) {
            return
          }

          setIsDragActive(false)
        }}
        onDragOver={(event) => {
          event.preventDefault()
          setIsDragActive(true)
        }}
        onDrop={(event) => {
          void handleDrop(event)
        }}
        onKeyDown={handleDropzoneKeyDown}
        role="button"
        tabIndex={0}
      >
        <div className={styles.dropIcon}>
          <Upload className="size-5" />
        </div>
        <p className={styles.dropTitle}>Drop files or folders</p>
        <p className={styles.dropHint}>or click to browse</p>
        <p className={styles.supported}>{compactImportFormats}</p>
      </div>

      <div className={styles.entry}>
        <p className={styles.entryLabel}>Manual path</p>
        <textarea
          className={styles.textarea}
          onChange={(event) => onPathDraftChange(event.target.value)}
          placeholder={importPathsPlaceholder}
          rows={3}
          spellCheck={false}
          value={pathDraft}
        />
        <div className={styles.actions}>
          <Button onClick={handleTextImport} size="sm" type="button" variant="outline">
            Import path
          </Button>
        </div>
      </div>

      {errorMessage ? <p className={styles.error}>{errorMessage}</p> : null}

      {lastSelection || importHistory.length > 0 ? (
        <div className={styles.status}>
          {importHistory.length === 0 && lastSelection ? (
            <p className={styles.statusText}>{lastSelection.description}</p>
          ) : null}
          {importHistory.length > 0 ? (
            <div className={styles.history}>
              <p className={styles.historyLabel}>Import history</p>
              <ul className={styles.historyList}>
                {importHistory.map((batch) => (
                  <li className={styles.historyBatch} key={batch.id}>
                    <p className={styles.statusText}>{formatImportResultSummary(batch.result)}</p>
                    <ul className={styles.resultList}>
                      {batch.result.importedFiles.map((file) => (
                        <li className={styles.resultItem} key={`${batch.id}-${file.sourcePath}`}>
                          <span className={styles.path}>{file.sourcePath}</span>
                        </li>
                      ))}
                      {batch.result.failedPaths.map((failure) => (
                        <li className={styles.resultItem} key={`${batch.id}-${failure.path}`}>
                          <span className={styles.path}>{failure.path}</span>
                        </li>
                      ))}
                    </ul>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}

const extractFilesFromDrop = async (event: DragEvent<HTMLDivElement>): Promise<File[]> => {
  const { extractDroppedFiles } = await import('../../utils/extractDroppedFiles')
  return extractDroppedFiles(event.dataTransfer)
}
