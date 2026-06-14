<template>
  <div>
    <a-card class="mb-4">
      <a-input-search
        v-model:value="cardNumber"
        placeholder="Nhập mã thẻ hoặc tên độc giả để tra cứu..."
        enter-button="Tìm kiếm"
        size="large"
        @search="search"
      />
    </a-card>

    <a-card v-if="result" :title="`Kết quả: ${result.readerName}`">
      <p class="muted result-card">{{ result.cardNo }}</p>
      <a-row :gutter="16" class="mb-4">
        <a-col :span="8"><a-statistic title="Đang mượn" :value="result.active" :value-style="{ color: '#1890ff' }" /></a-col>
        <a-col :span="8"><a-statistic title="Quá hạn" :value="result.overdue" :value-style="{ color: '#ff4d4f' }" /></a-col>
        <a-col :span="8"><a-statistic title="Đã trả" :value="result.returned" :value-style="{ color: '#52c41a' }" /></a-col>
      </a-row>
      <a-table :columns="cols" :data-source="result.transactions" size="small" :pagination="{ pageSize: 5 }" row-key="Id">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'book'">
            <div>
              <div class="font-medium">{{ store.bookTitleOf(record) }}</div>
              <div class="muted">
                {{ store.bookAuthorOf(record) || `Mã sách: ${store.bookIdOf(record)}` }}
              </div>
            </div>
          </template>
          <template v-else-if="column.key === 'Status'">
            <a-tag :color="statusColor(record.Status)">{{ statusLabel(record.Status) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'BorrowedAt'">{{ fmtDate(record.BorrowedAt || record.borrowedAt) }}</template>
          <template v-else-if="column.key === 'DueAt'">{{ fmtDate(record.DueAt || record.dueAt) }}</template>
        </template>
      </a-table>
    </a-card>

    <a-card v-if="!result" title="Danh sách độc giả">
      <a-list :data-source="readers" :loading="store.loading">
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta
              :title="item.readerName"
              :description="`${item.cardNumber} · ${item.count} phiếu mượn · ${item.overdue > 0 ? item.overdue + ' quá hạn' : 'Bình thường'}`"
            />
            <template #actions>
              <a-button size="small" type="primary" ghost @click="searchByCard(item.cardNumber)">Xem</a-button>
            </template>
          </a-list-item>
        </template>
      </a-list>
    </a-card>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import dayjs from 'dayjs'

const store = useLibrarianStore()
const cardNumber = ref('')
const result = ref(null)

const cols = [
  { title: 'Sách', key: 'book' },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: 130 },
  { title: 'Hạn trả', key: 'DueAt', width: 130 },
  { title: 'Trạng thái', key: 'Status', width: 140 }
]

const readers = computed(() => {
  const map = {}
  store.transactions.forEach(transaction => {
    const card = store.cardNumberOf(transaction)
    if (!card) return
    if (!map[card]) {
      map[card] = {
        cardNumber: card,
        readerName: store.readerNameOf(transaction),
        count: 0,
        active: 0,
        pending: 0,
        overdue: 0,
        returned: 0
      }
    }
    map[card].count++
    if (store.isActiveLoan(transaction)) map[card].active++
    if (store.isPending(transaction)) map[card].pending++
    if (store.isOverdue(transaction)) map[card].overdue++
    if (store.isReturned(transaction)) map[card].returned++
  })
  return Object.values(map).sort((a, b) => b.overdue - a.overdue || b.active - a.active)
})

function search() {
  const q = cardNumber.value.trim().toLowerCase()
  if (!q) return
  const reader = readers.value.find(item =>
    item.cardNumber.toLowerCase() === q ||
    item.readerName.toLowerCase().includes(q)
  )
  if (reader) searchByCard(reader.cardNumber)
}

function searchByCard(card) {
  if (!card.trim()) return
  cardNumber.value = card
  const txs = store.transactions.filter(t => store.cardNumberOf(t) === card)
  const first = txs[0] || {}
  result.value = {
    cardNo: card,
    readerName: store.readerNameOf(first),
    transactions: txs,
    active: txs.filter(store.isActiveLoan).length,
    overdue: txs.filter(store.isOverdue).length,
    returned: txs.filter(store.isReturned).length
  }
}

function fmtDate(date) {
  return date ? dayjs(date).format('DD/MM/YYYY HH:mm:ss') : '—'
}

function statusColor(status) {
  if (status === 'Pending') return 'orange'
  if (status === 'ReturnPending') return 'purple'
  if (status === 'Overdue') return 'red'
  if (status === 'Returned') return 'green'
  return 'blue'
}

function statusLabel(status) {
  if (status === 'Pending') return 'Chờ duyệt'
  if (status === 'ReturnPending') return 'Chờ trả'
  if (status === 'Overdue') return 'Quá hạn'
  if (status === 'Returned') return 'Đã trả'
  return 'Đang mượn'
}
</script>

<style scoped>
.mb-4 { margin-bottom: 16px; }
.font-medium { font-weight: 600; }
.muted {
  color: #94a3b8;
  font-size: 12px;
  margin-top: 2px;
}
.result-card { margin-top: -8px; margin-bottom: 16px; }
</style>
