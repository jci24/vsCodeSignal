import { useEffect, useRef } from 'react'
import { BotMessageSquare } from 'lucide-react'

import type { IAiFollowUpPrompt } from '@/entities/assistant/model/types'
import type { IAssistantMessage } from '@/features/ask-assistant/model/useAssistantConversation'
import { AssistantComposer } from '@/features/ask-assistant/ui/AssistantComposer'
import { Button } from '@/shared/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card'
import { ScrollArea } from '@/shared/ui/scroll-area'

interface AssistantChatProps {
  compact?: boolean
  errorMessage?: string | null
  followUps?: IAiFollowUpPrompt[]
  isLoading?: boolean
  messages: IAssistantMessage[]
  onSelectPrompt: (prompt: string) => Promise<void> | void
  onSubmit: (prompt: string) => Promise<void> | void
}

export function AssistantChat({
  compact = false,
  errorMessage,
  followUps = [],
  isLoading = false,
  messages,
  onSelectPrompt,
  onSubmit,
}: AssistantChatProps) {
  const bottomAnchorRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    bottomAnchorRef.current?.scrollIntoView({
      behavior: 'smooth',
      block: 'end',
    })
  }, [isLoading, messages.length])

  return (
    <Card className="flex min-h-0 flex-1 flex-col rounded-[1.75rem] border border-border/70 bg-background/80">
      <CardHeader className={compact ? 'space-y-1.5 pb-3' : 'space-y-2'}>
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <BotMessageSquare className="size-4" />
          Assistant chat
        </div>
        <CardTitle className={compact ? 'text-lg' : undefined}>Grounded questions and answers</CardTitle>
      </CardHeader>
      <CardContent className={compact ? 'flex min-h-0 flex-1 flex-col gap-3' : 'flex min-h-0 flex-1 flex-col gap-4'}>
        {followUps.length > 0 ? (
          <div className={compact ? 'flex flex-wrap gap-1.5' : 'flex flex-wrap gap-2'}>
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

        <ScrollArea className="min-h-0 flex-1 pr-4">
          <div className={compact ? 'space-y-2.5' : 'space-y-3'}>
            {messages.length === 0 ? (
              <div className="rounded-2xl border border-dashed border-border/70 px-4 py-5 text-sm text-muted-foreground">
                Ask about what stands out, what changed, what to inspect next, or request a supported action.
              </div>
            ) : (
              messages.map((message) => (
                <article
                  className={
                    message.role === 'user'
                      ? compact
                        ? 'ml-6 rounded-[1.4rem] bg-foreground px-4 py-2.5 text-sm text-background'
                        : 'ml-8 rounded-3xl bg-foreground px-4 py-3 text-sm text-background'
                      : compact
                        ? 'mr-6 rounded-[1.4rem] border border-border/70 bg-secondary/50 px-4 py-2.5 text-sm text-foreground'
                        : 'mr-8 rounded-3xl border border-border/70 bg-secondary/50 px-4 py-3 text-sm text-foreground'
                  }
                  key={message.id}
                >
                  <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                    {message.role === 'user' ? 'You' : 'Assistant'}
                  </p>
                  <p className="leading-6">{message.content}</p>
                </article>
              ))
            )}

            {isLoading ? (
              <article className={compact ? 'mr-6 rounded-[1.4rem] border border-border/70 bg-secondary/40 px-4 py-2.5 text-sm text-foreground' : 'mr-8 rounded-3xl border border-border/70 bg-secondary/40 px-4 py-3 text-sm text-foreground'}>
                <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                  Assistant
                </p>
                <p className="leading-6 text-muted-foreground">Working on it…</p>
              </article>
            ) : null}

            <div ref={bottomAnchorRef} />
          </div>
        </ScrollArea>

        {errorMessage ? (
          <div className="rounded-2xl border border-red-300/60 bg-red-50 px-4 py-3 text-sm text-red-700">
            {errorMessage}
          </div>
        ) : null}

        <AssistantComposer compact={compact} isLoading={isLoading} onSubmit={onSubmit} />
      </CardContent>
    </Card>
  )
}
