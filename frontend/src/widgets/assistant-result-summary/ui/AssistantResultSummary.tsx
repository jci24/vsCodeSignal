import type { IAiResponse } from '@/entities/assistant/model/types'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card'

interface AssistantResultSummaryProps {
  response: IAiResponse | null
}

export function AssistantResultSummary({ response }: AssistantResultSummaryProps) {
  if (!response?.executionResult) {
    return null
  }

  return (
    <Card className="rounded-[1.75rem] border border-border/70 bg-background/80">
      <CardHeader className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
          Result
        </p>
        <CardTitle>Workspace updated</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <p className="text-sm leading-6 text-foreground">{response.executionResult.message}</p>

        {response.executionResult.executedSteps.length > 0 ? (
          <ul className="space-y-2 text-sm text-muted-foreground">
            {response.executionResult.executedSteps.map((step) => (
              <li className="rounded-2xl border border-border/60 bg-secondary/30 px-3 py-2" key={step}>
                {step}
              </li>
            ))}
          </ul>
        ) : null}
      </CardContent>
    </Card>
  )
}
