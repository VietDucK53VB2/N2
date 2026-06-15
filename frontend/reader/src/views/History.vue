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
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useAppStore } from '@/stores/app'
import { titleColor, formatDate } from '@/utils/helpers'

const store = useAppStore()
const loading = ref(false)

const headers = [
  { title: 'Tên sách', key: 'book', sortable: false, width: '280px' },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: '120px' },
  { title: 'Hạn trả', key: 'DueAt', width: '120px' },
  { title: 'Ngày trả', key: 'ReturnedAt', width: '120px' },
  { title: 'Trạng thái', key: 'Status', width: '120px' }
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

const latestRejectLabel = computed(() => {
  const item = notifications.value.find(n => n.type === 'error')
  return item ? 'Có' : 'Không'
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

onMounted(async () => {
  loading.value = true
  await store.loadAll()
  loading.value = false
})
</script>

