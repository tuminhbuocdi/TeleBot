<template>
  <div>
    <h2>Crash Game Overview</h2>

    <div v-if="loading">Loading...</div>
    <div v-if="error" style="color: red">{{ error }}</div>

    <div v-if="!loading && !error" style="margin: 8px 0; color: #666">
      <div style="display: flex; align-items: center; gap: 10px; flex-wrap: wrap">
        <div>
          Next update in: <strong>{{ countdownSec }}s</strong>
          <span v-if="lastUpdatedAt"> | Last updated: {{ lastUpdatedAt }}</span>
        </div>

        <div v-if="latestRecord">
          Latest: <strong>{{ latestRecord.gameId }}</strong> -
          <strong>{{ latestRecord.rate }}</strong>
        </div>

        <button
          type="button"
          :disabled="testSending"
          @click="sendTestMessage"
          style="padding: 6px 10px; border: 1px solid #ddd; border-radius: 6px; background: white; cursor: pointer"
        >
          {{ testSending ? "Sending..." : "Send test message" }}
        </button>
        <span v-if="testStatus" :style="{ color: testStatusColor }">{{ testStatus }}</span>
      </div>
    </div>

    <div v-if="!loading && !error && signals">
      <h3>Signals</h3>

      <div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px">
        <div style="border: 1px solid #ddd; padding: 12px; border-radius: 6px">
          <div style="display: flex; justify-content: space-between; align-items: center">
            <h4 style="margin: 0">Cashout 2.00</h4>
            <span :style="badgeStyle(signals.t200.state)">{{ stateLabel(signals.t200.state) }}</span>
          </div>

          <div>Baseline p0 (&lt;2.00, last {{ signals.t200.baseN }}): {{ signals.t200.p0 }}</div>
          <div>p10: {{ signals.t200.p10 }}</div>
          <div>p30: {{ signals.t200.p30 }} | z30: {{ signals.t200.z30 }}</div>
          <div>p50: {{ signals.t200.p50 }} | z50: {{ signals.t200.z50 }}</div>
          <div style="color: #666">{{ signals.t200.reason }}</div>
        </div>

        <div style="border: 1px solid #ddd; padding: 12px; border-radius: 6px">
          <div style="display: flex; justify-content: space-between; align-items: center">
            <h4 style="margin: 0">Cashout 1.35</h4>
            <span :style="badgeStyle(signals.t135.state)">{{ stateLabel(signals.t135.state) }}</span>
          </div>

          <div>Baseline p0 (&lt;1.35, last {{ signals.t135.baseN }}): {{ signals.t135.p0 }}</div>
          <div>p10: {{ signals.t135.p10 }}</div>
          <div>p30: {{ signals.t135.p30 }} | z30: {{ signals.t135.z30 }}</div>
          <div>p50: {{ signals.t135.p50 }} | z50: {{ signals.t135.z50 }}</div>
          <div style="color: #666">{{ signals.t135.reason }}</div>
        </div>
      </div>
    </div>

    <div v-if="!loading && !error && timeline50.length" style="margin-top: 12px">
      <h3>Timeline (last 50)</h3>

      <div style="border: 1px solid #ddd; border-radius: 6px; padding: 12px">
        <svg :width="timelineSvg.width" :height="timelineSvg.height" style="display: block; width: 100%">
          <line
            :x1="0"
            :x2="timelineSvg.width"
            :y1="timelineSvg.y135"
            :y2="timelineSvg.y135"
            stroke="#f59e0b"
            stroke-width="1"
            stroke-dasharray="4 4"
          />
          <line
            :x1="0"
            :x2="timelineSvg.width"
            :y1="timelineSvg.y200"
            :y2="timelineSvg.y200"
            stroke="#10b981"
            stroke-width="1"
            stroke-dasharray="4 4"
          />
          <polyline :points="timelineSvg.points" fill="none" stroke="#2563eb" stroke-width="2" />
        </svg>

        <div style="margin-top: 10px; display: grid; gap: 6px">
          <div style="display: flex; align-items: center; gap: 8px">
            <strong style="min-width: 80px">Cashout 2.00</strong>
            <div style="display: flex; gap: 2px; flex-wrap: wrap">
              <span
                v-for="r in timeline50"
                :key="`t2-${r.gameId}`"
                :title="`Game ${r.gameId} - rate ${r.rate}`"
                :style="{
                  width: '10px',
                  height: '10px',
                  borderRadius: '2px',
                  background: r.rate >= 2 ? '#10b981' : '#ef4444',
                }"
              />
            </div>
          </div>

          <div style="display: flex; align-items: center; gap: 8px">
            <strong style="min-width: 80px">Cashout 1.35</strong>
            <div style="display: flex; gap: 2px; flex-wrap: wrap">
              <span
                v-for="r in timeline50"
                :key="`t135-${r.gameId}`"
                :title="`Game ${r.gameId} - rate ${r.rate}`"
                :style="{
                  width: '10px',
                  height: '10px',
                  borderRadius: '2px',
                  background: r.rate >= 1.35 ? '#10b981' : '#ef4444',
                }"
              />
            </div>
          </div>
        </div>

        <div style="margin-top: 8px; color: #666">
          <div>Line: rate</div>
          <div style="display: flex; gap: 12px; flex-wrap: wrap">
            <span>Dashed: 1.35 (orange)</span>
            <span>Dashed: 2.00 (green)</span>
          </div>
        </div>
      </div>
    </div>

    <div v-if="!loading && !error && redZoneStats" style="margin-top: 12px">
      <h3>Red zones (&lt; 2.00)</h3>

      <div style="border: 1px solid #ddd; border-radius: 6px; padding: 12px">
        <div>Total games analyzed: {{ redZoneStats.totalGames }}</div>
        <div>Red zones: {{ redZoneStats.zones }}</div>
        <div>Avg zone length (games): {{ redZoneStats.avgZoneLen }}</div>

        <div style="margin-top: 8px">
          Avg gap between zones (games): {{ redZoneStats.avgGap }}</div>
        <div>Median gap: {{ redZoneStats.medianGap }}</div>
        <div>Min gap: {{ redZoneStats.minGap }} | Max gap: {{ redZoneStats.maxGap }}</div>

        <div v-if="redZonePrediction" style="margin-top: 10px">
          <h4 style="margin: 0 0 6px 0">Next red zone (estimate)</h4>
          <div>
            Based on: median gap
          </div>
          <div>
            Expected start around index <strong>{{ redZonePrediction.expectedStartIndex }}</strong>
            (Game ~<strong>{{ redZonePrediction.expectedStartGameId }}</strong>)
          </div>
          <div>
            Remaining: ~<strong>{{ redZonePrediction.remainingGames }}</strong> games
          </div>
        </div>

        <div style="margin-top: 10px" v-if="redZones.length">
          <h4 style="margin: 0 0 6px 0">Latest zones</h4>
          <div style="display: grid; gap: 6px">
            <div
              v-for="z in redZones.slice(-5).reverse()"
              :key="`${z.startGameId}-${z.endGameId}`"
              style="border: 1px solid #eee; border-radius: 6px; padding: 8px"
            >
              <div>
                Range: <strong>{{ z.startGameId }}</strong> → <strong>{{ z.endGameId }}</strong>
                ({{ z.len }} games)
              </div>
              <div style="color: #666">Triggered by: {{ z.triggers }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from "vue"
import api, { type ApiError } from "@/infrastructure/http/apiClient"
import { getHubConnection, startHub, stopHub } from "@/infrastructure/realtime/signalR"

type CrashRecord = {
  gameId: number
  rate: number
}

type SignalState = "Setup" | "Trigger" | "Neutral" | "Exit"

type ThresholdSignal = {
  threshold: number
  baseN: number
  p0: number
  p10: number
  p30: number
  p50: number
  z30: number
  z50: number
  state: SignalState
  reason: string
}

type Signals = {
  t200: ThresholdSignal
  t135: ThresholdSignal
}

type RedZone = {
  startIndex: number
  endIndex: number
  startGameId: number
  endGameId: number
  len: number
  triggers: string
}

type RedZoneStats = {
  totalGames: number
  zones: number
  avgZoneLen: number
  avgGap: number
  medianGap: number
  minGap: number
  maxGap: number
}

type RedZonePrediction = {
  expectedStartIndex: number
  expectedStartGameId: number
  remainingGames: number
}

const rows = ref<CrashRecord[]>([])
const latestRecord = ref<CrashRecord | null>(null)
const timeline50 = ref<CrashRecord[]>([])
const timelineSvg = ref({
  width: 700,
  height: 180,
  points: "",
  y135: 0,
  y200: 0,
})
const redZones = ref<RedZone[]>([])
const redZoneStats = ref<RedZoneStats | null>(null)
const redZonePrediction = ref<RedZonePrediction | null>(null)
const signals = ref<Signals | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

const refreshEverySec = 10
const countdownSec = ref(refreshEverySec)
const lastUpdatedAt = ref<string | null>(null)

const notifyCooldownMs = 2 * 60 * 1000
const lastNotifyAt = ref<Record<string, number>>({})
const prevState = ref<Record<string, SignalState>>({})

const redTeleCooldownMs = 10 * 60 * 1000
const redTeleLastAt = ref<Record<string, number>>({})
const redZonePrevIn = ref(false)
const redZoneExitWindow = ref<number[]>([])
const redZoneExitArmed = ref(false)
const redZoneExitNotified = ref(false)
const redZoneExitFromGameId = ref<number | null>(null)
const lastRedZoneProcessedGameId = ref<number | null>(null)
const lastStopWarnKey = ref<string | null>(null)

const redZoneProgressNextIndex = ref<number | null>(null)
const redZoneProgressMinGames = 5
const redZoneProgressMaxGames = 8
const redZoneProgressCooldownMs = 60 * 1000
const redZoneProgressLastAt = ref<Record<string, number>>({})

const testSending = ref(false)
const testStatus = ref<string | null>(null)
const testStatusColor = ref("#666")

let pollId: number | null = null
let countdownId: number | null = null
let hubHandlerAttached = false

const round4 = (x: number) => Math.round(x * 10000) / 10000
const round3 = (x: number) => Math.round(x * 1000) / 1000
const clamp = (x: number, min: number, max: number) => Math.max(min, Math.min(max, x))

const badgeStyle = (state: SignalState) => {
  const base = {
    padding: "4px 10px",
    borderRadius: "999px",
    fontWeight: "700",
    fontSize: "12px",
    border: "1px solid transparent",
  } as const

  if (state === "Trigger") {
    return { ...base, background: "#dcfce7", color: "#166534", borderColor: "#86efac" }
  }
  if (state === "Setup") {
    return { ...base, background: "#ffedd5", color: "#9a3412", borderColor: "#fdba74" }
  }
  if (state === "Exit") {
    return { ...base, background: "#e0e7ff", color: "#3730a3", borderColor: "#a5b4fc" }
  }
  return { ...base, background: "#f3f4f6", color: "#374151", borderColor: "#e5e7eb" }
}

const stateLabel = (state: SignalState) => {
  if (state === "Trigger") return "Có thể đánh"
  if (state === "Setup") return "Nguy hiểm"
  if (state === "Exit") return "Nên nghỉ"
  return "Quan sát"
}

const nowText = () => {
  const d = new Date()
  const hh = String(d.getHours()).padStart(2, "0")
  const mm = String(d.getMinutes()).padStart(2, "0")
  const ss = String(d.getSeconds()).padStart(2, "0")
  return `${hh}:${mm}:${ss}`
}

const markUpdated = () => {
  lastUpdatedAt.value = nowText()
}

const resetCountdown = () => {
  countdownSec.value = refreshEverySec
}

const recomputeLatest = () => {
  let latest: CrashRecord | null = null
  for (const r of rows.value) {
    if (!latest || r.gameId > latest.gameId) {
      latest = r
    }
  }
  latestRecord.value = latest
}

const recomputeTimeline = () => {
  const sortedDesc = [...rows.value].sort((a, b) => b.gameId - a.gameId)
  const last50Desc = sortedDesc.slice(0, 50)
  // Display left-to-right from older -> newer
  timeline50.value = [...last50Desc].reverse()

  const width = 700
  const height = 180
  const pad = 10

  const capRate = (rate: number) => Math.min(10, rate)
  const maxRate = Math.max(2.0, ...timeline50.value.map((x) => capRate(x.rate)))
  const minRate = 0
  const y = (rate: number) => {
    const v = (capRate(rate) - minRate) / (maxRate - minRate || 1)
    return pad + (1 - v) * (height - pad * 2)
  }

  const n = timeline50.value.length
  const x = (idx: number) => {
    if (n <= 1) return pad
    return pad + (idx / (n - 1)) * (width - pad * 2)
  }

  const points = timeline50.value.map((r, i) => `${x(i)},${y(r.rate)}`).join(" ")
  timelineSvg.value = {
    width,
    height,
    points,
    y135: y(1.35),
    y200: y(2.0),
  }
}

const median = (arr: number[]) => {
  if (!arr.length) return 0
  const a = [...arr].sort((x, y) => x - y)
  const mid = Math.floor(a.length / 2)
  if (a.length % 2 === 1) return a[mid] ?? 0
  return ((a[mid - 1] ?? 0) + (a[mid] ?? 0)) / 2
}

const recomputeRedZones = () => {
  const sortedAsc = [...rows.value].sort((a, b) => a.gameId - b.gameId)
  const total = sortedAsc.length
  if (!total) {
    redZones.value = []
    redZoneStats.value = null
    redZonePrediction.value = null
    return
  }

  const isBad = sortedAsc.map((r) => (r.rate < 2 ? 1 : 0))
  const prefix = new Array<number>(total + 1).fill(0)
  for (let i = 0; i < total; i++) {
    prefix[i + 1] = prefix[i]! + isBad[i]!
  }

  const windows: { start: number; end: number; trigger: string }[] = []

  const addWindow = (end: number, n: number, trigger: string) => {
    const start = end - n + 1
    if (start < 0) return
    windows.push({ start, end, trigger })
  }

  for (let end = 0; end < total; end++) {
    const n30 = 30
    if (end - n30 + 1 >= 0) {
      const bad30 = prefix[end + 1]! - prefix[end + 1 - n30]!
      if (bad30 >= 20) {
        addWindow(end, n30, "30 games: >= 20 <2.0")
      }
    }

    const n34 = 34
    if (end - n34 + 1 >= 0) {
      const bad34 = prefix[end + 1]! - prefix[end + 1 - n34]!
      if (bad34 >= 20 && bad34 <= 24) {
        addWindow(end, n34, "34 games: 20-24 <2.0")
      }
    }
  }

  if (!windows.length) {
    redZones.value = []
    redZoneStats.value = {
      totalGames: total,
      zones: 0,
      avgZoneLen: 0,
      avgGap: 0,
      medianGap: 0,
      minGap: 0,
      maxGap: 0,
    }
    redZonePrediction.value = null
    return
  }

  windows.sort((a, b) => a.start - b.start || a.end - b.end)
  const merged: { start: number; end: number; triggers: Set<string> }[] = []

  for (const w of windows) {
    const last = merged[merged.length - 1]
    if (!last || w.start > last.end + 1) {
      merged.push({ start: w.start, end: w.end, triggers: new Set([w.trigger]) })
    } else {
      last.end = Math.max(last.end, w.end)
      last.triggers.add(w.trigger)
    }
  }

  redZones.value = merged.map((m) => {
    const startGameId = sortedAsc[m.start]!.gameId
    const endGameId = sortedAsc[m.end]!.gameId
    return {
      startIndex: m.start,
      endIndex: m.end,
      startGameId,
      endGameId,
      len: m.end - m.start + 1,
      triggers: Array.from(m.triggers.values()).join("; "),
    }
  })

  const lens = redZones.value.map((z) => z.len)
  const avgZoneLen = lens.reduce((s, x) => s + x, 0) / (lens.length || 1)

  const gaps: number[] = []
  for (let i = 1; i < redZones.value.length; i++) {
    const prev = redZones.value[i - 1]!
    const curr = redZones.value[i]!
    gaps.push(Math.max(0, curr.startIndex - prev.endIndex - 1))
  }

  const avgGap = gaps.length ? gaps.reduce((s, x) => s + x, 0) / gaps.length : 0
  const medianGap = gaps.length ? median(gaps) : 0
  const minGap = gaps.length ? Math.min(...gaps) : 0
  const maxGap = gaps.length ? Math.max(...gaps) : 0

  redZoneStats.value = {
    totalGames: total,
    zones: redZones.value.length,
    avgZoneLen: Math.round(avgZoneLen * 100) / 100,
    avgGap: Math.round(avgGap * 100) / 100,
    medianGap: Math.round(medianGap * 100) / 100,
    minGap,
    maxGap,
  }

  // Prediction: next red zone based on the latest zone + median gap.
  // Note: expectedStartIndex can be in the future (beyond current dataset end).
  if (!redZones.value.length) {
    redZonePrediction.value = null
    return
  }

  const latestZone = redZones.value[redZones.value.length - 1]!
  const expectedStartIndex = latestZone.endIndex + Math.round(medianGap) + 1
  const lastIndex = total - 1
  const remainingGames = Math.max(0, expectedStartIndex - lastIndex)

  const lastGameId = sortedAsc[lastIndex]!.gameId
  const expectedStartGameId = expectedStartIndex <= lastIndex ? sortedAsc[expectedStartIndex]!.gameId : lastGameId + remainingGames

  redZonePrediction.value = {
    expectedStartIndex,
    expectedStartGameId,
    remainingGames,
  }
}

const shouldNotify = (key: string) => {
  const now = Date.now()
  const last = lastNotifyAt.value[key] ?? 0
  if (now - last < notifyCooldownMs) return false
  lastNotifyAt.value[key] = now
  return true
}

const shouldNotifyRed = (key: string) => {
  const now = Date.now()
  const last = redTeleLastAt.value[key] ?? 0
  if (now - last < redTeleCooldownMs) return false
  redTeleLastAt.value[key] = now
  return true
}

const shouldNotifyRedProgress = (key: string) => {
  const now = Date.now()
  const last = redZoneProgressLastAt.value[key] ?? 0
  if (now - last < redZoneProgressCooldownMs) return false
  redZoneProgressLastAt.value[key] = now
  return true
}

const sendTelegram = async (message: string) => {
  try {
    await api.post("/notifications/telegram", { message })
  } catch {
    // Ignore notify errors to avoid breaking the screen
  }
}

const sendTestMessage = async () => {
  if (testSending.value) return

  testSending.value = true
  testStatus.value = null
  testStatusColor.value = "#666"

  try {
    const msg = `✅ Tin nhắn test từ CrashGameOverview (${nowText()})`
    await api.post("/notifications/telegram", { message: msg })
    testStatus.value = "Sent"
    testStatusColor.value = "#166534"
  } catch (e: any) {
    testStatus.value = "Failed"
    testStatusColor.value = "#b91c1c"
  } finally {
    testSending.value = false
  }
}

const badRate = (windowRows: CrashRecord[], threshold: number) => {
  const n = windowRows.length
  if (!n) return 0
  let bad = 0
  for (const r of windowRows) {
    if (r.rate < threshold) bad++
  }
  return bad / n
}

const zScore = (pN: number, p0: number, n: number) => {
  if (n <= 1) return 0
  const denom = Math.sqrt((p0 * (1 - p0)) / n)
  if (!isFinite(denom) || denom <= 0) return 0
  return (pN - p0) / denom
}

const buildThresholdSignal = (sortedDesc: CrashRecord[], threshold: number, baseN: number): ThresholdSignal => {
  const base = sortedDesc.slice(0, baseN)
  const w10 = sortedDesc.slice(0, 10)
  const w30 = sortedDesc.slice(0, 30)
  const w50 = sortedDesc.slice(0, 50)

  // Laplace smoothing to avoid extreme p0 when data is small/noisy
  const p0Raw = badRate(base, threshold)
  const p0 = clamp((p0Raw * base.length + 1) / (base.length + 2), 0, 1)

  const p10 = badRate(w10, threshold)
  const p30 = badRate(w30, threshold)
  const p50 = badRate(w50, threshold)

  const z30 = zScore(p30, p0, w30.length)
  const z50 = zScore(p50, p0, w50.length)
  const z10 = zScore(p10, p0, w10.length)

  const setupZ = threshold >= 2 ? 2.0 : 1.5
  const exitZ = threshold >= 2 ? 0.5 : 0.3
  const setup = z30 >= setupZ || z50 >= setupZ
  const cooling = p10 < p30 || z10 < z30

  let state: SignalState = "Neutral"
  let reason = ""

  if (setup && cooling) {
    state = "Trigger"
    reason = "Tỷ lệ rate < mốc đang cao bất thường (so với baseline) nhưng 10 ván gần nhất đang hạ nhiệt."
  } else if (setup) {
    state = "Setup"
    reason = "Tỷ lệ rate < mốc đang cao bất thường so với baseline (đang vào vùng xấu)."
  } else if (z30 <= exitZ) {
    state = "Exit"
    reason = "Đã quay về gần bình thường / cải thiện rõ (z30 thấp)."
  } else {
    state = "Neutral"
    reason = "Không có tín hiệu rõ ràng so với baseline."
  }

  return {
    threshold,
    baseN: base.length,
    p0: round4(p0),
    p10: round4(p10),
    p30: round4(p30),
    p50: round4(p50),
    z30: round3(z30),
    z50: round3(z50),
    state,
    reason,
  }
}

const buildSignals = (allRows: CrashRecord[]): Signals => {
  const sortedDesc = [...allRows].sort((a, b) => b.gameId - a.gameId)
  const baseN = 500

  return {
    t200: buildThresholdSignal(sortedDesc, 2.0, baseN),
    t135: buildThresholdSignal(sortedDesc, 1.35, baseN),
  }
}

const load = async () => {
  loading.value = true
  error.value = null
  try {
    rows.value = await api.get<CrashRecord[]>("/crash-game/overview")
    signals.value = buildSignals(rows.value)
    recomputeLatest()
    recomputeTimeline()
    recomputeRedZones()
    markUpdated()
    resetCountdown()
  } catch (e: any) {
    const err = e as ApiError
    error.value = err.message ?? "Failed to load"
  } finally {
    loading.value = false
  }
}

const refreshTop100 = async () => {
  try {
    const top = await api.get<CrashRecord[]>("/crash-game/overview?take=100")

    // Merge by gameId (newer wins). Keep existing dataset for long-term analytics.
    const map = new Map<number, CrashRecord>()
    for (const r of rows.value) {
      map.set(r.gameId, r)
    }
    for (const r of top) {
      map.set(r.gameId, r)
    }

    rows.value = Array.from(map.values())
    signals.value = buildSignals(rows.value)
    recomputeLatest()
    recomputeTimeline()
    recomputeRedZones()
    markUpdated()
    resetCountdown()
  } catch (e: any) {
    // Keep old data; just surface error
    const err = e as ApiError
    error.value = err.message ?? "Failed to refresh"
  }
}

onMounted(async () => {
  await load()

  const hub = getHubConnection()
  if (!hubHandlerAttached) {
    hub.on("crashRecordsUpdated", () => {
      void refreshTop100()
    })
    hubHandlerAttached = true
  }

  try {
    await startHub()
  } catch {
    // If realtime cannot connect, polling still works.
  }

  pollId = window.setInterval(() => {
    void refreshTop100()
  }, 10_000)

  countdownId = window.setInterval(() => {
    if (countdownSec.value > 0) {
      countdownSec.value--
    } else {
      countdownSec.value = refreshEverySec
    }
  }, 1000)
})

onUnmounted(() => {
  if (pollId) {
    window.clearInterval(pollId)
    pollId = null
  }

  if (countdownId) {
    window.clearInterval(countdownId)
    countdownId = null
  }

  const hub = getHubConnection()
  hub.off("crashRecordsUpdated")
  hubHandlerAttached = false
  void stopHub()
})

watch(
  signals,
  (s) => {
    if (!s) return

    const items = [
      { key: "t200", label: "2.00", sig: s.t200 },
      { key: "t135", label: "1.35", sig: s.t135 },
    ]

    for (const it of items) {
      const prev = prevState.value[it.key]
      const curr = it.sig.state
      prevState.value[it.key] = curr

      if (curr === "Trigger" && prev !== "Trigger") {
        if (!shouldNotify(it.key)) continue

        const msg =
          `🎯 TÍN HIỆU: CÓ THỂ ĐÁNH\n` +
          `Mốc cashout: ${it.label}\n` +
          `p0: ${it.sig.p0}\n` +
          `p10/p30/p50: ${it.sig.p10} / ${it.sig.p30} / ${it.sig.p50}\n` +
          `z30/z50: ${it.sig.z30} / ${it.sig.z50}\n` +
          `Thời gian: ${lastUpdatedAt.value ?? ""}`

        void sendTelegram(msg)
      }
    }
  },
  { deep: true }
)

watch(
  [rows, redZones, redZonePrediction, latestRecord],
  () => {
    if (!rows.value.length || !latestRecord.value) return

    // Avoid reprocessing if latest game did not change
    if (lastRedZoneProcessedGameId.value === latestRecord.value.gameId) return
    lastRedZoneProcessedGameId.value = latestRecord.value.gameId

    const sortedAsc = [...rows.value].sort((a, b) => a.gameId - b.gameId)
    const lastIndex = sortedAsc.length - 1
    const latestZone = redZones.value.length ? redZones.value[redZones.value.length - 1]! : null

    const inRedZone =
      !!latestZone && lastIndex >= latestZone.startIndex && lastIndex <= latestZone.endIndex

    // Enter red zone notification
    if (inRedZone && !redZonePrevIn.value) {
      redZonePrevIn.value = true
      redZoneExitWindow.value = []
      redZoneExitArmed.value = false
      redZoneExitNotified.value = false
      redZoneExitFromGameId.value = null
      redZoneProgressNextIndex.value = null

      const key = `red_enter_${latestZone!.startGameId}_${latestZone!.endGameId}`
      if (shouldNotifyRed(key)) {
        const msg =
          `🟥 CẢNH BÁO: ĐANG VÀO VÙNG ĐỎ (<2.00)\n` +
          `Khoảng: ${latestZone!.startGameId} → ${latestZone!.endGameId}\n` +
          `Game mới nhất: ${latestRecord.value.gameId} - ${latestRecord.value.rate}\n` +
          `Thời gian: ${lastUpdatedAt.value ?? ""}`
        void sendTelegram(msg)
      }
    }

    // Exit red zone: arm post-exit evaluation
    if (!inRedZone && redZonePrevIn.value) {
      redZonePrevIn.value = false
      redZoneExitWindow.value = []
      redZoneExitArmed.value = true
      redZoneExitNotified.value = false
      redZoneExitFromGameId.value = latestRecord.value.gameId

      // Start progress updates after exiting red zone
      const span =
        Math.floor(Math.random() * (redZoneProgressMaxGames - redZoneProgressMinGames + 1)) +
        redZoneProgressMinGames
      redZoneProgressNextIndex.value = lastIndex + span
    }

    // Post-exit signal: within next 5-10 games, if green ratio (>=2.00) >= 65%, notify can bet
    if (redZoneExitArmed.value && !redZoneExitNotified.value) {
      const isGreen = latestRecord.value.rate >= 2 ? 1 : 0
      redZoneExitWindow.value.push(isGreen)

      if (redZoneExitWindow.value.length > 10) {
        redZoneExitWindow.value.shift()
      }

      const n = redZoneExitWindow.value.length
      if (n >= 5) {
        const greens = redZoneExitWindow.value.reduce((s, x) => s + x, 0)
        const reds = n - greens
        const greenRatio = n ? greens / n : 0
        if (greenRatio >= 0.65) {
          redZoneExitNotified.value = true
          redZoneExitArmed.value = false

          const key = `red_exit_ok_${redZoneExitFromGameId.value ?? latestRecord.value.gameId}`
          if (shouldNotifyRed(key)) {
            const msg =
              `🟩 TÍN HIỆU: ĐÃ THOÁT VÙNG ĐỎ\n` +
              `${n} game gần nhất: xanh=${greens}, đỏ=${reds} (xanh: >=2.00 | đỏ: <2.00)\n` +
              `Tỉ lệ xanh: ${Math.round(greenRatio * 100)}% (yêu cầu >= 65%)\n` +
              `Bạn có thể đánh.\n` +
              `Game mới nhất: ${latestRecord.value.gameId} - ${latestRecord.value.rate}\n` +
              `Thời gian: ${lastUpdatedAt.value ?? ""}`
            void sendTelegram(msg)
          }
        }
      }
    }

    // Periodic progress update every ~15-20 games: remaining games until predicted red zone
    if (!inRedZone && redZonePrediction.value && redZonePrediction.value.remainingGames > 0) {
      if (redZoneProgressNextIndex.value === null) {
        const span =
          Math.floor(Math.random() * (redZoneProgressMaxGames - redZoneProgressMinGames + 1)) +
          redZoneProgressMinGames
        redZoneProgressNextIndex.value = lastIndex + span
      }

      if (lastIndex >= (redZoneProgressNextIndex.value ?? Number.MAX_SAFE_INTEGER)) {
        const span =
          Math.floor(Math.random() * (redZoneProgressMaxGames - redZoneProgressMinGames + 1)) +
          redZoneProgressMinGames
        redZoneProgressNextIndex.value = lastIndex + span

        const key = `red_progress_${redZonePrediction.value.expectedStartIndex}_${lastIndex}`
        if (shouldNotifyRedProgress(key)) {
          const msg =
            `📌 CẬP NHẬT DỰ BÁO VÙNG ĐỎ\n` +
            `Còn khoảng: ~${redZonePrediction.value.remainingGames} game có thể tới vùng đỏ\n` +
            `Dự kiến bắt đầu khoảng Game ~${redZonePrediction.value.expectedStartGameId}\n` +
            `Game mới nhất: ${latestRecord.value.gameId} - ${latestRecord.value.rate}\n` +
            `Thời gian: ${lastUpdatedAt.value ?? ""}`
          void sendTelegram(msg)
        }
      }
    }

    // Upcoming red zone warning based on prediction
    const stopWarnThreshold = 10
    if (!inRedZone && redZonePrediction.value && redZonePrediction.value.remainingGames <= stopWarnThreshold) {
      const warnKey = `stop_${redZonePrediction.value.expectedStartIndex}`
      if (lastStopWarnKey.value !== warnKey) {
        lastStopWarnKey.value = warnKey
        if (shouldNotifyRed(warnKey)) {
          const msg =
            `⚠️ CẢNH BÁO: SẮP TỚI VÙNG ĐỎ\n` +
            `Dự kiến bắt đầu khoảng Game ~${redZonePrediction.value.expectedStartGameId}\n` +
            `Còn khoảng: ~${redZonePrediction.value.remainingGames} game\n` +
            `Khuyến nghị: Nên dừng / giảm rủi ro.\n` +
            `Game mới nhất: ${latestRecord.value.gameId} - ${latestRecord.value.rate}\n` +
            `Thời gian: ${lastUpdatedAt.value ?? ""}`
          void sendTelegram(msg)
        }
      }
    }
  },
  { deep: true }
)
</script>
