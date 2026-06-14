<template>
  <div>
    <div class="mb-6">
      <h2 class="text-h5 font-weight-black">Lịch sử mượn trả</h2>
      <p class="text-body-2 text-grey">Toàn bộ yêu cầu mượn, trả và trạng thái xử lý sách.</p>
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="3" sm="6">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <div class="d-flex align-center ga-4">
            <v-avatar size="44" color="primary" variant="tonal">
              <v-icon>mdi-book</v-icon>
            </v-avatar>
            <div>
              <p class="text-caption text-grey font-weight-bold">Tổng phiếu</p>
              <p class="text-h5 font-weight-black">{{ store.myTransactions.length }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3" sm="6">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <div class="d-flex align-center ga-4">
            <v-avatar size="44" color="warning" variant="tonal">
              <v-icon>mdi-clock-outline</v-icon>
            </v-avatar>
            <div>
              <p class="text-caption text-grey font-weight-bold">Chờ duyệt</p>
              <p class="text-h5 font-weight-black text-warning">{{ store.pendingTransactions.length }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3" sm="6">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <div class="d-flex align-center ga-4">
            <v-avatar size="44" color="success" variant="tonal">
              <v-icon>mdi-check-circle</v-icon>
            </v-avatar>
            <div>
              <p class="text-caption text-grey font-weight-bold">Đã trả đúng hạn</p>
              <p class="text-h5 font-weight-black text-success">{{ onTimeCount }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3" sm="6">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <div class="d-flex align-center ga-4">
            <v-avatar size="44" color="error" variant="tonal">
              <v-icon>mdi-alert</v-icon>
            </v-avatar>
            <div>
              <p class="text-caption text-grey font-weight-bold">Quá hạn</p>
              <p class="text-h5 font-weight-black text-error">{{ store.overdueTransactions.length }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <v-card-title class="d-flex align-center justify-space-between">
        <span class="font-weight-bold">Danh sách chi tiết</span>
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
            <v-avatar size="40" :color="titleColor(item.TenSach || item.tenSach)" variant="flat" rounded="lg">
              <v-icon color="white" size="20">mdi-book-open-variant</v-icon>
            </v-avatar>
            <div>
              <p class="text-body-2 font-weight-bold">{{ item.TenSach || item.tenSach || '—' }}</p>
              <p class="text-caption text-grey">{{ item.TacGia || item.tacGia || '—' }}</p>
            </div>
          </div>
        </template>

        <template #item.BorrowedAt="{ item }">
          <div>{{ formatDateTime(item.BorrowedAt || item.borrowedAt) }}</div>
          <div class="history-note">{{ borrowTimeLabel(item) }}</div>
        </template>

        <template #item.DueAt="{ item }">
          <div>{{ formatDateTime(item.DueAt || item.dueAt) }}</div>
          <div class="history-note">{{ dueTimeLabel(item) }}</div>
        </template>

        <template #item.ReturnedAt="{ item }">
          <div>{{ returnedAtOf(item) ? formatDateTime(returnedAtOf(item)) : '—' }}</div>
          <div class="history-note">{{ returnTimeLabel(item) }}</div>
        </template>

        <template #item.Status="{ item }">
          <v-chip size="small" :color="getStatusColor(item)" variant="flat">
            {{ getStatusLabel(item) }}
          </v-chip>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useAppStore } from '@/stores/app'
import { titleColor, formatDateTime, durationSince, timeUntil } from '@/utils/helpers'

const store = useAppStore()
const loading = ref(false)
const now = ref(Date.now())
let clockTimer = null

const headers = [
  { title: 'Tên sách', key: 'book', sortable: false, width: '280px' },
  { title: 'Thời điểm mượn', key: 'BorrowedAt', width: '180px' },
  { title: 'Hạn trả', key: 'DueAt', width: '180px' },
  { title: 'Thời điểm trả', key: 'ReturnedAt', width: '180px' },
  { title: 'Trạng thái', key: 'Status', width: '150px' }
]

const onTimeCount = computed(() =>
  store.myTransactions.filter(t => {
    const returnedAt = returnedAtOf(t)
    const dueAt = t.DueAt || t.dueAt
    return store.isReturned(t) && returnedAt && dueAt && new Date(returnedAt) <= new Date(dueAt)
  }).length
)

function returnedAtOf(tx) {
  return tx.ReturnedAt || tx.returnedAt || null
}

function borrowTimeLabel(tx) {
  now.value
  if (store.isPending(tx)) return 'Yêu cầu đang chờ duyệt'
  if (store.isReturned(tx) && returnedAtOf(tx)) return `Đã mượn: ${durationSince(tx.BorrowedAt || tx.borrowedAt, new Date(returnedAtOf(tx)).getTime())}`
  if (store.isReturnPending(tx)) return `Đã mượn: ${durationSince(tx.BorrowedAt || tx.borrowedAt)}`
  if (store.isBorrowed(tx) || store.isOverdue(tx)) return `Đã mượn: ${durationSince(tx.BorrowedAt || tx.borrowedAt)}`
  return '—'
}

function dueTimeLabel(tx) {
  now.value
  if (store.isPending(tx)) return 'Chưa bắt đầu tính hạn'
  if (store.isReturned(tx)) return 'Đã hoàn tất'
  if (store.isReturnPending(tx)) return 'Đang chờ thủ thư kiểm tra trả'
  return `Còn lại: ${timeUntil(tx.DueAt || tx.dueAt)}`
}

function returnTimeLabel(tx) {
  if (store.isReturnPending(tx)) return 'Đã gửi yêu cầu trả, chờ xác nhận'
  if (store.isReturned(tx)) return 'Thủ thư đã xác nhận trả'
  if (store.isPending(tx)) return 'Chưa được duyệt mượn'
  return 'Chưa trả'
}

function getStatusColor(tx) {
  if (store.isPending(tx)) return 'warning'
  if (store.isReturnPending(tx)) return 'deep-purple'
  if (store.isOverdue(tx)) return 'error'
  if (store.isReturned(tx)) return 'success'
  return 'info'
}

function getStatusLabel(tx) {
  if (store.isPending(tx)) return 'Chờ duyệt mượn'
  if (store.isReturnPending(tx)) return 'Chờ kiểm tra trả'
  if (store.isOverdue(tx)) return 'Quá hạn'
  if (store.isReturned(tx)) return 'Đã trả'
  return 'Đang mượn'
}

onMounted(async () => {
  clockTimer = window.setInterval(() => { now.value = Date.now() }, 1000)
  loading.value = true
  await store.loadBooks()
  await store.loadMyTransactions()
  loading.value = false
})

onBeforeUnmount(() => {
  if (clockTimer) window.clearInterval(clockTimer)
})
</script>

<style scoped>
.history-note {
  margin-top: 2px;
  font-size: 12px;
  color: #8a98b5;
  line-height: 1.35;
}
</style>
