<template>
  <div class="history-page">
    <div class="mb-6">
      <h2 class="text-h5 font-weight-black">Lịch sử mượn trả</h2>
      <p class="text-body-2 text-grey">Toàn bộ giao dịch và thông báo liên quan đến tài khoản của bạn</p>
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="4">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <p class="text-caption text-grey font-weight-bold">Tổng giao dịch</p>
          <p class="text-h5 font-weight-black">{{ store.myTransactions.length }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <p class="text-caption text-grey font-weight-bold">Thông báo mới</p>
          <p class="text-h5 font-weight-black text-primary">{{ notifications.length }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <p class="text-caption text-grey font-weight-bold">Từ chối gần nhất</p>
          <p class="text-h5 font-weight-black text-error">{{ latestRejectLabel }}</p>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1" class="mb-5">
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
          <div class="text-caption text-grey mt-1">{{ formatDate(item.createdAt) }}</div>
        </v-alert>
        <v-alert v-if="!notifications.length" type="info" variant="tonal">
          Chưa có thông báo nào.
        </v-alert>
      </v-card-text>
    </v-card>

    <v-card rounded="xl" elevation="1" class="mb-5">
      <v-card-title class="d-flex align-center justify-space-between">
        <span class="font-weight-bold">Lịch sử thao tác</span>
        <v-chip size="small" variant="tonal">{{ activityEntries.length }} mục</v-chip>
      </v-card-title>
      <v-card-text class="pt-0">
        <v-list lines="two" density="comfortable">
          <v-list-item
            v-for="item in activityEntries"
            :key="item.id"
            rounded="lg"
            class="mb-2"
          >
            <template #prepend>
              <v-avatar :color="item.color" size="36" variant="tonal" class="mr-3">
                <v-icon :icon="item.icon" size="18" />
              </v-avatar>
            </template>
            <v-list-item-title class="font-weight-bold">{{ item.title }}</v-list-item-title>
            <v-list-item-subtitle>{{ item.message }}</v-list-item-subtitle>
            <template #append>
              <span class="text-caption text-grey">{{ formatDateTime(item.createdAt) }}</span>
            </template>
          </v-list-item>
          <v-alert v-if="!activityEntries.length" type="info" variant="tonal">
            Chưa có lịch sử thao tác nào.
          </v-alert>
        </v-list>
      </v-card-text>
    </v-card>

    <v-card rounded="xl" elevation="1">
      <v-card-title class="d-flex align-center justify-space-between">
        <span class="font-weight-bold">Lịch sử giao dịch</span>
        <v-chip size="small" variant="tonal">{{ store.myTransactions.length }} phiếu mượn</v-chip>
      </v-card-title>
      <v-data-table
        :headers="headers"
        :items="store.myTransactions"
        :loading="loading"
        :items-per-page="10"
        class="history-table"
      >
        <template #item.book="{ item }">
          <div class="d-flex align-center ga-3 py-2">
            <v-avatar size="40" :color="titleColor(item.TenSach || item.tenSach)" rounded="lg">
              <v-icon color="white" size="20">mdi-book-open-variant</v-icon>
            </v-avatar>
            <div>
              <p class="text-body-2 font-weight-bold">{{ item.TenSach || item.tenSach || '—' }}</p>
              <p class="text-caption text-grey">{{ item.TacGia || item.tacGia || '—' }}</p>
            </div>
          </div>
        </template>
        <template #item.BorrowedAt="{ item }">{{ formatDate(item.BorrowedAt) }}</template>
        <template #item.DueAt="{ item }">{{ formatDate(item.DueAt) }}</template>
        <template #item.ReturnedAt="{ item }">{{ item.ReturnedAt ? formatDate(item.ReturnedAt) : '—' }}</template>
        <template #item.Status="{ item }">
          <v-chip size="small" :color="getStatusColor(item)" variant="flat">
            {{ getStatusLabel(item) }}
          </v-chip>
        </template>
      </v-data-table>
    </v-card>

    <v-card rounded="xl" elevation="1" class="mt-5">
      <v-card-title class="d-flex align-center justify-space-between">
        <span class="font-weight-bold">Lịch sử phí phạt</span>
        <v-chip size="small" variant="tonal">{{ store.myFines.length }} khoản</v-chip>
      </v-card-title>
      <v-card-text class="pt-0">
        <v-btn-toggle
          v-model="fineFilter"
          mandatory
          density="comfortable"
          variant="outlined"
          class="mb-4 flex-wrap"
        >
          <v-btn value="all">Tất cả</v-btn>
          <v-btn value="unpaid">Chưa thanh toán</v-btn>
          <v-btn value="pending">Chờ duyệt</v-btn>
          <v-btn value="paid">Đã thanh toán</v-btn>
        </v-btn-toggle>

        <v-data-table
          :headers="fineHeaders"
          :items="filteredFines"
          :items-per-page="10"
          class="history-table"
        >
          <template #item.reason="{ item }">
            <div>
              <p class="text-body-2 font-weight-bold">{{ formatFineReason(item.Reason || item.reason || '') }}</p>
              <p class="text-caption text-grey">Book ID: {{ item.BookId || item.bookId || '—' }}</p>
            </div>
          </template>
          <template #item.amount="{ item }">{{ formatMoney(item.Amount || item.amount || 0) }}</template>
          <template #item.CreatedAt="{ item }">{{ formatDate(item.CreatedAt || item.createdAt) }}</template>
          <template #item.PaymentRequestedAt="{ item }">{{ item.PaymentRequestedAt || item.paymentRequestedAt ? formatDate(item.PaymentRequestedAt || item.paymentRequestedAt) : '—' }}</template>
          <template #item.PaidAt="{ item }">{{ item.PaidAt || item.paidAt ? formatDate(item.PaidAt || item.paidAt) : '—' }}</template>
          <template #item.status="{ item }">
            <v-chip size="small" :color="fineStatusColor(item)" variant="flat">
              {{ fineStatusLabel(item) }}
            </v-chip>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useAppStore } from '@/stores/app'
import { titleColor, formatDate, formatDateTime, formatMoney } from '@/utils/helpers'

const store = useAppStore()
const loading = ref(false)
const fineFilter = ref('all')

const headers = [
  { title: 'Tên sách', key: 'book', sortable: false, width: '280px' },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: '120px' },
  { title: 'Hạn trả', key: 'DueAt', width: '120px' },
  { title: 'Ngày trả', key: 'ReturnedAt', width: '120px' },
  { title: 'Trạng thái', key: 'Status', width: '120px' }
]

const fineHeaders = [
  { title: 'Lý do', key: 'reason', sortable: false, width: '280px' },
  { title: 'Số tiền', key: 'amount', width: '120px' },
  { title: 'Ngày tạo', key: 'CreatedAt', width: '130px' },
  { title: 'Yêu cầu lúc', key: 'PaymentRequestedAt', width: '130px' },
  { title: 'Đã thanh toán lúc', key: 'PaidAt', width: '130px' },
  { title: 'Trạng thái', key: 'status', width: '130px' }
]

const notifications = computed(() => {
  return (store.events || [])
    .map(e => {
      const payload = e.payload || {}
      const eventType = String(e.eventType || '').toLowerCase()
      const title = payload.Title || payload.title || eventType || 'Thông báo'
      const message = payload.Message || payload.message || ''
      const createdAt = payload.CreatedAt || payload.createdAt || e.publishedAt
      const isReject = eventType.includes('rejected')
      const isRenew = eventType.includes('renewed')
      const isReturn = eventType.includes('returned')
      return {
        id: e.id,
        title,
        message,
        createdAt,
        type: isReject ? 'error' : isRenew ? 'warning' : isReturn ? 'success' : 'info'
      }
    })
    .filter(x => x.message || x.title)
    .slice(0, 20)
})

const activityEntries = computed(() => {
  return (store.events || [])
    .map((item, index) => {
      const payload = item.payload || {}
      const eventType = String(item.eventType || '').toLowerCase()
      const title = payload.Title || payload.title || eventType || 'Thao tác'
      const message = payload.Message || payload.message || payload.Reason || payload.reason || 'Đã lưu lịch sử'
      const createdAt = payload.CreatedAt || payload.createdAt || item.publishedAt
      const isDanger = eventType.includes('cancel') || eventType.includes('reject') || eventType.includes('rejected') || eventType.includes('remove')
      const isWarning = eventType.includes('renew') || eventType.includes('request') || eventType.includes('pending')
      const isSuccess = eventType.includes('approved') || eventType.includes('borrowed') || eventType.includes('returned') || eventType.includes('paid')
      return {
        id: `${item.id || index}`,
        title,
        message,
        createdAt,
        icon: isDanger ? 'mdi-close-circle-outline' : isWarning ? 'mdi-clock-outline' : isSuccess ? 'mdi-check-circle-outline' : 'mdi-history',
        color: isDanger ? 'error' : isWarning ? 'warning' : isSuccess ? 'success' : 'info'
      }
    })
    .filter(x => x.message || x.title)
    .slice(0, 40)
})

const latestRejectLabel = computed(() => {
  const item = notifications.value.find(n => n.type === 'error')
  return item ? 'Có' : 'Không'
})

const filteredFines = computed(() => {
  const items = store.myFines || []
  if (fineFilter.value === 'paid') return items.filter(isFinePaid)
  if (fineFilter.value === 'pending') return items.filter(isFinePending)
  if (fineFilter.value === 'unpaid') return items.filter(item => !isFinePaid(item) && !isFinePending(item))
  return items
})

function getStatusColor(tx) {
  if (store.isOverdue(tx)) return 'error'
  if (store.isReturned(tx)) return 'success'
  const status = store.statusOf(tx)
  if (status === 'Overdue') return 'error'
  if (status === 'Returned') return 'success'
  return 'info'
}

function getStatusLabel(tx) {
  if (store.isOverdue(tx)) return 'Quá hạn'
  if (store.isReturned(tx)) return 'Đã trả'
  const status = store.statusOf(tx)
  if (status === 'Overdue') return 'Quá hạn'
  if (status === 'Returned') return 'Đã trả'
  if (status === 'ReturnPending') return 'Chờ trả'
  if (status === 'Pending') return 'Chờ duyệt'
  return 'Đang mượn'
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

function fineStatusLabel(item = {}) {
  if (isFinePaid(item)) return 'Đã thanh toán'
  if (isFinePending(item)) return 'Chờ duyệt'
  return 'Chưa yêu cầu'
}

function fineStatusColor(item = {}) {
  if (isFinePaid(item)) return 'success'
  if (isFinePending(item)) return 'warning'
  return 'grey'
}

function formatFineReason(reason = '') {
  const text = String(reason || '').trim()
  if (!text) return '—'
  const overdueMatch = text.match(/Overdue return by (\d+) day\(s\)/i)
  if (overdueMatch) return `Phạt trả quá hạn ${overdueMatch[1]} ngày`
  if (/lost book fee/i.test(text)) return 'Phí mất sách'
  return text
}

onMounted(async () => {
  loading.value = true
  await store.loadAll()
  loading.value = false
})
</script>

