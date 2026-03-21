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

import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui/button'

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
    <div className="min-h-svh bg-background p-3">
      <div className="flex min-h-[calc(100svh-1.5rem)] flex-col gap-3 md:flex-row">
      <aside
        className={cn(
          'flex shrink-0 flex-col rounded-[2rem] border border-border/70 bg-[linear-gradient(180deg,rgba(249,247,241,0.96),rgba(241,237,227,0.92))] shadow-[0_24px_60px_-36px_rgba(22,32,72,0.4)] transition-[width] duration-200',
          isCollapsed ? 'md:w-24' : 'md:w-80',
        )}
      >
        <div
          className={cn(
            'border-b border-border/60',
            isCollapsed
              ? 'flex flex-col items-center gap-4 px-3 py-4'
              : 'flex items-start justify-between gap-3 px-4 py-5',
          )}
        >
          <div
            className={cn(
              'min-w-0',
              isCollapsed ? 'flex flex-col items-center text-center' : 'block',
            )}
          >
            {isCollapsed ? (
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl border border-border/60 bg-background/65 text-sm font-semibold uppercase tracking-[0.24em] text-foreground">
                SS
              </div>
            ) : (
              <>
                <p className="text-xs font-medium uppercase tracking-[0.32em] text-muted-foreground">
                  Signal Studio
                </p>
                <h1 className="mt-3 text-xl font-semibold">
                  Explore signals with a focused workspace.
                </h1>
              </>
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
            {isCollapsed ? <ChevronRight className="size-4" /> : <ChevronLeft className="size-4" />}
          </Button>
        </div>

        <nav aria-label="Primary navigation" className="flex-1 px-3 py-4">
          <p
            className={cn(
              'px-3 pb-3 text-[11px] font-medium uppercase tracking-[0.28em] text-muted-foreground',
              isCollapsed ? 'md:hidden' : 'block',
            )}
          >
            Primary navigation
          </p>
          <ul className="space-y-1.5">
            {navigationItems.map((item) => {
              const Icon = item.icon

              return (
                <li key={item.label}>
                  <NavLink
                    className={({ isActive }) =>
                      cn(
                        'flex items-center gap-3 rounded-2xl px-3 py-3 text-sm font-medium text-muted-foreground transition-colors',
                        isActive
                          ? 'bg-primary text-primary-foreground shadow-sm'
                          : 'hover:bg-card/80 hover:text-foreground',
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

        <div className="border-t border-border/60 px-4 py-4">
          <div className="flex items-center gap-2 text-sm font-medium text-foreground">
            <ActivitySquare className="size-4 text-primary" />
            <span className={cn(isCollapsed ? 'md:hidden' : 'inline')}>
              App shell ready
            </span>
          </div>
          <p
            className={cn(
              'mt-2 text-sm leading-6 text-muted-foreground',
              isCollapsed ? 'md:hidden' : 'block',
            )}
          >
            Navigation and routing are grouped here as product chrome. Page
            content stays separate on the right.
          </p>
        </div>
      </aside>

      <main className="flex min-h-[24rem] flex-1">
        <div className="flex min-h-full w-full flex-1 flex-col rounded-[2rem] border border-border/70 bg-card/90 px-6 py-8 shadow-[0_24px_60px_-36px_rgba(22,32,72,0.28)] backdrop-blur-xl md:px-10">
          <header className="border-b border-border/60 pb-6">
            <p className="text-xs font-medium uppercase tracking-[0.32em] text-muted-foreground">
              {activeTitle}
            </p>
            <h2 className="mt-3 text-3xl font-semibold tracking-tight md:text-4xl">
              {activeTitle}
            </h2>
            <p className="mt-3 max-w-2xl text-sm leading-7 text-muted-foreground">
              {activeDescription}
            </p>
          </header>
          <div className="flex flex-1 py-6">
            <Outlet />
          </div>
        </div>
      </main>
      </div>
    </div>
  )
}
