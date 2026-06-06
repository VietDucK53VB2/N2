<template>
  <a-card title="Danh sách Phiếu mượn">
    <template #extra>
      <a-badge :count="store.pendingTx.length" :number-style="{ backgroundColor: '#faad14' }" />
    </template>

    <a-space class="mb-4">
      <a-radio-group v-model:value="filter" button-style="solid" size="small">
        <a-radio-button value="all">Tất cả</a-radio-button>
        <a-radio-button value="Pending">Chờ duyệt</a-radio-button>
        <a-radio-button value="Borrowed">Đang mượn</a-radio-button>
        <a-radio-button value="Overdue">Quá hạn</a-radio-button>
        <a-radio-button value="Returned">Đã trả</a-radio-button>
      </a-radio-group>
    </a-space>

    <a-table
      :columns="columns"
      :data-source="filteredTx"
      :loading="store.loading"
      :pagination="{ pageSize: 10 }"
      size="middle"
      row-key="Id"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'Status'">
          <a-tag :color="statusColor(record.Status)">{{ statusLabel(record.Status) }}</a-tag>
        </template>
        <template v-if="column.key === 'BorrowedAt'">{{ fmtDate(record.BorrowedAt) }}</template>
        <template v-if="column.key === 'DueAt'">{{ fmtDate(record.DueAt) }}</template>
        <template v-if="column.key === 'actions'">
          <a-space v-if="store.isPending(record)">
            <a-button type="primary" size="small" :loading="actionId === record.Id + 'a'" @click="doApprove(record)">
              <CheckOutlined /> Duyệt
            </a-button>
            <a-button danger size="small" :loading="actionId === record.Id + 'r'" @click="doReject(record)">
              <CloseOutlined /> Từ chối
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup>
import { ref, computed } from 'vue'
import { message } from 'ant-design-vue'
import { useLibrarianStore } from '@/stores/librarian'
import { CheckOutlined, CloseOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'

const store = useLibrarianStore()
const filter = ref('all')
const actionId = ref(null)

const columns = [
  { title: 'Mã thẻ', dataIndex: 'CardNumber', key: 'CardNumber', width: 140 },
  { title: 'Book ID', dataIndex: 'BookId', key: 'BookId', width: 100 },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: 120 },
  { title: 'Hạn trả', key: 'DueAt', width: 120 },
  { title: 'Trạng thái', key: 'Status', width: 110 },
  { title: 'Hành động', key: 'actions', width: 200 }
]

const filteredTx = computed(() => {
  if (filter.value === 'all') return store.transactions
  if (filter.value === 'Pending') return store.pendingTx
  if (filter.value === 'Borrowed') return store.borrowedTx
  if (filter.value === 'Overdue') return store.overdueTx
  if (filter.value === 'Returned') return store.returnedTx
  return store.transactions.filter(t => store.statusOf(t) === filter.value)
})

async function doApprove(r) { actionId.value = r.Id + 'a'; const res = await store.approve(r.Id); if (res.ok) message.success('Đã duyệt!'); else message.error('Lỗi'); actionId.value = null }
async function doReject(r) { actionId.value = r.Id + 'r'; const res = await store.reject(r.Id); if (res.ok) message.success('Đã từ chối!'); else message.error('Lỗi'); actionId.value = null }
function fmtDate(d) { return d ? dayjs(d).format('DD/MM/YYYY') : '—' }
function statusColor(s) { return s === 'Pending' ? 'orange' : s === 'Overdue' ? 'red' : s === 'Returned' ? 'green' : 'blue' }
function statusLabel(s) { return s === 'Pending' ? 'Chờ duyệt' : s === 'Overdue' ? 'Quá hạn' : s === 'Returned' ? 'Đã trả' : 'Đang mượn' }
</script>

<style scoped>
.mb-4 { margin-bottom: 16px; }
</style>
