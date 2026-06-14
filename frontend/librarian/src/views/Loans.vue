<template>
  <a-card class="loans-card" :bordered="false">
    <template #title>
      <div class="card-title">
        <span>Danh sách phiếu mượn</span>
        <small>Theo dõi, duyệt mượn và xác nhận trả sách</small>
      </div>
    </template>
    <template #extra>
      <a-space>
        <a-badge :count="store.pendingTx.length" :number-style="{ backgroundColor: '#faad14' }" />
        <a-tag color="purple">{{ store.returnPendingTx.length }} chờ trả</a-tag>
      </a-space>
    </template>

    <div class="loan-stats">
      <div class="loan-stat waiting">
        <span>Chờ duyệt</span>
        <strong>{{ store.pendingTx.length }}</strong>
      </div>
      <div class="loan-stat borrowing">
        <span>Đang mượn</span>
        <strong>{{ store.borrowedTx.length }}</strong>
      </div>
      <div class="loan-stat returning">
        <span>Chờ trả</span>
        <strong>{{ store.returnPendingTx.length }}</strong>
      </div>
      <div class="loan-stat overdue">
        <span>Quá hạn</span>
        <strong>{{ store.overdueTx.length }}</strong>
      </div>
      <div class="loan-stat done">
        <span>Đã trả</span>
        <strong>{{ store.returnedTx.length }}</strong>
      </div>
    </div>

    <div class="loan-toolbar">
      <a-radio-group v-model:value="filter" button-style="solid" size="small">
        <a-radio-button value="all">Tất cả</a-radio-button>
        <a-radio-button value="Pending">Chờ duyệt</a-radio-button>
        <a-radio-button value="Borrowed">Đang mượn</a-radio-button>
        <a-radio-button value="ReturnPending">Chờ trả</a-radio-button>
        <a-radio-button value="Overdue">Quá hạn</a-radio-button>
        <a-radio-button value="Returned">Đã trả</a-radio-button>
      </a-radio-group>
      <div class="policy-control">
        <span>Giới hạn sách đang giữ</span>
        <a-input-number v-model:value="monthlyLimitDraft" :min="1" :max="100" size="small" />
        <a-button size="small" type="primary" :loading="savingPolicy" @click="saveMonthlyLimit">Lưu</a-button>
      </div>
      <a-button size="small" @click="store.loadAll()">Làm mới</a-button>
    </div>

    <a-table
      class="loans-table"
      :columns="columns"
      :data-source="filteredTx"
      :loading="store.loading"
      :pagination="{ pageSize: 10 }"
      size="middle"
      row-key="Id"
      :scroll="{ x: 1180 }"
      :custom-row="rowProps"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'reader'">
          <div>
            <div class="font-medium">{{ store.readerNameOf(record) }}</div>
            <div class="muted">{{ store.cardNumberOf(record) }}</div>
          </div>
        </template>

        <template v-else-if="column.key === 'book'">
          <div>
            <div class="font-medium">{{ store.bookTitleOf(record) }}</div>
            <div class="muted">
              <span v-if="store.bookAuthorOf(record)">{{ store.bookAuthorOf(record) }} · </span>
              Mã sách: {{ store.bookIdOf(record) }}
            </div>
          </div>
        </template>

        <template v-else-if="column.key === 'Status'">
          <a-tag :color="statusColor(record.Status)">{{ statusLabel(record.Status) }}</a-tag>
        </template>

        <template v-else-if="column.key === 'BorrowedAt'">
          {{ fmtDate(record.BorrowedAt || record.borrowedAt) }}
          <div v-if="isClockActive(record)" class="muted">Đã mượn: {{ borrowedDuration(record) }}</div>
          <div v-else-if="store.isReturned(record)" class="muted">Đã trả lúc: {{ fmtDate(returnedAtOf(record)) }}</div>
          <div v-else class="muted">Chưa bắt đầu tính</div>
        </template>

        <template v-else-if="column.key === 'DueAt'">
          {{ fmtDate(record.DueAt || record.dueAt) }}
          <div v-if="isClockActive(record)" class="muted">Còn lại: {{ dueCountdown(record) }}</div>
          <div v-else-if="store.isReturned(record)" class="muted">Đã hoàn tất</div>
          <div v-else class="muted">Chưa bắt đầu</div>
        </template>

        <template v-else-if="column.key === 'actions'">
          <a-space v-if="store.isPending(record)">
            <a-button type="primary" size="small" :loading="actionId === record.Id + 'a'" @click.stop="doApprove(record)">
              <CheckOutlined /> Duyệt mượn
            </a-button>
            <a-button danger size="small" :loading="actionId === record.Id + 'r'" @click.stop="doReject(record)">
              <CloseOutlined /> Từ chối
            </a-button>
          </a-space>

          <a-space v-else-if="store.isReturnPending(record)">
            <a-button type="primary" size="small" @click.stop="openConditionDialog(record)">
              <CheckOutlined /> Kiểm tra & trả
            </a-button>
            <a-button size="small" :loading="actionId === record.Id + 'rr'" @click.stop="doRejectReturn(record)">
              <CloseOutlined /> Chưa nhận
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="conditionDialog"
      title="Kiểm tra tình trạng sách"
      ok-text="Xác nhận trả"
      cancel-text="Hủy"
      :confirm-loading="confirming"
      @ok="doApproveReturn"
    >
      <div v-if="selectedReturn" class="condition-summary">
        <div class="font-medium">{{ store.bookTitleOf(selectedReturn) }}</div>
        <div class="muted">
          {{ store.readerNameOf(selectedReturn) }} · {{ store.cardNumberOf(selectedReturn) }} · Mã sách: {{ store.bookIdOf(selectedReturn) }}
        </div>
      </div>

      <a-form layout="vertical">
        <a-form-item label="Tình trạng sách khi nhận lại" required>
          <a-select v-model:value="conditionForm.condition">
            <a-select-option value="Good">Tốt, dùng bình thường</a-select-option>
            <a-select-option value="LightDamage">Hư nhẹ / bẩn nhẹ</a-select-option>
            <a-select-option value="HeavyDamage">Hư nặng / rách nhiều</a-select-option>
            <a-select-option value="Lost">Mất sách</a-select-option>
          </a-select>
        </a-form-item>

        <a-form-item label="Ghi chú kiểm tra">
          <a-textarea
            v-model:value="conditionForm.conditionNote"
            :rows="4"
            placeholder="Ví dụ: Bìa hơi cong, trang 24 có vết bẩn..."
            allow-clear
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal v-model:open="detailDialog" title="Chi tiết phiếu mượn" :footer="null" width="560px">
      <a-descriptions v-if="detailTx" bordered size="small" :column="1">
        <a-descriptions-item label="Người dùng">{{ store.readerNameOf(detailTx) }}</a-descriptions-item>
        <a-descriptions-item label="Mã thẻ">{{ store.cardNumberOf(detailTx) }}</a-descriptions-item>
        <a-descriptions-item label="Sách">{{ store.bookTitleOf(detailTx) }}</a-descriptions-item>
        <a-descriptions-item label="Mã sách">{{ store.bookIdOf(detailTx) }}</a-descriptions-item>
        <a-descriptions-item label="Ngày mượn">{{ fmtDate(detailTx.BorrowedAt || detailTx.borrowedAt) }}</a-descriptions-item>
        <a-descriptions-item label="Hạn trả">{{ fmtDate(detailTx.DueAt || detailTx.dueAt) }}</a-descriptions-item>
        <a-descriptions-item label="Thời gian đã mượn">{{ borrowedDuration(detailTx) }}</a-descriptions-item>
        <a-descriptions-item label="Thời gian còn lại">{{ dueCountdown(detailTx) }}</a-descriptions-item>
        <a-descriptions-item label="Trạng thái">{{ statusLabel(detailTx.Status) }}</a-descriptions-item>
      </a-descriptions>
      <div class="modal-footer">
        <a-button @click="detailDialog = false">Trở ra</a-button>
      </div>
    </a-modal>
  </a-card>
</template>

<script setup>
import { ref, computed, reactive, onMounted, onBeforeUnmount, watch } from 'vue'
import { message } from 'ant-design-vue'
import { useLibrarianStore } from '@/stores/librarian'
import { CheckOutlined, CloseOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'

const store = useLibrarianStore()
const filter = ref('all')
const actionId = ref(null)
const conditionDialog = ref(false)
const confirming = ref(false)
const selectedReturn = ref(null)
const detailDialog = ref(false)
const detailTx = ref(null)
const now = ref(Date.now())
const monthlyLimitDraft = ref(5)
const savingPolicy = ref(false)
let clockTimer = null
const conditionForm = reactive({
  condition: 'Good',
  conditionNote: ''
})
const timezonePattern = /(?:Z|[+-]\d{2}:?\d{2})$/i

const columns = [
  { title: 'Mã thẻ', dataIndex: 'CardNumber', key: 'CardNumber', width: 130 },
  { title: 'Tên người dùng', key: 'reader', width: 220 },
  { title: 'Sách', key: 'book', width: 280 },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: 120 },
  { title: 'Hạn trả', key: 'DueAt', width: 120 },
  { title: 'Trạng thái', key: 'Status', width: 150 },
  { title: 'Hành động', key: 'actions', width: 280 }
]

const filteredTx = computed(() => {
  if (filter.value === 'all') return store.transactions
  if (filter.value === 'Pending') return store.pendingTx
  if (filter.value === 'Borrowed') return store.borrowedTx
  if (filter.value === 'ReturnPending') return store.returnPendingTx
  if (filter.value === 'Overdue') return store.overdueTx
  if (filter.value === 'Returned') return store.returnedTx
  return store.transactions.filter(t => store.statusOf(t) === filter.value)
})

async function doApprove(record) {
  actionId.value = record.Id + 'a'
  const response = await store.approve(record.Id)
  if (response.ok) message.success('Đã duyệt mượn sách.')
  else message.error(await readError(response))
  actionId.value = null
}

async function saveMonthlyLimit() {
  savingPolicy.value = true
  const response = await store.saveBorrowPolicy(monthlyLimitDraft.value)
  if (response.ok) message.success('Đã lưu giới hạn mượn theo tháng.')
  else message.error(await readError(response))
  savingPolicy.value = false
}

watch(
  () => store.borrowPolicy.monthlyBorrowLimit,
  value => {
    monthlyLimitDraft.value = Number(value) || 5
  },
  { immediate: true }
)

async function doReject(record) {
  actionId.value = record.Id + 'r'
  const response = await store.reject(record.Id)
  if (response.ok) message.success('Đã từ chối yêu cầu mượn.')
  else message.error(await readError(response))
  actionId.value = null
}

function openConditionDialog(record) {
  selectedReturn.value = record
  conditionForm.condition = 'Good'
  conditionForm.conditionNote = ''
  conditionDialog.value = true
}

async function doApproveReturn() {
  if (!selectedReturn.value) return
  confirming.value = true
  actionId.value = selectedReturn.value.Id + 'ra'
  const response = await store.approveReturn(selectedReturn.value.Id, {
    condition: conditionForm.condition,
    conditionNote: conditionForm.conditionNote
  })
  if (response.ok) {
    message.success('Đã kiểm tra tình trạng và xác nhận trả sách.')
    conditionDialog.value = false
    selectedReturn.value = null
  } else {
    message.error(await readError(response))
  }
  actionId.value = null
  confirming.value = false
}

async function doRejectReturn(record) {
  actionId.value = record.Id + 'rr'
  const response = await store.rejectReturn(record.Id)
  if (response.ok) message.success('Đã chuyển lại trạng thái đang mượn.')
  else message.error(await readError(response))
  actionId.value = null
}

async function readError(response) {
  const data = await response.json().catch(() => null)
  return data?.Message || data?.message || 'Lỗi xử lý.'
}

function fmtDate(date) {
  const parsed = parseApiDate(date)
  return parsed ? dayjs(parsed).format('DD/MM/YYYY HH:mm:ss') : '�'
}

function parseApiDate(value) {
  if (!value) return null
  if (value instanceof Date) return value
  if (typeof value === 'number') return new Date(value)
  if (typeof value !== 'string') return new Date(value)
  const text = value.trim()
  if (!text) return null
  return new Date(timezonePattern.test(text) ? text : `${text}Z`)
}

function apiDateMs(value) {
  const date = parseApiDate(value)
  const ms = date?.getTime()
  return Number.isFinite(ms) ? ms : NaN
}

function rowProps(record) {
  return {
    class: 'clickable-row',
    onClick: () => openDetail(record)
  }
}

function openDetail(record) {
  detailTx.value = record
  detailDialog.value = true
}

function formatDurationMs(ms) {
  if (ms === null || ms === undefined || Number.isNaN(ms)) return '—'
  const overdue = ms < 0
  let totalSeconds = Math.abs(Math.floor(ms / 1000))
  const days = Math.floor(totalSeconds / 86400)
  totalSeconds %= 86400
  const hours = Math.floor(totalSeconds / 3600)
  totalSeconds %= 3600
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  const clock = [hours, minutes, seconds].map(v => String(v).padStart(2, '0')).join(':')
  const text = days > 0 ? `${days} ngày ${clock}` : clock
  return overdue ? `Quá hạn ${text}` : text
}

function borrowedDuration(record) {
  now.value
  const start = record?.BorrowedAt || record?.borrowedAt
  if (!start) return '—'
  const end = store.isReturned(record) ? returnedAtOf(record) : null
  const endTime = end ? apiDateMs(end) : Date.now()
  return formatDurationMs(endTime - apiDateMs(start))
}

function dueCountdown(record) {
  now.value
  if (!isClockActive(record)) return store.isReturned(record) ? 'Đã hoàn tất' : 'Chưa bắt đầu'
  const due = record?.DueAt || record?.dueAt
  if (!due) return '—'
  return formatDurationMs(apiDateMs(due) - Date.now())
}

function returnedAtOf(record) {
  return record?.ReturnedAt || record?.returnedAt || null
}

function isClockActive(record) {
  return store.isBorrowed(record) || store.isOverdue(record)
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
  if (status === 'ReturnPending') return 'Chờ kiểm tra trả'
  if (status === 'Overdue') return 'Quá hạn'
  if (status === 'Returned') return 'Đã trả'
  return 'Đang mượn'
}
onMounted(() => {
  clockTimer = window.setInterval(() => { now.value = Date.now() }, 1000)
})

onBeforeUnmount(() => {
  if (clockTimer) window.clearInterval(clockTimer)
})
</script>

<style scoped>
.loans-card {
  border-radius: 14px;
  box-shadow: 0 10px 30px rgba(15, 23, 42, 0.06);
  overflow: hidden;
}
.card-title {
  display: flex;
  flex-direction: column;
  gap: 2px;
  font-weight: 800;
  color: #0f172a;
}
.card-title small {
  font-size: 12px;
  font-weight: 500;
  color: #94a3b8;
}
.loan-stats {
  display: grid;
  grid-template-columns: repeat(5, minmax(120px, 1fr));
  gap: 12px;
  margin-bottom: 16px;
}
.loan-stat {
  border: 1px solid #edf2f7;
  border-radius: 12px;
  padding: 12px 14px;
  background: #fff;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.8);
}
.loan-stat span {
  display: block;
  font-size: 12px;
  font-weight: 700;
  color: #64748b;
  margin-bottom: 6px;
}
.loan-stat strong {
  display: block;
  font-size: 24px;
  line-height: 1;
  font-weight: 900;
}
.loan-stat.waiting { background: linear-gradient(135deg, #fff7ed, #fff); }
.loan-stat.waiting strong { color: #d97706; }
.loan-stat.borrowing { background: linear-gradient(135deg, #eff6ff, #fff); }
.loan-stat.borrowing strong { color: #2563eb; }
.loan-stat.returning { background: linear-gradient(135deg, #faf5ff, #fff); }
.loan-stat.returning strong { color: #9333ea; }
.loan-stat.overdue { background: linear-gradient(135deg, #fef2f2, #fff); }
.loan-stat.overdue strong { color: #dc2626; }
.loan-stat.done { background: linear-gradient(135deg, #ecfdf5, #fff); }
.loan-stat.done strong { color: #059669; }
.loan-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
  margin-bottom: 16px;
  padding: 10px;
  border-radius: 12px;
  background: #f8fafc;
  border: 1px solid #eef2f7;
}
.policy-control {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 4px 8px;
  border-radius: 10px;
  background: #fff;
  border: 1px solid #e5e7eb;
  color: #334155;
  font-size: 12px;
  font-weight: 700;
}
.font-medium { font-weight: 600; }
.muted {
  color: #94a3b8;
  font-size: 12px;
  margin-top: 2px;
}
.condition-summary {
  padding: 12px 14px;
  margin-bottom: 16px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #f8fafc;
}
.modal-footer {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
:deep(.clickable-row) {
  cursor: pointer;
}
:deep(.clickable-row:hover td) {
  background: #f0fdf4 !important;
}
:deep(.loans-table .ant-table) {
  border-radius: 12px;
  overflow: hidden;
}
:deep(.loans-table .ant-table-thead > tr > th) {
  background: #f8fafc !important;
  color: #334155;
  font-weight: 800;
}
:deep(.loans-table .ant-table-tbody > tr > td) {
  padding-top: 14px;
  padding-bottom: 14px;
}
:deep(.ant-radio-button-wrapper:first-child) {
  border-start-start-radius: 8px;
  border-end-start-radius: 8px;
}
:deep(.ant-radio-button-wrapper:last-child) {
  border-start-end-radius: 8px;
  border-end-end-radius: 8px;
}
@media (max-width: 1100px) {
  .loan-stats {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>


