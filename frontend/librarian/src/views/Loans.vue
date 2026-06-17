<template>
  <div class="page-shell">
    <a-card class="hero-card">
      <div class="hero-copy">
        <div class="eyebrow">Quản lý mượn trả</div>
        <h2>Danh sách phiếu mượn</h2>
        <p>Duyệt yêu cầu mượn, gia hạn và theo dõi các trạng thái xử lý.</p>
      </div>
      <div class="hero-badges">
        <div class="hero-badge">
          <span class="badge-label">Chờ duyệt</span>
          <strong>{{ store.pendingTx.length }}</strong>
        </div>
        <div class="hero-badge">
          <span class="badge-label">Đang mượn</span>
          <strong>{{ store.borrowedTx.length }}</strong>
        </div>
        <div class="hero-badge">
          <span class="badge-label">Chờ trả</span>
          <strong>{{ store.returnPendingTx.length }}</strong>
        </div>
        <div class="hero-badge alert">
          <span class="badge-label">Quá hạn</span>
          <strong>{{ store.overdueTx.length }}</strong>
        </div>
      </div>
    </a-card>

    <a-card class="panel-card">
      <div class="toolbar">
        <a-radio-group v-model:value="filter" button-style="solid" size="small">
          <a-radio-button value="all">Tất cả</a-radio-button>
          <a-radio-button value="Pending">Chờ duyệt</a-radio-button>
          <a-radio-button value="Borrowed">Đang mượn</a-radio-button>
          <a-radio-button value="ReturnPending">Chờ trả</a-radio-button>
          <a-radio-button value="Overdue">Quá hạn</a-radio-button>
          <a-radio-button value="Returned">Đã trả</a-radio-button>
        </a-radio-group>

        <a-tag color="blue">{{ filteredTx.length }} phiếu</a-tag>
      </div>

      <a-table
        :columns="columns"
        :data-source="filteredTx"
        :loading="store.loading"
        :pagination="{ pageSize: 10 }"
        size="middle"
        row-key="Id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'reader'">
            <div>
              <div class="font-medium">{{ readerNameOf(record) }}</div>
              <div class="muted">{{ store.cardNumberOf(record) }}</div>
            </div>
          </template>
          <template v-else-if="column.key === 'book'">
            <div>
              <div class="font-medium">{{ bookTitleOf(record) }}</div>
              <div class="muted">Book ID: {{ store.bookIdOf(record) }}</div>
            </div>
          </template>
          <template v-else-if="column.key === 'Status'">
            <a-tag :color="statusColor(record)">{{ statusLabel(record) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'BorrowedAt'">{{ fmtDateTime(record.BorrowedAt) }}</template>
          <template v-else-if="column.key === 'DueAt'">{{ fmtDateTime(record.DueAt) }}</template>
          <template v-else-if="column.key === 'actions'">
            <a-space v-if="store.isPending(record)">
              <a-button type="primary" size="small" :loading="actionId === record.Id + 'a'" @click="doApprove(record)">
                <CheckOutlined /> Duyệt mượn
              </a-button>
              <a-button danger size="small" :loading="actionId === record.Id + 'r'" @click="doReject(record)">
                <CloseOutlined /> Từ chối
              </a-button>
            </a-space>
            <a-space v-else-if="store.isBorrowed(record) || store.isOverdue(record)">
              <a-button type="primary" size="small" :loading="actionId === record.Id + 're'" @click="doRenew(record)">
                <ReloadOutlined /> Duyệt gia hạn
              </a-button>
              <a-button danger size="small" :loading="actionId === record.Id + 'rrn'" @click="doRejectRenew(record)">
                <CloseOutlined /> Từ chối gia hạn
              </a-button>
            </a-space>
            <a-space v-else-if="store.isReturnPending(record)">
              <a-button type="primary" size="small" @click="openConditionDialog(record)">
                <CheckOutlined /> Kiểm tra & trả
              </a-button>
              <a-button size="small" :loading="actionId === record.Id + 'rr'" @click="doRejectReturn(record)">
                <CloseOutlined /> Chưa nhận
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal
      v-model:open="conditionDialog"
      title="Kiểm tra tình trạng sách"
      ok-text="Xác nhận trả"
      cancel-text="Hủy"
      :confirm-loading="confirming"
      @ok="doApproveReturn"
    >
      <div v-if="selectedReturn" class="condition-summary">
        <div class="font-medium">{{ selectedReturn.TenSach || selectedReturn.BookId }}</div>
        <div class="muted">{{ selectedReturn.CardNumber }} · Book ID: {{ selectedReturn.BookId }}</div>
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
  </div>
</template>

<script setup>
import { ref, computed, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import { useLibrarianStore } from '@/stores/librarian'
import { CheckOutlined, CloseOutlined, ReloadOutlined } from '@ant-design/icons-vue'

const store = useLibrarianStore()
const filter = ref('all')
const actionId = ref(null)
const conditionDialog = ref(false)
const confirming = ref(false)
const selectedReturn = ref(null)
const conditionForm = reactive({
  condition: 'Good',
  conditionNote: ''
})

const columns = [
  { title: 'Độc giả', key: 'reader', width: 220 },
  { title: 'Sách', key: 'book' },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: 120 },
  { title: 'Hạn trả', key: 'DueAt', width: 120 },
  { title: 'Trạng thái', key: 'Status', width: 160 },
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

async function doApprove(r) {
  actionId.value = r.Id + 'a'
  const res = await store.approve(r.Id)
  if (res.ok) message.success('Đã duyệt mượn sách.')
  else message.error(await readError(res))
  actionId.value = null
}

async function doReject(r) {
  actionId.value = r.Id + 'r'
  const reason = store.promptRejectReason('Không đủ điều kiện mượn sách')
  const res = await store.reject(r.Id, reason)
  if (res.ok) message.success('Đã từ chối yêu cầu mượn.')
  else message.error(await readError(res))
  actionId.value = null
}

async function doRenew(r) {
  actionId.value = r.Id + 're'
  const reason = store.promptRejectReason('Gia hạn theo đề nghị độc giả')
  const res = await store.renew(r.Id, {
    extraDays: 7,
    reason: reason || 'Gia hạn theo đề nghị độc giả'
  })
  if (res.ok) message.success('Đã duyệt gia hạn.')
  else message.error(await readError(res))
  actionId.value = null
}

async function doRejectRenew(r) {
  actionId.value = r.Id + 'rrn'
  const reason = store.promptRejectReason('Không đủ điều kiện gia hạn')
  const res = await store.rejectRenew(r.Id, reason)
  if (res.ok) message.success('Đã từ chối gia hạn.')
  else message.error(await readError(res))
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
  const res = await store.approveReturn(selectedReturn.value.Id, {
    condition: conditionForm.condition,
    conditionNote: conditionForm.conditionNote
  })
  if (res.ok) {
    message.success('Đã kiểm tra tình trạng và xác nhận trả sách.')
    conditionDialog.value = false
    selectedReturn.value = null
  } else {
    message.error(await readError(res))
  }
  actionId.value = null
  confirming.value = false
}

async function doRejectReturn(r) {
  actionId.value = r.Id + 'rr'
  const reason = store.promptRejectReason('Không đủ điều kiện trả sách')
  const res = await store.rejectReturn(r.Id, reason)
  if (res.ok) message.success('Đã chuyển lại trạng thái đang mượn.')
  else message.error(await readError(res))
  actionId.value = null
}

async function readError(res) {
  const data = await res.json().catch(() => null)
  return data?.Message || data?.message || 'Lỗi xử lý.'
}

function readerNameOf(record = {}) {
  return (
    record.ReaderName ||
    record.readerName ||
    record.FullName ||
    record.fullName ||
    record.Username ||
    record.username ||
    store.cardNumberOf(record)
  )
}

function bookTitleOf(record = {}) {
  return (
    record.TenSach ||
    record.tenSach ||
    record.Title ||
    record.title ||
    record.BookTitle ||
    record.bookTitle ||
    record.BookName ||
    record.bookName ||
    `Book #${store.bookIdOf(record)}`
  )
}

function fmtDateTime(d) {
  return d ? dayjs(d).format('DD/MM/YYYY HH:mm:ss') : '—'
}

function statusColor(record) {
  if (store.isPending(record)) return 'orange'
  if (store.isReturnPending(record)) return 'purple'
  if (store.isOverdue(record)) return 'red'
  if (store.isReturned(record)) return 'green'
  return 'blue'
}

function statusLabel(record) {
  if (store.isPending(record)) return 'Chờ duyệt'
  if (store.isReturnPending(record)) return 'Chờ kiểm tra trả'
  if (store.isOverdue(record)) return 'Quá hạn'
  if (store.isReturned(record)) return 'Đã trả'
  return 'Đang mượn'
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
.panel-card {
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

.hero-badges {
  display: grid;
  grid-template-columns: repeat(4, minmax(120px, 1fr));
  gap: 12px;
  width: min(100%, 620px);
}

.hero-badge {
  background: #f7fbf8;
  border: 1px solid #e5eee8;
  border-radius: 14px;
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  align-items: flex-start;
}

.hero-badge.alert {
  background: #fff8ed;
  border-color: #f0d4a7;
}

.badge-label {
  color: #7d8a83;
  font-size: 12px;
  font-weight: 700;
}

.hero-badge strong {
  color: #103b35;
  font-size: 22px;
  line-height: 1;
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
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

@media (max-width: 992px) {
  .hero-card :deep(.ant-card-body) {
    flex-direction: column;
    align-items: flex-start;
  }

  .hero-badges {
    width: 100%;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 640px) {
  .hero-badges {
    grid-template-columns: 1fr;
  }

  .toolbar {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
