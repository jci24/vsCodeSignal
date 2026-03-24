import { useState, type FormEvent, type KeyboardEvent } from 'react'
import { SendHorizontal } from 'lucide-react'

import { Button } from '@/shared/ui/button'

interface AssistantComposerProps {
  compact?: boolean
  disabled?: boolean
  isLoading?: boolean
  onSubmit: (prompt: string) => Promise<void> | void
}

export function AssistantComposer({
  compact = false,
  disabled = false,
  isLoading = false,
  onSubmit,
}: AssistantComposerProps) {
  const [draft, setDraft] = useState('')

  async function submitDraft(): Promise<void> {
    const trimmed = draft.trim()

    if (!trimmed) {
      return
    }

    setDraft('')
    await onSubmit(trimmed)
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      void submitDraft()
    }
  }

  return (
    <form
      className={compact ? 'flex flex-col gap-2' : 'flex flex-col gap-3'}
      onSubmit={(event: FormEvent<HTMLFormElement>) => {
        event.preventDefault()
        void submitDraft()
      }}
    >
      <textarea
        className={
          compact
            ? 'min-h-20 rounded-[1.4rem] border border-border/70 bg-background/80 px-4 py-3 text-sm text-foreground outline-none transition-colors placeholder:text-muted-foreground focus:border-foreground/30'
            : 'min-h-28 rounded-3xl border border-border/70 bg-background/80 px-4 py-3 text-sm text-foreground outline-none transition-colors placeholder:text-muted-foreground focus:border-foreground/30'
        }
        disabled={disabled || isLoading}
        onChange={(event) => setDraft(event.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="Ask about the active signal, compare set, or request a supported action."
        value={draft}
      />
      <div className={compact ? 'flex items-center justify-between gap-2' : 'flex items-center justify-between gap-3'}>
        <p className="text-xs text-muted-foreground">
          {compact
            ? 'Actions require confirmation.'
            : 'Actions are proposed first and never execute without confirmation.'}
        </p>
        <Button disabled={disabled || isLoading || draft.trim().length === 0} type="submit">
          <SendHorizontal className="mr-2 size-4" />
          Send
        </Button>
      </div>
    </form>
  )
}
