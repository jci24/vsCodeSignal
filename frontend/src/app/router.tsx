import { createBrowserRouter, type RouteObject } from 'react-router-dom'

import { InvestigationsPage } from '@/pages/investigations/ui/investigations-page'
import { PipelinesPage } from '@/pages/pipelines/ui/pipelines-page'
import { ReportsPage } from '@/pages/reports/ui/reports-page'
import { SettingsPage } from '@/pages/settings/ui/settings-page'
import { SignalsPage } from '@/pages/signals/ui/signals-page'
import { WorkspacePage } from '@/pages/workspace/ui/workspace-page'
import { WorkspaceShell } from '@/widgets/workspace-shell/ui/workspace-shell'

export const appRoutes: RouteObject[] = [
  {
    children: [
      {
        element: <WorkspacePage />,
        handle: {
          description: 'Workspace overview, imports, and session-level coordination.',
          title: 'Workspace',
        },
        path: '/',
      },
      {
        element: <SignalsPage />,
        handle: {
          description: 'Signal captures, channels, and direct waveform exploration.',
          title: 'Signals',
        },
        path: '/signals',
      },
      {
        element: <PipelinesPage />,
        handle: {
          description: 'Reusable processing chains, transforms, and run configurations.',
          title: 'Pipelines',
        },
        path: '/pipelines',
      },
      {
        element: <InvestigationsPage />,
        handle: {
          description: 'AI-assisted reasoning, anomaly review, and comparative analysis.',
          title: 'Investigations',
        },
        path: '/investigations',
      },
      {
        element: <ReportsPage />,
        handle: {
          description: 'Exportable findings, summaries, and stakeholder-ready outputs.',
          title: 'Reports',
        },
        path: '/reports',
      },
      {
        element: <SettingsPage />,
        handle: {
          description: 'Application preferences, providers, integrations, and defaults.',
          title: 'Settings',
        },
        path: '/settings',
      },
    ],
    element: <WorkspaceShell />,
  },
]

export const appRouter = createBrowserRouter(appRoutes)
