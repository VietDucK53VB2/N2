<template>
  <a-card title="Danh sách phiếu mượn">
    <template #extra>
      <a-space>
        <a-badge :count="store.pendingTx.length" :number-style="{ backgroundColor: '#faad14' }" />
        <a-tag color="purple">{{ store.returnPendingTx.length }} chờ trả</a-tag>
      </a-space>
    </template>

    <a-space class="mb-4">
      <a-radio-group v-model:value="filter" button-style="solid" size="small">
        <a-radio-button value="all">Tất cả</a-radio-button>
        <a-radio-button value="Pending">Chờ duyệt</a-radio-button>
        <a-radio-button value="Borrowed">Đang mượn</a-radio-button>
        <a-radio-button value="ReturnPending">Chờ trả</a-radio-button>
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
        <template v-else-if="column.key === 'BorrowedAt'">{{ fmtDate(record.BorrowedAt) }}</template>
        <template v-else-if="column.key === 'DueAt'">{{ fmtDate(record.DueAt) }}</template>
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
        <div class="muted">
          {{ selectedReturn.CardNumber }} · Book ID: {{ selectedReturn.BookId }}
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
  </a-card>
</template>

<script setup>
import { ref, computed, reactive } from 'vue'
import { message } from 'ant-design-vue'
import { useLibrarianStore } from '@/stores/librarian'
import { CheckOutlined, CloseOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'

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
  { title: 'Mã thẻ', dataIndex: 'CardNumber', key: 'CardNumber', width: 140 },
  { title: 'Book ID', dataIndex: 'BookId', key: 'BookId', width: 100 },
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

function fmtDate(d) { return d ? dayjs(d).format('DD/MM/YYYY') : '—' }

function statusColor(s) {
  if (s === 'Pending') return 'orange'
  if (s === 'ReturnPending') return 'purple'
  if (s === 'Overdue') return 'red'
  if (s === 'Returned') return 'green'
  return 'blue'
}

function statusLabel(s) {
  if (s === 'Pending') return 'Chờ duyệt'
  if (s === 'ReturnPending') return 'Chờ kiểm tra trả'
  if (s === 'Overdue') return 'Quá hạn'
  if (s === 'Returned') return 'Đã trả'
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
.condition-summary {
  padding: 12px 14px;
  margin-bottom: 16px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #f8fafc;
}
</style>
