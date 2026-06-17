<template>
  <div class="page-shell">
    <a-row :gutter="[16, 16]" class="stats-row">
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic title="Tổng nhóm sách" :value="groups.length" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic title="Tổng lượt đánh giá" :value="totalReviews" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic title="Điểm trung bình" :value="avgRating" :precision="1" suffix="/5" />
        </a-card>
      </a-col>
    </a-row>

    <a-card class="panel-card">
      <template #title>
        <div class="panel-head">
          <div>
            <div class="panel-title">Đánh giá sách</div>
            <div class="panel-subtitle">Dữ liệu phản hồi của độc giả theo từng đầu sách</div>
          </div>
        </div>
      </template>

      <a-table
        :columns="columns"
        :data-source="groups"
        :loading="loading"
        :pagination="{ pageSize: 10 }"
        row-key="bookId"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'title'">
            <span class="font-weight-bold">{{ record.title || record.bookId || '—' }}</span>
          </template>
          <template v-if="column.key === 'rating'">
            <a-rate :value="record.averageRating" disabled allow-half />
            <span class="ml-2">{{ record.averageRating.toFixed(1) }}/5</span>
          </template>
          <template v-else-if="column.key === 'reviews'">
            <a-tag color="blue">{{ record.reviews.length }}</a-tag>
          </template>
          <template v-else-if="column.key === 'latest'">
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
    const [reviewsResponse, catalogResponse] = await Promise.all([
      fetch(`${window.location.origin}/api/circulation/books/reviews`, {
        headers: {
          'Content-Type': 'application/json',
          ...(localStorage.getItem('authToken') ? { Authorization: `Bearer ${localStorage.getItem('authToken')}` } : {})
        }
      }),
      fetch(`${window.location.origin}/api/catalog/books/products`, {
        headers: {
          'Content-Type': 'application/json',
          ...(localStorage.getItem('authToken') ? { Authorization: `Bearer ${localStorage.getItem('authToken')}` } : {})
        }
      })
    ])

    if (!reviewsResponse.ok) {
      groups.value = []
      return
    }

    const reviewsData = await reviewsResponse.json()
    const catalogData = catalogResponse.ok ? await catalogResponse.json() : []
    const catalogMap = new Map(
      Array.isArray(catalogData)
        ? catalogData.map(item => [
            String(item.id || item.Id || item.bookId || item.BookId || ''),
            item.tenSach || item.TenSach || item.title || item.Title || item.bookName || item.BookName || ''
          ]).filter(([id]) => id)
        : []
    )

    groups.value = Array.isArray(reviewsData)
      ? reviewsData.map(item => {
          const bookId = String(item.bookId || item.BookId || item.id || item.Id || '')
          return {
            bookId,
            title: item.title || item.Title || item.tenSach || item.TenSach || item.bookName || item.BookName || catalogMap.get(bookId) || '',
            averageRating: Number(item.averageRating || item.AverageRating || 0),
            reviews: Array.isArray(item.reviews || item.Reviews) ? (item.reviews || item.Reviews) : []
          }
        })
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
.page-shell {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.stats-row {
  margin-bottom: 0;
}

.mini-card,
.panel-card {
  border-radius: 18px !important;
  border: 1px solid #edf1ee !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04) !important;
}

.mini-card :deep(.ant-card-body) {
  padding: 18px 20px;
}

.panel-card :deep(.ant-card-head) {
  border-bottom: none;
  padding: 18px 20px 0;
}

.panel-card :deep(.ant-card-body) {
  padding: 18px 20px 20px;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.panel-title {
  font-size: 16px;
  font-weight: 800;
  color: #103b35;
  letter-spacing: -0.01em;
}

.panel-subtitle {
  font-size: 12px;
  color: #8c98a5;
  margin-top: 2px;
}

.ml-2 { margin-left: 8px; }
.text-secondary { color: rgba(0,0,0,0.45); font-size: 12px; }
.font-weight-bold { font-weight: 600; }
</style>
