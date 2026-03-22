import type { JSX } from 'react'
import { useEffect, useRef, useState } from 'react'
import { Check, ChevronDown, LayoutGrid } from 'lucide-react'

import { Button } from '@/shared/ui/button'

import type { CompareLayoutMode } from '../../utils/types'
import styles from './CompareControls.module.scss'

interface CompareControlsProps {
  allowOverlay: boolean
  canCompare: boolean
  isCompareMode: boolean
  layoutMode: CompareLayoutMode
  onClear: () => void
  onLayoutChange: (layoutMode: CompareLayoutMode) => void
}

const LAYOUT_LABELS: Record<CompareLayoutMode, string> = {
  grid: 'Grid',
  overlay: 'Overlay',
  stack: 'Stack',
}

export function CompareControls({
  allowOverlay,
  canCompare,
  isCompareMode,
  layoutMode,
  onClear,
  onLayoutChange,
}: CompareControlsProps): JSX.Element | null {
  const [isMenuOpen, setIsMenuOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!isMenuOpen) {
      return
    }

    const handlePointerDown = (event: PointerEvent): void => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setIsMenuOpen(false)
      }
    }

    const handleKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        setIsMenuOpen(false)
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [isMenuOpen])

  if (!canCompare || !isCompareMode) {
    return null
  }

  const layoutOptions: CompareLayoutMode[] = allowOverlay
    ? ['stack', 'grid', 'overlay']
    : ['stack', 'grid']

  return (
    <div className={styles.root}>
      <div className={styles.menuWrap} ref={menuRef}>
        <Button
          aria-expanded={isMenuOpen}
          aria-haspopup="menu"
          className={styles.layoutTrigger}
          data-open={isMenuOpen}
          onClick={() => setIsMenuOpen((current) => !current)}
          type="button"
          variant="outline"
        >
          <LayoutGrid className="size-4" />
          {LAYOUT_LABELS[layoutMode]}
          <ChevronDown className={styles.triggerChevron} />
        </Button>

        {isMenuOpen ? (
          <div aria-label="Compare layout" className={styles.menu} role="menu">
            {layoutOptions.map((option) => (
              <button
                aria-checked={layoutMode === option}
                className={styles.menuItem}
                data-active={layoutMode === option}
                key={option}
                onClick={() => {
                  onLayoutChange(option)
                  setIsMenuOpen(false)
                }}
                role="menuitemradio"
                type="button"
              >
                <span>{LAYOUT_LABELS[option]}</span>
                <Check className={styles.menuCheck} />
              </button>
            ))}
          </div>
        ) : null}
      </div>

      <Button className={styles.clear} onClick={onClear} type="button" variant="ghost">
        Clear
      </Button>
    </div>
  )
}
