import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'

import { ApiError } from '@/api/client'
import { workspaceImportedEventName } from '@/features/import/utils/workspaceImportEvents'

import { workspaceService } from '../service/workspaceService'
import type {
  IWorkspaceImportBatch,
  IWorkspaceImportedFile,
  IWorkspaceImportsResponse,
} from '../utils/types'

interface IWorkspaceSelection {
  batch: IWorkspaceImportBatch | null
  file: IWorkspaceImportedFile | null
}

export const useWorkspaceImports = () => {
  const [searchParams, setSearchParams] = useSearchParams()
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [workspace, setWorkspace] = useState<IWorkspaceImportsResponse | null>(null)
  const workspaceRef = useRef<IWorkspaceImportsResponse | null>(null)

  const requestedBatchId = searchParams.get('batch')
  const requestedFileId = searchParams.get('file')
  const refreshKey = searchParams.get('refresh')

  useEffect(() => {
    workspaceRef.current = workspace
  }, [workspace])

  const loadWorkspaceImports = useCallback(async (): Promise<void> => {
    const isInitialLoad = workspaceRef.current === null

    if (isInitialLoad) {
      setIsLoading(true)
    } else {
      setIsRefreshing(true)
    }

    setErrorMessage(null)

    try {
      const result = await workspaceService.getCurrentImports()
      setWorkspace(result)
      workspaceRef.current = result
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message)
      } else {
        setErrorMessage('Workspace imports could not be loaded.')
      }
    } finally {
      setIsLoading(false)
      setIsRefreshing(false)
    }
  }, [])

  useEffect(() => {
    void loadWorkspaceImports()
  }, [loadWorkspaceImports, refreshKey])

  useEffect(() => {
    function handleWorkspaceImported(): void {
      void loadWorkspaceImports()
    }

    window.addEventListener(workspaceImportedEventName, handleWorkspaceImported)

    return () => {
      window.removeEventListener(workspaceImportedEventName, handleWorkspaceImported)
    }
  }, [loadWorkspaceImports])

  const selection = useMemo<IWorkspaceSelection>(() => {
    if (!workspace || workspace.batches.length === 0) {
      return {
        batch: null,
        file: null,
      }
    }

    const matchingFileBatch = requestedFileId
      ? workspace.batches.find((batch) =>
          batch.importedFiles.some((file) => file.id === requestedFileId),
        )
      : null

    const batch =
      matchingFileBatch ??
      (requestedBatchId
        ? workspace.batches.find((entry) => entry.id === requestedBatchId) ?? null
        : null) ??
      workspace.batches[0]

    if (!batch) {
      return {
        batch: null,
        file: null,
      }
    }

    const file =
      (requestedFileId
        ? batch.importedFiles.find((entry) => entry.id === requestedFileId) ?? null
        : null) ??
      batch.importedFiles[0] ??
      null

    return {
      batch,
      file,
    }
  }, [requestedBatchId, requestedFileId, workspace])

  const selectFile = useCallback((batchId: string, fileId: string): void => {
    const nextParams = new URLSearchParams(searchParams)

    nextParams.set('batch', batchId)
    nextParams.set('file', fileId)
    nextParams.delete('refresh')

    setSearchParams(nextParams, { replace: true })
  }, [searchParams, setSearchParams])

  return {
    batches: workspace?.batches ?? [],
    batchCount: workspace?.batchCount ?? 0,
    errorMessage,
    failedFileCount: workspace?.failedFileCount ?? 0,
    importedFileCount: workspace?.importedFileCount ?? 0,
    isLoading,
    isRefreshing,
    reloadWorkspace: loadWorkspaceImports,
    selectedBatch: selection.batch,
    selectedFile: selection.file,
    selectFile,
    workspaceId: workspace?.workspaceId ?? 'current',
  }
}
