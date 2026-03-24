import { Button } from '@/shared/ui/button'

interface AiActionConfirmationProps {
  disabled?: boolean
  isExecuting?: boolean
  onCancel: () => void
  onConfirm: () => void
}

export function AiActionConfirmation({
  disabled = false,
  isExecuting = false,
  onCancel,
  onConfirm,
}: AiActionConfirmationProps) {
  return (
    <div className="flex items-center justify-end gap-2">
      <Button disabled={disabled || isExecuting} onClick={onCancel} type="button" variant="ghost">
        Dismiss
      </Button>
      <Button disabled={disabled || isExecuting} onClick={onConfirm} type="button">
        {isExecuting ? 'Running…' : 'Confirm action'}
      </Button>
    </div>
  )
}
