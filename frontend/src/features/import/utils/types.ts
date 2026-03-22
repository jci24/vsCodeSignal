export interface IImportFailure {
  path: string
  reason: string
}

export interface IImportedSignalFile {
  adapter: string
  channelCount: number | null
  durationSeconds: number | null
  format: string
  metadata: Record<string, string>
  sampleRateHz: number | null
  signalKind: string
  sizeBytes: number
  sourcePath: string
}

export interface IImportRequest {
  filePaths: string[]
}

export interface IImportResponse {
  failedPaths: IImportFailure[]
  importedFiles: IImportedSignalFile[]
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
