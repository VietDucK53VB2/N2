<template>
  <div>
    <div class="mb-6">
      <h2 class="text-h5 font-weight-black">Lịch sử mượn trả</h2>
      <p class="text-body-2 text-grey">Toàn bộ giao dịch mượn sách</p>
    </div>

    <!-- Stats -->
    <v-row class="mb-6">
      <v-col cols="12" md="4">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <div class="d-flex align-center ga-4">
            <v-avatar size="44" color="primary" variant="tonal">
              <v-icon>mdi-book</v-icon>
            </v-avatar>
            <div>
              <p class="text-caption text-grey font-weight-bold">Tổng sách mượn</p>
              <p class="text-h5 font-weight-black">{{ store.myTransactions.length }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <div class="d-flex align-center ga-4">
            <v-avatar size="44" color="success" variant="tonal">
              <v-icon>mdi-check-circle</v-icon>
            </v-avatar>
            <div>
              <p class="text-caption text-grey font-weight-bold">Trả đúng hạn</p>
              <p class="text-h5 font-weight-black text-success">{{ onTimeCount }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
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

    <!-- Data Table -->
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
          {{ formatDate(item.BorrowedAt) }}
        </template>
        <template #item.DueAt="{ item }">
          {{ formatDate(item.DueAt) }}
        </template>
        <template #item.ReturnedAt="{ item }">
          {{ item.ReturnedAt ? formatDate(item.ReturnedAt) : '—' }}
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
import { ref, computed, onMounted } from 'vue'
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

const onTimeCount = computed(() =>
  store.myTransactions.filter(t => {
    const returnedAt = t.ReturnedAt || t.returnedAt
    const dueAt = t.DueAt || t.dueAt
    return store.isReturned(t) && returnedAt && dueAt && new Date(returnedAt) <= new Date(dueAt)
  }).length
)

function getStatusColor(tx) {
  if (store.isOverdue(tx)) return 'error'
  if (store.isReturned(tx)) return 'success'
  const status = store.statusOf(tx)
  if (status === 'Overdue') return 'error'
  if (status === 'Returned') return 'success'
  return 'info'
}
function getStatusLabel(tx) {
  if (store.isOverdue(tx)) return 'Qu\u00e1 h\u1ea1n'
  if (store.isReturned(tx)) return '\u0110\u00e3 tr\u1ea3'
  const status = store.statusOf(tx)
  if (status === 'Overdue') return 'Quá hạn'
  if (status === 'Returned') return 'Đã trả'
  return 'Đang mượn'
}

onMounted(async () => {
  loading.value = true
  await store.loadMyTransactions()
  loading.value = false
})
</script>
