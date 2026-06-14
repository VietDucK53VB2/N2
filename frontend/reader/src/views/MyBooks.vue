<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <div>
        <h2 class="text-h5 font-weight-black">Sách của tôi</h2>
        <p class="text-body-2 text-grey">Quản lý yêu cầu mượn, sách đang mượn và trả sách</p>
      </div>
      <v-btn class="btn-gradient" size="large" prepend-icon="mdi-plus" @click="borrowDialog = true">
        Mượn sách mới
      </v-btn>
    </div>

    <v-chip-group v-model="filter" mandatory selected-class="text-white bg-primary" class="mb-6">
      <v-chip value="all" variant="outlined" filter>Tất cả</v-chip>
      <v-chip value="pending" variant="outlined" filter>Chờ duyệt</v-chip>
      <v-chip value="returnPending" variant="outlined" filter>Chờ trả</v-chip>
      <v-chip value="soon" variant="outlined" filter>Sắp hết hạn</v-chip>
      <v-chip value="overdue" variant="outlined" filter>Quá hạn</v-chip>
    </v-chip-group>

    <v-row v-if="filteredBooks.length">
      <v-col v-for="tx in filteredBooks" :key="tx.Id || tx.id" cols="6" sm="4" md="3" lg="2" xl="2" class="d-flex">
        <v-card class="book-item" hover @click="openDetail(tx)">
          <div class="book-cover" :style="{ backgroundColor: titleColor(tx.TenSach || tx.tenSach) }">
            <v-img v-if="tx.ImageUrl || tx.imageUrl" :src="tx.ImageUrl || tx.imageUrl" cover class="cover-img" />
            <v-icon v-else size="48" color="white" style="opacity:0.3">mdi-book-open-variant</v-icon>
            <div class="cover-gradient"></div>
            <div class="cover-info">
              <p class="text-body-2 font-weight-bold text-white">{{ tx.TenSach || tx.tenSach || '-' }}</p>
              <p class="text-caption text-white-50">{{ tx.TacGia || tx.tacGia || '-' }}</p>
            </div>
            <v-chip class="status-chip" size="x-small" :color="statusColor(tx)" variant="flat">
              <v-icon start size="10">{{ statusIcon(tx) }}</v-icon>
              {{ statusText(tx) }}
            </v-chip>
          </div>

          <v-card-text class="pa-3">
            <v-row dense class="mb-3">
              <v-col cols="6">
                <div class="date-box">
                  <span class="date-label">Ngày mượn</span>
                  <span class="date-value">{{ formatDateTime(tx.BorrowedAt || tx.borrowedAt) }}</span>
                </div>
              </v-col>
              <v-col cols="6">
                <div class="date-box">
                  <span class="date-label">Hạn trả</span>
                  <span class="date-value" :class="{ 'text-error': store.isOverdue(tx) }">
                    {{ formatDateTime(tx.DueAt || tx.dueAt) }}
                  </span>
                </div>
              </v-col>
            </v-row>

            <div v-if="isLoanClockActive(tx)" class="time-summary mb-3">
              <span>Đã mượn: {{ borrowedDuration(tx) }}</span>
              <span :class="{ 'text-error': store.isOverdue(tx) }">Còn lại: {{ dueCountdown(tx) }}</span>
            </div>
            <div v-else class="time-summary pending mb-3">
              <span>Yêu cầu gửi lúc: {{ formatDateTime(tx.BorrowedAt || tx.borrowedAt) }}</span>
              <span>Chưa bắt đầu tính thời gian mượn</span>
            </div>

            <v-progress-linear
              v-if="isLoanClockActive(tx)"
              :model-value="progressPercent(tx)"
              :color="progressColor(tx)"
              rounded
              height="6"
              class="mb-4"
            />
            <v-progress-linear
              v-else
              model-value="8"
              color="warning"
              rounded
              height="6"
              class="mb-4"
            />

            <v-btn
              block
              variant="tonal"
              color="success"
              prepend-icon="mdi-undo"
              :disabled="store.isPending(tx) || store.isReturnPending(tx)"
              @click.stop="openReturn(tx)"
            >
              {{ returnButtonText(tx) }}
            </v-btn>
            <v-btn
              block
              class="mt-2"
              variant="text"
              color="amber-darken-2"
              prepend-icon="mdi-star"
              :disabled="!canReview(tx)"
              @click.stop="openReview(tx)"
            >
              {{ reviewButtonText(tx) }}
            </v-btn>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-card v-else flat color="transparent" class="text-center pa-12">
      <v-icon size="72" color="grey-lighten-2" class="mb-4">mdi-book-open-variant</v-icon>
      <p class="text-body-1 text-grey mb-4">Không có sách nào</p>
      <v-btn class="btn-gradient" prepend-icon="mdi-plus" @click="borrowDialog = true">Mượn sách mới</v-btn>
    </v-card>

    <v-dialog v-model="returnDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title class="text-h6 font-weight-bold">Gửi yêu cầu trả sách</v-card-title>
        <v-card-text>
          <v-alert v-if="returnError" type="error" variant="tonal" density="compact" class="mb-3">
            {{ returnError }}
          </v-alert>
          <v-alert type="info" variant="tonal" class="mb-0">
            Yêu cầu trả sách <strong>{{ returnBook?.TenSach || returnBook?.tenSach }}</strong>
            sẽ được chuyển cho thủ thư kiểm tra tình trạng sách.
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="returnDialog = false">Hủy</v-btn>
          <v-btn color="success" :loading="returning" @click="submitReturn">Gửi yêu cầu</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="reviewDialog" max-width="460">
      <v-card rounded="xl">
        <v-card-title class="text-h6 font-weight-bold">Đánh giá sách</v-card-title>
        <v-card-text>
          <p class="text-body-2 mb-3">{{ reviewBookData?.TenSach || reviewBookData?.tenSach || 'Sách' }}</p>
          <v-rating
            v-model="reviewRating"
            color="amber"
            active-color="amber"
            hover
            length="5"
            size="36"
            class="mb-3"
          />
          <v-textarea v-model="reviewComment" label="Nhận xét" rows="3" auto-grow hide-details />
          <v-alert v-if="reviewError" type="error" variant="tonal" density="compact" class="mt-3">
            {{ reviewError }}
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="reviewDialog = false">Hủy</v-btn>
          <v-btn color="amber-darken-2" :loading="reviewing" @click="submitReview">Gửi đánh giá</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="detailDialog" max-width="520">
      <v-card rounded="xl">
        <v-card-title class="d-flex align-center ga-2 text-h6 font-weight-bold">
          <v-btn icon="mdi-arrow-left" variant="text" size="small" @click="detailDialog = false" />
          Chi tiết phiếu mượn
        </v-card-title>
        <v-card-text v-if="detailTx">
          <v-list density="compact" lines="two">
            <v-list-item title="Sách" :subtitle="`${detailTx.TenSach || detailTx.tenSach || '-'} - ${detailTx.TacGia || detailTx.tacGia || '-'}`" />
            <v-list-item title="Mã sách" :subtitle="String(store.bookIdOf(detailTx) || '-')" />
            <v-list-item title="Trạng thái" :subtitle="statusText(detailTx)" />
            <v-list-item title="Ngày mượn" :subtitle="formatDateTime(detailTx.BorrowedAt || detailTx.borrowedAt)" />
            <v-list-item title="Hạn trả" :subtitle="formatDateTime(detailTx.DueAt || detailTx.dueAt)" />
            <template v-if="isLoanClockActive(detailTx)">
              <v-list-item title="Thời gian đã mượn" :subtitle="borrowedDuration(detailTx)" />
              <v-list-item title="Thời gian còn lại" :subtitle="dueCountdown(detailTx)" />
            </template>
            <template v-else>
              <v-list-item title="Thời gian mượn" subtitle="Chưa bắt đầu vì phiếu mượn chưa được duyệt" />
            </template>
          </v-list>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="detailDialog = false">Trở ra</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <BorrowDialog v-model="borrowDialog" @success="handleBorrowSuccess" />
    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="3000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useAppStore } from '@/stores/app'
import {
  getReaderCard,
  returnBook as apiReturn,
  returnTransaction,
  reviewBook as apiReviewBook,
  fetchBookReviews
} from '@/utils/api'
import { titleColor, formatDateTime, daysLeft, durationSince, timeUntil } from '@/utils/helpers'
import BorrowDialog from '@/components/BorrowDialog.vue'

const store = useAppStore()
const filter = ref('all')
const returnDialog = ref(false)
const returnBookData = ref(null)
const returning = ref(false)
const borrowDialog = ref(false)
const returnError = ref('')
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const reviewDialog = ref(false)
const reviewBookData = ref(null)
const reviewRating = ref(5)
const reviewComment = ref('')
const reviewError = ref('')
const reviewing = ref(false)
const reviewedTransactionIds = ref(new Set())
const detailDialog = ref(false)
const detailTx = ref(null)
const now = ref(Date.now())
let clockTimer = null

const visibleTransactions = computed(() => [...store.pendingTransactions, ...store.activeTransactions])
const filteredBooks = computed(() => {
  const items = visibleTransactions.value
  if (filter.value === 'pending') return items.filter(store.isPending)
  if (filter.value === 'returnPending') return items.filter(store.isReturnPending)
  if (filter.value === 'overdue') return items.filter(store.isOverdue)
  if (filter.value === 'soon') {
    return items.filter(t => {
      if (store.isPending(t) || store.isReturnPending(t) || store.isOverdue(t)) return false
      const d = daysLeft(t.DueAt || t.dueAt)
      return d !== null && d <= 3
    })
  }
  return items
})

const returnBook = computed(() => returnBookData.value)

function statusColor(tx) {
  if (store.isPending(tx)) return 'warning'
  if (store.isReturnPending(tx)) return 'deep-purple'
  if (store.isOverdue(tx)) return 'error'
  const d = daysLeft(tx.DueAt || tx.dueAt)
  return d !== null && d <= 3 ? 'warning' : 'info'
}

function statusIcon(tx) {
  if (store.isPending(tx)) return 'mdi-clock-outline'
  if (store.isReturnPending(tx)) return 'mdi-book-clock'
  if (store.isOverdue(tx)) return 'mdi-alert'
  return 'mdi-calendar'
}

function statusText(tx) {
  if (store.isPending(tx)) return 'Chờ duyệt mượn'
  if (store.isReturnPending(tx)) return 'Chờ xác nhận trả'
  if (store.isOverdue(tx)) return 'Quá hạn'
  const d = daysLeft(tx.DueAt || tx.dueAt)
  return d !== null ? `Còn ${d} ngày` : 'Đang mượn'
}

function returnButtonText(tx) {
  if (store.isPending(tx)) return 'Chờ duyệt mượn'
  if (store.isReturnPending(tx)) return 'Chờ thủ thư xác nhận'
  return 'Trả sách'
}

function progressPercent(tx) {
  if (store.isPending(tx)) return 8
  if (store.isReturnPending(tx)) return 100
  const d = daysLeft(tx.DueAt || tx.dueAt)
  if (d === null) return 50
  return Math.min(100, Math.max(0, (1 - d / 14) * 100))
}

function progressColor(tx) {
  if (store.isPending(tx)) return 'warning'
  if (store.isReturnPending(tx)) return 'deep-purple'
  if (store.isOverdue(tx)) return 'error'
  const d = daysLeft(tx.DueAt || tx.dueAt)
  return d !== null && d <= 3 ? 'warning' : 'primary'
}

function openReturn(tx) {
  returnError.value = ''
  returnBookData.value = tx
  returnDialog.value = true
}

function openDetail(tx) {
  detailTx.value = tx
  detailDialog.value = true
}

function isLoanClockActive(tx) {
  return !!tx && (store.isBorrowed(tx) || store.isOverdue(tx) || store.isReturnPending(tx))
}

function borrowedDuration(tx) {
  now.value
  const start = tx?.BorrowedAt || tx?.borrowedAt
  const returnedAt = tx?.ReturnedAt || tx?.returnedAt
  if (store.isReturned(tx) && returnedAt) return durationSince(start, new Date(returnedAt).getTime())
  if (!isLoanClockActive(tx)) return 'Chưa bắt đầu'
  return durationSince(start)
}

function dueCountdown(tx) {
  now.value
  if (store.isReturned(tx)) return 'Đã hoàn tất'
  if (!isLoanClockActive(tx)) return 'Chưa bắt đầu'
  return timeUntil(tx?.DueAt || tx?.dueAt)
}

async function submitReturn() {
  const tx = returnBookData.value
  if (!tx) return
  const cardNumber = store.cardNumberOf(tx) || getReaderCard()
  const bookId = store.bookIdOf(tx)
  if (!cardNumber || !bookId) {
    returnError.value = 'Thiếu mã thẻ hoặc mã sách, không thể trả sách.'
    return
  }
  returnError.value = ''
  returning.value = true
  try {
    const transactionId = tx.Id || tx.id
    const r = transactionId ? await returnTransaction(transactionId) : await apiReturn(cardNumber, bookId)
    if (r.ok) {
      returnDialog.value = false
      snackbarText.value = 'Đã gửi yêu cầu trả sách. Vui lòng chờ thủ thư xác nhận.'
      snackbarColor.value = 'success'
      snackbar.value = true
      await loadData()
    } else {
      const data = await r.json().catch(() => null)
      returnError.value = data?.message || data?.Message || 'Không gửi được yêu cầu trả sách.'
    }
  } catch {
    returnError.value = 'Không kết nối được máy chủ. Vui lòng thử lại.'
  } finally {
    returning.value = false
  }
}

function openReview(tx) {
  if (store.isPending(tx)) {
    snackbarText.value = 'Bạn cần chờ thủ thư duyệt mượn trước khi đánh giá sách.'
    snackbarColor.value = 'info'
    snackbar.value = true
    return
  }
  if (hasReviewed(tx)) {
    snackbarText.value = 'Bạn đã đánh giá cuốn sách này rồi.'
    snackbarColor.value = 'info'
    snackbar.value = true
    return
  }
  reviewBookData.value = tx
  reviewRating.value = 5
  reviewComment.value = ''
  reviewError.value = ''
  reviewDialog.value = true
}

async function submitReview() {
  const tx = reviewBookData.value
  const bookId = store.bookIdOf(tx)
  const transactionId = tx?.Id || tx?.id
  const cardNumber = store.cardNumberOf(tx) || getReaderCard()
  if (!bookId || !transactionId || !cardNumber) {
    reviewError.value = 'Thiếu mã sách hoặc mã thẻ, không thể đánh giá.'
    return
  }
  reviewing.value = true
  reviewError.value = ''
  try {
    const r = await apiReviewBook(bookId, {
      transactionId,
      cardNumber,
      rating: Math.max(0, Math.min(5, Number(reviewRating.value) || 0)),
      comment: reviewComment.value
    })
    if (r.ok) {
      reviewDialog.value = false
      reviewedTransactionIds.value = new Set([...reviewedTransactionIds.value, String(transactionId)])
      snackbarText.value = 'Đã gửi đánh giá.'
      snackbarColor.value = 'success'
      snackbar.value = true
    } else {
      const data = await r.json().catch(() => null)
      reviewError.value = data?.message || data?.Message || 'Không gửi được đánh giá.'
    }
  } catch {
    reviewError.value = 'Không kết nối được máy chủ. Vui lòng thử lại.'
  } finally {
    reviewing.value = false
  }
}

async function handleBorrowSuccess() {
  snackbarText.value = 'Đã gửi yêu cầu mượn sách. Vui lòng chờ thủ thư duyệt.'
  snackbarColor.value = 'success'
  snackbar.value = true
  await loadData()
}

async function loadData() {
  await store.loadUserInfo()
  if (!store.books.length) {
    await store.loadBooks()
  }
  await store.loadMyTransactions()
  await loadMyReviews()
}

async function loadMyReviews() {
  const cardNumber = getReaderCard()
  if (!cardNumber) {
    reviewedTransactionIds.value = new Set()
    return
  }

  const groups = await fetchBookReviews()
  const ids = new Set()
  for (const group of groups || []) {
    const reviews = group.reviews || group.Reviews || []
    for (const review of reviews) {
      const reviewCard = review.cardNumber || review.CardNumber || ''
      const transactionId = review.transactionId || review.TransactionId
      if (transactionId && String(reviewCard).toLowerCase() === String(cardNumber).toLowerCase()) {
        ids.add(String(transactionId))
      }
    }
  }
  reviewedTransactionIds.value = ids
}

function hasReviewed(tx) {
  const transactionId = tx?.Id || tx?.id
  return transactionId ? reviewedTransactionIds.value.has(String(transactionId)) : false
}

function canReview(tx) {
  return !store.isPending(tx) && !hasReviewed(tx)
}

function reviewButtonText(tx) {
  if (store.isPending(tx)) return 'Chờ duyệt mới đánh giá'
  if (hasReviewed(tx)) return 'Đã đánh giá'
  return 'Đánh giá'
}

onMounted(() => {
  clockTimer = window.setInterval(() => { now.value = Date.now() }, 1000)
  loadData()
})

onBeforeUnmount(() => {
  if (clockTimer) window.clearInterval(clockTimer)
})
</script>

<style scoped lang="scss">
.book-item {
  width: 100%;
  height: 100%;
  display: flex !important;
  flex-direction: column;
  overflow: hidden;
  border-radius: 12px !important;
  border: none !important;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08) !important;
  transition: transform 0.25s, box-shadow 0.25s;
  &:hover {
    transform: translateY(-6px);
    box-shadow: 0 16px 40px rgba(0, 0, 0, 0.15) !important;
    .cover-img { transform: scale(1.05); }
  }
}
.book-cover {
  position: relative;
  width: 100%;
  aspect-ratio: 2 / 3;
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}
.cover-img {
  position: absolute !important;
  inset: 0;
  width: 100% !important;
  height: 100% !important;
  object-fit: cover;
  transition: transform 0.35s ease !important;
}
:deep(.book-cover .v-img) {
  position: absolute !important;
  inset: 0;
  width: 100% !important;
  height: 100% !important;
}
:deep(.book-cover .v-img__img) {
  object-fit: cover !important;
}
.cover-gradient {
  position: absolute;
  inset: 0;
  background: linear-gradient(to top, rgba(0,0,0,0.75), transparent 60%);
  z-index: 1;
}
.cover-info {
  position: absolute;
  bottom: 10px;
  left: 10px;
  right: 10px;
  z-index: 2;
}
.status-chip {
  position: absolute;
  top: 6px;
  right: 6px;
  z-index: 2;
}
.date-box {
  background: #faf7f2;
  border-radius: 8px;
  padding: 7px 8px;
}
.date-label {
  display: block;
  font-size: 10px;
  font-weight: 700;
  color: #777;
  letter-spacing: 0;
  margin-bottom: 3px;
}
.date-value {
  font-size: 11px;
  font-weight: 600;
  line-height: 1.35;
  word-break: break-word;
}
.book-item :deep(.v-card-text) {
  flex: 1 1 auto;
  display: flex;
  flex-direction: column;
}
.time-summary {
  display: grid;
  gap: 4px;
  font-size: 10.5px;
  color: #64748b;
  font-weight: 600;
}
.time-summary.pending {
  color: #b45309;
}
.text-white-50 {
  color: rgba(255,255,255,0.6) !important;
}
</style>

