export type JwtPayload = {
  exp?: number
  [k: string]: unknown
}

export function readJwtPayload(token: string): JwtPayload | null {
  try {
    const parts = token.split(".")
    const base64Url = parts[1]
    if (parts.length < 2 || !base64Url) return null

    const base64 = base64Url.replace(/-/g, "+").replace(/_/g, "/")
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split("")
        .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
        .join(""),
    )

    return JSON.parse(jsonPayload) as JwtPayload
  } catch {
    return null
  }
}

export function isTokenExpired(token: string, nowMs = Date.now()): boolean {
  const payload = readJwtPayload(token)
  const exp = payload?.exp
  if (!exp) return true

  const nowSec = Math.floor(nowMs / 1000)
  return nowSec >= exp
}

export function clearAuthSession() {
  localStorage.removeItem("token")
}
