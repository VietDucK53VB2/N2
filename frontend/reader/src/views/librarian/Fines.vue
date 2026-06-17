<template>
  <div>
    <!-- Stats -->
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

    <!-- Fines Table -->
    <v-card rounded="xl" elevation="1">
      <v-card-title class="font-weight-bold">Danh sách Phí phạt</v-card-title>
      <v-data-table
        :headers="headers"
        :items="libStore.fines"
        :loading="libStore.loading"
        density="comfortable"
        :items-per-page="10"
      >
        <template #item.PaymentStatus="{ item }">
          <v-chip size="small" :color="fineStatusColor(item)" variant="flat">
            {{ fineStatusLabel(item) }}
          </v-chip>
        </template>
        <template #item.PaymentRequestedAt="{ item }">
          {{ formatDate(item.PaymentRequestedAt || item.paymentRequestedAt) || '—' }}
        </template>
        <template #item.Amount="{ item }">
          <span class="font-weight-bold text-error">{{ formatMoney(item.Amount || item.amount || 0) }}</span>
        </template>
        <template #item.CreatedAt="{ item }">
          {{ formatDate(item.CreatedAt || item.createdAt) }}
        </template>
        <template #item.actions="{ item }">
          <v-btn
            v-if="isPending(item)"
            size="small" color="success" variant="flat"
            :loading="payingId === (item.Id || item.id)"
            @click="markPaid(item)"
          >
            <v-icon start>mdi-check</v-icon> Duyệt thanh toán
          </v-btn>
          <v-chip v-else-if="isPaid(item)" size="small" color="success" variant="flat">
            Đã thanh toán
          </v-chip>
          <v-chip v-else size="small" color="grey" variant="flat">
            Chờ độc giả gửi yêu cầu
          </v-chip>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import { formatDate, formatMoney } from '@/utils/helpers'

const libStore = useLibrarianStore()
const payingId = ref(null)

const headers = [
  { title: 'Mã thẻ', key: 'CardNumber', width: '140px' },
  { title: 'Số tiền', key: 'Amount', width: '120px' },
  { title: 'Lý do', key: 'Reason', width: '200px' },
  { title: 'Ngày tạo', key: 'CreatedAt', width: '120px' },
  { title: 'Yêu cầu lúc', key: 'PaymentRequestedAt', width: '120px' },
  { title: 'Trạng thái', key: 'PaymentStatus', width: '140px' },
  { title: '', key: 'actions', width: '120px', sortable: false }
]

const isPaid = item => Boolean(item.IsPaid || item.isPaid || item.PaymentStatus === 'Paid' || item.paymentStatus === 'Paid' || item.PaidAt || item.paidAt)
const isPending = item => Boolean(item.IsPaymentPending || item.isPaymentPending || item.PaymentStatus === 'PendingApproval' || item.paymentStatus === 'PendingApproval')
const pendingFines = computed(() => libStore.fines.filter(isPending))
const paidFines = computed(() => libStore.fines.filter(isPaid))
const totalUnpaid = computed(() => libStore.fines.filter(f => !isPaid(f)).reduce((s, f) => s + Number(f.Amount || f.amount || 0), 0))

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

async function markPaid(item) {
  const id = item.Id || item.id
  payingId.value = id
  try {
    await libStore.markFinePaid(id)
  } finally {
    payingId.value = null
  }
}

onMounted(() => libStore.loadFines())
</script>
