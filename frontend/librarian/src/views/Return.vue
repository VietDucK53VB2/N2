<template>
  <a-card title="Xác nhận Trả sách">
    <a-steps :current="step" class="mb-6">
      <a-step title="Chọn phiếu" />
      <a-step title="Xác nhận" />
      <a-step title="Hoàn tất" />
    </a-steps>

    <div v-if="step === 0">
      <div class="return-toolbar">
        <a-input-search
          v-model:value="keyword"
          placeholder="Lọc theo mã thẻ, Book ID, tên sách..."
          allow-clear
          size="large"
          style="max-width:420px"
        />
        <a-tag color="blue">{{ filteredLoans.length }} phiếu có thể trả</a-tag>
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
            <a-tag :color="store.isOverdue(record) ? 'red' : 'blue'">
              {{ store.isOverdue(record) ? 'Quá hạn' : 'Đang mượn' }}
            </a-tag>
          </template>

          <template v-else-if="column.key === 'action'">
            <a-button type="primary" size="small" @click="selectLoan(record)">
              Trả sách
            </a-button>
          </template>
        </template>
      </a-table>
    </div>

    <div v-if="step === 1">
      <a-alert type="info" show-icon class="mb-4">
        <template #message>Xác nhận thông tin trả sách</template>
        <template #description>
          <p><strong>Mã thẻ:</strong> {{ cardNum }}</p>
          <p><strong>Sách:</strong> {{ selectedLoan ? bookTitle(selectedLoan) : '—' }}</p>
          <p><strong>Book ID:</strong> {{ bookId }}</p>
          <p><strong>Hạn trả:</strong> {{ fmtDate(selectedLoan?.DueAt || selectedLoan?.dueAt) }}</p>
        </template>
      </a-alert>
      <a-space>
        <a-button @click="step = 0"><LeftOutlined /> Quay lại</a-button>
        <a-button type="primary" :loading="returning" @click="submitReturn">
          <CheckOutlined /> Xác nhận trả sách
        </a-button>
      </a-space>
    </div>

    <div v-if="step === 2" class="text-center" style="padding:40px 0">
      <a-result
        :status="fineAmount > 0 ? 'warning' : 'success'"
        :title="fineAmount > 0 ? 'Trả sách thành công - Có phí phạt' : 'Trả sách thành công!'"
        :sub-title="fineAmount > 0 ? `Tiền phạt: ${fineAmount.toLocaleString()} VND` : 'Không có phí phạt.'"
      >
        <template #extra>
          <a-button type="primary" @click="reset">Trả sách khác</a-button>
        </template>
      </a-result>
    </div>
  </a-card>
</template>

<script setup>
import { computed, ref } from 'vue'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import { useLibrarianStore } from '@/stores/librarian'
import { LeftOutlined, CheckOutlined } from '@ant-design/icons-vue'

const store = useLibrarianStore()
const step = ref(0)
const keyword = ref('')
const cardNum = ref('')
const bookId = ref('')
const selectedLoan = ref(null)
const returning = ref(false)
const fineAmount = ref(0)

const columns = [
  { title: 'Độc giả', key: 'reader', width: 180 },
  { title: 'Sách', key: 'book' },
  { title: 'Ngày mượn', key: 'borrowedAt', width: 130 },
  { title: 'Hạn trả', key: 'dueAt', width: 130 },
  { title: 'Trạng thái', key: 'status', width: 120 },
  { title: '', key: 'action', width: 110 }
]

const returnableLoans = computed(() =>
  store.activeTx.map((loan, index) => ({
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

function selectLoan(loan) {
  selectedLoan.value = loan
  cardNum.value = store.cardNumberOf(loan)
  bookId.value = store.bookIdOf(loan)
  fineAmount.value = 0
  step.value = 1
}

async function submitReturn() {
  returning.value = true
  try {
    const r = await store.returnBook(cardNum.value, bookId.value)
    if (r.ok) {
      const d = await r.json().catch(() => ({}))
      fineAmount.value = d.FineAmount || d.fineAmount || 0
      step.value = 2
      message.success('Trả sách thành công!')
    } else {
      const e = await r.json().catch(() => ({}))
      message.error(e.Message || e.message || 'Không trả được sách')
    }
  } catch {
    message.error('Lỗi kết nối')
  } finally {
    returning.value = false
  }
}

function reset() {
  step.value = 0
  cardNum.value = ''
  bookId.value = ''
  selectedLoan.value = null
  fineAmount.value = 0
  store.loadAll()
}

function bookTitle(loan = {}) {
  return loan.TenSach || loan.tenSach || loan.Title || loan.title || '—'
}

function fmtDate(d) {
  return d ? dayjs(d).format('DD/MM/YYYY') : '—'
}
</script>

<style scoped>
.mb-6 { margin-bottom: 24px; }
.mb-4 { margin-bottom: 16px; }
.text-center { text-align: center; }
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
</style>
