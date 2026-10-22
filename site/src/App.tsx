import { DownloadButton } from '@/components/download-button'
import { HeroScene } from '@/components/hero-scene'
import { ISSUES_URL, RELEASES_URL, REPO_URL } from '@/lib/release'

const SUURD_DIGITAL_URL = 'https://suurd.digital/?utm_source=better-airplay&utm_medium=referral&utm_campaign=footer'

const steps = [
  {
    title: 'Run the installer',
    body: 'It puts Better AirPlay in the notification area, lets it through Windows Firewall, and starts receiving right away.',
  },
  {
    title: "Tap your PC's name",
    body: 'On your iPhone or iPad, open Control Center and tap Screen Mirroring. On a Mac, use the Screen Mirroring menu. Your PC is in the list.',
  },
  {
    title: "That's it",
    body: 'Your screen opens in a window on the PC. Move the mouse to the top for the controls, or double-click for full screen.',
  },
]

const features = [
  {
    title: 'Snappy by default.',
    body: 'Low latency mode is on, so what you do on your phone shows on the PC without a wait. Watching a film? Turn it off in the menu and the sound stays in sync with the picture.',
  },
  {
    title: 'A code before anyone connects.',
    body: "Turn on a 4-digit connection code. It's shown in the menu, and a device enters it once.",
  },
  {
    title: 'Any name you like.',
    body: 'Change what Apple devices see in their list: your name, the room, anything.',
  },
  {
    title: 'Nothing in the way.',
    body: 'The video opens in a window without a title bar. Move the mouse to the top for the controls, drag the bar to move the window, double-click for full screen.',
  },
  {
    title: 'Out of the way.',
    body: 'It lives in the notification area: red while receiving, grey when off. Start it when you sign in, and let it install updates itself.',
  },
  {
    title: 'Free and open source.',
    body: 'Better AirPlay is MIT licensed and built on UxPlay, GStreamer and FFmpeg. The code is on GitHub.',
  },
]

const notes = [
  {
    title: 'Same network.',
    body: 'Your PC and your Apple device must be on the same Wi-Fi. Guest networks with client isolation and VPNs block discovery.',
  },
  {
    title: 'Private network.',
    body: 'The installer allows the receiver through Windows Firewall on Private networks only. Set your home Wi-Fi to Private in Windows Settings, under Network & internet.',
  },
  {
    title: 'No protected video.',
    body: "Video with DRM, such as the Apple TV app, can't be mirrored. That's an Apple restriction, and it applies to every receiver.",
  },
  {
    title: 'One device at a time.',
    body: 'Disconnect on one device before you connect another.',
  },
  {
    title: 'Still nothing?',
    body: 'Right-click the icon and choose Show log. Errors are listed there.',
  },
]

export default function App() {
  return (
    <div className="mx-auto w-full max-w-5xl px-6">
      <header className="flex items-center justify-between py-6">
        <a href="#" className="flex items-center gap-2.5 font-semibold">
          <img src="./icon.svg" alt="" className="size-7" />
          Better AirPlay
        </a>
        <nav className="flex gap-6 text-sm text-muted-foreground">
          <a href="#how" className="hover:text-foreground">
            How it works
          </a>
          <a href={REPO_URL} className="hover:text-foreground">
            GitHub
          </a>
        </nav>
      </header>

      <main>
        <section className="pt-12 pb-16 text-center md:pt-20 md:pb-24">
          <h1 className="mx-auto max-w-3xl text-5xl font-semibold tracking-tight text-balance md:text-7xl">
            AirPlay to your Windows PC.
          </h1>
          <p className="mx-auto mt-6 max-w-2xl text-lg leading-relaxed text-pretty text-muted-foreground md:text-xl">
            Better AirPlay puts your PC in the Screen Mirroring list on your iPhone, iPad and Mac. Install one file, tap
            your PC's name, and what's on your phone is on your screen.
          </p>
          <div className="mt-10">
            <DownloadButton />
          </div>
          <div className="mt-16 md:mt-20">
            <HeroScene />
          </div>
        </section>

        <section id="how" className="scroll-mt-10 border-t pt-20 md:pt-28">
          <h2 className="max-w-xl text-3xl font-semibold tracking-tight text-balance md:text-4xl">
            Three steps, and you're mirroring.
          </h2>
          <ol className="mt-12 grid gap-10 md:grid-cols-3 md:gap-8">
            {steps.map((step, index) => (
              <li key={step.title}>
                <span className="text-5xl font-semibold text-primary tabular-nums">{index + 1}</span>
                <h3 className="mt-4 text-xl font-semibold">{step.title}</h3>
                <p className="mt-2 leading-relaxed text-pretty text-muted-foreground">{step.body}</p>
              </li>
            ))}
          </ol>
        </section>

        <section className="pt-24 md:pt-32">
          <h2 className="max-w-xl text-3xl font-semibold tracking-tight text-balance md:text-4xl">What you get</h2>
          <dl className="mt-12 grid gap-x-12 gap-y-10 md:grid-cols-2">
            {features.map((feature) => (
              <div key={feature.title} className="max-w-md">
                <dt className="text-lg font-semibold">{feature.title}</dt>
                <dd className="mt-1.5 leading-relaxed text-pretty text-muted-foreground">{feature.body}</dd>
              </div>
            ))}
          </dl>
        </section>

        <section className="pt-24 md:pt-32">
          <h2 className="max-w-xl text-3xl font-semibold tracking-tight text-balance md:text-4xl">Good to know</h2>
          <dl className="mt-10 max-w-2xl divide-y">
            {notes.map((note) => (
              <div key={note.title} className="py-5 md:grid md:grid-cols-4 md:gap-6">
                <dt className="font-semibold">{note.title}</dt>
                <dd className="mt-1 leading-relaxed text-pretty text-muted-foreground md:col-span-3 md:mt-0">{note.body}</dd>
              </div>
            ))}
          </dl>
        </section>

        <section className="border-t py-24 text-center md:py-32">
          <h2 className="mx-auto max-w-xl text-3xl font-semibold tracking-tight text-balance md:text-5xl">
            Put your phone on the big screen.
          </h2>
          <div className="mt-10">
            <DownloadButton />
          </div>
          <p className="mt-6 text-sm text-muted-foreground">
            Or see every version on the{' '}
            <a href={RELEASES_URL} className="underline underline-offset-4 hover:text-foreground">
              releases page
            </a>
            .
          </p>
        </section>
      </main>

      <footer className="flex flex-col gap-4 border-t py-10 text-sm text-muted-foreground md:flex-row md:items-center md:justify-between">
        <p className="max-w-lg text-pretty">
          Better AirPlay is open source under the MIT license. A project of{' '}
          <a href={SUURD_DIGITAL_URL} className="underline underline-offset-4 hover:text-foreground">
            Suurd Digital
          </a>
          .
        </p>
        <nav className="flex gap-6">
          <a href={REPO_URL} className="hover:text-foreground">
            GitHub
          </a>
          <a href={RELEASES_URL} className="hover:text-foreground">
            Releases
          </a>
          <a href={ISSUES_URL} className="hover:text-foreground">
            Report a problem
          </a>
        </nav>
      </footer>
    </div>
  )
}
