<template>
  <a-card title="Xác nhận trả sách">
    <div class="return-toolbar">
      <a-input-search
        v-model:value="keyword"
        placeholder="Lọc theo mã thẻ, tên người dùng, tên sách..."
        allow-clear
        size="large"
        style="max-width: 460px"
      />
      <a-tag color="purple">{{ filteredLoans.length }} phiếu đang chờ trả</a-tag>
    </div>

    <a-table
      :columns="columns"
      :data-source="filteredLoans"
      :loading="store.loading"
      :pagination="{ pageSize: 8 }"
      row-key="returnKey"
      :custom-row="rowProps"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'reader'">
          <div class="reader-cell">
            <a-avatar class="reader-avatar">{{ store.readerNameOf(record).slice(0, 1) }}</a-avatar>
            <div>
              <div class="font-medium">{{ store.readerNameOf(record) }}</div>
              <div class="muted">{{ store.cardNumberOf(record) }}</div>
            </div>
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

        <template v-else-if="column.key === 'borrowedAt'">
          {{ fmtDate(record.BorrowedAt || record.borrowedAt) }}
          <div class="muted">Đã mượn: {{ borrowedDuration(record) }}</div>
        </template>

        <template v-else-if="column.key === 'dueAt'">
          <span :class="{ overdue: store.isOverdue(record) }">
            {{ fmtDate(record.DueAt || record.dueAt) }}
          </span>
          <div class="muted">Còn lại: {{ dueCountdown(record) }}</div>
        </template>

        <template v-else-if="column.key === 'status'">
          <a-tag color="purple">Chờ kiểm tra tình trạng</a-tag>
        </template>

        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="primary" size="small" @click.stop="openConditionDialog(record)">
              Đánh giá & xác nhận
            </a-button>
            <a-button size="small" :loading="actionId === record.Id + 'no'" @click.stop="reject(record)">
              Chưa nhận
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
      @ok="approveWithCondition"
    >
      <div v-if="selectedLoan" class="condition-summary">
        <div class="font-medium">{{ store.bookTitleOf(selectedLoan) }}</div>
        <div class="muted">
          {{ store.readerNameOf(selectedLoan) }} · {{ store.cardNumberOf(selectedLoan) }} · Mã sách: {{ store.bookIdOf(selectedLoan) }}
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

    <a-modal v-model:open="detailDialog" title="Chi tiết phiếu trả" :footer="null" width="560px">
      <a-descriptions v-if="detailLoan" bordered size="small" :column="1">
        <a-descriptions-item label="Người dùng">{{ store.readerNameOf(detailLoan) }}</a-descriptions-item>
        <a-descriptions-item label="Mã thẻ">{{ store.cardNumberOf(detailLoan) }}</a-descriptions-item>
        <a-descriptions-item label="Sách">{{ store.bookTitleOf(detailLoan) }}</a-descriptions-item>
        <a-descriptions-item label="Mã sách">{{ store.bookIdOf(detailLoan) }}</a-descriptions-item>
        <a-descriptions-item label="Ngày mượn">{{ fmtDate(detailLoan.BorrowedAt || detailLoan.borrowedAt) }}</a-descriptions-item>
        <a-descriptions-item label="Hạn trả">{{ fmtDate(detailLoan.DueAt || detailLoan.dueAt) }}</a-descriptions-item>
        <a-descriptions-item label="Thời gian đã mượn">{{ borrowedDuration(detailLoan) }}</a-descriptions-item>
        <a-descriptions-item label="Thời gian còn lại">{{ dueCountdown(detailLoan) }}</a-descriptions-item>
        <a-descriptions-item label="Trạng thái">Chờ kiểm tra tình trạng</a-descriptions-item>
      </a-descriptions>
      <div class="modal-footer">
        <a-button @click="detailDialog = false">Trở ra</a-button>
      </div>
    </a-modal>
  </a-card>
</template>

<script setup>
import { computed, reactive, ref, onMounted, onBeforeUnmount } from 'vue'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import { useLibrarianStore } from '@/stores/librarian'

const store = useLibrarianStore()
const keyword = ref('')
const actionId = ref(null)
const conditionDialog = ref(false)
const confirming = ref(false)
const selectedLoan = ref(null)
const detailDialog = ref(false)
const detailLoan = ref(null)
const now = ref(Date.now())
let clockTimer = null
const timezonePattern = /(?:Z|[+-]\d{2}:?\d{2})$/i
const conditionForm = reactive({
  condition: 'Good',
  conditionNote: ''
})

const columns = [
  { title: 'Độc giả', key: 'reader', width: 230 },
  { title: 'Sách', key: 'book' },
  { title: 'Ngày mượn', key: 'borrowedAt', width: 130 },
  { title: 'Hạn trả', key: 'dueAt', width: 130 },
  { title: 'Trạng thái', key: 'status', width: 180 },
  { title: '', key: 'action', width: 260 }
]

const returnableLoans = computed(() =>
  store.returnPendingTx.map((loan, index) => ({
    ...loan,
    returnKey: loan.Id || loan.id || `${store.cardNumberOf(loan)}-${store.bookIdOf(loan)}-${index}`
  }))
)

const filteredLoans = computed(() => {
  const q = keyword.value.trim().toLowerCase()
  if (!q) return returnableLoans.value
  return returnableLoans.value.filter(loan => {
    const haystack = [
      store.cardNumberOf(loan),
      store.readerNameOf(loan),
      store.readerUsernameOf(loan),
      store.bookIdOf(loan),
      store.bookTitleOf(loan),
      store.bookAuthorOf(loan),
      store.statusOf(loan)
    ].join(' ').toLowerCase()
    return haystack.includes(q)
  })
})

function openConditionDialog(record) {
  selectedLoan.value = record
  conditionForm.condition = 'Good'
  conditionForm.conditionNote = ''
  conditionDialog.value = true
}

function rowProps(record) {
  return {
    class: 'clickable-row',
    onClick: () => openDetail(record)
  }
}

function openDetail(record) {
  detailLoan.value = record
  detailDialog.value = true
}

async function approveWithCondition() {
  if (!selectedLoan.value) return
  confirming.value = true
  actionId.value = selectedLoan.value.Id + 'ok'
  const response = await store.approveReturn(selectedLoan.value.Id, {
    condition: conditionForm.condition,
    conditionNote: conditionForm.conditionNote
  })
  if (response.ok) {
    message.success('Đã kiểm tra tình trạng và xác nhận trả sách.')
    conditionDialog.value = false
    selectedLoan.value = null
  } else {
    message.error(await readError(response))
  }
  actionId.value = null
  confirming.value = false
}

async function reject(record) {
  actionId.value = record.Id + 'no'
  const response = await store.rejectReturn(record.Id)
  if (response.ok) message.success('Đã chuyển lại trạng thái đang mượn.')
  else message.error(await readError(response))
  actionId.value = null
}

async function readError(response) {
  const data = await response.json().catch(() => null)
  return data?.Message || data?.message || 'Không xử lý được phiếu trả.'
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
  return formatDurationMs(Date.now() - apiDateMs(start))
}

function dueCountdown(record) {
  now.value
  const due = record?.DueAt || record?.dueAt
  if (!due) return '—'
  return formatDurationMs(apiDateMs(due) - Date.now())
}

onMounted(() => {
  clockTimer = window.setInterval(() => { now.value = Date.now() }, 1000)
})

onBeforeUnmount(() => {
  if (clockTimer) window.clearInterval(clockTimer)
})
</script>

<style scoped>
.return-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
}
.reader-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}
.reader-avatar {
  background: #e6f7ef;
  color: #047857;
}
.font-medium { font-weight: 600; }
.muted {
  color: #94a3b8;
  font-size: 12px;
  margin-top: 2px;
}
.overdue {
  color: #ef4444;
  font-weight: 700;
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
</style>

