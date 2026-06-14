<template>
  <div>
    <a-row :gutter="16" class="mb-4">
      <a-col :span="8">
        <a-card>
          <a-statistic title="Tổng chưa thu" :value="store.totalUnpaid" :precision="0" suffix="đ" :value-style="{ color: '#cf1322', fontWeight: 800 }" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card>
          <a-statistic title="Số khoản chưa thu" :value="store.unpaidFines.length" :value-style="{ color: '#d97706' }" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card>
          <a-statistic title="Đã thu" :value="paidCount" :value-style="{ color: '#389e0d' }" />
        </a-card>
      </a-col>
    </a-row>

    <a-card title="Danh sách phí phạt" :body-style="{ padding: 0 }">
      <a-table
        :columns="columns"
        :data-source="store.fines"
        :loading="store.loading"
        :pagination="{ pageSize: 10 }"
        size="middle"
        row-key="Id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'reader'">
            <div>
              <div class="font-medium">{{ fineReaderName(record) }}</div>
              <div class="muted">{{ record.CardNumber || record.cardNumber || '—' }}</div>
            </div>
          </template>
          <template v-else-if="column.key === 'Amount'">
            <span class="money">{{ Number(record.Amount || record.amount || 0).toLocaleString() }} đ</span>
          </template>
          <template v-else-if="column.key === 'CreatedAt'">{{ fmtDate(record.CreatedAt || record.createdAt) }}</template>
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
import { ref, computed } from 'vue'
import { message } from 'ant-design-vue'
import { useLibrarianStore } from '@/stores/librarian'
import { CheckOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'

const store = useLibrarianStore()
const payingId = ref(null)
const paidCount = computed(() => store.paidFines.length)

const columns = [
  { title: 'Độc giả', key: 'reader', width: 220 },
  { title: 'Số tiền', key: 'Amount', width: 130 },
  { title: 'Lý do', dataIndex: 'Reason', key: 'Reason', width: 240 },
  { title: 'Ngày tạo', key: 'CreatedAt', width: 130 },
  { title: 'Trạng thái', key: 'IsPaid', width: 110 },
  { title: '', key: 'actions', width: 110 }
]

async function markPaid(record) {
  const id = record.Id || record.id
  payingId.value = id
  const response = await store.payFine(id)
  if (response.ok) message.success('Đã đánh dấu thu.')
  else message.error('Không thu được phí.')
  payingId.value = null
}

function fineReaderName(record = {}) {
  const card = record.CardNumber || record.cardNumber || ''
  const transaction = store.transactions.find(item => store.cardNumberOf(item) === card)
  return transaction ? store.readerNameOf(transaction) : card || '—'
}

function fmtDate(date) {
  return date ? dayjs(date).format('DD/MM/YYYY HH:mm:ss') : '—'
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
.money {
  font-weight: 700;
  color: #cf1322;
}
</style>
