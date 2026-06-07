<template>
  <a-card title="Xác nhận trả sách">
    <div class="return-toolbar">
      <a-input-search
        v-model:value="keyword"
        placeholder="Lọc theo mã thẻ, Book ID, tên sách..."
        allow-clear
        size="large"
        style="max-width:420px"
      />
      <a-tag color="purple">{{ filteredLoans.length }} phiếu đang chờ trả</a-tag>
    </div>

    <a-table
      :columns="columns"
      :data-source="filteredLoans"
      :loading="store.loading"
      :pagination="{ pageSize: 8 }"
      row-key="returnKey"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'reader'">
          <a-space>
            <a-avatar class="reader-avatar">{{ store.cardNumberOf(record).slice(0, 1) }}</a-avatar>
            <span class="font-medium">{{ store.cardNumberOf(record) }}</span>
          </a-space>
        </template>

        <template v-else-if="column.key === 'book'">
          <div>
            <div class="font-medium">{{ bookTitle(record) }}</div>
            <div class="muted">Book ID: {{ store.bookIdOf(record) }}</div>
          </div>
        </template>

        <template v-else-if="column.key === 'borrowedAt'">
          {{ fmtDate(record.BorrowedAt || record.borrowedAt) }}
        </template>

        <template v-else-if="column.key === 'dueAt'">
          <span :class="{ overdue: store.isOverdue(record) }">
            {{ fmtDate(record.DueAt || record.dueAt) }}
          </span>
        </template>

        <template v-else-if="column.key === 'status'">
          <a-tag color="purple">Chờ kiểm tra tình trạng</a-tag>
        </template>

        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="primary" size="small" @click="openConditionDialog(record)">
              Đánh giá & xác nhận
            </a-button>
            <a-button size="small" :loading="actionId === record.Id + 'no'" @click="reject(record)">
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
        <div class="font-medium">{{ bookTitle(selectedLoan) }}</div>
        <div class="muted">
          {{ store.cardNumberOf(selectedLoan) }} · Book ID: {{ store.bookIdOf(selectedLoan) }}
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
import { computed, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import { useLibrarianStore } from '@/stores/librarian'

const store = useLibrarianStore()
const keyword = ref('')
const actionId = ref(null)
const conditionDialog = ref(false)
const confirming = ref(false)
const selectedLoan = ref(null)
const conditionForm = reactive({
  condition: 'Good',
  conditionNote: ''
})

const columns = [
  { title: 'Độc giả', key: 'reader', width: 180 },
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
      store.bookIdOf(loan),
      bookTitle(loan),
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

async function approveWithCondition() {
  if (!selectedLoan.value) return
  confirming.value = true
  actionId.value = selectedLoan.value.Id + 'ok'
  const res = await store.approveReturn(selectedLoan.value.Id, {
    condition: conditionForm.condition,
    conditionNote: conditionForm.conditionNote
  })
  if (res.ok) {
    message.success('Đã kiểm tra tình trạng và xác nhận trả sách.')
    conditionDialog.value = false
    selectedLoan.value = null
  } else {
    message.error(await readError(res))
  }
  actionId.value = null
  confirming.value = false
}

async function reject(record) {
  actionId.value = record.Id + 'no'
  const res = await store.rejectReturn(record.Id)
  if (res.ok) message.success('Đã chuyển lại trạng thái đang mượn.')
  else message.error(await readError(res))
  actionId.value = null
}

async function readError(res) {
  const data = await res.json().catch(() => null)
  return data?.Message || data?.message || 'Không xử lý được phiếu trả.'
}

function bookTitle(loan = {}) {
  return loan.TenSach || loan.tenSach || loan.Title || loan.title || '—'
}

function fmtDate(d) {
  return d ? dayjs(d).format('DD/MM/YYYY') : '—'
}
</script>

<style scoped>
.return-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
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
</style>
