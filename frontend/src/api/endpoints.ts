export const API_ENDPOINTS = {
  AI: {
    ASK: '/api/ai/ask',
    CONTEXT: (workspaceId: string) => `/api/ai/context/${workspaceId}`,
    EXECUTE_ACTION: '/api/ai/execute-action',
    PLAN_ACTION: '/api/ai/plan-action',
    SUMMARY: '/api/ai/summary',
  },
  ANALYSIS: {
    FFT: '/fft',
    METRICS: '/metrics',
    SPECTROGRAM: '/spectrogram',
    WAVEFORM: '/waveform',
  },
  IMPORT: {
    IMPORT: '/import',
  },
  WORKSPACES: {
    CURRENT_IMPORTS: '/workspaces/current/imports',
  },
} as const
