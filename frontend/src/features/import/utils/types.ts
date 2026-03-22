export interface IImportFailure {
  path: string
  reason: string
}

export interface IImportedSignalFile {
  adapter: string
  batchId: string
  channelCount: number | null
  durationSeconds: number | null
  format: string
  id: string
  metadata: Record<string, string>
  previewUrl: string
  sampleRateHz: number | null
  signalKind: string
  sizeBytes: number
  sourcePath: string
}

export interface IImportRequest {
  filePaths: string[]
}

export interface IImportResponse {
  batchId: string | null
  failedPaths: IImportFailure[]
  importedFiles: IImportedSignalFile[]
  workspaceBatchCount: number
  workspaceId: string
  workspaceImportedFileCount: number
}

export interface IImportHistoryBatch {
  id: string
  result: IImportResponse
  selection: IImportSelection
  timestamp: number
}

export interface IImportSelection {
  count: number
  description: string
  paths: string[]
}
