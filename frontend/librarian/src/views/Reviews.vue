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
            <div class="panel-subtitle">Xem người đánh giá, nội dung nhận xét và xóa từng đánh giá khi cần</div>
          </div>
        </div>
      </template>
      <template #extra>
        <a-tag color="blue">{{ reviewRows.length }} lượt</a-tag>
      </template>

      <a-table
        :columns="columns"
        :data-source="reviewRows"
        :loading="loading"
        :pagination="{ pageSize: 10, showSizeChanger: false }"
        row-key="rowKey"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'book'">
            <div class="book-cell">
              <div class="font-weight-bold">{{ record.title || record.bookId || '—' }}</div>
              <div class="text-secondary">Book ID: {{ record.bookId }}</div>
            </div>
          </template>

          <template v-else-if="column.key === 'reviewer'">
            <div class="reviewer-cell">
              <a-avatar class="reviewer-avatar" :size="32">
                {{ initials(record.reviewerName) }}
              </a-avatar>
              <div class="reviewer-meta">
                <div class="font-weight-bold">{{ record.reviewerName }}</div>
                <div class="text-secondary">{{ record.reviewerDetail }}</div>
              </div>
            </div>
          </template>

          <template v-else-if="column.key === 'rating'">
            <div class="rating-cell">
              <a-rate :value="record.rating" disabled allow-half />
              <span class="rating-text">{{ record.rating.toFixed(1) }}/5</span>
            </div>
          </template>

          <template v-else-if="column.key === 'comment'">
            <div class="comment-cell">
              {{ record.comment || 'Không có nhận xét' }}
            </div>
          </template>

          <template v-else-if="column.key === 'createdAt'">
            <span class="text-secondary">{{ fmtDate(record.createdAt) }}</span>
          </template>

          <template v-else-if="column.key === 'actions'">
            <a-popconfirm
              title="Xóa đánh giá này?"
              :description="`Đánh giá của ${record.reviewerName} sẽ bị ẩn khỏi hệ thống.`"
              ok-text="Xóa"
              cancel-text="Hủy"
              placement="left"
              @confirm="deleteReview(record)"
            >
              <a-button
                danger
                type="text"
                size="small"
                :disabled="!record.reviewKey"
                :loading="deletingKey === record.reviewKey"
              >
                Xóa
              </a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'

const loading = ref(false)
const groups = ref([])
const deletingKey = ref('')

const columns = [
  { title: 'Mã sách', dataIndex: 'bookId', key: 'book', width: 220 },
  { title: 'Người đánh giá', dataIndex: 'reviewerName', key: 'reviewer', width: 220 },
  { title: 'Điểm', dataIndex: 'rating', key: 'rating', width: 180 },
  { title: 'Nhận xét', dataIndex: 'comment', key: 'comment' },
  { title: 'Ngày tạo', dataIndex: 'createdAt', key: 'createdAt', width: 160 },
  { title: 'Hành động', key: 'actions', width: 110 }
]

const totalReviews = computed(() => groups.value.reduce((sum, item) => sum + (item.reviews?.length || 0), 0))
const avgRating = computed(() => {
  const all = groups.value
    .flatMap(item => item.reviews || [])
    .map(review => Number(review.rating ?? review.Rating ?? 0))
    .filter(value => Number.isFinite(value))

  if (!all.length) return 0
  return all.reduce((sum, value) => sum + value, 0) / all.length
})

const reviewRows = computed(() => {
  const rows = groups.value.flatMap(group => {
    const bookId = String(group.bookId || group.BookId || '').trim()
    const title = group.title || group.Title || bookId || '—'
    const reviews = Array.isArray(group.reviews || group.Reviews) ? (group.reviews || group.Reviews) : []

    return reviews.map(review => {
      const reviewId = String(review.reviewId || review.ReviewId || '').trim()
      const transactionId = String(review.transactionId || review.TransactionId || '').trim()
      const id = String(review.id || review.Id || '').trim()
      const createdAt = review.createdAt || review.CreatedAt || ''
      const reviewerName = review.fullName || review.FullName || review.username || review.Username || review.cardNumber || review.CardNumber || 'Độc giả'
      const reviewerDetail = [
        review.username || review.Username,
        review.cardNumber || review.CardNumber
      ].filter(Boolean).join(' • ') || 'Chưa có mã thẻ'

      return {
        rowKey: buildReviewKey(review, bookId),
        bookId,
        title,
        reviewerName,
        reviewerDetail,
        rating: Number(review.rating ?? review.Rating ?? 0),
        comment: review.comment || review.Comment || '',
        createdAt,
        reviewKey: buildReviewKey(review, bookId),
        reviewId: review.reviewId || review.ReviewId || '',
        transactionId: review.transactionId || review.TransactionId || '',
        id: review.id || review.Id || '',
        userId: review.userId || review.UserId || '',
        cardNumber: review.cardNumber || review.CardNumber || '',
        username: review.username || review.Username || '',
        fullName: review.fullName || review.FullName || ''
      }
    })
  })

  return rows.sort((a, b) => new Date(b.createdAt || 0).getTime() - new Date(a.createdAt || 0).getTime())
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
        ? catalogData
            .map(item => [
              String(item.id || item.Id || item.bookId || item.BookId || '').trim(),
              item.tenSach || item.TenSach || item.title || item.Title || item.bookName || item.BookName || ''
            ])
            .filter(([id]) => id)
        : []
    )

    groups.value = Array.isArray(reviewsData)
      ? reviewsData.map(item => {
          const bookId = String(item.bookId || item.BookId || item.id || item.Id || '').trim()
          const title = item.title || item.Title || item.tenSach || item.TenSach || item.bookName || item.BookName || catalogMap.get(bookId) || ''
          const reviews = Array.isArray(item.reviews || item.Reviews) ? (item.reviews || item.Reviews) : []

          return {
            bookId,
            title,
            averageRating: Number(item.averageRating || item.AverageRating || 0),
            reviews
          }
        })
      : []
  } catch {
    groups.value = []
  } finally {
    loading.value = false
  }
}

async function deleteReview(record) {
  const reviewKey = record.reviewKey
  if (!reviewKey) {
    message.error('Không tìm thấy mã đánh giá để xóa.')
    return
  }

  deletingKey.value = reviewKey
  try {
    const response = await fetch(
      `${window.location.origin}/api/circulation/books/${encodeURIComponent(record.bookId)}/reviews/${encodeURIComponent(reviewKey)}`,
      {
        method: 'DELETE',
        headers: {
          'Content-Type': 'application/json',
          ...(localStorage.getItem('authToken') ? { Authorization: `Bearer ${localStorage.getItem('authToken')}` } : {})
        },
        body: JSON.stringify({
          BookId: record.bookId,
          Id: record.id || undefined,
          ReviewId: record.reviewId || undefined,
          TransactionId: record.transactionId || undefined,
          UserId: record.userId || undefined,
          CardNumber: record.cardNumber || undefined,
          Username: record.username || undefined,
          FullName: record.fullName || undefined,
          Rating: record.rating,
          Comment: record.comment || '',
          CreatedAt: normalizeReviewCreatedAt(record.createdAt) || record.createdAt || undefined,
          ReviewKey: reviewKey
        })
      }
    )

    const payload = await response.json().catch(() => null)
    if (!response.ok) {
      message.error(payload?.message || payload?.Message || 'Không xóa được đánh giá.')
      return
    }

    message.success('Đã xóa đánh giá.')
    await load()
  } catch {
    message.error('Không kết nối được máy chủ. Vui lòng thử lại.')
  } finally {
    deletingKey.value = ''
  }
}

function initials(value) {
  const text = String(value || 'ĐG').trim()
  const chars = text
    .split(/\s+/)
    .map(part => part[0])
    .filter(Boolean)
    .join('')
    .slice(0, 2)
  return (chars || 'DG').toUpperCase()
}

function buildReviewKey(review = {}, bookId = '') {
  const transactionId = String(review.transactionId || review.TransactionId || '').trim()
  if (transactionId) return transactionId

  const reviewId = String(review.reviewId || review.ReviewId || '').trim()
  if (reviewId) return reviewId

  const id = String(review.id || review.Id || '').trim()
  if (id) return id

  const reviewerName = review.fullName || review.FullName || review.username || review.Username || review.cardNumber || review.CardNumber || 'Độc giả'
  const createdAt = review.createdAt || review.CreatedAt || ''
  return [
    String(bookId || '').trim(),
    String(review.userId || review.UserId || '').trim(),
    String(review.cardNumber || review.CardNumber || '').trim(),
    String(review.username || review.Username || '').trim(),
    String(reviewerName || '').trim(),
    String(review.rating ?? review.Rating ?? '').trim(),
    String(review.comment || review.Comment || '').trim(),
    normalizeReviewCreatedAt(createdAt)
  ].join('|')
}

function normalizeReviewCreatedAt(value) {
  if (!value) return ''

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''

  return date.toISOString().replace(/\.\d{3}Z$/, 'Z')
}

function fmtDate(value) {
  return value ? dayjs(value).format('DD/MM/YYYY HH:mm') : '—'
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

.book-cell,
.reviewer-cell,
.rating-cell {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.reviewer-cell {
  flex-direction: row;
  align-items: center;
  gap: 10px;
}

.reviewer-avatar {
  background: linear-gradient(135deg, #0f766e, #115e59);
  color: #fff;
  font-weight: 800;
  flex-shrink: 0;
}

.reviewer-meta {
  min-width: 0;
}

.rating-cell {
  align-items: flex-start;
}

.rating-text {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.65);
}

.comment-cell {
  color: #334155;
  line-height: 1.45;
}

.text-secondary {
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}

.font-weight-bold {
  font-weight: 600;
}
</style>
