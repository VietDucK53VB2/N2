<template>
  <div class="page-shell">
    <a-card class="hero-card">
      <div class="hero-copy">
        <div class="eyebrow">Tra cứu độc giả</div>
        <h2>Nhập mã thẻ để xem lịch sử mượn</h2>
        <p>Tra nhanh số phiếu, tình trạng quá hạn và các lượt đã trả của từng độc giả.</p>
      </div>

      <a-input-search
        v-model:value="cardNumber"
        placeholder="Nhập mã thẻ độc giả..."
        enter-button="Tìm kiếm"
        size="large"
        class="search-box"
        @search="search"
      />
    </a-card>

    <a-row v-if="result" :gutter="[16, 16]" class="summary-row">
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic title="Đang mượn" :value="result.active" :value-style="{ color: '#1f5f55' }" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic title="Quá hạn" :value="result.overdue" :value-style="{ color: '#dc2626' }" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic title="Đã trả" :value="result.returned" :value-style="{ color: '#059669' }" />
        </a-card>
      </a-col>
    </a-row>

    <a-card v-if="result" class="panel-card" :title="`Kết quả: ${result.cardNo}`">
      <a-table :columns="cols" :data-source="result.transactions" size="small" :pagination="{ pageSize: 6 }" row-key="Id">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'Status'">
            <a-tag :color="statusColor(record.Status)">{{ statusLabel(record.Status) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'BorrowedAt'">{{ fmtDateTime(record.BorrowedAt) }}</template>
          <template v-else-if="column.key === 'DueAt'">{{ fmtDateTime(record.DueAt) }}</template>
        </template>
      </a-table>
    </a-card>

    <a-card v-else class="panel-card" title="Danh sách độc giả">
      <a-list :data-source="readers" :loading="store.loading" item-layout="horizontal">
        <template #renderItem="{ item }">
          <a-list-item class="reader-item">
            <a-list-item-meta
              :title="item.cardNumber"
              :description="`${item.count} phiếu mượn · ${item.overdue > 0 ? item.overdue + ' quá hạn' : 'Bình thường'}`"
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
import { ref, computed, onMounted } from 'vue'
import dayjs from 'dayjs'
import { useLibrarianStore } from '@/stores/librarian'

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

function search() {
  searchByCard(cardNumber.value)
}

function searchByCard(c) {
  if (!String(c || '').trim()) return
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

function fmtDateTime(d) {
  return d ? dayjs(d).format('DD/MM/YYYY HH:mm:ss') : '—'
}

function statusColor(s) {
  return s === 'Pending' ? 'orange' : s === 'ReturnPending' ? 'purple' : s === 'Overdue' ? 'red' : s === 'Returned' ? 'green' : 'blue'
}

function statusLabel(s) {
  return s === 'Pending'
    ? 'Chờ duyệt'
    : s === 'ReturnPending'
      ? 'Chờ trả'
      : s === 'Overdue'
        ? 'Quá hạn'
        : s === 'Returned'
          ? 'Đã trả'
          : 'Đang mượn'
}

onMounted(() => {
  if (!store.transactions.length || !store.fines.length) {
    store.loadAll()
  }
})
</script>

<style scoped>
.page-shell {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.hero-card,
.panel-card,
.mini-card {
  border-radius: 18px !important;
  border: 1px solid #edf1ee !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04) !important;
}

.hero-card :deep(.ant-card-body) {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 20px 22px;
}

.hero-copy h2 {
  margin: 6px 0 6px;
  font-size: 26px;
  font-weight: 800;
  color: #103b35;
}

.hero-copy p {
  margin: 0;
  color: #7d8a83;
}

.eyebrow {
  color: #1f5f55;
  font-size: 12px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: .08em;
}

.search-box {
  max-width: 420px;
  width: 100%;
}

.summary-row {
  margin-bottom: 0;
}

.mini-card :deep(.ant-card-body) {
  padding: 18px 20px;
}

.reader-item {
  padding-inline: 0 !important;
}

@media (max-width: 992px) {
  .hero-card :deep(.ant-card-body) {
    flex-direction: column;
    align-items: stretch;
  }

  .search-box {
    max-width: none;
  }
}
</style>
