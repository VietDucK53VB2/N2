<template>
  <div class="page-shell">
    <a-row :gutter="[16, 16]" class="stats-row">
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic
            title="Tổng phí chưa thanh toán"
            :value="totalUnpaid"
            :precision="0"
            suffix="đ"
            :value-style="{ color: '#dc2626', fontWeight: 800 }"
          />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic title="Đang chờ duyệt" :value="pendingFines.length" :value-style="{ color: '#d97706', fontWeight: 800 }" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card class="mini-card">
          <a-statistic title="Đã thanh toán" :value="paidFines.length" :value-style="{ color: '#059669', fontWeight: 800 }" />
        </a-card>
      </a-col>
    </a-row>

    <a-card class="panel-card">
      <template #title>
        <div class="panel-head">
          <div>
            <div class="panel-title">Danh sách phí phạt</div>
            <div class="panel-subtitle">Theo dõi các khoản phí chờ xử lý, chờ duyệt và đã thanh toán</div>
          </div>
        </div>
      </template>

      <div class="toolbar">
        <a-radio-group v-model:value="filter" button-style="solid" size="small">
          <a-radio-button value="all">Tất cả</a-radio-button>
          <a-radio-button value="Pending">Chờ duyệt</a-radio-button>
          <a-radio-button value="Unpaid">Chưa yêu cầu</a-radio-button>
          <a-radio-button value="Paid">Đã thanh toán</a-radio-button>
        </a-radio-group>

        <a-tag color="blue">{{ filteredFines.length }} khoản</a-tag>
      </div>

      <a-table
        :columns="columns"
        :data-source="filteredFines"
        :loading="libStore.loading"
        :pagination="{ pageSize: 10 }"
        size="middle"
        row-key="Id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'reader'">
            <div class="reader-cell">
              <a-avatar class="reader-avatar">{{ (displayReader(record) || displayCard(record)).slice(0, 1) }}</a-avatar>
              <div>
                <div class="font-medium">{{ displayReader(record) }}</div>
                <div class="muted">{{ displayCard(record) }}</div>
                <div class="muted" v-if="displayUsername(record)">{{ displayUsername(record) }}</div>
              </div>
            </div>
          </template>

          <template v-else-if="column.key === 'book'">
            <div>
              <div class="font-medium">{{ displayBook(record) }}</div>
              <div class="muted">Book ID: {{ record.BookId || record.bookId || '—' }}</div>
            </div>
          </template>

          <template v-else-if="column.key === 'Amount'">
            <span class="money">{{ formatMoney(record.Amount || record.amount || 0) }}</span>
          </template>

          <template v-else-if="column.key === 'Reason'">
            {{ translateFineReason(record.Reason || record.reason || '') }}
          </template>

          <template v-else-if="column.key === 'CreatedAt'">
            {{ formatDateTime(record.CreatedAt || record.createdAt) }}
          </template>

          <template v-else-if="column.key === 'PaymentRequestedAt'">
            {{ formatDateTime(record.PaymentRequestedAt || record.paymentRequestedAt) }}
          </template>

          <template v-else-if="column.key === 'PaymentStatus'">
            <a-tag :color="fineStatusColor(record)">{{ fineStatusLabel(record) }}</a-tag>
          </template>

          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button
                v-if="isPending(record)"
                type="primary"
                size="small"
                :loading="actionId === actionKey(record, 'approve')"
                @click="approve(record)"
              >
                Duyệt phí phạt
              </a-button>

              <a-button
                v-if="isPending(record)"
                danger
                size="small"
                :loading="actionId === actionKey(record, 'reject')"
                @click="reject(record)"
              >
                Từ chối duyệt
              </a-button>

              <a-tag v-else-if="isPaid(record)" color="green">Đã thanh toán</a-tag>
              <a-tag v-else color="gold">Chờ độc giả gửi yêu cầu</a-tag>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal
      v-model:open="rejectDialog"
      title="Từ chối duyệt phí phạt"
      ok-text="Từ chối"
      cancel-text="Hủy"
      :confirm-loading="rejecting"
      @ok="confirmReject"
    >
      <div v-if="selectedFine" class="reject-summary">
        <div class="font-medium">{{ displayReader(selectedFine) }}</div>
        <div class="muted">{{ displayCard(selectedFine) }} · {{ displayBook(selectedFine) }}</div>
      </div>

      <a-form layout="vertical">
        <a-form-item label="Lý do từ chối" required>
          <a-textarea
            v-model:value="rejectForm.reason"
            :rows="4"
            placeholder="Nhập lý do từ chối duyệt phí phạt"
            allow-clear
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup>
import { computed, onMounted, reactive, ref } from 'vue'
import dayjs from 'dayjs'
import { message } from 'ant-design-vue'
import { useLibrarianStore } from '@/stores/librarian'

const libStore = useLibrarianStore()
const embedMode = libStore.embedMode
const filter = ref('all')
const actionId = ref(null)
const rejectDialog = ref(false)
const rejecting = ref(false)
const selectedFine = ref(null)
const rejectForm = reactive({
  reason: ''
})

const columns = computed(() => {
  const baseColumns = [
    { title: 'Độc giả', key: 'reader', width: 240, sortable: false },
    { title: 'Sách', key: 'book', width: 260, sortable: false },
    { title: 'Số tiền', key: 'Amount', width: 120 },
    { title: 'Lý do', key: 'Reason', width: 220 },
    { title: 'Ngày tạo', key: 'CreatedAt', width: 150 },
    { title: 'Yêu cầu lúc', key: 'PaymentRequestedAt', width: 150 },
    { title: 'Trạng thái', key: 'PaymentStatus', width: 140 }
  ]

  if (embedMode) return baseColumns
  return [...baseColumns, { title: 'Hành động', key: 'actions', width: 240, sortable: false }]
})

const filteredFines = computed(() => {
  if (filter.value === 'all') return libStore.fines
  if (filter.value === 'Paid') return libStore.fines.filter(item => isPaid(item))
  if (filter.value === 'Pending') return libStore.fines.filter(item => isPending(item))
  return libStore.fines.filter(item => !isPaid(item) && !isPending(item))
})

const pendingFines = computed(() => libStore.fines.filter(item => isPending(item)))
const paidFines = computed(() => libStore.fines.filter(item => isPaid(item)))
const totalUnpaid = computed(() =>
  libStore.fines
    .filter(item => !isPaid(item))
    .reduce((sum, item) => sum + Number(item.Amount || item.amount || 0), 0)
)

function actionKey(item, suffix) {
  return `${item.Id || item.id || 'row'}:${suffix}`
}

function displayReader(item = {}) {
  return item.ReaderName || item.readerName || item.FullName || item.fullName || item.ReaderUsername || item.readerUsername || displayCard(item)
}

function displayUsername(item = {}) {
  return item.ReaderUsername || item.readerUsername || ''
}

function displayCard(item = {}) {
  return item.CardNumber || item.cardNumber || '—'
}

function displayBook(item = {}) {
  return libStore.bookTitleOf(item)
}

function isPaid(item) {
  return Boolean(item.IsPaid || item.isPaid || item.PaymentStatus === 'Paid' || item.paymentStatus === 'Paid' || item.PaidAt || item.paidAt)
}

function isPending(item) {
  if (isPaid(item)) return false
  return Boolean(
    item.IsPaymentPending ||
    item.isPaymentPending ||
    item.PaymentRequestedAt ||
    item.paymentRequestedAt ||
    item.PaymentStatus === 'PendingApproval' ||
    item.paymentStatus === 'PendingApproval'
  )
}

function fineStatusLabel(item) {
  if (isPaid(item)) return 'Đã thanh toán'
  if (isPending(item)) return 'Chờ duyệt'
  return 'Chưa yêu cầu'
}

function fineStatusColor(item) {
  if (isPaid(item)) return 'success'
  if (isPending(item)) return 'warning'
  return 'grey'
}

function translateFineReason(reason = '') {
  const text = String(reason || '').trim()
  if (!text) return '—'
  const matched = text.match(/Overdue return by (\d+) day\(s\)/i)
  if (matched) return `Phạt trả quá hạn ${matched[1]} ngày`
  if (/lost book fee/i.test(text)) return 'Phí mất sách'
  return text
}

function formatDateTime(value) {
  return value ? dayjs(value).format('DD/MM/YYYY HH:mm:ss') : '—'
}

function formatMoney(value) {
  return `${Number(value || 0).toLocaleString('vi-VN')} đ`
}

async function approve(item) {
  const id = item.Id || item.id
  if (!id) return
  actionId.value = actionKey(item, 'approve')
  try {
    const res = await libStore.payFine(id)
    if (res.ok) message.success('Đã duyệt thanh toán phí phạt.')
    else message.error(await readError(res))
  } finally {
    actionId.value = null
  }
}

async function reject(item) {
  selectedFine.value = item
  rejectForm.reason = 'Không đủ điều kiện duyệt thanh toán phí phạt'
  rejectDialog.value = true
}

async function confirmReject() {
  if (!selectedFine.value) return
  const id = selectedFine.value.Id || selectedFine.value.id
  if (!id) return
  rejecting.value = true
  actionId.value = actionKey(selectedFine.value, 'reject')
  try {
    const reason = rejectForm.reason.trim() || 'Không đủ điều kiện duyệt thanh toán phí phạt'
    const res = await libStore.rejectFinePayment(id, reason)
    if (res.ok) {
      message.success('Đã từ chối duyệt phí phạt.')
      rejectDialog.value = false
      selectedFine.value = null
    } else {
      message.error(await readError(res))
    }
  } finally {
    actionId.value = null
    rejecting.value = false
  }
}

async function readError(res) {
  const data = await res.json().catch(() => null)
  return data?.Message || data?.message || 'Không xử lý được khoản phí phạt.'
}

onMounted(() => {
  if (!libStore.fines.length) {
    libStore.loadFines()
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

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 14px;
  flex-wrap: wrap;
}

.reader-cell {
  display: flex;
  align-items: center;
  gap: 12px;
}

.reader-avatar {
  background: linear-gradient(135deg, #1f5f55, #2d8579);
  color: #fff;
  font-weight: 800;
}

.font-medium {
  font-weight: 700;
  color: #103b35;
}

.muted {
  font-size: 12px;
  color: #8c98a5;
  margin-top: 2px;
}

.money {
  font-weight: 800;
  color: #dc2626;
}
</style>
