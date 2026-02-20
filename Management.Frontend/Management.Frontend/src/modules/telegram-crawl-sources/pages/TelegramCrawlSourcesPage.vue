<template>
  <div>
    <h2>Telegram Crawl Sources</h2>

    <div style="display: flex; gap: 8px; flex-wrap: wrap; margin: 8px 0">
      <input v-model="q" placeholder="Search title/username..." style="padding: 6px 8px" />

      <select v-model="enabledFilter" style="padding: 6px 8px">
        <option value="all">All</option>
        <option value="enabled">Enabled</option>
        <option value="disabled">Disabled</option>
      </select>

      <button
        type="button"
        @click="load"
        :disabled="loading"
        style="padding: 6px 10px; border: 1px solid #ddd; border-radius: 6px; background: white; cursor: pointer"
      >
        {{ loading ? "Loading..." : "Reload" }}
      </button>

      <label style="display: inline-flex; gap: 6px; align-items: center; color: #333">
        <input type="checkbox" v-model="includeHidden" />
        Show hidden
      </label>
    </div>

    <div v-if="error" style="color: red; margin: 8px 0">{{ error }}</div>

    <div v-if="!loading && rows.length === 0" style="color: #666">No sources</div>

    <div
      v-for="s in rows"
      :key="s.sourceId"
      style="border: 1px solid #eee; border-radius: 8px; padding: 10px; margin: 8px 0"
    >
      <div style="display: flex; justify-content: space-between; align-items: start; gap: 10px">
        <div style="flex: 1; display: flex; gap: 10px">
          <div style="width: 40px; height: 40px; flex: 0 0 40px">
            <img
              v-if="avatarUrl(s)"
              :src="avatarUrl(s)"
              style="width: 40px; height: 40px; border-radius: 999px; object-fit: cover; border: 1px solid #eee"
              @error="onAvatarError(s.sourceId)"
            />
            <div
              v-else
              style="width: 40px; height: 40px; border-radius: 999px; background: #f3f4f6; display: flex; align-items: center; justify-content: center; border: 1px solid #eee; color: #111"
            >
              {{ (s.title || s.peerUsername || '?').slice(0, 1).toUpperCase() }}
            </div>
          </div>

          <div>
            <div style="display: flex; gap: 8px; flex-wrap: wrap; align-items: center">
              <strong>{{ s.title || '(no title)' }}</strong>
              <span style="color: #666">{{ s.peerType }} : {{ s.peerId }}</span>
              <span v-if="s.peerUsername" style="color: #666">@{{ s.peerUsername }}</span>
              <span v-if="s.accessHash" style="color: #666">hash: {{ s.accessHash }}</span>
              <span :style="{ color: s.isEnabled ? '#166534' : '#b91c1c' }">
                {{ s.isEnabled ? 'Enabled' : 'Disabled' }}
              </span>
              <span v-if="s.isHidden" style="color: #6b7280">Hidden</span>
            </div>
          </div>
        </div>

        <div>
          <button
            type="button"
            @click="toggle(s)"
            :disabled="savingId === s.sourceId"
            style="padding: 6px 10px; border: 1px solid #ddd; border-radius: 6px; background: white; cursor: pointer"
          >
            {{ savingId === s.sourceId ? 'Saving...' : s.isEnabled ? 'Disable' : 'Enable' }}
          </button>

          <button
            type="button"
            @click="toggleHidden(s)"
            :disabled="savingId === s.sourceId"
            style="margin-left: 6px; padding: 6px 10px; border: 1px solid #ddd; border-radius: 6px; background: white; cursor: pointer"
          >
            {{ savingId === s.sourceId ? 'Saving...' : s.isHidden ? 'Unhide' : 'Hide' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue"
import api, { type ApiError } from "@/infrastructure/http/apiClient"

type CrawlSourceRow = {
  sourceId: string
  peerType: string
  peerId: number
  accessHash: number | null
  peerUsername: string | null
  title: string | null
  isEnabled: boolean
  isHidden: boolean
}

const rows = ref<CrawlSourceRow[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

const q = ref("")
const enabledFilter = ref<"all" | "enabled" | "disabled">("all")
const includeHidden = ref(false)
const savingId = ref<string | null>(null)

const avatarFailed = ref<Record<string, true>>({})

const onAvatarError = (sourceId: string) => {
  avatarFailed.value = { ...avatarFailed.value, [sourceId]: true }
}

const avatarUrl = (s: CrawlSourceRow) => {
  if (!s.peerUsername) return null
  if (avatarFailed.value[s.sourceId]) return null
  return `https://t.me/i/userpic/320/${encodeURIComponent(s.peerUsername)}.jpg`
}

const isEnabledQuery = computed(() => {
  if (enabledFilter.value === "enabled") return true
  if (enabledFilter.value === "disabled") return false
  return null
})

const load = async () => {
  loading.value = true
  error.value = null
  try {
    rows.value = await api.get<CrawlSourceRow[]>("/telegram-crawl-sources", {
      params: {
        isEnabled: isEnabledQuery.value,
        includeHidden: includeHidden.value,
        q: q.value,
      },
    } as any)
  } catch (e: any) {
    const err = e as ApiError
    error.value = err.message ?? "Failed to load"
  } finally {
    loading.value = false
  }
}

const toggleHidden = async (s: CrawlSourceRow) => {
  savingId.value = s.sourceId
  error.value = null
  try {
    await api.put(`/telegram-crawl-sources/${s.sourceId}/hidden`, {
      isHidden: !s.isHidden,
    })
    s.isHidden = !s.isHidden
    if (!includeHidden.value && s.isHidden) {
      rows.value = rows.value.filter((x) => x.sourceId !== s.sourceId)
    }
  } catch (e: any) {
    const err = e as ApiError
    error.value = err.message ?? "Failed to update"
  } finally {
    savingId.value = null
  }
}

const toggle = async (s: CrawlSourceRow) => {
  savingId.value = s.sourceId
  error.value = null
  try {
    await api.put(`/telegram-crawl-sources/${s.sourceId}/enabled`, {
      isEnabled: !s.isEnabled,
    })
    s.isEnabled = !s.isEnabled
  } catch (e: any) {
    const err = e as ApiError
    error.value = err.message ?? "Failed to update"
  } finally {
    savingId.value = null
  }
}

void load()
</script>
