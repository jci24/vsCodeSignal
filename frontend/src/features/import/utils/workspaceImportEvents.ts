export const workspaceImportedEventName = 'signal-studio:workspace-imported'

interface IWorkspaceImportedDetail {
  batchId?: string | null
  fileId?: string | null
}

export const dispatchWorkspaceImportedEvent = (
  detail: IWorkspaceImportedDetail = {},
): void => {
  if (typeof window === 'undefined') {
    return
  }

  window.dispatchEvent(
    new CustomEvent<IWorkspaceImportedDetail>(workspaceImportedEventName, {
      detail,
    }),
  )
}
