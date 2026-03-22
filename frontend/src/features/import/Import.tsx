import type { JSX } from 'react'
import { useRef, type ChangeEvent } from 'react'

import { importAcceptValue } from '@/features/import/utils/importConfig'
import type { IImportRequest } from '@/features/import/utils/types'

import { FolderSelector } from './components/FolderSelector/FolderSelector'
import { ImportProgress } from './components/ImportProgress/ImportProgress'
import { TextBasedImporter } from './components/TextBasedImporter/TextBasedImporter'
import { ImportTrigger } from './components/ImportTrigger'
import { useFileImport } from './hooks/useFileImport'
import styles from './Import.module.scss'
import { isWebView } from './utils/validation'

export const Import = (): JSX.Element => {
  const {
    containerRef,
    compactImportFormats,
    errorMessage,
    handleBrowserImport,
    handleImport,
    handlePathDraftChange,
    importHistory,
    importHistorySummary,
    importPathsPlaceholder,
    isLoading,
    isOpen,
    lastSelection,
    pathDraft,
    toggleImport,
  } = useFileImport()
  const filesInputRef = useRef<HTMLInputElement | null>(null)
  const isInWebView: boolean = isWebView()

  const handleUploadSelection = (event: ChangeEvent<HTMLInputElement>): void => {
    if (event.target.files) {
      void handleBrowserImport(event.target.files)
    }

    event.target.value = ''
  }

  const handleFolderSelected = (folderPath: string): void => {
    void handleImport({ filePaths: [folderPath] })
  }

  const handleFileSelectorError = (error: string): void => {
    console.error('File selector error:', error)
  }

  const handleTextBasedImport = (request: IImportRequest): void => {
    void handleImport(request)
  }

  const openUploadPicker = (): void => {
    filesInputRef.current?.click()
  }

  return (
    <div className={styles.root} ref={containerRef}>
      <ImportTrigger
        isOpen={isOpen}
        lastSuccessfulImportSummary={importHistorySummary}
        onToggle={toggleImport}
      />

      {isOpen ? (
        <div className={styles.panel}>
          {isLoading ? (
            <ImportProgress />
          ) : (
            <>
              {isInWebView ? (
                <div className={styles.webviewMode}>
                  <FolderSelector
                    onError={handleFileSelectorError}
                    onFolderSelected={handleFolderSelected}
                  />
                </div>
              ) : (
                <div className={styles.textMode}>
                  <TextBasedImporter
                    compactImportFormats={compactImportFormats}
                    errorMessage={errorMessage}
                    importHistory={importHistory}
                    importPathsPlaceholder={importPathsPlaceholder}
                    lastSelection={lastSelection}
                    onBrowserImport={(files) => {
                      void handleBrowserImport(files)
                    }}
                    onImport={handleTextBasedImport}
                    onOpenFileSelector={openUploadPicker}
                    onPathDraftChange={handlePathDraftChange}
                    pathDraft={pathDraft}
                  />
                </div>
              )}
            </>
          )}
        </div>
      ) : null}

      <input
        accept={importAcceptValue}
        className={styles.hiddenInput}
        multiple
        onChange={handleUploadSelection}
        ref={filesInputRef}
        type="file"
      />
    </div>
  )
}
