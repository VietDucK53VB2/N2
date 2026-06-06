<template>
  <div>
    <!-- Stats -->
    <v-row class="mb-6">
      <v-col cols="12" sm="4">
        <v-card rounded="xl" class="pa-4" elevation="1" color="error" variant="tonal">
          <p class="text-caption font-weight-bold">Tổng phí phạt chưa thu</p>
          <p class="text-h5 font-weight-black text-error">{{ formatMoney(totalUnpaid) }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" sm="4">
        <v-card rounded="xl" class="pa-4" elevation="1">
          <p class="text-caption font-weight-bold text-grey">Số khoản phạt</p>
          <p class="text-h5 font-weight-black">{{ unpaidFines.length }}</p>
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
        <template #item.Amount="{ item }">
          <span class="font-weight-bold text-error">{{ formatMoney(item.Amount || item.amount || 0) }}</span>
        </template>
        <template #item.CreatedAt="{ item }">
          {{ formatDate(item.CreatedAt || item.createdAt) }}
        </template>
        <template #item.IsPaid="{ item }">
          <v-chip size="small" :color="(item.IsPaid || item.isPaid) ? 'success' : 'error'" variant="flat">
            {{ (item.IsPaid || item.isPaid) ? 'Đã thu' : 'Chưa thu' }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn
            v-if="!(item.IsPaid || item.isPaid)"
            size="small" color="success" variant="flat"
            :loading="payingId === (item.Id || item.id)"
            @click="markPaid(item)"
          >
            <v-icon start>mdi-check</v-icon> Đã thu
          </v-btn>
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
  { title: 'Trạng thái', key: 'IsPaid', width: '100px' },
  { title: '', key: 'actions', width: '120px', sortable: false }
]

const unpaidFines = computed(() => libStore.fines.filter(f => !(f.IsPaid || f.isPaid)))
const paidFines = computed(() => libStore.fines.filter(f => f.IsPaid || f.isPaid))
const totalUnpaid = computed(() => unpaidFines.value.reduce((s, f) => s + Number(f.Amount || f.amount || 0), 0))

async function markPaid(item) {
  const id = item.Id || item.id
  payingId.value = id
  await libStore.markFinePaid(id)
  payingId.value = null
}

onMounted(() => libStore.loadFines())
</script>
