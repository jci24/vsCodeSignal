import { Cable, CircleAlert, Clock3, FolderTree, Layers2 } from 'lucide-react'

import { primaryCapture } from '@/entities/signal/model/mock-signals'
import { Badge } from '@/shared/ui/badge'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/ui/card'
import { ScrollArea } from '@/shared/ui/scroll-area'
import { Separator } from '@/shared/ui/separator'

export function SignalExplorer() {
  return (
    <Card className="panel-surface flex h-full min-h-0 flex-col rounded-[1.5rem] border">
      <CardHeader className="space-y-2">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <FolderTree className="h-4 w-4" />
          Explorer
        </div>
        <CardTitle className="text-xl">Workspace files</CardTitle>
        <CardDescription>
          Imported captures, reusable pipelines, and bookmarked regions.
        </CardDescription>
      </CardHeader>
      <CardContent className="min-h-0 flex-1">
        <ScrollArea className="h-full pr-4">
          <div className="space-y-4">
            <div className="rounded-2xl border border-border/70 bg-secondary/70 p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                Active capture
              </p>
              <div className="flex items-start justify-between gap-3">
                <div className="mt-3">
                  <p className="font-semibold">{primaryCapture.name}</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {primaryCapture.source}
                  </p>
                </div>
                <Badge className="bg-chart-2/15 text-chart-2">synced</Badge>
              </div>

              <div className="mt-4 grid gap-2 text-sm text-muted-foreground">
                <div className="flex items-center gap-2">
                  <Clock3 className="h-3.5 w-3.5" />
                  1.6s capture / 2.4 kHz sample rate
                </div>
                <div className="flex items-center gap-2">
                  <CircleAlert className="h-3.5 w-3.5" />
                  Drift event flagged in selected window
                </div>
              </div>
            </div>

            <Separator />

            <div className="rounded-2xl border border-border/70 bg-background/60 p-3">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                Channels
              </p>
              <div className="mt-3 space-y-2">
                {primaryCapture.channels.map((channel) => (
                  <div
                    key={channel.id}
                    className="rounded-2xl border border-border/70 bg-background/70 p-3"
                  >
                    <div className="flex items-center justify-between gap-3">
                      <div className="flex items-center gap-3">
                        <span
                          className="status-dot"
                          style={{ backgroundColor: channel.color }}
                        />
                        <div>
                          <p className="text-sm font-semibold">{channel.name}</p>
                          <p className="text-xs text-muted-foreground">
                            {channel.unit} / {channel.sampleRateHz} Hz
                          </p>
                        </div>
                      </div>
                      <Badge variant="outline">{channel.quality}</Badge>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <Separator />

            <div className="rounded-2xl border border-border/70 bg-background/60 p-3">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                Pipeline presets
              </p>
              <div className="mt-3 space-y-2">
                <div className="rounded-2xl border border-border/70 bg-background/70 p-3">
                  <div className="flex items-center gap-2 text-sm font-semibold">
                    <Layers2 className="h-4 w-4 text-primary" />
                    Drift isolation
                  </div>
                  <p className="mt-2 text-sm text-muted-foreground">
                    Normalize, low-pass, FFT, anomaly scoring, export summary.
                  </p>
                </div>
                <div className="rounded-2xl border border-border/70 bg-background/70 p-3">
                  <div className="flex items-center gap-2 text-sm font-semibold">
                    <Cable className="h-4 w-4 text-chart-3" />
                    Sync validation
                  </div>
                  <p className="mt-2 text-sm text-muted-foreground">
                    Channel alignment, lag measurement, cross-correlation map.
                  </p>
                </div>
              </div>
            </div>
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  )
}
