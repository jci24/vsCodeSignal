export type PipelineStep = {
  durationMs: number
  name: string
  status: 'ok' | 'running' | 'warning'
  summary: string
}
