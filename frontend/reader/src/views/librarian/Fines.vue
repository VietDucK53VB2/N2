<template>
  <div class="page-shell">
    <v-row class="mb-6">
      <v-col cols="12" sm="4">
        <v-card rounded="xl" class="pa-4" elevation="1" color="error" variant="tonal">
          <p class="text-caption font-weight-bold">Tổng phí chưa thanh toán</p>
          <p class="text-h5 font-weight-black text-error">{{ formatMoney(totalUnpaid) }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" sm="4">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <p class="text-caption font-weight-bold text-grey">Đang chờ duyệt</p>
          <p class="text-h5 font-weight-black">{{ pendingFines.length }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" sm="4">
        <v-card rounded="xl" class="pa-4" elevation="1" color="success" variant="tonal">
          <p class="text-caption font-weight-bold">Đã thanh toán</p>
          <p class="text-h5 font-weight-black text-success">{{ paidFines.length }}</p>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <v-card-title class="font-weight-bold">Danh sách phí phạt</v-card-title>
      <v-card-text class="pt-0">
        <v-chip-group v-model="filter" mandatory class="mb-4">
          <v-chip value="all" variant="outlined" filter>Tất cả</v-chip>
          <v-chip value="Pending" variant="outlined" filter color="warning">Chờ duyệt</v-chip>
          <v-chip value="Unpaid" variant="outlined" filter color="error">Chưa yêu cầu</v-chip>
          <v-chip value="Paid" variant="outlined" filter color="success">Đã thanh toán</v-chip>
        </v-chip-group>
      </v-card-text>

      <v-data-table
        :headers="headers"
        :items="filteredFines"
        :loading="libStore.loading"
        density="comfortable"
        :items-per-page="10"
        class="fines-table"
      >
        <template #item.reader="{ item }">
          <div>
            <div class="font-weight-bold">{{ displayReader(item) }}</div>
            <div class="text-caption text-grey">{{ displayCard(item) }}</div>
          </div>
        </template>

        <template #item.book="{ item }">
          <div>
            <div class="font-weight-bold">{{ displayBook(item) }}</div>
            <div class="text-caption text-grey">Book ID: {{ item.BookId || item.bookId || '—' }}</div>
          </div>
        </template>

        <template #item.Amount="{ item }">
          <span class="font-weight-bold text-error">{{ formatMoney(item.Amount || item.amount || 0) }}</span>
        </template>

        <template #item.Reason="{ item }">
          {{ translateFineReason(item.Reason || item.reason || '') }}
        </template>

        <template #item.CreatedAt="{ item }">
          {{ formatDateTime(item.CreatedAt || item.createdAt) }}
        </template>

        <template #item.PaymentRequestedAt="{ item }">
          {{ formatDateTime(item.PaymentRequestedAt || item.paymentRequestedAt) }}
        </template>

        <template #item.PaymentStatus="{ item }">
          <v-chip size="small" :color="fineStatusColor(item)" variant="flat">
            {{ fineStatusLabel(item) }}
          </v-chip>
        </template>

        <template #item.actions="{ item }">
          <div class="d-flex flex-wrap ga-2">
            <v-btn
              v-if="isPending(item)"
              size="small"
              color="success"
              variant="flat"
              :loading="actionId === actionKey(item, 'approve')"
              @click="approve(item)"
            >
              <v-icon start>mdi-check</v-icon> Duyệt phí phạt
            </v-btn>
            <v-btn
              v-if="isPending(item)"
              size="small"
              color="error"
              variant="tonal"
              :loading="actionId === actionKey(item, 'reject')"
              @click="reject(item)"
            >
              <v-icon start>mdi-close</v-icon> Từ chối duyệt
            </v-btn>
            <v-chip v-else-if="isPaid(item)" size="small" color="success" variant="flat">
              Đã thanh toán
            </v-chip>
            <v-chip v-else size="small" color="grey" variant="flat">
              Chờ độc giả gửi yêu cầu
            </v-chip>
          </div>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import { formatDateTime, formatMoney, getDisplayBookTitle, getDisplayCardNumber, getDisplayReaderName, translateFineReason } from '@/utils/helpers'

const libStore = useLibrarianStore()
const filter = ref('all')
const actionId = ref(null)

const headers = [
  { title: 'Độc giả', key: 'reader', width: '220px', sortable: false },
  { title: 'Sách', key: 'book', width: '260px', sortable: false },
  { title: 'Số tiền', key: 'Amount', width: '120px' },
  { title: 'Lý do', key: 'Reason', width: '240px' },
  { title: 'Ngày tạo', key: 'CreatedAt', width: '140px' },
  { title: 'Yêu cầu lúc', key: 'PaymentRequestedAt', width: '140px' },
  { title: 'Trạng thái', key: 'PaymentStatus', width: '140px' },
  { title: 'Hành động', key: 'actions', width: '220px', sortable: false }
]

const filteredFines = computed(() => {
  if (filter.value === 'all') return libStore.fines
  if (filter.value === 'Paid') return libStore.fines.filter(item => isPaid(item))
  if (filter.value === 'Pending') return libStore.fines.filter(item => isPending(item))
  return libStore.fines.filter(item => !isPaid(item) && !isPending(item))
})

const pendingFines = computed(() => libStore.fines.filter(item => isPending(item)))
const paidFines = computed(() => libStore.fines.filter(item => isPaid(item)))
const totalUnpaid = computed(() => libStore.fines.filter(item => !isPaid(item)).reduce((sum, item) => sum + Number(item.Amount || item.amount || 0), 0))

function actionKey(item, suffix) {
  return `${item.Id || item.id || 'row'}:${suffix}`
}

function displayReader(item = {}) {
  return getDisplayReaderName(item, 'Độc giả')
}

function displayCard(item = {}) {
  return getDisplayCardNumber(item, item.CardNumber || item.cardNumber || '—')
}

function displayBook(item = {}) {
  return getDisplayBookTitle(item, item.BookId || item.bookId || '—')
}

function isPaid(item) {
  return Boolean(item.IsPaid || item.isPaid || item.PaymentStatus === 'Paid' || item.paymentStatus === 'Paid' || item.PaidAt || item.paidAt)
}

function isPending(item) {
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

async function approve(item) {
  const id = item.Id || item.id
  actionId.value = actionKey(item, 'approve')
  try {
    await libStore.markFinePaid(id)
  } finally {
    actionId.value = null
  }
}

async function reject(item) {
  const id = item.Id || item.id
  const reason = libStore.promptRejectReason('Không đủ điều kiện duyệt thanh toán phí phạt')
  if (!reason) return
  actionId.value = actionKey(item, 'reject')
  try {
    await libStore.rejectFinePayment(id, reason)
  } finally {
    actionId.value = null
  }
}

onMounted(() => libStore.loadFines())
</script>
