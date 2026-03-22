import type { IImportResponse } from '@/features/import/utils/types'

export function formatSelectionSummary(count: number, mode: 'upload' | 'paths') {
  const noun = count === 1 ? 'item' : 'items'

  if (mode === 'upload') {
    return `${count} ${noun} selected`
  }

  return `${count} ${noun} queued`
}

export const importPathsPlaceholder = '/Users/you/Capture.wav\n/Users/you/Captures'

export function formatImportResultSummary(result: IImportResponse) {
  const importedCount = result.importedFiles.length
  const failedCount = result.failedPaths.length

  if (importedCount > 0 && failedCount > 0) {
    return `${importedCount} imported, ${failedCount} failed`
  }

  if (importedCount > 0) {
    return `${importedCount} imported successfully`
  }

  if (failedCount > 0) {
    return `${failedCount} failed to import`
  }

  return 'No files were imported'
}
