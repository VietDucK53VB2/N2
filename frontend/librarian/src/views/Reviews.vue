<template>
  <div class="reviews-page">
    <a-row :gutter="[16, 16]" class="mb-4">
      <a-col :xs="24" :md="8">
        <a-card>
          <a-statistic title="Tổng đánh giá" :value="store.totalReviewCount" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card>
          <a-statistic title="Điểm trung bình" :value="store.averageReviewRating" :precision="1" suffix="/ 5" />
          <a-rate class="mt-2" :value="store.averageReviewRating" disabled allow-half />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card>
          <a-statistic title="Sách có đánh giá" :value="store.reviews.length" />
        </a-card>
      </a-col>
    </a-row>

    <a-card title="Danh sách đánh giá sách">
      <template #extra>
        <a-space>
          <a-input-search
            v-model:value="keyword"
            placeholder="Tìm theo sách, người dùng, mã thẻ..."
            allow-clear
            style="width: 300px"
          />
          <a-button :loading="store.loading" @click="reload">Làm mới</a-button>
        </a-space>
      </template>

      <a-table
        :columns="columns"
        :data-source="filteredReviews"
        :loading="store.loading"
        :pagination="{ pageSize: 10 }"
        row-key="reviewId"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'book'">
            <div>
              <div class="font-medium">{{ record.bookTitle }}</div>
              <div class="muted">
                <span v-if="record.bookAuthor">{{ record.bookAuthor }} · </span>
                Mã sách: {{ record.bookId }}
              </div>
            </div>
          </template>

          <template v-else-if="column.key === 'reader'">
            <div>
              <div class="font-medium">{{ record.readerName }}</div>
              <div class="muted">{{ record.cardNumber || record.userId || '—' }}</div>
            </div>
          </template>

          <template v-else-if="column.key === 'rating'">
            <a-rate :value="record.rating" disabled />
            <span class="rating-number">{{ record.rating }}/5</span>
          </template>

          <template v-else-if="column.key === 'comment'">
            <span v-if="record.comment">{{ record.comment }}</span>
            <span v-else class="muted">Không có bình luận</span>
          </template>

          <template v-else-if="column.key === 'createdAt'">
            {{ fmtDate(record.createdAt) }}
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import dayjs from 'dayjs'
import { useLibrarianStore } from '@/stores/librarian'

const store = useLibrarianStore()
const keyword = ref('')

const columns = [
  { title: 'Sách', key: 'book', width: 280 },
  { title: 'Người đánh giá', key: 'reader', width: 200 },
  { title: 'Số sao', key: 'rating', width: 180 },
  { title: 'Bình luận', key: 'comment' },
  { title: 'Thời gian', key: 'createdAt', width: 170 }
]

const filteredReviews = computed(() => {
  const q = keyword.value.trim().toLowerCase()
  if (!q) return store.reviewRows
  return store.reviewRows.filter(review => [
    review.bookTitle,
    review.bookAuthor,
    review.bookId,
    review.readerName,
    review.cardNumber,
    review.comment
  ].some(value => String(value || '').toLowerCase().includes(q)))
})

function fmtDate(date) {
  return date ? dayjs(date).format('DD/MM/YYYY HH:mm:ss') : '—'
}

async function reload() {
  await store.loadReviews()
}

onMounted(async () => {
  if (!store.books.length) await store.loadBooks()
  if (!store.transactions.length) await store.loadTransactions()
  await store.loadReviews()
})
</script>

<style scoped>
.mb-4 { margin-bottom: 16px; }
.mt-2 { margin-top: 8px; }
.font-medium { font-weight: 600; }
.muted {
  color: #94a3b8;
  font-size: 12px;
}
.rating-number {
  margin-left: 8px;
  color: #64748b;
  font-weight: 600;
}
</style>
