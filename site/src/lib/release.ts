import { useEffect, useState } from 'react'

export const REPO = 'bramsuurdje/better-airplay'
export const REPO_URL = `https://github.com/${REPO}`
export const RELEASES_URL = `${REPO_URL}/releases`
export const ISSUES_URL = `${REPO_URL}/issues`
/** Stable link to the newest installer. The release workflow attaches a copy under this name. */
export const INSTALLER_URL = `${RELEASES_URL}/latest/download/BetterAirPlay-Setup.exe`

const INSTALLER_NAME = 'BetterAirPlay-Setup.exe'

export type LatestRelease = { version: string; installerBytes: number }

/** Reads the version and installer size of the newest GitHub release. Throws when the response doesn't look like one. */
export async function fetchLatestRelease(): Promise<LatestRelease> {
  const response = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, {
    headers: { Accept: 'application/vnd.github+json' },
  })
  if (!response.ok) throw new Error(`GitHub answered ${response.status}`)
  const release: unknown = await response.json()
  if (!isRelease(release)) throw new Error('Unexpected release shape')
  const installer = release.assets.find((asset) => asset.name === INSTALLER_NAME) ?? release.assets[0]
  if (!installer) throw new Error('Release has no installer')
  return { version: release.tag_name.replace(/^v/, ''), installerBytes: installer.size }
}

type ReleaseJson = { tag_name: string; assets: { name: string; size: number }[] }

function isRelease(value: unknown): value is ReleaseJson {
  if (typeof value !== 'object' || value === null) return false
  const { tag_name, assets } = value as Record<string, unknown>
  return typeof tag_name === 'string' && Array.isArray(assets)
}

/** The latest release once it has loaded. Stays null if GitHub can't be reached; the download link works regardless. */
export function useLatestRelease() {
  const [release, setRelease] = useState<LatestRelease | null>(null)
  useEffect(() => {
    let cancelled = false
    fetchLatestRelease()
      .then((latest) => {
        if (!cancelled) setRelease(latest)
      })
      .catch(() => {})
    return () => {
      cancelled = true
    }
  }, [])
  return release
}
