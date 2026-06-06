<template>
  <div>
    <!-- Search -->
    <v-card rounded="xl" elevation="1" class="mb-6">
      <v-card-text>
        <v-text-field
          v-model="cardNumber"
          label="Nhập mã thẻ độc giả"
          prepend-inner-icon="mdi-card-account-details"
          append-inner-icon="mdi-magnify"
          hide-details
          @click:append-inner="search"
          @keyup.enter="search"
        />
      </v-card-text>
    </v-card>

    <!-- Results -->
    <v-card v-if="result" rounded="xl" elevation="1">
      <v-card-title class="d-flex align-center ga-3">
        <v-avatar color="primary" variant="tonal"><v-icon>mdi-account</v-icon></v-avatar>
        <div>
          <p class="text-subtitle-1 font-weight-bold mb-0">{{ result.cardNo }}</p>
          <p class="text-caption text-grey">{{ result.transactions.length }} phiếu mượn</p>
        </div>
      </v-card-title>

      <v-card-text>
        <!-- Mini stats -->
        <v-row dense class="mb-4">
          <v-col cols="4">
            <v-card variant="tonal" color="info" class="pa-3 text-center" rounded="lg">
              <p class="text-h6 font-weight-bold">{{ result.active }}</p>
              <p class="text-caption">Đang mượn</p>
            </v-card>
          </v-col>
          <v-col cols="4">
            <v-card variant="tonal" color="error" class="pa-3 text-center" rounded="lg">
              <p class="text-h6 font-weight-bold">{{ result.overdue }}</p>
              <p class="text-caption">Quá hạn</p>
            </v-card>
          </v-col>
          <v-col cols="4">
            <v-card variant="tonal" color="success" class="pa-3 text-center" rounded="lg">
              <p class="text-h6 font-weight-bold">{{ result.returned }}</p>
              <p class="text-caption">Đã trả</p>
            </v-card>
          </v-col>
        </v-row>

        <v-data-table
          :headers="headers"
          :items="result.transactions"
          density="compact"
          :items-per-page="5"
        >
          <template #item.Status="{ item }">
            <v-chip size="x-small" :color="statusColor(item.Status)" variant="flat">{{ statusLabel(item.Status) }}</v-chip>
          </template>
          <template #item.BorrowedAt="{ item }">{{ formatDate(item.BorrowedAt) }}</template>
          <template #item.DueAt="{ item }">{{ formatDate(item.DueAt) }}</template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- All Readers -->
    <v-card v-if="!result" rounded="xl" elevation="1">
      <v-card-title class="font-weight-bold">Danh sách Độc giả</v-card-title>
      <v-list lines="two">
        <v-list-item v-for="reader in readers" :key="reader.cardNumber" @click="searchByCard(reader.cardNumber)">
          <template #prepend>
            <v-avatar color="primary" variant="tonal"><v-icon>mdi-account</v-icon></v-avatar>
          </template>
          <v-list-item-title class="font-weight-bold">{{ reader.cardNumber }}</v-list-item-title>
          <v-list-item-subtitle>{{ reader.count }} phiếu mượn · {{ reader.overdue > 0 ? `⚠️ ${reader.overdue} quá hạn` : '✅ Không quá hạn' }}</v-list-item-subtitle>
          <template #append>
            <v-chip size="small" :color="reader.overdue > 0 ? 'error' : 'info'" variant="tonal">
              {{ reader.active }} đang mượn
            </v-chip>
          </template>
        </v-list-item>
      </v-list>
    </v-card>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import { formatDate } from '@/utils/helpers'

const libStore = useLibrarianStore()
const cardNumber = ref('')
const result = ref(null)

const headers = [
  { title: 'Book ID', key: 'BookId' },
  { title: 'Ngày mượn', key: 'BorrowedAt' },
  { title: 'Hạn trả', key: 'DueAt' },
  { title: 'Trạng thái', key: 'Status' }
]

const readers = computed(() => {
  const map = {}
  libStore.transactions.forEach(t => {
    const card = t.CardNumber || t.cardNumber || '—'
    if (!map[card]) map[card] = { cardNumber: card, count: 0, active: 0, overdue: 0 }
    map[card].count++
    if (t.Status !== 'Returned') map[card].active++
    if (t.Status === 'Overdue') map[card].overdue++
  })
  return Object.values(map).sort((a, b) => b.overdue - a.overdue || b.active - a.active)
})

function search() { searchByCard(cardNumber.value) }

function searchByCard(card) {
  if (!card.trim()) return
  cardNumber.value = card
  const txs = libStore.transactions.filter(t => (t.CardNumber || t.cardNumber) === card)
  result.value = {
    cardNo: card,
    transactions: txs,
    active: txs.filter(t => t.Status !== 'Returned').length,
    overdue: txs.filter(t => t.Status === 'Overdue').length,
    returned: txs.filter(t => t.Status === 'Returned').length
  }
}

function statusColor(s) {
  if (s === 'Overdue') return 'error'
  if (s === 'Returned') return 'success'
  return 'info'
}
function statusLabel(s) {
  if (s === 'Overdue') return 'Quá hạn'
  if (s === 'Returned') return 'Đã trả'
  return 'Đang mượn'
}

onMounted(() => libStore.loadTransactions())
</script>
