import type { IImportFailure } from '@/features/import/utils/types'

export interface IWorkspaceImportedFile {
  adapter: string
  batchId: string
  channelCount: number | null
  durationSeconds: number | null
  format: string
  id: string
  importedAtUtc: string
  metadata: Record<string, string>
  previewUrl: string
  sampleRateHz: number | null
  signalKind: string
  sizeBytes: number
  sourcePath: string
  storedInWorkspace: boolean
}

export interface IWorkspaceImportBatch {
  failedFileCount: number
  failedPaths: IImportFailure[]
  id: string
  importedAtUtc: string
  importedFileCount: number
  importedFiles: IWorkspaceImportedFile[]
}

export interface IWorkspaceImportsResponse {
  batchCount: number
  batches: IWorkspaceImportBatch[]
  failedFileCount: number
  importedFileCount: number
  workspaceId: string
}
