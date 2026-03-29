import { useEffect, useRef } from 'react'
import { BotMessageSquare } from 'lucide-react'

import type { IAiFollowUpPrompt } from '@/entities/assistant/model/types'
import type { IAssistantMessage } from '@/features/ask-assistant/model/useAssistantConversation'
import { cn } from '@/shared/lib/cn'
import { AssistantComposer } from '@/features/ask-assistant/ui/AssistantComposer'
import { Button } from '@/shared/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card'
import { ScrollArea } from '@/shared/ui/scroll-area'

interface AssistantChatProps {
  compact?: boolean
  description?: string
  emptyStateMessage?: string
  eyebrow?: string
  errorMessage?: string | null
  followUps?: IAiFollowUpPrompt[]
  isLoading?: boolean
  messages: IAssistantMessage[]
  onSelectPrompt: (prompt: string) => Promise<void> | void
  onSubmit: (prompt: string) => Promise<void> | void
  scrollable?: boolean
  title?: string
}

export function AssistantChat({
  compact = false,
  description,
  emptyStateMessage = 'Ask about what stands out, what changed, what to inspect next, or request a supported action.',
  eyebrow = 'Assistant chat',
  errorMessage,
  followUps = [],
  isLoading = false,
  messages,
  onSelectPrompt,
  onSubmit,
  scrollable = false,
  title = 'Grounded questions and answers',
}: AssistantChatProps) {
  const transcriptRef = useRef<HTMLDivElement | null>(null)
  const scrollAreaRef = useRef<HTMLDivElement | null>(null)
  const useNativeScrollableTranscript = compact && scrollable
  const useScrollableViewport = !compact || scrollable || messages.length > 0 || isLoading

  useEffect(() => {
    const scrollTarget = useNativeScrollableTranscript
      ? transcriptRef.current
      : scrollAreaRef.current?.querySelector<HTMLElement>(
          '[data-radix-scroll-area-viewport]',
        )

    if (!scrollTarget) {
      return
    }

    scrollTarget.scrollTo({
      behavior: 'smooth',
      top: scrollTarget.scrollHeight,
    })
  }, [isLoading, messages.length, useNativeScrollableTranscript])

  const transcriptContent = (
    <div className="min-w-0 w-full space-y-4">
      {messages.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-border/70 px-4 py-4 text-sm text-muted-foreground">
          {emptyStateMessage}
        </div>
      ) : (
        messages.map((message) => (
          <article
            className={
              message.role === 'user'
                ? compact
                  ? 'ml-6 max-w-[calc(100%-1.5rem)] rounded-[1.4rem] bg-foreground px-4 py-3 text-sm text-background'
                  : 'ml-8 max-w-[calc(100%-2rem)] rounded-3xl bg-foreground px-4 py-3 text-sm text-background'
                : compact
                  ? 'mr-6 max-w-[calc(100%-1.5rem)] rounded-[1.4rem] border border-border/70 bg-secondary/50 px-4 py-3 text-sm text-foreground'
                  : 'mr-8 max-w-[calc(100%-2rem)] rounded-3xl border border-border/70 bg-secondary/50 px-4 py-3 text-sm text-foreground'
            }
            key={message.id}
          >
            <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
              {message.role === 'user' ? 'You' : message.isPreview ? 'Assistant (first pass)' : 'Assistant'}
            </p>
            <p className="break-words whitespace-pre-wrap leading-6">{message.content}</p>
          </article>
        ))
      )}

      {isLoading ? (
        <article className={compact ? 'mr-6 max-w-[calc(100%-1.5rem)] rounded-[1.4rem] border border-border/70 bg-secondary/40 px-4 py-4 text-sm text-foreground' : 'mr-8 max-w-[calc(100%-2rem)] rounded-3xl border border-border/70 bg-secondary/40 px-4 py-4 text-sm text-foreground'}>
          <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
            Assistant
          </p>
          <p className="break-words whitespace-pre-wrap leading-6 text-muted-foreground">
            {messages.some((message) => message.isPreview) ? 'Refining the answer…' : 'Working on it…'}
          </p>
        </article>
      ) : null}
    </div>
  )

  return (
    <Card
      className={cn(
        'flex min-h-0 flex-1 flex-col rounded-[1.75rem] border border-border/70 bg-background/80',
        compact && scrollable ? 'max-h-[32rem] overflow-hidden' : '',
      )}
    >
      <CardHeader className={compact ? 'space-y-2 pb-4' : 'space-y-4 pb-6'}>
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <BotMessageSquare className="size-4" />
          {eyebrow}
        </div>
        <CardTitle className={compact ? 'text-lg' : undefined}>{title}</CardTitle>
        {description ? <p className="text-sm leading-6 text-muted-foreground">{description}</p> : null}
      </CardHeader>
      <CardContent className={compact ? 'flex min-h-0 min-w-0 flex-1 flex-col gap-4' : 'flex min-h-0 min-w-0 flex-1 flex-col gap-6'}>
        {followUps.length > 0 ? (
          <div className={compact ? 'flex flex-wrap gap-2' : 'flex flex-wrap gap-2'}>
            {followUps.map((item) => (
              <Button
                className="rounded-full"
                key={item.id}
                onClick={() => void onSelectPrompt(item.prompt)}
                size="sm"
                type="button"
                variant="outline"
              >
                {item.label}
              </Button>
            ))}
          </div>
        ) : null}

        {useScrollableViewport ? (
          useNativeScrollableTranscript ? (
            <div
              className="min-h-0 max-h-[18rem] overflow-y-auto overscroll-contain pr-2"
              ref={transcriptRef}
            >
              {transcriptContent}
            </div>
          ) : (
            <ScrollArea
              className={
                compact
                  ? scrollable
                    ? 'min-h-0 flex-1 pr-4'
                    : 'max-h-36 pr-4'
                  : 'min-h-0 min-w-0 flex-1 pr-4'
              }
              ref={scrollAreaRef}
            >
              {transcriptContent}
            </ScrollArea>
          )
        ) : compact ? (
          <p className="text-sm leading-6 text-muted-foreground">{emptyStateMessage}</p>
        ) : null}

        {errorMessage ? (
          <div className="rounded-2xl border border-red-300/60 bg-red-50 px-4 py-4 text-sm text-red-700">
            {errorMessage}
          </div>
        ) : null}

        <AssistantComposer compact={compact} isLoading={isLoading} onSubmit={onSubmit} />
      </CardContent>
    </Card>
  )
}
