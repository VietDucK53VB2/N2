<template>
  <div class="page-shell">
    <v-card rounded="xl" elevation="1" class="hero-card mb-6">
      <v-card-text class="pa-6">
        <div class="d-flex align-center justify-space-between flex-wrap ga-4">
          <div>
            <div class="eyebrow">Quản lý mượn trả</div>
            <h2 class="text-h5 font-weight-black mb-1">Danh sách phiếu mượn</h2>
            <p class="text-body-2 text-grey mb-0">Duyệt yêu cầu mượn, gia hạn và theo dõi các trạng thái xử lý.</p>
          </div>

          <div class="d-flex flex-wrap ga-3">
            <v-chip color="warning" variant="tonal" class="hero-badge">Chờ duyệt: {{ pendingCount }}</v-chip>
            <v-chip color="info" variant="tonal" class="hero-badge">Đang mượn: {{ borrowedCount }}</v-chip>
            <v-chip color="purple" variant="tonal" class="hero-badge">Chờ trả: {{ returnPendingCount }}</v-chip>
            <v-chip color="error" variant="tonal" class="hero-badge">Quá hạn: {{ overdueCount }}</v-chip>
          </div>
        </div>
      </v-card-text>
    </v-card>

    <v-card rounded="xl" elevation="1" class="panel-card">
      <v-card-text class="pa-5">
        <div class="d-flex align-center justify-space-between flex-wrap ga-4 mb-4">
          <v-chip-group v-model="filter" mandatory selected-class="text-white" class="filter-group">
            <v-chip value="all" color="primary" variant="flat">Tất cả</v-chip>
            <v-chip value="Pending" color="warning" variant="outlined">Chờ duyệt</v-chip>
            <v-chip value="Borrowed" color="info" variant="outlined">Đang mượn</v-chip>
            <v-chip value="ReturnPending" color="purple" variant="outlined">Chờ trả</v-chip>
            <v-chip value="Overdue" color="error" variant="outlined">Quá hạn</v-chip>
            <v-chip value="Returned" color="success" variant="outlined">Đã trả</v-chip>
          </v-chip-group>
          <v-chip color="blue" variant="tonal">{{ filteredTransactions.length }} phiếu</v-chip>
        </div>

        <v-data-table
          :headers="headers"
          :items="filteredTransactions"
          :loading="libStore.loading"
          density="comfortable"
          :items-per-page="10"
          class="loans-table"
        >
          <template #item.reader="{ item }">
            <div>
              <div class="font-weight-bold">{{ displayReader(item) }}</div>
              <div class="text-caption text-grey">{{ displayCard(item) }}</div>
            </div>
          </template>

          <template #item.book="{ item }">
            <div>
              <div class="font-weight-bold">{{ displayBook(item) }}</div>
              <div class="text-caption text-grey">Book ID: {{ item.BookId || item.bookId || '—' }}</div>
            </div>
          </template>

          <template #item.BorrowedAt="{ item }">
            {{ formatDateTime(item.BorrowedAt || item.borrowedAt) }}
          </template>

          <template #item.DueAt="{ item }">
            {{ formatDateTime(item.DueAt || item.dueAt) }}
          </template>

          <template #item.Status="{ item }">
            <div class="status-wrap">
              <v-chip size="small" :color="statusColor(item)" variant="flat">
                {{ statusLabel(item) }}
              </v-chip>
              <div class="text-caption text-grey mt-1">{{ statusDetail(item) }}</div>
            </div>
          </template>

          <template #item.actions="{ item }">
            <div class="d-flex flex-wrap ga-2">
              <template v-if="isPending(item)">
                <v-btn
                  size="small"
                  color="success"
                  variant="flat"
                  :loading="actionId === actionKey(item, 'approve')"
                  @click="approve(item)"
                >
                  <v-icon start>mdi-check</v-icon> Duyệt mượn
                </v-btn>
                <v-btn
                  size="small"
                  color="error"
                  variant="tonal"
                  :loading="actionId === actionKey(item, 'reject')"
                  @click="rejectBorrow(item)"
                >
                  <v-icon start>mdi-close</v-icon> Từ chối
                </v-btn>
              </template>

              <template v-else-if="isBorrowed(item) || isOverdue(item)">
                <v-btn
                  size="small"
                  color="primary"
                  variant="flat"
                  :loading="actionId === actionKey(item, 'renew')"
                  @click="renew(item)"
                >
                  <v-icon start>mdi-refresh</v-icon> Duyệt gia hạn
                </v-btn>
                <v-btn
                  size="small"
                  color="error"
                  variant="tonal"
                  :loading="actionId === actionKey(item, 'reject-renew')"
                  @click="rejectRenewal(item)"
                >
                  <v-icon start>mdi-close</v-icon> Từ chối gia hạn
                </v-btn>
              </template>

              <template v-else-if="isReturnPending(item)">
                <v-btn
                  size="small"
                  color="success"
                  variant="flat"
                  :loading="actionId === actionKey(item, 'return-approve')"
                  @click="openReturnDialog(item)"
                >
                  <v-icon start>mdi-check</v-icon> Kiểm tra & trả
                </v-btn>
                <v-btn
                  size="small"
                  color="error"
                  variant="tonal"
                  :loading="actionId === actionKey(item, 'return-reject')"
                  @click="rejectReturn(item)"
                >
                  <v-icon start>mdi-close</v-icon> Chưa nhận
                </v-btn>
              </template>

              <v-chip v-else-if="isReturned(item)" size="small" color="success" variant="flat">
                Đã trả
              </v-chip>
            </div>
          </template>
        </v-data-table>

        <v-dialog v-model="reasonDialog" max-width="560">
          <v-card rounded="xl">
            <v-card-title class="font-weight-bold">{{ reasonTitle }}</v-card-title>
            <v-card-text>
              <p class="text-body-2 text-grey mb-4">{{ reasonMessage }}</p>
              <v-textarea
                v-model="reasonValue"
                label="Lý do"
                variant="outlined"
                auto-grow
                rows="4"
                hide-details
              />
            </v-card-text>
            <v-card-actions class="px-4 pb-4">
              <v-spacer />
              <v-btn variant="text" @click="closeReasonDialog">Hủy</v-btn>
              <v-btn color="error" :loading="reasonBusy" @click="confirmReason">
                <v-icon start>mdi-close</v-icon> Xác nhận
              </v-btn>
            </v-card-actions>
          </v-card>
        </v-dialog>
      </v-card-text>
    </v-card>

    <v-dialog v-model="returnDialog" max-width="640">
      <v-card rounded="xl">
        <v-card-title class="font-weight-bold">Kiểm tra tình trạng sách</v-card-title>
        <v-card-text>
          <v-alert v-if="selectedTransaction" type="info" variant="tonal" class="mb-4">
            <div class="font-weight-bold">{{ displayBook(selectedTransaction) }}</div>
            <div class="text-caption">{{ displayReader(selectedTransaction) }} · {{ displayCard(selectedTransaction) }}</div>
          </v-alert>

          <v-radio-group v-model="returnForm.condition" inline class="mb-4">
            <v-radio label="Tốt, dùng bình thường" value="Good" />
            <v-radio label="Hư nhẹ / bẩn nhẹ" value="LightDamage" />
            <v-radio label="Hư nặng / rách nhiều" value="HeavyDamage" />
            <v-radio label="Mất sách" value="Lost" />
          </v-radio-group>

          <v-textarea
            v-model="returnForm.conditionNote"
            rows="4"
            label="Ghi chú kiểm tra"
            placeholder="Ví dụ: Bìa hơi cong, trang 24 có vết bẩn..."
            auto-grow
            variant="outlined"
          />
        </v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" @click="returnDialog = false">Hủy</v-btn>
          <v-btn color="success" :loading="returning" @click="confirmReturn">
            <v-icon start>mdi-check</v-icon> Xác nhận trả
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { computed, onMounted, reactive, ref } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import { formatDateTime, getDisplayBookTitle, getDisplayCardNumber, getDisplayReaderName } from '@/utils/helpers'

const libStore = useLibrarianStore()
const filter = ref('all')
const actionId = ref(null)
const reasonDialog = ref(false)
const reasonBusy = ref(false)
const reasonTitle = ref('')
const reasonMessage = ref('')
const reasonValue = ref('')
const reasonAction = ref('')
const reasonTarget = ref(null)
const returnDialog = ref(false)
const returning = ref(false)
const selectedTransaction = ref(null)
const returnForm = reactive({ condition: 'Good', conditionNote: '' })

const headers = [
  { title: 'Độc giả', key: 'reader', width: '220px', sortable: false },
  { title: 'Sách', key: 'book', width: '280px', sortable: false },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: '140px' },
  { title: 'Hạn trả', key: 'DueAt', width: '140px' },
  { title: 'Trạng thái', key: 'Status', width: '180px' },
  { title: 'Hành động', key: 'actions', width: '320px', sortable: false }
]

const filteredTransactions = computed(() => {
  if (filter.value === 'all') return libStore.transactions
  return libStore.transactions.filter(tx => statusOf(tx) === filter.value)
})

const pendingCount = computed(() => libStore.transactions.filter(tx => statusOf(tx) === 'Pending').length)
const borrowedCount = computed(() => libStore.transactions.filter(tx => statusOf(tx) === 'Borrowed').length)
const returnPendingCount = computed(() => libStore.transactions.filter(tx => statusOf(tx) === 'ReturnPending').length)
const overdueCount = computed(() => libStore.transactions.filter(tx => statusOf(tx) === 'Overdue').length)

function statusOf(item = {}) {
  const status = String(item.Status || item.status || '').trim()
  if (status === 'Pending' || status === 'Borrowed' || status === 'ReturnPending' || status === 'Returned' || status === 'Overdue') {
    return status
  }
  if (item.ReturnedAt || item.returnedAt) return 'Returned'
  if (item.DueAt && new Date(item.DueAt) < new Date()) return 'Overdue'
  return 'Borrowed'
}

function isPending(item) { return statusOf(item) === 'Pending' }
function isBorrowed(item) { return statusOf(item) === 'Borrowed' || statusOf(item) === 'Overdue' }
function isOverdue(item) { return statusOf(item) === 'Overdue' }
function isReturned(item) { return statusOf(item) === 'Returned' }
function isReturnPending(item) { return statusOf(item) === 'ReturnPending' }

function actionKey(item, suffix) {
  return `${item.Id || item.id || 'row'}:${suffix}`
}

function displayReader(item = {}) {
  return getDisplayReaderName(item, 'Độc giả')
}

function displayCard(item = {}) {
  return getDisplayCardNumber(item, item.CardNumber || item.cardNumber || '—')
}

function displayBook(item = {}) {
  return getDisplayBookTitle(item, item.BookId || item.bookId || '—')
}

function statusColor(item) {
  const status = statusOf(item)
  if (status === 'Pending') return 'warning'
  if (status === 'Borrowed') return 'info'
  if (status === 'ReturnPending') return 'purple'
  if (status === 'Overdue') return 'error'
  if (status === 'Returned') return 'success'
  return 'grey'
}

function statusLabel(item) {
  const status = statusOf(item)
  if (status === 'Pending') return 'Chờ duyệt'
  if (status === 'Borrowed') return 'Đang mượn'
  if (status === 'ReturnPending') return 'Chờ trả'
  if (status === 'Overdue') return 'Quá hạn'
  if (status === 'Returned') return 'Đã trả'
  return 'Đang xử lý'
}

function formatDurationText(from, to = new Date()) {
  const start = new Date(from)
  const end = new Date(to)
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return '—'
  const diff = Math.max(0, Math.abs(end.getTime() - start.getTime()))
  const days = Math.floor(diff / 86400000)
  const hours = Math.floor((diff % 86400000) / 3600000)
  const minutes = Math.floor((diff % 3600000) / 60000)
  const seconds = Math.floor((diff % 60000) / 1000)
  return `${days} ngày ${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

function statusDetail(item = {}) {
  const borrowedAt = item.BorrowedAt || item.borrowedAt
  const dueAt = item.DueAt || item.dueAt
  const returnedAt = item.ReturnedAt || item.returnedAt
  const now = new Date()

  if (isPending(item)) return 'Chưa bắt đầu tính thời gian mượn'
  if (isReturnPending(item)) return borrowedAt ? `Đã mượn: ${formatDurationText(borrowedAt, now)}` : 'Đang chờ trả'
  if (isReturned(item)) {
    if (borrowedAt && returnedAt) return `Đã trả sau: ${formatDurationText(borrowedAt, returnedAt)}`
    return 'Đã trả'
  }
  if (!dueAt) return '—'
  if (isOverdue(item)) return `Quá hạn: ${formatDurationText(dueAt, now)}`
  return `Còn lại: ${formatDurationText(now, dueAt)}`
}

async function approve(item) {
  actionId.value = actionKey(item, 'approve')
  try {
    const r = await libStore.approveTransaction(item.Id || item.id)
    if (!r.ok) {
      console.error('Duyệt mượn thất bại')
    }
  } finally {
    actionId.value = null
  }
}

async function rejectBorrow(item) {
  openReasonDialog({
    action: 'reject-borrow',
    target: item.Id || item.id,
    title: 'Từ chối mượn',
    message: `Độc giả ${displayReader(item)} · ${displayCard(item)} · ${displayBook(item)}`,
    defaultReason: 'Không đủ điều kiện mượn sách'
  })
}

async function renew(item) {
  openReasonDialog({
    action: 'renew',
    target: item.Id || item.id,
    title: 'Duyệt gia hạn',
    message: `Độc giả ${displayReader(item)} · ${displayCard(item)} · ${displayBook(item)}`,
    defaultReason: 'Gia hạn theo đề nghị độc giả'
  })
}

async function rejectRenewal(item) {
  openReasonDialog({
    action: 'reject-renew',
    target: item.Id || item.id,
    title: 'Từ chối gia hạn',
    message: `Độc giả ${displayReader(item)} · ${displayCard(item)} · ${displayBook(item)}`,
    defaultReason: 'Không đủ điều kiện gia hạn'
  })
}

function openReturnDialog(item) {
  selectedTransaction.value = item
  returnForm.condition = 'Good'
  returnForm.conditionNote = ''
  returnDialog.value = true
}

async function confirmReturn() {
  if (!selectedTransaction.value) return
  returning.value = true
  actionId.value = actionKey(selectedTransaction.value, 'return-approve')
  try {
    await libStore.approveReturn(selectedTransaction.value.Id || selectedTransaction.value.id, returnForm)
    returnDialog.value = false
    selectedTransaction.value = null
  } finally {
    actionId.value = null
    returning.value = false
  }
}

async function rejectReturn(item) {
  openReasonDialog({
    action: 'reject-return',
    target: item.Id || item.id,
    title: 'Từ chối trả sách',
    message: `Độc giả ${displayReader(item)} · ${displayCard(item)} · ${displayBook(item)}`,
    defaultReason: 'Không đủ điều kiện trả sách'
  })
}

function openReasonDialog({ action, target, title, message, defaultReason }) {
  reasonAction.value = action
  reasonTarget.value = target
  reasonTitle.value = title
  reasonMessage.value = message
  reasonValue.value = defaultReason
  reasonDialog.value = true
}

function closeReasonDialog() {
  reasonDialog.value = false
  reasonBusy.value = false
  reasonAction.value = ''
  reasonTarget.value = null
}

async function confirmReason() {
  const id = reasonTarget.value
  const reason = String(reasonValue.value || '').trim()
  if (!id || !reason) return

  reasonBusy.value = true
  try {
    if (reasonAction.value === 'reject-borrow') {
      actionId.value = actionKey({ Id: id }, 'reject')
      await libStore.rejectTransaction(id, reason)
    } else if (reasonAction.value === 'renew') {
      actionId.value = actionKey({ Id: id }, 'renew')
      await libStore.renewTransaction(id, 7, reason)
    } else if (reasonAction.value === 'reject-renew') {
      actionId.value = actionKey({ Id: id }, 'reject-renew')
      await libStore.rejectRenew(id, reason)
    } else if (reasonAction.value === 'reject-return') {
      actionId.value = actionKey({ Id: id }, 'return-reject')
      await libStore.rejectReturn(id, reason)
    }
    closeReasonDialog()
  } finally {
    actionId.value = null
    reasonBusy.value = false
  }
}

onMounted(() => libStore.loadAll())
</script>
