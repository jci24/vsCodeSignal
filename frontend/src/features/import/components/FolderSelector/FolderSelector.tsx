import type { JSX } from 'react'
import { FolderSearch } from 'lucide-react'

import { Button } from '@/shared/ui/button'

import styles from './FolderSelector.module.scss'

interface IFolderSelectorProps {
  onError: (error: string) => void
  onFolderSelected: (folderPath: string) => void
}

export const FolderSelector = ({
  onError,
  onFolderSelected,
}: IFolderSelectorProps): JSX.Element => {
  const handleSelectFolder = (): void => {
    const folderPath = window.prompt('Enter folder path')

    if (!folderPath?.trim()) {
      onError('Folder selection was cancelled.')
      return
    }

    onFolderSelected(folderPath.trim())
  }

  return (
    <div className={styles.root}>
      <Button className={styles.button} onClick={handleSelectFolder} type="button">
        <FolderSearch className="size-4" />
        Select folder
      </Button>
      <p className={styles.caption}>WebView host integration required.</p>
    </div>
  )
}
