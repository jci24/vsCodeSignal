import { useCallback, useEffect, useRef, useState } from 'react'

import { ApiError } from '@/api/client'
import {
  importFiles,
  uploadFiles,
} from '@/features/import/hooks/service/importService'
import { compactImportFormats, importPathsPlaceholder } from '@/features/import/utils/importConfig'
import {
  formatSelectionSummary,
} from '@/features/import/utils/formatImportSummary'
import type {
  IImportHistoryBatch,
  IImportResponse,
  IImportSelection,
} from '@/features/import/utils/types'

export function useFileImport() {
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [importHistory, setImportHistory] = useState<IImportHistoryBatch[]>([])
  const [isImporting, setIsImporting] = useState(false)
  const [isOpen, setIsOpen] = useState(false)
  const [lastSelection, setLastSelection] = useState<IImportSelection | null>(null)
  const [pathDraft, setPathDraft] = useState('')
  const containerRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!isOpen) {
      return
    }

    function handlePointerDown(event: PointerEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        setIsOpen(false)
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsOpen(false)
      }
    }

    window.addEventListener('pointerdown', handlePointerDown)
    window.addEventListener('keydown', handleKeyDown)

    return () => {
      window.removeEventListener('pointerdown', handlePointerDown)
      window.removeEventListener('keydown', handleKeyDown)
    }
  }, [isOpen])

  const closeImport = useCallback(() => {
    setIsOpen(false)
  }, [])

  const openImport = useCallback(() => {
    setIsOpen(true)
  }, [])

  const toggleImport = useCallback(() => {
    setIsOpen((current) => !current)
  }, [])

  const appendImportHistory = useCallback(
    (selection: IImportSelection, result: IImportResponse): void => {
      setImportHistory((current) => [
        {
          id: `${Date.now()}-${crypto.randomUUID()}`,
          result,
          selection,
          timestamp: Date.now(),
        },
        ...current,
      ])
    },
    [],
  )

  const handleImport = useCallback(async (request: { filePaths: string[] }): Promise<void> => {
    const nextPaths = request.filePaths
      .map((entry) => entry.trim())
      .filter(Boolean)

    if (nextPaths.length === 0) {
      setErrorMessage('Add one or more absolute paths to continue.')
      return
    }

    const nextSelection: IImportSelection = {
      count: nextPaths.length,
      description: formatSelectionSummary(nextPaths.length, 'paths'),
      paths: nextPaths,
    }

    setErrorMessage(null)
    setIsImporting(true)
    setLastSelection(nextSelection)

    try {
      const result = await importFiles({
        filePaths: nextPaths,
      })
      appendImportHistory(nextSelection, result)
      setPathDraft('')
      setIsOpen(false)
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message)
      } else {
        setErrorMessage('Import failed. Check the backend connection and try again.')
      }
      console.error(error instanceof Error ? error.message : 'Unknown import error', error)
    } finally {
      setIsImporting(false)
    }
  }, [appendImportHistory])

  const handleBrowserImport = useCallback(async (files: FileList | File[]): Promise<void> => {
    const nextFiles = Array.from(files)

    if (nextFiles.length === 0) {
      return
    }

    const nextSelection: IImportSelection = {
      count: nextFiles.length,
      description: formatSelectionSummary(nextFiles.length, 'upload'),
      paths: nextFiles.map((file) => file.name),
    }

    setErrorMessage(null)
    setIsImporting(true)
    setLastSelection(nextSelection)

    try {
      const result = await uploadFiles(nextFiles)
      appendImportHistory(nextSelection, result)
      setIsOpen(false)
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message)
      } else {
        setErrorMessage('Upload failed. Check the backend connection and try again.')
      }
      console.error(error instanceof Error ? error.message : 'Unknown upload error', error)
    } finally {
      setIsImporting(false)
    }
  }, [appendImportHistory])

  const handlePathDraftChange = useCallback((value: string): void => {
    setPathDraft(value)
    setErrorMessage(null)
  }, [])

  const totalImportedFileCount = importHistory.reduce(
    (count, batch) => count + batch.result.importedFiles.length,
    0,
  )
  const totalFailedFileCount = importHistory.reduce(
    (count, batch) => count + batch.result.failedPaths.length,
    0,
  )
  const importHistorySummary =
    importHistory.length > 0
      ? [
          `${totalImportedFileCount} imported`,
          importHistory.length > 1
            ? `across ${importHistory.length} batches`
            : null,
          totalFailedFileCount > 0 ? `${totalFailedFileCount} failed` : null,
        ]
          .filter(Boolean)
          .join(' ')
      : null

  return {
    closeImport,
    compactImportFormats,
    containerRef,
    errorMessage,
    importHistory,
    importHistorySummary,
    importPathsPlaceholder,
    isLoading: isImporting,
    isOpen,
    lastSelection,
    openImport,
    pathDraft,
    handleBrowserImport,
    handleImport,
    handlePathDraftChange,
    toggleImport,
  }
}
