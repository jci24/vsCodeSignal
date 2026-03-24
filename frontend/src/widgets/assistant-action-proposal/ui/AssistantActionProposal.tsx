import type { IAiActionProposal } from '@/entities/assistant/model/types'
import { AiActionConfirmation } from '@/features/confirm-ai-action/ui/AiActionConfirmation'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card'

interface AssistantActionProposalProps {
  isExecuting?: boolean
  onCancel: () => void
  onConfirm: () => void
  proposal: IAiActionProposal | null
}

export function AssistantActionProposal({
  isExecuting = false,
  onCancel,
  onConfirm,
  proposal,
}: AssistantActionProposalProps) {
  if (!proposal) {
    return null
  }

  return (
    <Card className="rounded-[1.75rem] border border-border/70 bg-background/80">
      <CardHeader className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
          Action proposal
        </p>
        <CardTitle>{proposal.title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm leading-6 text-foreground">
          {proposal.status === 'unsupported'
            ? proposal.unsupportedReason
            : proposal.status === 'needs_clarification'
              ? proposal.clarificationQuestion
              : proposal.summary}
        </p>

        {proposal.steps.length > 0 ? (
          <ol className="space-y-2 text-sm text-muted-foreground">
            {proposal.steps.map((step, index) => (
              <li className="rounded-2xl border border-border/60 bg-secondary/30 px-3 py-2" key={`${step.command}-${index}`}>
                {step.displayText || step.command}
              </li>
            ))}
          </ol>
        ) : null}

        {proposal.warnings.length > 0 ? (
          <ul className="space-y-2 text-sm text-amber-700">
            {proposal.warnings.map((warning) => (
              <li className="rounded-2xl border border-amber-300/60 bg-amber-50 px-3 py-2" key={warning}>
                {warning}
              </li>
            ))}
          </ul>
        ) : null}

        {proposal.status === 'needs_confirmation' ? (
          <AiActionConfirmation isExecuting={isExecuting} onCancel={onCancel} onConfirm={onConfirm} />
        ) : null}
      </CardContent>
    </Card>
  )
}
