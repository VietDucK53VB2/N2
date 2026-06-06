<template>
  <div>
    <v-card rounded="xl" elevation="1">
      <v-card-title class="d-flex align-center justify-space-between">
        <span class="font-weight-bold">Danh sách Phiếu mượn</span>
        <v-chip color="warning" variant="tonal" size="small">{{ libStore.pendingCount }} chờ duyệt</v-chip>
      </v-card-title>

      <v-card-text class="pt-0">
        <v-chip-group v-model="filter" mandatory class="mb-4">
          <v-chip value="all" variant="outlined" filter>Tất cả</v-chip>
          <v-chip value="Pending" variant="outlined" filter color="warning">Chờ duyệt</v-chip>
          <v-chip value="Borrowed" variant="outlined" filter color="info">Đang mượn</v-chip>
          <v-chip value="Overdue" variant="outlined" filter color="error">Quá hạn</v-chip>
          <v-chip value="Returned" variant="outlined" filter color="success">Đã trả</v-chip>
        </v-chip-group>
      </v-card-text>

      <v-data-table
        :headers="headers"
        :items="filteredTransactions"
        :loading="libStore.loading"
        density="comfortable"
        :items-per-page="10"
      >
        <template #item.Status="{ item }">
          <v-chip size="small" :color="statusColor(item.Status)" variant="flat">
            {{ statusLabel(item.Status) }}
          </v-chip>
        </template>
        <template #item.BorrowedAt="{ item }">
          {{ formatDate(item.BorrowedAt) }}
        </template>
        <template #item.DueAt="{ item }">
          {{ formatDate(item.DueAt) }}
        </template>
        <template #item.actions="{ item }">
          <v-btn
            v-if="item.Status === 'Pending'"
            size="small" color="success" variant="flat" class="mr-1"
            :loading="actionId === item.Id + 'a'"
            @click="approve(item)"
          >
            <v-icon start>mdi-check</v-icon> Duyệt
          </v-btn>
          <v-btn
            v-if="item.Status === 'Pending'"
            size="small" color="error" variant="tonal"
            :loading="actionId === item.Id + 'r'"
            @click="reject(item)"
          >
            <v-icon start>mdi-close</v-icon> Từ chối
          </v-btn>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import { formatDate } from '@/utils/helpers'

const libStore = useLibrarianStore()
const filter = ref('all')
const actionId = ref(null)

const headers = [
  { title: 'Mã thẻ', key: 'CardNumber', width: '140px' },
  { title: 'Book ID', key: 'BookId', width: '100px' },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: '120px' },
  { title: 'Hạn trả', key: 'DueAt', width: '120px' },
  { title: 'Trạng thái', key: 'Status', width: '120px' },
  { title: 'Hành động', key: 'actions', width: '200px', sortable: false }
]

const filteredTransactions = computed(() => {
  if (filter.value === 'all') return libStore.transactions
  return libStore.transactions.filter(t => t.Status === filter.value)
})

function statusColor(s) {
  if (s === 'Pending') return 'warning'
  if (s === 'Overdue') return 'error'
  if (s === 'Returned') return 'success'
  return 'info'
}
function statusLabel(s) {
  if (s === 'Pending') return 'Chờ duyệt'
  if (s === 'Overdue') return 'Quá hạn'
  if (s === 'Returned') return 'Đã trả'
  return 'Đang mượn'
}

async function approve(item) {
  actionId.value = item.Id + 'a'
  await libStore.approveTransaction(item.Id)
  actionId.value = null
}

async function reject(item) {
  actionId.value = item.Id + 'r'
  await libStore.rejectTransaction(item.Id)
  actionId.value = null
}

onMounted(() => libStore.loadTransactions())
</script>
