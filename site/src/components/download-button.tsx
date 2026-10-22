import { Download } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { INSTALLER_URL, useLatestRelease } from '@/lib/release'

/** The main call to action: a direct download of the newest installer, with its version and size once known. */
export function DownloadButton() {
  const release = useLatestRelease()
  const details = release
    ? `Version ${release.version} for Windows 10 and 11, ${Math.round(release.installerBytes / 1048576)} MB.`
    : 'For Windows 10 and 11.'

  return (
    <div className="flex flex-col items-center gap-3">
      <Button size="xl" nativeButton={false} render={<a href={INSTALLER_URL} download="BetterAirPlay-Setup.exe" />}>
        <Download data-icon="inline-start" aria-hidden />
        Download for Windows
      </Button>
      <p className="text-sm text-muted-foreground">Free. {details}</p>
    </div>
  )
}
