type WebkitDataTransferItem = DataTransferItem & {
  webkitGetAsEntry?: () => FileSystemEntry | null
}

function readFile(entry: FileSystemFileEntry) {
  return new Promise<File>((resolve, reject) => {
    entry.file(resolve, reject)
  })
}

function readEntries(reader: FileSystemDirectoryReader) {
  return new Promise<FileSystemEntry[]>((resolve, reject) => {
    reader.readEntries(resolve, reject)
  })
}

async function collectDirectoryFiles(entry: FileSystemDirectoryEntry): Promise<File[]> {
  const reader = entry.createReader()
  const files: File[] = []

  while (true) {
    const entries = await readEntries(reader)

    if (entries.length === 0) {
      return files
    }

    for (const childEntry of entries) {
      files.push(...(await collectEntryFiles(childEntry)))
    }
  }
}

async function collectEntryFiles(entry: FileSystemEntry): Promise<File[]> {
  if (entry.isFile) {
    const file = await readFile(entry as FileSystemFileEntry)
    return [file]
  }

  if (entry.isDirectory) {
    return collectDirectoryFiles(entry as FileSystemDirectoryEntry)
  }

  return []
}

export async function extractDroppedFiles(dataTransfer: DataTransfer) {
  const itemEntries = Array.from(dataTransfer.items)
    .map((item) => (item as WebkitDataTransferItem).webkitGetAsEntry?.() ?? null)
    .filter((entry): entry is FileSystemEntry => entry !== null)

  if (itemEntries.length === 0) {
    return Array.from(dataTransfer.files)
  }

  const files = await Promise.all(itemEntries.map((entry) => collectEntryFiles(entry)))

  return files.flat()
}
