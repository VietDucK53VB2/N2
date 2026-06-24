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
      <v-col v-for="tx in filteredBooks" :key="tx.Id || tx.id" cols="6" sm="4" md="3" lg="2">
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

            <div class="timeline-box mb-3">
              <div class="timeline-line">
                <span class="timeline-label">Đã mượn từ:</span>
                <span class="timeline-value" :class="{ 'text-warning': store.isPending(tx), 'text-error': store.isOverdue(tx) }">
                  {{ loanTimeText(tx) }}
                </span>
              </div>
              <div v-if="timeRemainderText(tx)" class="timeline-line">
                <span class="timeline-label">Còn lại:</span>
                <span class="timeline-value" :class="{ 'text-warning': !store.isOverdue(tx), 'text-error': store.isOverdue(tx) }">
                  {{ timeRemainderText(tx) }}
                </span>
              </div>
            </div>

            <v-progress-linear
              :model-value="progressPercent(tx)"
              :color="progressColor(tx)"
              rounded
              height="6"
              class="mb-4"
            />

            <v-btn
              v-if="store.isPending(tx)"
              block
              variant="tonal"
              color="error"
              prepend-icon="mdi-close-circle-outline"
              @click="openCancelBorrow(tx)"
            >
              Hủy mượn
            </v-btn>
            <v-btn
              v-else-if="store.isReturnPending(tx)"
              block
              variant="tonal"
              color="warning"
              prepend-icon="mdi-close-circle-outline"
              @click="openCancelReturn(tx)"
            >
              Hủy trả
            </v-btn>
            <template v-else>
              <template v-if="isRenewPending(tx)">
                <v-btn
                  block
                  variant="tonal"
                  color="grey-darken-1"
                  prepend-icon="mdi-close-circle-outline"
                  @click="openCancelRenew(tx)"
                >
                  Hủy gia hạn
                </v-btn>
                <div class="text-caption text-grey-darken-1 mt-2">
                  Đang chờ thủ thư duyệt gia hạn.
                </div>
              </template>
              <template v-else>
                <v-btn
                  block
                  variant="tonal"
                  color="success"
                  prepend-icon="mdi-undo"
                  @click="openReturn(tx)"
                >
                  {{ returnButtonText(tx) }}
                </v-btn>
                <v-btn
                  v-if="canRequestRenew(tx)"
                  block
                  class="mt-2"
                  variant="tonal"
                  color="indigo"
                  prepend-icon="mdi-calendar-clock"
                  @click="openRenew(tx)"
                >
                  Gia hạn
                </v-btn>
              </template>
            </template>
            <v-btn
              block
              class="mt-2"
              variant="text"
              color="amber-darken-2"
              prepend-icon="mdi-star"
              :disabled="!canReview(tx)"
              @click="openReview(tx)"
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

    <v-dialog v-model="actionDialog" max-width="480">
      <v-card rounded="xl">
        <v-card-title class="text-h6 font-weight-bold">{{ actionTitle }}</v-card-title>
        <v-card-text>
          <v-alert v-if="actionError" type="error" variant="tonal" density="compact" class="mb-3">
            {{ actionError }}
          </v-alert>
          <v-alert type="info" variant="tonal" class="mb-4">
            {{ actionMessage }}
          </v-alert>
          <template v-if="actionType === 'renew'">
            <v-text-field
              v-model="renewDays"
              type="number"
              min="1"
              max="60"
              label="Số ngày gia hạn"
              variant="outlined"
              density="comfortable"
              class="mb-3"
            />
            <v-textarea
              v-model="renewReason"
              label="Lý do gia hạn"
              rows="3"
              auto-grow
              hide-details
              variant="outlined"
            />
          </template>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="closeActionDialog">Hủy</v-btn>
          <v-btn :color="actionColor" :loading="actionLoading" @click="submitAction">
            {{ actionConfirmLabel }}
          </v-btn>
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
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useAppStore } from '@/stores/app'
import {
  getReaderCard,
  returnBook as apiReturn,
  returnTransaction,
  cancelBorrowRequest as apiCancelBorrow,
  cancelReturnRequest as apiCancelReturn,
  requestRenewal as apiRequestRenewal,
  cancelRenewRequest as apiCancelRenew,
  reviewBook as apiReviewBook,
  fetchBookReviews
} from '@/utils/api'
import { titleColor, formatDateTime as formatDateTimeText, formatDurationText, daysLeft } from '@/utils/helpers'
import dayjs from 'dayjs'
import 'dayjs/locale/vi'
dayjs.locale('vi')

const store = useAppStore()
const filter = ref('all')
const returnDialog = ref(false)
const returnBookData = ref(null)
const returning = ref(false)
const returnError = ref('')
const actionDialog = ref(false)
const actionType = ref('')
const actionTx = ref(null)
const actionLoading = ref(false)
const actionError = ref('')
const renewDays = ref(7)
const renewReason = ref('')
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
const nowTick = ref(Date.now())
const tickHandle = setInterval(() => {
  nowTick.value = Date.now()
}, 1000)

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
  if (isRenewPending(tx)) return 'indigo'
  if (store.isOverdue(tx)) return 'error'
  const d = daysLeft(tx.DueAt || tx.dueAt)
  return d !== null && d <= 3 ? 'warning' : 'info'
}

function statusIcon(tx) {
  if (store.isPending(tx)) return 'mdi-clock-outline'
  if (store.isReturnPending(tx)) return 'mdi-book-clock'
  if (isRenewPending(tx)) return 'mdi-calendar-clock'
  if (store.isOverdue(tx)) return 'mdi-alert'
  return 'mdi-calendar'
}

function statusText(tx) {
  if (store.isPending(tx)) return 'Chờ duyệt mượn'
  if (store.isReturnPending(tx)) return 'Chờ xác nhận trả'
  if (isRenewPending(tx)) return 'Chờ duyệt gia hạn'
  if (store.isOverdue(tx)) {
    const dueAt = tx.DueAt || tx.dueAt
    return dueAt ? `Quá hạn ${formatDurationText(dueAt, nowTick.value)}` : 'Quá hạn'
  }
  const dueAt = tx.DueAt || tx.dueAt
  return dueAt ? `Còn lại ${formatDurationText(nowTick.value, dueAt)}` : 'Đang mượn'
}

function requestDate(tx) {
  return tx.RequestDate || tx.requestDate || tx.CreatedAt || tx.createdAt || tx.BorrowedAt || tx.borrowedAt || ''
}

function borrowedDate(tx) {
  return tx.BorrowedAt || tx.borrowedAt || requestDate(tx)
}

function formatDateTime(value) {
  return formatDateTimeText(value)
}

function loanTimeText(tx) {
  if (store.isPending(tx)) return 'Chưa bắt đầu tính thời gian mượn'
  const borrowedAt = borrowedDate(tx)
  return borrowedAt ? formatDurationText(borrowedAt, nowTick.value) : 'Đã mượn'
}

function timeRemainderText(tx) {
  if (store.isPending(tx)) return ''
  const dueAt = tx.DueAt || tx.dueAt
  if (!dueAt) return ''
  const now = new Date()
  const due = new Date(dueAt)
  if (Number.isNaN(due.getTime())) return ''
  if (due.getTime() >= now.getTime()) {
    return `Còn lại: ${formatDurationText(nowTick.value, dueAt)}`
  }
  return `Quá hạn: ${formatDurationText(dueAt, nowTick.value)}`
}

function reviewButtonText(tx) {
  if (hasReviewed(tx)) return 'Đã đánh giá'
  if (store.isPending(tx)) return 'Chờ duyệt mới đánh giá'
  if (store.isReturnPending(tx)) return 'Chờ trả sách mới đánh giá'
  return 'Đánh giá'
}

function canReview(tx) {
  return !hasReviewed(tx) && !store.isPending(tx) && !store.isReturnPending(tx)
}

function returnButtonText(tx) {
  if (store.isPending(tx)) return 'Chờ duyệt mượn'
  if (store.isReturnPending(tx)) return 'Chờ thủ thư xác nhận'
  if (isRenewPending(tx)) return 'Trả sách'
  return 'Trả sách'
}

function progressPercent(tx) {
  if (store.isPending(tx)) return 8
  if (store.isReturnPending(tx)) return 100
  const borrowedAt = borrowedDate(tx)
  const dueAt = tx.DueAt || tx.dueAt
  const start = borrowedAt ? new Date(borrowedAt) : null
  const due = dueAt ? new Date(dueAt) : null
  if (!start || !due || Number.isNaN(start.getTime()) || Number.isNaN(due.getTime()) || due.getTime() <= start.getTime()) {
    return 50
  }
  const elapsed = Math.max(0, nowTick.value - start.getTime())
  const total = Math.max(1, due.getTime() - start.getTime())
  return Math.min(100, Math.max(0, Math.round((elapsed / total) * 100)))
}

function progressColor(tx) {
  if (store.isPending(tx)) return 'warning'
  if (store.isReturnPending(tx)) return 'deep-purple'
  if (isRenewPending(tx)) return 'indigo'
  if (store.isOverdue(tx)) return 'error'
  const d = daysLeft(tx.DueAt || tx.dueAt)
  return d !== null && d <= 3 ? 'warning' : 'primary'
}

function isRenewPending(tx) {
  return store.statusOf(tx) === 'RenewPending'
}

function canRequestRenew(tx) {
  return !store.isPending(tx) && !store.isReturnPending(tx) && !isRenewPending(tx) && !store.isReturned(tx)
}

function openReturn(tx) {
  returnError.value = ''
  returnBookData.value = tx
  returnDialog.value = true
}

function openCancelBorrow(tx) {
  openActionDialog('cancel-borrow', tx)
}

function openCancelReturn(tx) {
  openActionDialog('cancel-return', tx)
}

function openRenew(tx) {
  renewDays.value = 7
  renewReason.value = ''
  openActionDialog('renew', tx)
}

function openCancelRenew(tx) {
  openActionDialog('cancel-renew', tx)
}

function openActionDialog(type, tx) {
  actionType.value = type
  actionTx.value = tx
  actionError.value = ''
  actionDialog.value = true
}

function closeActionDialog() {
  actionDialog.value = false
  actionTx.value = null
  actionType.value = ''
  actionError.value = ''
  actionLoading.value = false
}

const actionTitle = computed(() => {
  if (actionType.value === 'cancel-borrow') return 'Hủy yêu cầu mượn'
  if (actionType.value === 'cancel-return') return 'Hủy yêu cầu trả'
  if (actionType.value === 'cancel-renew') return 'Hủy yêu cầu gia hạn'
  if (actionType.value === 'renew') return 'Gửi yêu cầu gia hạn'
  return 'Xác nhận thao tác'
})

const actionMessage = computed(() => {
  const tx = actionTx.value
  const title = tx?.TenSach || tx?.tenSach || 'cuốn sách này'
  if (actionType.value === 'cancel-borrow') return `Hủy yêu cầu mượn "${title}" khỏi hàng chờ thủ thư.`
  if (actionType.value === 'cancel-return') return `Hủy yêu cầu trả "${title}" để tiếp tục giữ sách.`
  if (actionType.value === 'cancel-renew') return `Hủy yêu cầu gia hạn "${title}" đang chờ thủ thư duyệt.`
  if (actionType.value === 'renew') return `Gửi yêu cầu gia hạn cho "${title}" và chờ thủ thư duyệt.`
  return 'Bạn có chắc muốn thực hiện thao tác này?'
})

const actionConfirmLabel = computed(() => {
  if (actionType.value === 'cancel-borrow' || actionType.value === 'cancel-return' || actionType.value === 'cancel-renew') return 'Xác nhận hủy'
  if (actionType.value === 'renew') return 'Gửi yêu cầu'
  return 'Xác nhận'
})

const actionColor = computed(() => {
  if (actionType.value === 'renew') return 'indigo'
  return 'error'
})

async function submitAction() {
  const tx = actionTx.value
  if (!tx) return
  const transactionId = tx.Id || tx.id
  if (!transactionId) {
    actionError.value = 'Không tìm thấy mã giao dịch.'
    return
  }

  const currentAction = actionType.value
  actionLoading.value = true
  actionError.value = ''
  try {
    let response
    if (currentAction === 'cancel-borrow') {
      response = await apiCancelBorrow(transactionId)
    } else if (currentAction === 'cancel-return') {
      response = await apiCancelReturn(transactionId)
    } else if (currentAction === 'renew') {
      const days = Math.min(60, Math.max(1, Number(renewDays.value) || 7))
      response = await apiRequestRenewal(transactionId, days, renewReason.value || '')
    } else if (currentAction === 'cancel-renew') {
      response = await apiCancelRenew(transactionId)
    } else {
      actionError.value = 'Thao tác không hợp lệ.'
      return
    }

    if (response.ok) {
      closeActionDialog()
      snackbarText.value =
        currentAction === 'renew'
          ? 'Đã gửi yêu cầu gia hạn. Vui lòng chờ thủ thư duyệt.'
          : 'Đã cập nhật yêu cầu thành công.'
      snackbarColor.value = 'success'
      snackbar.value = true
      await loadData()
      return
    }

    const data = await response.json().catch(() => null)
    actionError.value = data?.message || data?.Message || 'Không thể thực hiện thao tác này.'
  } catch {
    actionError.value = 'Không kết nối được máy chủ. Vui lòng thử lại.'
  } finally {
    actionLoading.value = false
  }
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
onUnmounted(() => clearInterval(tickHandle))
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
  aspect-ratio: 2 / 3;
  min-height: 0;
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
.timeline-box {
  display: grid;
  gap: 8px;
  padding: 12px;
  border-radius: 14px;
  border: 1px solid #f3eadb;
  background: #fffdf8;
}
.timeline-line {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.timeline-label {
  font-size: 10px;
  font-weight: 700;
  color: #9ca3af;
  text-transform: uppercase;
  letter-spacing: 0.02em;
}
.timeline-value {
  font-size: 12px;
  font-weight: 600;
  color: #374151;
}
.text-warning {
  color: #d97706 !important;
}
.text-white-50 {
  color: rgba(255,255,255,0.6) !important;
}
.text-error {
  color: #dc2626 !important;
}
</style>

