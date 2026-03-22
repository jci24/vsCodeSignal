import {
  ActivitySquare,
  ChevronLeft,
  ChevronRight,
  FileBarChart2,
  FolderKanban,
  PanelLeft,
  ScanSearch,
  Settings2,
  Waves,
} from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet, useMatches } from 'react-router-dom'

import { Import } from '@/features/import/Import'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui/button'
import { SignalStudioLogo } from '@/shared/ui/signal-studio-logo/SignalStudioLogo'

const navigationItems = [
  {
    icon: PanelLeft,
    label: 'Workspace',
    to: '/',
  },
  {
    icon: Waves,
    label: 'Signals',
    to: '/signals',
  },
  {
    icon: FolderKanban,
    label: 'Pipelines',
    to: '/pipelines',
  },
  {
    icon: ScanSearch,
    label: 'Investigations',
    to: '/investigations',
  },
  {
    icon: FileBarChart2,
    label: 'Reports',
    to: '/reports',
  },
  {
    icon: Settings2,
    label: 'Settings',
    to: '/settings',
  },
] as const

interface RouteHandle {
  description?: string
  title?: string
}

export function WorkspaceShell() {
  const [isCollapsed, setIsCollapsed] = useState(false)
  const matches = useMatches()
  const activeHandle = matches.at(-1)?.handle as RouteHandle | undefined
  const activeTitle = activeHandle?.title ?? 'Workspace'
  const activeDescription =
    activeHandle?.description ??
    'Persistent navigation on the left, focused content on the right.'

  return (
    <div className="min-h-svh overflow-hidden bg-background p-3">
      <div className="flex h-[calc(100svh-1.5rem)] flex-col md:flex-row">
        <aside
          className={cn(
            'flex shrink-0 flex-col overflow-hidden border-b border-border/60 bg-background transition-[width] duration-200 md:h-full md:border-r md:border-b-0',
            isCollapsed ? 'md:w-24' : 'md:w-80',
          )}
        >
          <div
            className={cn(
              'border-b border-border/60',
              isCollapsed
                ? 'flex flex-col items-center gap-3 px-3 py-3'
                : 'flex items-start justify-between gap-3 px-4 py-3.5',
            )}
          >
            <div
              className={cn(
                'min-w-0',
                isCollapsed ? 'flex flex-col items-center text-center' : 'block',
              )}
            >
              {isCollapsed ? (
                <SignalStudioLogo compact />
              ) : (
                <SignalStudioLogo />
              )}
            </div>
            <Button
              aria-label={isCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
              className="shrink-0"
              onClick={() => setIsCollapsed((value) => !value)}
              size="icon"
              type="button"
              variant="outline"
            >
              {isCollapsed ? (
                <ChevronRight className="size-4" />
              ) : (
                <ChevronLeft className="size-4" />
              )}
            </Button>
          </div>

          <nav aria-label="Primary navigation" className="min-h-0 flex-1 overflow-y-auto px-3 py-3">
            <p
              className={cn(
                'px-2 pb-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground',
                isCollapsed ? 'md:hidden' : 'block',
              )}
            >
              Main menu
            </p>
            <ul className="space-y-1.5">
              {navigationItems.map((item) => {
                const Icon = item.icon

                return (
                  <li key={item.label}>
                    <NavLink
                      className={({ isActive }) =>
                        cn(
                          'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors',
                          isActive
                            ? 'border border-border/70 bg-secondary text-foreground'
                            : 'text-muted-foreground hover:bg-secondary/70 hover:text-foreground',
                          isCollapsed ? 'md:justify-center' : 'justify-start',
                        )
                      }
                      title={item.label}
                      to={item.to}
                    >
                      <Icon className="size-4 shrink-0" />
                      <span className={cn(isCollapsed ? 'md:hidden' : 'inline')}>
                        {item.label}
                      </span>
                    </NavLink>
                  </li>
                )
              })}
            </ul>
          </nav>

          <div className="border-t border-border/60 px-4 py-3">
            <div className="flex items-center gap-2 text-sm font-medium text-foreground">
              <ActivitySquare className="size-4 text-foreground" />
              <span className={cn(isCollapsed ? 'md:hidden' : 'inline')}>
                App shell ready
              </span>
            </div>
          </div>
        </aside>

        <main className="flex min-h-0 min-w-0 flex-1">
          <div className="flex min-h-full w-full flex-1 flex-col overflow-hidden px-6 py-8 md:px-10">
            <header className="border-b border-border/60 pb-6">
              <div className="flex flex-col gap-6 md:flex-row md:items-start md:justify-between">
                <div>
                  <p className="text-xs font-medium uppercase tracking-[0.32em] text-muted-foreground">
                    {activeTitle}
                  </p>
                  <h2 className="mt-3 text-3xl font-semibold tracking-tight text-foreground md:text-4xl">
                    {activeTitle}
                  </h2>
                  <p className="mt-3 max-w-2xl text-sm leading-7 text-muted-foreground">
                    {activeDescription}
                  </p>
                </div>
                <Import />
              </div>
            </header>
            <div className="flex min-h-0 flex-1 overflow-auto py-6">
              <Outlet />
            </div>
          </div>
        </main>
      </div>
    </div>
  )
}
