<template>
  <div>
    <a-card class="mb-4">
      <a-input-search
        v-model:value="cardNumber"
        placeholder="Nhập mã thẻ độc giả để tra cứu..."
        enter-button="Tìm kiếm"
        size="large"
        @search="search"
      />
    </a-card>

    <a-card v-if="result" :title="`Kết quả: ${result.cardNo}`">
      <a-row :gutter="16" class="mb-4">
        <a-col :span="8"><a-statistic title="Đang mượn" :value="result.active" :value-style="{ color: '#1890ff' }" /></a-col>
        <a-col :span="8"><a-statistic title="Quá hạn" :value="result.overdue" :value-style="{ color: '#ff4d4f' }" /></a-col>
        <a-col :span="8"><a-statistic title="Đã trả" :value="result.returned" :value-style="{ color: '#52c41a' }" /></a-col>
      </a-row>
      <a-table :columns="cols" :data-source="result.transactions" size="small" :pagination="{ pageSize: 5 }" row-key="Id">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'Status'"><a-tag :color="statusColor(record.Status)">{{ statusLabel(record.Status) }}</a-tag></template>
          <template v-if="column.key === 'BorrowedAt'">{{ fmtDate(record.BorrowedAt) }}</template>
          <template v-if="column.key === 'DueAt'">{{ fmtDate(record.DueAt) }}</template>
        </template>
      </a-table>
    </a-card>

    <a-card v-if="!result" title="Danh sách Độc giả">
      <a-list :data-source="readers" :loading="store.loading">
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta :title="item.cardNumber" :description="`${item.count} phiếu mượn · ${item.overdue > 0 ? '⚠️ ' + item.overdue + ' quá hạn' : '✅ Bình thường'}`" />
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
  { title: 'Book ID', dataIndex: 'BookId', key: 'BookId' },
  { title: 'Ngày mượn', key: 'BorrowedAt' },
  { title: 'Hạn trả', key: 'DueAt' },
  { title: 'Trạng thái', key: 'Status' }
]

const readers = computed(() => {
  const map = {}
  store.transactions.forEach(t => {
    const c = store.cardNumberOf(t)
    if (!map[c]) map[c] = { cardNumber: c, count: 0, active: 0, pending: 0, overdue: 0, returned: 0 }
    map[c].count++
    if (store.isActiveLoan(t)) map[c].active++
    if (store.isPending(t)) map[c].pending++
    if (store.isOverdue(t)) map[c].overdue++
    if (store.isReturned(t)) map[c].returned++
  })
  return Object.values(map).sort((a, b) => b.overdue - a.overdue || b.active - a.active)
})

function search() { searchByCard(cardNumber.value) }
function searchByCard(c) {
  if (!c.trim()) return
  cardNumber.value = c
  const txs = store.transactions.filter(t => store.cardNumberOf(t) === c)
  result.value = {
    cardNo: c,
    transactions: txs,
    active: txs.filter(store.isActiveLoan).length,
    overdue: txs.filter(store.isOverdue).length,
    returned: txs.filter(store.isReturned).length
  }
}

function fmtDate(d) { return d ? dayjs(d).format('DD/MM/YYYY') : '—' }
function statusColor(s) { return s === 'Pending' ? 'orange' : s === 'ReturnPending' ? 'purple' : s === 'Overdue' ? 'red' : s === 'Returned' ? 'green' : 'blue' }
function statusLabel(s) { return s === 'Pending' ? 'Chờ duyệt' : s === 'ReturnPending' ? 'Chờ trả' : s === 'Overdue' ? 'Quá hạn' : s === 'Returned' ? 'Đã trả' : 'Đang mượn' }
</script>

<style scoped>
.mb-4 { margin-bottom: 16px; }
</style>
