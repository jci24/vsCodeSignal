import { useEffect, useMemo, useState } from 'react'

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
  const [selectedComparisonIds, setSelectedComparisonIds] = useState<string[]>([])

  const comparableFiles = useMemo(
    () => files.filter((file) => file.id !== primaryFileId),
    [files, primaryFileId],
  )
  const compareFiles = useMemo(
    () =>
      comparableFiles.filter((file) => selectedComparisonIds.includes(file.id)),
    [comparableFiles, selectedComparisonIds],
  )
  const isCompareMode = compareFiles.length > 0
  const comparableFileIdsKey = comparableFiles.map((file) => file.id).join('|')

  useEffect(() => {
    if (!primaryFileId || comparableFiles.length === 0) {
      setSelectedComparisonIds((current) => (current.length === 0 ? current : []))
      return
    }

    setSelectedComparisonIds((current) =>
      {
        const next = current.filter((fileId) =>
          comparableFiles.some((file) => file.id === fileId),
        )

        return next.length === current.length &&
          next.every((fileId, index) => fileId === current[index])
          ? current
          : next
      },
    )
  }, [comparableFileIdsKey, comparableFiles, primaryFileId])

  const toggleComparisonFile = (fileId: string): void => {
    setSelectedComparisonIds((current) =>
      current.includes(fileId)
        ? current.filter((currentId) => currentId !== fileId)
        : [...current, fileId],
    )
  }

  const clearCompareSelection = (): void => {
    setSelectedComparisonIds([])
  }

  return {
    canCompare: comparableFiles.length > 0,
    compareCount: compareFiles.length + 1,
    compareFiles,
    isCompareMode,
    isFileSelectedForCompare: (fileId: string) => selectedComparisonIds.includes(fileId),
    layoutMode,
    clearCompareSelection,
    setLayoutMode,
    toggleComparisonFile,
  }
}
