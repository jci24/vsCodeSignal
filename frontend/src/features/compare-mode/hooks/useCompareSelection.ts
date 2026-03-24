import { useMemo, useState } from 'react'

import type { IWorkspaceImportedFile } from '@/pages/workspace/utils/types'

import type { CompareLayoutMode } from '../utils/types'

interface UseCompareSelectionParams {
  files: IWorkspaceImportedFile[]
  primaryFileId: string | null
}

export const useCompareSelection = ({
  files,
  primaryFileId,
}: UseCompareSelectionParams) => {
  const [layoutMode, setLayoutMode] = useState<CompareLayoutMode>('stack')
  const [requestedComparisonIds, setRequestedComparisonIds] = useState<string[]>([])

  const comparableFiles = useMemo(
    () => files.filter((file) => file.id !== primaryFileId),
    [files, primaryFileId],
  )
  const comparableFileIds = useMemo(
    () => comparableFiles.map((file) => file.id),
    [comparableFiles],
  )
  const comparableIdSet = useMemo(
    () => new Set(comparableFileIds),
    [comparableFileIds],
  )
  const selectedComparisonIds = useMemo(
    () => {
      if (!primaryFileId || comparableFileIds.length === 0) {
        return []
      }

      return requestedComparisonIds.filter((fileId) => comparableIdSet.has(fileId))
    },
    [comparableFileIds.length, comparableIdSet, primaryFileId, requestedComparisonIds],
  )
  const compareFiles = useMemo(
    () =>
      comparableFiles.filter((file) => selectedComparisonIds.includes(file.id)),
    [comparableFiles, selectedComparisonIds],
  )
  const isCompareMode = compareFiles.length > 0

  const toggleComparisonFile = (fileId: string): void => {
    if (!comparableIdSet.has(fileId)) {
      return
    }

    setRequestedComparisonIds((current) =>
      current.includes(fileId)
        ? current.filter((currentId) => currentId !== fileId)
        : [...current, fileId],
    )
  }

  const clearCompareSelection = (): void => {
    setRequestedComparisonIds([])
  }

  const replaceComparisonIds = (fileIds: string[]): void => {
    const next = fileIds.filter((fileId) => comparableIdSet.has(fileId))
    setRequestedComparisonIds(next)
  }

  return {
    canCompare: comparableFiles.length > 0,
    compareCount: compareFiles.length + 1,
    compareFiles,
    isCompareMode,
    isFileSelectedForCompare: (fileId: string) => selectedComparisonIds.includes(fileId),
    layoutMode,
    clearCompareSelection,
    replaceComparisonIds,
    selectedComparisonIds,
    setLayoutMode,
    toggleComparisonFile,
  }
}
