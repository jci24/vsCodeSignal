export const isWebView = (): boolean => {
  const webViewWindow = window as Window & {
    acquireVsCodeApi?: unknown
    chrome?: {
      webview?: unknown
    }
  }

  return Boolean(webViewWindow.acquireVsCodeApi || webViewWindow.chrome?.webview)
}
