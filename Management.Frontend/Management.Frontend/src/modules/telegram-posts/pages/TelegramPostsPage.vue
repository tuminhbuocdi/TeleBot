<template>
  <div>
    <h2>Telegram Posts</h2>

    <div style="display: flex; gap: 8px; flex-wrap: wrap; margin: 8px 0">
      <input v-model="q" placeholder="Search title/content..." style="padding: 6px 8px" />

      <select v-model="activeFilter" style="padding: 6px 8px">
        <option value="all">All</option>
        <option value="active">Active</option>
        <option value="inactive">Inactive</option>
      </select>

      <button
        type="button"
        @click="load"
        :disabled="loading"
        style="padding: 6px 10px; border: 1px solid #ddd; border-radius: 6px; background: white; cursor: pointer"
      >
        {{ loading ? "Loading..." : "Reload" }}
      </button>
    </div>

    <div v-if="error" style="color: red; margin: 8px 0">{{ error }}</div>

    <div v-if="!loading && posts.length === 0" style="color: #666">
      No posts
    </div>

    <div v-if="posts.length > 0" class="posts-grid">
      <div v-for="p in posts" :key="p.postId" class="post-card">
        <div class="post-thumb" v-if="p.firstTelegramFileId">
          <div v-if="p.firstMediaType === 'photo'" class="post-thumb-media">
            <img :src="filePreviewUrl(p.firstTelegramFileId)" class="post-thumb-img" />
          </div>
          <div v-else-if="p.firstMediaType === 'video'" class="post-thumb-video">
            <div class="post-thumb-video-label">Video</div>
          </div>
        </div>
        <div v-else class="post-thumb post-thumb-empty"></div>

        <div class="post-body">
          <div class="post-title" :title="p.title || '(no title)'">{{ p.title || "(no title)" }}</div>

          <div class="post-meta">
            <span class="post-meta-item">{{ p.createdAt ? fmt(p.createdAt) : "" }}</span>
            <span class="post-meta-item">Media: {{ p.mediaCount }}</span>
            <span class="post-meta-item" :style="{ color: p.isActive ? '#166534' : '#b91c1c' }">
              {{ p.isActive ? "Active" : "Inactive" }}
            </span>
          </div>

          <div class="post-desc">{{ previewText(p.content) }}</div>

          <div class="post-actions">
            <button
              type="button"
              @click="openDetail(p.postId)"
              class="post-btn"
            >
              Detail
            </button>

            <button
              type="button"
              @click="toggleActive(p)"
              :disabled="savingId === p.postId"
              class="post-btn"
            >
              {{ savingId === p.postId ? "Saving..." : (p.isActive ? "Disable" : "Enable") }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <div style="display: flex; gap: 8px; align-items: center; margin-top: 12px">
      <button
        type="button"
        @click="prev"
        :disabled="loading || page <= 1"
        style="padding: 6px 10px; border: 1px solid #ddd; border-radius: 6px; background: white; cursor: pointer"
      >
        Prev
      </button>
      <div>Page: {{ page }}</div>
      <button
        type="button"
        @click="next"
        :disabled="loading || posts.length < pageSize"
        style="padding: 6px 10px; border: 1px solid #ddd; border-radius: 6px; background: white; cursor: pointer"
      >
        Next
      </button>
    </div>

    <div v-if="detail" style="margin-top: 18px; border-top: 1px solid #eee; padding-top: 12px">
      <h3>Post medias</h3>
      <div style="display: flex; gap: 10px; flex-wrap: wrap">
        <div v-for="m in detail.medias" :key="m.mediaId" style="border: 1px solid #eee; padding: 8px; border-radius: 6px">
          <div><strong>{{ m.mediaType }}</strong> | order {{ m.sortOrder }}</div>
          <div style="color: #666" v-if="m.duration">duration: {{ m.duration }}s</div>
          <div style="color: #666" v-if="m.fileSize">size: {{ m.fileSize }}</div>
          <div style="margin-top: 6px" v-if="m.mediaType === 'photo' && m.telegramFileId">
            <img :src="filePreviewUrl(m.telegramFileId)" style="max-width: 220px; border-radius: 6px" />
          </div>
          <div style="color: #666; margin-top: 6px" v-else>
            telegramFileId: {{ m.telegramFileId || '-' }}
          </div>
        </div>
      </div>

      <button
        type="button"
        @click="detail = null"
        style="margin-top: 10px; padding: 6px 10px; border: 1px solid #ddd; border-radius: 6px; background: white; cursor: pointer"
      >
        Close detail
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue"
import api, { type ApiError } from "@/infrastructure/http/apiClient"

type TelegramPostListRow = {
  postId: string
  title: string | null
  content: string | null
  viewCount: number
  likeCount: number
  isActive: boolean
  createdAt: string | null
  updatedAt: string | null
  mediaCount: number
  firstMediaType: string | null
  firstTelegramFileId: string | null
}

type TelegramPostMediaRow = {
  mediaId: string
  postId: string
  mediaType: string
  fileUrl: string
  telegramFileId: string | null
  duration: number | null
  fileSize: number | null
  thumbnailUrl: string | null
  sortOrder: number
  isActive: boolean
  createdAt: string | null
}

type DetailResponse = {
  postId: string
  medias: TelegramPostMediaRow[]
}

const posts = ref<TelegramPostListRow[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

const page = ref(1)
const pageSize = 20

const q = ref("")
const activeFilter = ref<"all" | "active" | "inactive">("all")

const savingId = ref<string | null>(null)

const detail = ref<DetailResponse | null>(null)

const isActiveQuery = computed(() => {
  if (activeFilter.value === "active") return true
  if (activeFilter.value === "inactive") return false
  return null
})

const fmt = (iso: string) => {
  try {
    return new Date(iso).toLocaleString()
  } catch {
    return iso
  }
}

const previewText = (s: string | null) => {
  if (!s) return ""
  const t = s.trim()
  if (t.length <= 180) return t
  return t.slice(0, 180) + "..."
}

// NOTE: Telegram file_id cannot be previewed directly via HTTP.
// This function is a placeholder so UI can evolve when you add a proxy endpoint.
const filePreviewUrl = (telegramFileId: string) => {
  return ""
}

const load = async () => {
  loading.value = true
  error.value = null
  detail.value = null
  try {
    const res = await api.get<TelegramPostListRow[]>("/telegram-posts", {
      params: {
        page: page.value,
        pageSize,
        isActive: isActiveQuery.value,
        q: q.value,
      },
    } as any)

    posts.value = res
  } catch (e: any) {
    const err = e as ApiError
    error.value = err.message ?? "Failed to load"
  } finally {
    loading.value = false
  }
}

const openDetail = async (postId: string) => {
  error.value = null
  try {
    detail.value = await api.get<DetailResponse>(`/telegram-posts/${postId}`)
  } catch (e: any) {
    const err = e as ApiError
    error.value = err.message ?? "Failed to load detail"
  }
}

const toggleActive = async (p: TelegramPostListRow) => {
  savingId.value = p.postId
  error.value = null
  try {
    await api.put(`/telegram-posts/${p.postId}/active`, {
      isActive: !p.isActive,
    })

    p.isActive = !p.isActive
  } catch (e: any) {
    const err = e as ApiError
    error.value = err.message ?? "Failed to update"
  } finally {
    savingId.value = null
  }
}

const prev = async () => {
  if (page.value <= 1) return
  page.value--
  await load()
}

const next = async () => {
  page.value++
  await load()
}

// initial load
void load()
</script>

<style scoped>
.posts-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(210px, 1fr));
  gap: 12px;
  margin-top: 10px;
}

.post-card {
  border: 1px solid #eee;
  border-radius: 12px;
  overflow: hidden;
  background: #fff;
  display: flex;
  flex-direction: column;
}

.post-thumb {
  width: 100%;
  aspect-ratio: 16 / 9;
  background: #f3f4f6;
  display: flex;
  align-items: center;
  justify-content: center;
}

.post-thumb-media {
  width: 100%;
  height: 100%;
}

.post-thumb-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.post-thumb-video {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: flex-end;
  justify-content: flex-end;
  padding: 8px;
  box-sizing: border-box;
}

.post-thumb-video-label {
  background: rgba(0, 0, 0, 0.65);
  color: #fff;
  padding: 4px 8px;
  border-radius: 999px;
  font-size: 12px;
}

.post-thumb-empty {
  background: linear-gradient(180deg, #f3f4f6, #ffffff);
}

.post-body {
  padding: 10px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.post-title {
  font-weight: 600;
  line-height: 1.25;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.post-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 10px;
  color: #666;
  font-size: 12px;
}

.post-meta-item {
  white-space: nowrap;
}

.post-desc {
  color: #444;
  font-size: 13px;
  line-height: 1.35;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
  min-height: 52px;
}

.post-actions {
  display: flex;
  gap: 8px;
  margin-top: 2px;
}

.post-btn {
  padding: 6px 10px;
  border: 1px solid #ddd;
  border-radius: 8px;
  background: white;
  cursor: pointer;
  font-size: 13px;
}

.post-btn:disabled {
  cursor: not-allowed;
  opacity: 0.7;
}
</style>
