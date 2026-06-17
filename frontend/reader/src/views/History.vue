<template>
  <div class="history-page">
    <div class="mb-6">
      <div class="eyebrow">LỊCH SỬ CÁ NHÂN</div>
      <h2 class="text-h5 text-md-h4 font-weight-black mb-2">Mượn trả và thanh toán</h2>
      <p class="text-body-2 text-grey-darken-1">
        Theo dõi phiếu mượn, khoản thanh toán phí phạt và các thông báo hệ thống dành riêng cho tài khoản của bạn.
      </p>
    </div>

    <v-row class="mb-6" dense>
      <v-col v-for="stat in stats" :key="stat.label" cols="12" sm="6" md="3">
        <v-card rounded="xl" class="pa-4 history-stat-card" elevation="1">
          <p class="text-caption text-grey font-weight-bold">{{ stat.label }}</p>
          <p class="text-h5 font-weight-black" :class="stat.color">{{ stat.value }}</p>
          <p class="text-caption text-grey-darken-1 mt-1">{{ stat.helper }}</p>
        </v-card>
      </v-col>
    </v-row>

    <v-row dense class="align-start">
      <v-col cols="12" xl="8">
        <v-card rounded="xl" elevation="1" class="mb-5 overflow-hidden">
          <v-card-title class="d-flex align-center justify-space-between flex-wrap ga-2">
            <div>
              <div class="font-weight-bold">Lịch sử mượn trả</div>
              <div class="text-caption text-grey-darken-1">Các phiếu mượn, ngày mượn, hạn trả và trạng thái xử lý.</div>
            </div>
            <v-chip size="small" variant="tonal">{{ store.myTransactions.length }} phiếu</v-chip>
          </v-card-title>

          <v-data-table
            :headers="borrowHeaders"
            :items="borrowHistory"
            :loading="loading"
            :items-per-page="8"
            class="history-table"
          >
            <template #item.book="{ item }">
              <div class="d-flex align-center ga-3 py-2">
                <v-avatar size="40" :color="titleColor(item.bookTitle)" rounded="lg">
                  <v-icon color="white" size="20">mdi-book-open-variant</v-icon>
                </v-avatar>
                <div class="min-w-0">
                  <p class="text-body-2 font-weight-bold text-truncate">{{ item.bookTitle }}</p>
                  <p class="text-caption text-grey-darken-1 text-truncate">{{ item.author || '—' }}</p>
                </div>
              </div>
            </template>
            <template #item.borrowedAt="{ item }">{{ formatDateTime(item.borrowedAt) }}</template>
            <template #item.dueAt="{ item }">{{ formatDateTime(item.dueAt) }}</template>
            <template #item.returnedAt="{ item }">
              {{ item.returnedAt ? formatDateTime(item.returnedAt) : '—' }}
            </template>
            <template #item.status="{ item }">
              <v-chip size="small" :color="item.statusColor" variant="flat">
                {{ item.statusLabel }}
              </v-chip>
            </template>
            <template #item.actions="{ item }">
              <v-btn size="small" variant="text" color="primary" prepend-icon="mdi-eye" @click="openDetail(item)">
                Xem
              </v-btn>
            </template>
          </v-data-table>
        </v-card>

        <v-card rounded="xl" elevation="1" class="overflow-hidden">
          <v-card-title class="d-flex align-center justify-space-between flex-wrap ga-2">
            <div>
              <div class="font-weight-bold">Lịch sử thanh toán</div>
              <div class="text-caption text-grey-darken-1">
                Xem lại các khoản thu mượn, phí phạt, thời điểm gửi yêu cầu và trạng thái thanh toán.
              </div>
            </div>
            <v-chip size="small" variant="tonal">{{ paymentHistory.length }} khoản</v-chip>
          </v-card-title>

          <v-card-text class="pt-0">
            <v-btn-toggle
              v-model="paymentFilter"
              mandatory
              density="comfortable"
              variant="outlined"
              class="mb-4 flex-wrap"
            >
              <v-btn value="all">Tất cả</v-btn>
              <v-btn value="borrow">Thu mượn</v-btn>
              <v-btn value="fine">Thu phí phạt</v-btn>
              <v-btn value="pending">Chờ duyệt</v-btn>
              <v-btn value="paid">Đã thanh toán</v-btn>
            </v-btn-toggle>

            <v-data-table
              :headers="paymentHeaders"
              :items="filteredPayments"
              :items-per-page="8"
              class="history-table"
            >
              <template #item.type="{ item }">
                <v-chip size="small" :color="item.typeColor" variant="flat">{{ item.typeLabel }}</v-chip>
              </template>
              <template #item.subject="{ item }">
                <div>
                  <p class="text-body-2 font-weight-bold">{{ item.subject }}</p>
                  <p class="text-caption text-grey-darken-1">{{ item.reference }}</p>
                </div>
              </template>
              <template #item.amount="{ item }">
                <span class="font-weight-bold" :class="item.amount > 0 ? 'text-error' : 'text-grey'">
                  {{ item.amount > 0 ? formatMoney(item.amount) : '—' }}
                </span>
              </template>
              <template #item.createdAt="{ item }">{{ formatDateTime(item.createdAt) }}</template>
              <template #item.requestedAt="{ item }">
                {{ item.requestedAt ? formatDateTime(item.requestedAt) : '—' }}
              </template>
              <template #item.paidAt="{ item }">
                {{ item.paidAt ? formatDateTime(item.paidAt) : '—' }}
              </template>
              <template #item.status="{ item }">
                <v-chip size="small" :color="item.statusColor" variant="flat">
                  {{ item.statusLabel }}
                </v-chip>
              </template>
              <template #item.actions="{ item }">
                <v-btn size="small" variant="text" color="primary" prepend-icon="mdi-eye" @click="openDetail(item)">
                  Xem
                </v-btn>
              </template>
            </v-data-table>
          </v-card-text>
        </v-card>
      </v-col>

      <v-col cols="12" xl="4">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="d-flex align-center justify-space-between">
            <span class="font-weight-bold">Thông báo hệ thống</span>
            <v-chip size="small" variant="tonal">{{ notifications.length }} mục</v-chip>
          </v-card-title>
          <v-card-text>
            <v-alert
              v-for="item in notifications"
              :key="item.id"
              :type="item.type"
              variant="tonal"
              class="mb-3"
              border="start"
            >
              <div class="font-weight-bold">{{ item.title }}</div>
              <div class="text-body-2">{{ item.message }}</div>
              <div class="text-caption text-grey mt-1">{{ formatDateTime(item.createdAt) }}</div>
            </v-alert>
            <v-alert v-if="!notifications.length" type="info" variant="tonal">
              Chưa có thông báo nào.
            </v-alert>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-dialog v-model="detailDialog" max-width="720">
      <v-card rounded="xl">
        <v-card-title class="d-flex align-center justify-space-between">
          <span class="font-weight-bold">{{ selectedRecord?.title || 'Chi tiết lịch sử' }}</span>
          <v-chip v-if="selectedRecord" size="small" :color="selectedRecord.statusColor" variant="flat">
            {{ selectedRecord.statusLabel }}
          </v-chip>
        </v-card-title>
        <v-card-text v-if="selectedRecord">
          <v-row dense>
            <v-col cols="12" md="6">
              <div class="detail-box">
                <div class="detail-label">Loại giao dịch</div>
                <div class="detail-value">{{ selectedRecord.typeLabel }}</div>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="detail-box">
                <div class="detail-label">Số tiền</div>
                <div class="detail-value text-error">
                  {{ selectedRecord.amount > 0 ? formatMoney(selectedRecord.amount) : '—' }}
                </div>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="detail-box">
                <div class="detail-label">Ngày tạo</div>
                <div class="detail-value">{{ formatDateTime(selectedRecord.createdAt) }}</div>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="detail-box">
                <div class="detail-label">Yêu cầu lúc</div>
                <div class="detail-value">{{ selectedRecord.requestedAt ? formatDateTime(selectedRecord.requestedAt) : '—' }}</div>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="detail-box">
                <div class="detail-label">Đã thanh toán lúc</div>
                <div class="detail-value">{{ selectedRecord.paidAt ? formatDateTime(selectedRecord.paidAt) : '—' }}</div>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="detail-box">
                <div class="detail-label">Tham chiếu</div>
                <div class="detail-value">{{ selectedRecord.reference }}</div>
              </div>
            </v-col>
            <v-col cols="12">
              <div class="detail-box">
                <div class="detail-label">Liên quan</div>
                <div class="detail-value">{{ selectedRecord.subject }}</div>
              </div>
            </v-col>
            <v-col cols="12" v-if="selectedRecord.description">
              <div class="detail-box">
                <div class="detail-label">Ghi chú</div>
                <div class="detail-value">{{ selectedRecord.description }}</div>
              </div>
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="detailDialog = false">Đóng</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useAppStore } from '@/stores/app'
import { titleColor, formatDateTime, formatMoney, getDisplayBookTitle, translateFineReason } from '@/utils/helpers'

const store = useAppStore()
const loading = ref(false)
const paymentFilter = ref('all')
const detailDialog = ref(false)
const selectedRecord = ref(null)

const borrowHeaders = [
  { title: 'Sách', key: 'book', sortable: false, width: '280px' },
  { title: 'Mã phiếu', key: 'reference', width: '120px' },
  { title: 'Ngày mượn', key: 'borrowedAt', width: '140px' },
  { title: 'Hạn trả', key: 'dueAt', width: '140px' },
  { title: 'Ngày trả', key: 'returnedAt', width: '140px' },
  { title: 'Trạng thái', key: 'status', width: '130px' },
  { title: 'Hành động', key: 'actions', sortable: false, width: '110px' }
]

const paymentHeaders = [
  { title: 'Loại', key: 'type', width: '120px' },
  { title: 'Nội dung', key: 'subject', sortable: false, width: '280px' },
  { title: 'Số tiền', key: 'amount', width: '120px' },
  { title: 'Ngày tạo', key: 'createdAt', width: '140px' },
  { title: 'Yêu cầu lúc', key: 'requestedAt', width: '140px' },
  { title: 'Thanh toán lúc', key: 'paidAt', width: '140px' },
  { title: 'Trạng thái', key: 'status', width: '130px' },
  { title: 'Hành động', key: 'actions', sortable: false, width: '110px' }
]

const bookMap = computed(() => {
  return new Map((store.books || []).map(book => [String(book.id ?? book.Id ?? book.bookId ?? book.BookId ?? ''), book]))
})

function resolveBook(bookId) {
  return bookMap.value.get(String(bookId)) || {}
}

function safeDate(value) {
  if (!value) return null
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

function estimateBorrowAmount(tx = {}) {
  const borrowedAt = safeDate(tx.BorrowedAt || tx.borrowedAt || tx.RequestDate || tx.requestDate || tx.CreatedAt || tx.createdAt)
  const dueAt = safeDate(tx.DueAt || tx.dueAt || tx.returnAt || tx.ReturnAt)
  if (!borrowedAt || !dueAt) return 0
  const days = Math.max(1, Math.ceil((dueAt.getTime() - borrowedAt.getTime()) / 86400000))
  return days * 5000
}

function borrowStatus(tx = {}) {
  const status = String(store.statusOf(tx) || '').trim()
  if (status === 'Pending') return { label: 'Chờ duyệt', color: 'warning' }
  if (status === 'ReturnPending') return { label: 'Chờ trả', color: 'info' }
  if (status === 'RenewPending') return { label: 'Chờ gia hạn', color: 'secondary' }
  if (status === 'Returned') return { label: 'Đã trả', color: 'success' }
  if (store.isOverdue(tx)) return { label: 'Quá hạn', color: 'error' }
  return { label: 'Đang mượn', color: 'success' }
}

function getBorrowRecords() {
  return (store.myTransactions || []).map((tx, index) => {
    const book = resolveBook(tx.BookId || tx.bookId)
    const borrowedAt = tx.BorrowedAt || tx.borrowedAt || tx.RequestDate || tx.requestDate || tx.CreatedAt || tx.createdAt
    const dueAt = tx.DueAt || tx.dueAt || tx.returnAt || tx.ReturnAt
    const returnedAt = tx.ReturnedAt || tx.returnedAt
    const amount = estimateBorrowAmount(tx)
    const status = borrowStatus(tx)
    const bookTitle = getDisplayBookTitle(
      {
        tenSach: tx.TenSach || tx.tenSach || tx.Title || tx.title || tx.bookTitle || tx.BookTitle || book.tenSach || book.TenSach,
        TenSach: tx.TenSach || tx.tenSach || tx.Title || tx.title || tx.bookTitle || tx.BookTitle || book.tenSach || book.TenSach,
        title: tx.Title || tx.title || tx.TenSach || tx.tenSach || book.tenSach || book.TenSach
      },
      `Sách #${String(tx.BookId || tx.bookId || index + 1)}`
    )
    const author = tx.TacGia || tx.tacGia || book.tacGia || book.TacGia || ''

    return {
      id: `borrow-${tx.Id || tx.id || index}`,
      kind: 'borrow',
      title: 'Thu mượn',
      typeLabel: 'Thu mượn',
      typeColor: 'success',
      reference: `Phiếu #${String(tx.Id || tx.id || index + 1).slice(0, 8)}`,
      subject: author ? `${bookTitle} - ${author}` : bookTitle,
      description: returnedAt ? 'Phiếu đã hoàn tất và đã trả sách.' : 'Phiếu mượn đang được theo dõi trong lịch sử.',
      amount,
      createdAt: borrowedAt,
      requestedAt: borrowedAt,
      paidAt: status.label === 'Chờ duyệt' ? null : borrowedAt,
      statusLabel: status.label,
      statusColor: status.color,
      sortAt: safeDate(returnedAt || dueAt || borrowedAt)?.getTime() || 0,
      raw: tx
    }
  })
}

function isFinePaid(item = {}) {
  return Boolean(
    item.IsPaid ||
    item.isPaid ||
    item.PaymentStatus === 'Paid' ||
    item.paymentStatus === 'Paid' ||
    item.PaidAt ||
    item.paidAt
  )
}

function isFinePending(item = {}) {
  if (isFinePaid(item)) return false
  return Boolean(
    item.IsPaymentPending ||
    item.isPaymentPending ||
    item.PaymentRequestedAt ||
    item.paymentRequestedAt ||
    item.PaymentStatus === 'PendingApproval' ||
    item.paymentStatus === 'PendingApproval'
  )
}

function getFineStatus(fine = {}) {
  if (isFinePaid(fine)) return { label: 'Đã thanh toán', color: 'success' }
  if (isFinePending(fine)) return { label: 'Chờ duyệt', color: 'warning' }
  return { label: 'Chưa yêu cầu', color: 'grey' }
}

function getFineRecords() {
  return (store.myFines || []).map((fine, index) => {
    const book = resolveBook(fine.BookId || fine.bookId)
    const status = getFineStatus(fine)
    const reason = translateFineReason(fine.Reason || fine.reason || '')
    const amount = Number(fine.Amount || fine.amount || 0)
    const createdAt = fine.CreatedAt || fine.createdAt
    const requestedAt = fine.PaymentRequestedAt || fine.paymentRequestedAt
    const paidAt = fine.PaidAt || fine.paidAt

    return {
      id: `fine-${fine.Id || fine.id || index}`,
      kind: 'fine',
      title: 'Thu phí phạt',
      typeLabel: 'Phí phạt',
      typeColor: 'warning',
      reference: `Phí #${String(fine.Id || fine.id || index + 1).slice(0, 8)}`,
      subject: book.tenSach || book.TenSach ? `${reason} - ${book.tenSach || book.TenSach}` : reason,
      description: `Book ID: ${fine.BookId || fine.bookId || '—'}`,
      amount,
      createdAt,
      requestedAt,
      paidAt,
      statusLabel: status.label,
      statusColor: status.color,
      sortAt: safeDate(paidAt || requestedAt || createdAt)?.getTime() || 0,
      raw: fine
    }
  })
}

const borrowHistory = computed(() => getBorrowRecords().sort((a, b) => b.sortAt - a.sortAt))

const paymentHistory = computed(() =>
  [...getBorrowRecords(), ...getFineRecords()].sort((a, b) => b.sortAt - a.sortAt)
)

const filteredPayments = computed(() => {
  if (paymentFilter.value === 'borrow') return paymentHistory.value.filter(item => item.kind === 'borrow')
  if (paymentFilter.value === 'fine') return paymentHistory.value.filter(item => item.kind === 'fine')
  if (paymentFilter.value === 'pending') return paymentHistory.value.filter(item => item.statusLabel.includes('Chờ'))
  if (paymentFilter.value === 'paid') return paymentHistory.value.filter(item => item.statusColor === 'success')
  return paymentHistory.value
})

const notifications = computed(() => {
  return (store.events || [])
    .map(e => {
      const payload = e.payload || {}
      const eventType = String(e.eventType || '').toLowerCase()
      const visibleToReader = Boolean(payload.VisibleToReader ?? payload.visibleToReader)
      const allowedEvent =
        eventType === 'reader.notification' ||
        eventType.startsWith('reader.') ||
        eventType.includes('notification') ||
        visibleToReader
      if (!allowedEvent) return null

      const title = String(payload.Title || payload.title || '').trim()
      const message = String(payload.Message || payload.message || '').trim()
      if (!title && !message) return null

      const isReject = eventType.includes('rejected') || eventType.includes('reject')
      const isRenew = eventType.includes('renew')
      const isReturn = eventType.includes('return')

      return {
        id: e.id,
        title: title || 'Thông báo',
        message,
        createdAt: payload.CreatedAt || payload.createdAt || e.publishedAt,
        type: isReject ? 'error' : isRenew ? 'warning' : isReturn ? 'success' : 'info'
      }
    })
    .filter(Boolean)
    .slice(0, 20)
})

const stats = computed(() => [
  {
    label: 'Phiếu mượn',
    value: store.myTransactions.length,
    color: 'text-primary',
    helper: 'Toàn bộ lịch sử mượn trả'
  },
  {
    label: 'Đang mượn',
    value: store.activeTransactions.length,
    color: 'text-success',
    helper: 'Các phiếu còn hiệu lực'
  },
  {
    label: 'Thanh toán',
    value: paymentHistory.value.length,
    color: 'text-teal-darken-2',
    helper: 'Gồm thu mượn và phí phạt'
  },
  {
    label: 'Chờ duyệt',
    value: paymentHistory.value.filter(item => item.statusLabel.includes('Chờ')).length,
    color: 'text-warning',
    helper: 'Các giao dịch cần thủ thư xử lý'
  }
])

function openDetail(item) {
  selectedRecord.value = item
  detailDialog.value = true
}

onMounted(async () => {
  loading.value = true
  await store.loadAll()
  loading.value = false
})
</script>

<style scoped lang="scss">
.history-page {
  color: #0f172a;
}

.eyebrow {
  font-size: 12px;
  font-weight: 800;
  letter-spacing: .12em;
  color: #0f766e;
  margin-bottom: 6px;
}

.history-stat-card,
.history-table,
.detail-box {
  border: 1px solid #e6efe9;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.05) !important;
}

.history-table :deep(.v-data-table__td) {
  vertical-align: middle;
}

.min-w-0 {
  min-width: 0;
}

.detail-box {
  border-radius: 14px;
  padding: 14px;
  background: linear-gradient(180deg, #fbfdfb, #f7fbf8);
}

.detail-label {
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: .08em;
  color: #64748b;
  font-weight: 800;
  margin-bottom: 4px;
}

.detail-value {
  font-size: 14px;
  font-weight: 600;
  word-break: break-word;
}
</style>
