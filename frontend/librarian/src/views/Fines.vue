<template>
  <div class="page-shell">
    <a-row :gutter="[16, 16]" class="stats-row">
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic
            title="Tổng chưa thu"
            :value="store.totalUnpaid"
            :precision="0"
            suffix="đ"
            :value-style="{ color: '#dc2626', fontWeight: 800 }"
          />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic title="Số khoản chưa thu" :value="store.unpaidFines.length" :value-style="{ color: '#d97706' }" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic title="Đã thu" :value="paidCount" :value-style="{ color: '#059669' }" />
        </a-card>
      </a-col>
    </a-row>

    <a-card class="panel-card">
      <template #title>
        <div class="panel-head">
          <div>
            <div class="panel-title">Danh sách phí phạt</div>
            <div class="panel-subtitle">Theo dõi các khoản phí chờ xử lý và đã thanh toán</div>
          </div>
        </div>
      </template>

      <a-table
        :columns="columns"
        :data-source="store.fines"
        :loading="store.loading"
        :pagination="{ pageSize: 10 }"
        size="middle"
        row-key="Id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'Amount'">
            <span class="money">{{ Number(record.Amount || record.amount || 0).toLocaleString() }} đ</span>
          </template>
          <template v-else-if="column.key === 'CreatedAt'">{{ fmtDateTime(record.CreatedAt || record.createdAt) }}</template>
          <template v-else-if="column.key === 'IsPaid'">
            <a-tag :color="(record.IsPaid || record.isPaid) ? 'green' : 'red'">
              {{ (record.IsPaid || record.isPaid) ? 'Đã thu' : 'Chưa thu' }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-button
              v-if="!(record.IsPaid || record.isPaid)"
              type="primary"
              size="small"
              :loading="payingId === (record.Id || record.id)"
              @click="markPaid(record)"
            >
              <CheckOutlined /> Đã thu
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { useLibrarianStore } from '@/stores/librarian'
import { CheckOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'

const store = useLibrarianStore()
const payingId = ref(null)
const paidCount = computed(() => store.paidFines.length)

const columns = [
  { title: 'Mã thẻ', dataIndex: 'CardNumber', key: 'CardNumber', width: 140 },
  { title: 'Số tiền', key: 'Amount', width: 120 },
  { title: 'Lý do', dataIndex: 'Reason', key: 'Reason', width: 200 },
  { title: 'Ngày tạo', key: 'CreatedAt', width: 120 },
  { title: 'Trạng thái', key: 'IsPaid', width: 100 },
  { title: '', key: 'actions', width: 100 }
]

async function markPaid(r) {
  const id = r.Id || r.id
  payingId.value = id
  const res = await store.payFine(id)
  if (res.ok) message.success('Đã đánh dấu thu!')
  else message.error('Lỗi')
  payingId.value = null
}

function fmtDateTime(d) {
  return d ? dayjs(d).format('DD/MM/YYYY HH:mm:ss') : '—'
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

.stats-row {
  margin-bottom: 0;
}

.mini-card,
.panel-card {
  border-radius: 18px !important;
  border: 1px solid #edf1ee !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04) !important;
}

.mini-card :deep(.ant-card-body) {
  padding: 18px 20px;
}

.panel-card :deep(.ant-card-head) {
  border-bottom: none;
  padding: 18px 20px 0;
}

.panel-card :deep(.ant-card-body) {
  padding: 18px 20px 20px;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.panel-title {
  font-size: 16px;
  font-weight: 800;
  color: #103b35;
  letter-spacing: -0.01em;
}

.panel-subtitle {
  font-size: 12px;
  color: #8c98a5;
  margin-top: 2px;
}

.money {
  font-weight: 800;
  color: #dc2626;
}
</style>
