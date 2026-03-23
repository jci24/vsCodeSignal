export const API_ENDPOINTS = {
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
