import { HeroScene } from '@/components/hero-scene'

/**
 * The social preview card, rendered by og.html. public/og.png is a 1200×630 screenshot of it (see README).
 */
export function SocialPreview() {
  return (
    <main className="flex h-screen w-screen items-center gap-12 overflow-hidden pr-14 pl-18">
      <div className="flex w-120 shrink-0 flex-col">
        <div className="flex items-center gap-3 text-2xl font-semibold">
          <img src="/icon.svg" alt="" className="size-10" />
          Better AirPlay
        </div>
        <h1 className="mt-12 text-7xl font-semibold tracking-tight text-balance">AirPlay to your Windows PC.</h1>
        <p className="mt-8 text-2xl text-muted-foreground">Free for Windows 10 and 11.</p>
      </div>
      <div className="min-w-0 flex-1">
        <HeroScene animated={false} />
      </div>
    </main>
  )
}
