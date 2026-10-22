import {
  Bluetooth,
  Calculator,
  Camera,
  Check,
  Flashlight,
  Lock,
  Monitor,
  Moon,
  Plane,
  Play,
  Signal,
  SkipBack,
  SkipForward,
  Sun,
  Timer,
  Tv,
  Volume2,
  Wifi,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import './hero-scene.css'

/**
 * The hero illustration: a Windows desktop and an iPhone with Control Center open.
 * The sequence plays once the scene is on screen, holds the connected state, then replays.
 * While connected, hovering the video window reveals its bar, like the real app.
 */
const STEPS = [
  900, // 1: Control Center slides down
  1700, // 2: the Screen Mirroring tile is tapped and its popover opens
  2500, // 3: "Living room PC" is picked
  3100, // 4: connected: the video window opens on the PC with its bar showing
  4800, // 5: the bar slides away; hover brings it back
] as const

const SETTLED = STEPS.length
/** Length of one loop, in ms. Everything after the last step is the hold on the connected state. */
const CYCLE = 11000
/** How long to wait after the pointer leaves before replaying a cycle that ended mid-hover, in ms. */
const RESUME_AFTER_HOVER = 1500

function prefersReducedMotion() {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

/**
 * Loops through STEPS while at least half the scene is visible and resets when it scrolls away.
 * A cycle that ends while the pointer is over the scene waits, so the window's bar can be tried.
 * With reduced motion it stays on the final step.
 */
function useLoopingSequence() {
  const ref = useRef<HTMLDivElement>(null)
  const [step, setStep] = useState(() => (prefersReducedMotion() ? SETTLED : 0))

  useEffect(() => {
    const scene = ref.current
    if (!scene || prefersReducedMotion()) return

    let timers: number[] = []
    let hovering = false
    let replayPending = false

    const stop = () => {
      timers.forEach(clearTimeout)
      timers = []
      replayPending = false
    }
    const play = () => {
      stop()
      setStep(0)
      timers = STEPS.map((delay, index) => window.setTimeout(() => setStep(index + 1), delay))
      timers.push(
        window.setTimeout(() => {
          if (hovering) replayPending = true
          else play()
        }, CYCLE),
      )
    }

    const onEnter = () => {
      hovering = true
    }
    const onLeave = () => {
      hovering = false
      if (replayPending) timers.push(window.setTimeout(play, RESUME_AFTER_HOVER))
    }

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry?.isIntersecting) play()
        else {
          stop()
          setStep(0)
        }
      },
      { threshold: 0.5 },
    )
    observer.observe(scene)
    scene.addEventListener('pointerenter', onEnter)
    scene.addEventListener('pointerleave', onLeave)
    return () => {
      observer.disconnect()
      scene.removeEventListener('pointerenter', onEnter)
      scene.removeEventListener('pointerleave', onLeave)
      stop()
    }
  }, [])

  return { ref, step }
}

export function HeroScene() {
  const { ref, step } = useLoopingSequence()

  return (
    <div
      ref={ref}
      className="scene"
      data-step={step}
      role="img"
      aria-label="An iPhone with Control Center open and Living room PC picked under Screen Mirroring. On the PC, the phone's screen shows in a window."
    >
      <div className="desktop">
        <div className="window">
          <Photo />
          <div className="window-bar">
            <span className="window-title">Bram's iPhone</span>
            <span className="window-buttons">
              <svg viewBox="0 0 10 10"><path d="M1 5h8" /></svg>
              <svg viewBox="0 0 10 10"><rect x="1.5" y="1.5" width="7" height="7" rx="1" /></svg>
              <svg viewBox="0 0 10 10"><path d="M1 1l8 8M9 1l-8 8" /></svg>
            </span>
          </div>
        </div>
        <div className="taskbar">
          <span className="taskbar-apps">
            <span className="taskbar-logo" />
            <span className="taskbar-app" />
            <span className="taskbar-app" />
            <span className="taskbar-app" />
          </span>
          <span className="taskbar-tray">
            <img src="./icon.svg" alt="" />
            <span>20:14</span>
          </span>
        </div>
      </div>

      <div className="phone">
        <div className="phone-screen">
          <Photo />
          <div className="control-center">
            <div className="cc-grid">
              <div className="tile tile-2x2 tile-connect">
                <span className="cc-circle"><Plane /></span>
                <span className="cc-circle is-green"><Signal /></span>
                <span className="cc-circle is-blue"><Wifi /></span>
                <span className="cc-circle is-blue"><Bluetooth /></span>
              </div>
              <div className="tile tile-2x2 tile-media">
                <span className="media-art" />
                <span className="media-controls">
                  <SkipBack />
                  <Play />
                  <SkipForward />
                </span>
              </div>
              <div className="tile"><Lock /></div>
              <div className="tile tile-mirror"><MirroringIcon /></div>
              <div className="tile tile-1x2 tile-slider tile-brightness"><Sun /></div>
              <div className="tile tile-1x2 tile-slider tile-volume"><Volume2 /></div>
              <div className="tile tile-2x1 tile-focus">
                <span className="cc-circle"><Moon /></span>
                Focus
              </div>
              <div className="tile"><Flashlight /></div>
              <div className="tile"><Timer /></div>
              <div className="tile"><Calculator /></div>
              <div className="tile"><Camera /></div>
            </div>
            <div className="popover">
              <div className="popover-title">
                <MirroringIcon />
                Screen Mirroring
              </div>
              <div className="popover-row popover-row-pc">
                <Monitor />
                Living room PC
                <Check className="popover-check" />
              </div>
              <div className="popover-row">
                <Tv />
                Bedroom TV
              </div>
              <div className="popover-stop">
                <span>Stop Mirroring</span>
              </div>
            </div>
          </div>
          <div className="status-bar">
            <span className="status-time">20:14</span>
            <span className="status-icons">
              <span className="status-signal"><i /><i /><i /><i /></span>
              <Wifi />
              <span className="status-battery" />
            </span>
          </div>
          <div className="island" />
        </div>
      </div>
    </div>
  )
}

/** iOS's two overlapping screens glyph for Screen Mirroring. */
function MirroringIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="5" width="13" height="10" rx="2" />
      <path d="M9 19h12a2 2 0 0 0 2-2V9" />
    </svg>
  )
}

/** A sunset over water, drawn with gradients. The same picture appears on the phone and in the window. */
function Photo() {
  return (
    <div className="photo">
      <div className="photo-glow" />
      <div className="photo-hills" />
    </div>
  )
}
