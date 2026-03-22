import type { FormEvent, JSX, PointerEvent as ReactPointerEvent } from 'react'
import { useEffect, useRef, useState } from 'react'
import { Pause, Play, Volume2, VolumeX } from 'lucide-react'

import { Button } from '@/shared/ui/button'

import styles from './AudioPreviewPlayer.module.scss'

interface AudioPreviewPlayerProps {
  src: string
}

export function AudioPreviewPlayer({ src }: AudioPreviewPlayerProps): JSX.Element {
  const audioRef = useRef<HTMLAudioElement | null>(null)
  const [isSeeking, setIsSeeking] = useState(false)
  const [currentTime, setCurrentTime] = useState(0)
  const [duration, setDuration] = useState(0)
  const [isMuted, setIsMuted] = useState(false)
  const [isPlaying, setIsPlaying] = useState(false)
  const [volume, setVolume] = useState(0.8)

  useEffect(() => {
    const audio = audioRef.current

    if (!audio) {
      return
    }

    const handleLoadedMetadata = (): void => {
      setDuration(Number.isFinite(audio.duration) ? audio.duration : 0)
    }

    const handleTimeUpdate = (): void => {
      setCurrentTime(audio.currentTime)
    }

    const handleEnded = (): void => {
      setIsPlaying(false)
    }

    const handlePause = (): void => {
      setIsPlaying(false)
    }

    const handlePlay = (): void => {
      setIsPlaying(true)
    }

    audio.addEventListener('loadedmetadata', handleLoadedMetadata)
    audio.addEventListener('timeupdate', handleTimeUpdate)
    audio.addEventListener('ended', handleEnded)
    audio.addEventListener('pause', handlePause)
    audio.addEventListener('play', handlePlay)

    return () => {
      audio.removeEventListener('loadedmetadata', handleLoadedMetadata)
      audio.removeEventListener('timeupdate', handleTimeUpdate)
      audio.removeEventListener('ended', handleEnded)
      audio.removeEventListener('pause', handlePause)
      audio.removeEventListener('play', handlePlay)
    }
  }, [src])

  useEffect(() => {
    const audio = audioRef.current

    if (!audio) {
      return
    }

    audio.volume = volume
    audio.muted = isMuted
  }, [isMuted, volume])

  useEffect(() => {
    setCurrentTime(0)
    setDuration(0)
    setIsPlaying(false)
  }, [src])

  const togglePlayback = async (): Promise<void> => {
    const audio = audioRef.current

    if (!audio) {
      return
    }

    if (audio.paused) {
      await audio.play()
      return
    }

    audio.pause()
  }

  const seekFromClientX = (clientX: number, element: HTMLDivElement): void => {
    const audio = audioRef.current

    if (!audio || duration <= 0) {
      return
    }

    const rect = element.getBoundingClientRect()
    const ratio = clamp((clientX - rect.left) / rect.width, 0, 1)
    const nextTime = duration * ratio

    audio.currentTime = nextTime
    setCurrentTime(nextTime)
  }

  const handleTimelinePointerDown = (event: ReactPointerEvent<HTMLDivElement>): void => {
    setIsSeeking(true)
    event.currentTarget.setPointerCapture(event.pointerId)
    seekFromClientX(event.clientX, event.currentTarget)
  }

  const handleTimelinePointerMove = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (!isSeeking) {
      return
    }

    seekFromClientX(event.clientX, event.currentTarget)
  }

  const handleTimelinePointerUp = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId)
    }

    setIsSeeking(false)
  }

  const handleVolumeChange = (event: FormEvent<HTMLInputElement>): void => {
    const nextVolume = Number(event.currentTarget.value)

    if (Number.isNaN(nextVolume)) {
      return
    }

    setVolume(nextVolume)
    setIsMuted(nextVolume === 0)
  }

  const toggleMute = (): void => {
    setIsMuted((current) => !current)
  }

  const remainingTime = Math.max(duration - currentTime, 0)
  const progressPercent = duration > 0 ? (currentTime / duration) * 100 : 0

  return (
    <div className={styles.root}>
      <audio preload="metadata" ref={audioRef} src={src} />

      <div className={styles.chrome}>
        <Button
          aria-label={isPlaying ? 'Pause audio' : 'Play audio'}
          className={styles.playButton}
          onClick={() => {
            void togglePlayback()
          }}
          size="icon"
          type="button"
        >
          {isPlaying ? <Pause className="size-4" /> : <Play className="size-4" />}
        </Button>

        <span className={styles.timeValue}>{formatClock(currentTime)}</span>

        <div
          aria-label="Seek audio"
          aria-valuemax={Math.floor(duration || 0)}
          aria-valuemin={0}
          aria-valuenow={Math.floor(currentTime)}
          className={styles.timeline}
          onPointerDown={handleTimelinePointerDown}
          onPointerMove={handleTimelinePointerMove}
          onPointerUp={handleTimelinePointerUp}
          onPointerCancel={handleTimelinePointerUp}
          role="slider"
          tabIndex={0}
        >
          <div className={styles.timelineTrack} />
          <div
            className={styles.timelineFill}
            style={{ width: `${progressPercent}%` }}
          />
          <div
            className={styles.timelineThumb}
            style={{ left: `${progressPercent}%` }}
          />
        </div>

        <span className={styles.timeValue}>-{formatClock(remainingTime)}</span>

        <div className={styles.volumeGroup}>
          <button
            aria-label={isMuted || volume === 0 ? 'Unmute audio' : 'Mute audio'}
            className={styles.iconButton}
            onClick={toggleMute}
            type="button"
          >
            {isMuted || volume === 0 ? (
              <VolumeX className="size-4" />
            ) : (
              <Volume2 className="size-4" />
            )}
          </button>
          <input
            aria-label="Adjust volume"
            className={styles.volume}
            max={1}
            min={0}
            onInput={handleVolumeChange}
            step={0.05}
            type="range"
            value={isMuted ? 0 : volume}
          />
        </div>
      </div>
    </div>
  )
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max)
}

function formatClock(value: number): string {
  if (!Number.isFinite(value) || value <= 0) {
    return '0:00'
  }

  const totalSeconds = Math.floor(value)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60

  return `${minutes}:${seconds.toString().padStart(2, '0')}`
}
