<template>
  <div>
    <a-row :gutter="16" class="mb-4">
      <a-col :span="8">
        <a-card>
          <a-statistic title="Tổng nhóm sách" :value="groups.length" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card>
          <a-statistic title="Tổng lượt đánh giá" :value="totalReviews" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card>
          <a-statistic title="Điểm trung bình" :value="avgRating" :precision="1" suffix="/5" />
        </a-card>
      </a-col>
    </a-row>

    <a-card title="Đánh giá sách" :body-style="{ padding: 0 }">
      <a-table
        :columns="columns"
        :data-source="groups"
        :loading="loading"
        :pagination="{ pageSize: 10 }"
        row-key="bookId"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'rating'">
            <a-rate :value="record.averageRating" disabled allow-half />
            <span class="ml-2">{{ record.averageRating.toFixed(1) }}/5</span>
          </template>
          <template v-if="column.key === 'reviews'">
            <a-tag color="blue">{{ record.reviews.length }}</a-tag>
          </template>
          <template v-if="column.key === 'latest'">
            <div v-if="record.reviews[0]">
              <div class="font-weight-bold">{{ record.reviews[0].comment || record.reviews[0].Comment || 'Không có nhận xét' }}</div>
              <div class="text-secondary">{{ fmtDate(record.reviews[0].createdAt || record.reviews[0].CreatedAt) }}</div>
            </div>
            <span v-else>—</span>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import dayjs from 'dayjs'

const loading = ref(false)
const groups = ref([])

const columns = [
  { title: 'Mã sách', dataIndex: 'bookId', key: 'bookId', width: 120 },
  { title: 'Tiêu đề', dataIndex: 'title', key: 'title', width: 220 },
  { title: 'Điểm', key: 'rating', width: 220 },
  { title: 'Lượt', key: 'reviews', width: 90 },
  { title: 'Nhận xét mới nhất', key: 'latest' }
]

const totalReviews = computed(() => groups.value.reduce((sum, item) => sum + item.reviews.length, 0))
const avgRating = computed(() => {
  const all = groups.value.flatMap(item => item.reviews.map(r => Number(r.rating || r.Rating || 0))).filter(n => Number.isFinite(n))
  if (!all.length) return 0
  return all.reduce((sum, n) => sum + n, 0) / all.length
})

async function load() {
  loading.value = true
  try {
    const r = await fetch(`${window.location.origin}/api/circulation/books/reviews`, {
      headers: {
        'Content-Type': 'application/json',
        ...(localStorage.getItem('authToken') ? { Authorization: `Bearer ${localStorage.getItem('authToken')}` } : {})
      }
    })
    if (!r.ok) {
      groups.value = []
      return
    }
    const data = await r.json()
    groups.value = Array.isArray(data)
      ? data.map(item => ({
          bookId: item.bookId || item.BookId || '',
          title: item.title || item.Title || '',
          averageRating: Number(item.averageRating || item.AverageRating || 0),
          reviews: Array.isArray(item.reviews || item.Reviews) ? (item.reviews || item.Reviews) : []
        }))
      : []
  } catch {
    groups.value = []
  } finally {
    loading.value = false
  }
}

function fmtDate(d) {
  return d ? dayjs(d).format('DD/MM/YYYY HH:mm') : '—'
}

onMounted(load)
</script>

<style scoped>
.mb-4 { margin-bottom: 16px; }
.ml-2 { margin-left: 8px; }
.text-secondary { color: rgba(0,0,0,0.45); font-size: 12px; }
.font-weight-bold { font-weight: 600; }
</style>
