<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <div>
        <h2 class="text-h5 font-weight-black">Sách của tôi</h2>
        <p class="text-body-2 text-grey">Quản lý yêu cầu mượn, sách đang mượn và trả sách</p>
      </div>
    </div>

    <v-chip-group v-model="filter" mandatory selected-class="text-white bg-primary" class="mb-6">
      <v-chip value="all" variant="outlined" filter>Tất cả</v-chip>
      <v-chip value="pending" variant="outlined" filter>Chờ duyệt</v-chip>
      <v-chip value="returnPending" variant="outlined" filter>Chờ trả</v-chip>
      <v-chip value="soon" variant="outlined" filter>Sắp hết hạn</v-chip>
      <v-chip value="overdue" variant="outlined" filter>Quá hạn</v-chip>
    </v-chip-group>

    <v-row v-if="filteredBooks.length">
      <v-col v-for="tx in filteredBooks" :key="tx.Id || tx.id" cols="12" sm="6" lg="4" xl="3">
        <v-card class="book-item" hover>
          <div class="book-cover" :style="{ backgroundColor: titleColor(tx.TenSach || tx.tenSach) }">
            <v-img v-if="tx.ImageUrl || tx.imageUrl" :src="tx.ImageUrl || tx.imageUrl" cover height="100%" />
            <v-icon v-else size="48" color="white" style="opacity:0.3">mdi-book-open-variant</v-icon>
            <div class="cover-gradient"></div>
            <div class="cover-info">
              <p class="text-body-2 font-weight-bold text-white">{{ tx.TenSach || tx.tenSach || '—' }}</p>
              <p class="text-caption text-white-50">{{ tx.TacGia || tx.tacGia || '—' }}</p>
            </div>
            <v-chip class="status-chip" size="x-small" :color="statusColor(tx)" variant="flat">
              <v-icon start size="10">{{ statusIcon(tx) }}</v-icon>
              {{ statusText(tx) }}
            </v-chip>
          </div>

          <v-card-text class="pa-4">
            <v-row dense class="mb-3">
              <v-col cols="6">
                <div class="date-box">
                  <span class="date-label">Ngày mượn</span>
                  <span class="date-value">{{ formatDate(tx.BorrowedAt || tx.borrowedAt) }}</span>
                </div>
              </v-col>
              <v-col cols="6">
                <div class="date-box">
                  <span class="date-label">Hạn trả</span>
                  <span class="date-value" :class="{ 'text-error': store.isOverdue(tx) }">
                    {{ formatDate(tx.DueAt || tx.dueAt) }}
                  </span>
                </div>
              </v-col>
            </v-row>

            <v-progress-linear
              :model-value="progressPercent(tx)"
              :color="progressColor(tx)"
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
              @click="openReturn(tx)"
            >
              {{ returnButtonText(tx) }}
            </v-btn>
            <v-btn
              block
              class="mt-2"
              variant="text"
              color="amber-darken-2"
              prepend-icon="mdi-star"
              :disabled="hasReviewed(tx)"
              @click="openReview(tx)"
            >
              {{ hasReviewed(tx) ? 'Đã đánh giá' : 'Đánh giá' }}
            </v-btn>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-card v-else flat color="transparent" class="text-center pa-12">
      <v-icon size="72" color="grey-lighten-2" class="mb-4">mdi-book-open-variant</v-icon>
      <p class="text-body-1 text-grey mb-4">Không có sách nào</p>
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
    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="3000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAppStore } from '@/stores/app'
import {
  getReaderCard,
  returnBook as apiReturn,
  returnTransaction,
  reviewBook as apiReviewBook,
  fetchBookReviews
} from '@/utils/api'
import { titleColor, formatDate, daysLeft } from '@/utils/helpers'

const store = useAppStore()
const filter = ref('all')
const returnDialog = ref(false)
const returnBookData = ref(null)
const returning = ref(false)
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
const reviewedBookIds = ref(new Set())

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
  const cardNumber = store.cardNumberOf(tx) || getReaderCard()
  if (!bookId || !cardNumber) {
    reviewError.value = 'Thiếu mã sách hoặc mã thẻ, không thể đánh giá.'
    return
  }
  reviewing.value = true
  reviewError.value = ''
  try {
    const r = await apiReviewBook(bookId, {
      cardNumber,
      rating: Math.max(0, Math.min(5, Number(reviewRating.value) || 0)),
      comment: reviewComment.value
    })
    if (r.ok) {
      reviewDialog.value = false
      reviewedBookIds.value = new Set([...reviewedBookIds.value, String(bookId)])
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


async function loadData() {
  await store.loadMyTransactions()
  await loadMyReviews()
}

async function loadMyReviews() {
  const cardNumber = getReaderCard()
  if (!cardNumber) {
    reviewedBookIds.value = new Set()
    return
  }

  const groups = await fetchBookReviews()
  const ids = new Set()
  for (const group of groups || []) {
    const bookId = group.bookId || group.BookId
    const reviews = group.reviews || group.Reviews || []
    const alreadyReviewed = reviews.some(review =>
      String(review.cardNumber || review.CardNumber || '').toLowerCase() === String(cardNumber).toLowerCase()
    )
    if (bookId && alreadyReviewed) ids.add(String(bookId))
  }
  reviewedBookIds.value = ids
}

function hasReviewed(tx) {
  const bookId = store.bookIdOf(tx)
  return bookId ? reviewedBookIds.value.has(String(bookId)) : false
}

onMounted(loadData)
</script>

<style scoped lang="scss">
.book-item {
  overflow: hidden;
  transition: transform 0.25s, box-shadow 0.25s;
  &:hover {
    transform: translateY(-4px);
  }
}
.book-cover {
  position: relative;
  height: 200px;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}
.cover-gradient {
  position: absolute;
  inset: 0;
  background: linear-gradient(to top, rgba(0,0,0,0.75), transparent 60%);
  z-index: 1;
}
.cover-info {
  position: absolute;
  bottom: 12px;
  left: 12px;
  right: 12px;
  z-index: 2;
}
.status-chip {
  position: absolute;
  top: 10px;
  right: 10px;
  z-index: 2;
}
.date-box {
  background: #faf7f2;
  border-radius: 10px;
  padding: 8px 10px;
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
  font-size: 12px;
  font-weight: 600;
}
.text-white-50 {
  color: rgba(255,255,255,0.6) !important;
}
</style>

