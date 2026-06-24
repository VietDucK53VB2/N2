<template>
  <div class="page-shell">
    <v-card rounded="xl" elevation="1" class="mb-6">
      <v-card-text>
        <v-row dense>
          <v-col cols="12" md="8">
            <v-text-field
              v-model="cardNumber"
              label="Nhập mã thẻ hoặc tên độc giả"
              prepend-inner-icon="mdi-card-account-details"
              append-inner-icon="mdi-magnify"
              hide-details
              @click:append-inner="search"
              @keyup.enter="search"
            />
          </v-col>
          <v-col cols="12" md="4" class="d-flex align-center">
            <v-btn color="primary" variant="flat" class="w-100" @click="search">
              <v-icon start>mdi-magnify</v-icon> Tra cứu
            </v-btn>
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <v-card v-if="result" rounded="xl" elevation="1" class="mb-6">
      <v-card-title class="d-flex align-center ga-3">
        <v-avatar color="primary" variant="tonal"><v-icon>mdi-account</v-icon></v-avatar>
        <div>
          <p class="text-subtitle-1 font-weight-bold mb-0">{{ result.readerName }}</p>
          <p class="text-caption text-grey mb-0">{{ result.cardNo }} · {{ result.transactions.length }} phiếu mượn</p>
        </div>
      </v-card-title>
      <v-card-text>
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
          density="comfortable"
          :items-per-page="5"
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
          <template #item.BorrowedAt="{ item }">{{ formatDate(item.BorrowedAt || item.borrowedAt) }}</template>
          <template #item.DueAt="{ item }">{{ formatDate(item.DueAt || item.dueAt) }}</template>
          <template #item.Status="{ item }">
            <v-chip size="x-small" :color="statusColor(item)" variant="flat">{{ statusLabel(item) }}</v-chip>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <v-card v-if="!result" rounded="xl" elevation="1">
      <v-card-title class="font-weight-bold">Danh sách độc giả</v-card-title>
      <v-list lines="two">
        <v-list-item v-for="reader in readers" :key="reader.cardNumber" @click="searchByCard(reader.cardNumber)">
          <template #prepend>
            <v-avatar color="primary" variant="tonal"><v-icon>mdi-account</v-icon></v-avatar>
          </template>
          <v-list-item-title class="font-weight-bold">{{ reader.name }}</v-list-item-title>
          <v-list-item-subtitle>
            {{ reader.cardNumber }} · {{ reader.count }} phiếu mượn
            · {{ reader.overdue > 0 ? `${reader.overdue} quá hạn` : 'Không quá hạn' }}
          </v-list-item-subtitle>
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
import { computed, onMounted, ref } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import { formatDate, getDisplayBookTitle, getDisplayCardNumber, getDisplayReaderName } from '@/utils/helpers'

const libStore = useLibrarianStore()
const cardNumber = ref('')
const result = ref(null)

const headers = [
  { title: 'Độc giả', key: 'reader', width: '220px', sortable: false },
  { title: 'Sách', key: 'book', width: '280px', sortable: false },
  { title: 'Ngày mượn', key: 'BorrowedAt' },
  { title: 'Hạn trả', key: 'DueAt' },
  { title: 'Trạng thái', key: 'Status' }
]

const readers = computed(() => {
  const map = new Map()
  libStore.transactions.forEach(tx => {
    const card = tx.CardNumber || tx.cardNumber || '—'
    const name = displayReader(tx)
    if (!map.has(card)) {
      map.set(card, { cardNumber: card, name, count: 0, active: 0, overdue: 0 })
    }
    const item = map.get(card)
    item.count += 1
    if (statusOf(tx) !== 'Returned') item.active += 1
    if (statusOf(tx) === 'Overdue') item.overdue += 1
    if (!item.name && name) item.name = name
  })
  return [...map.values()].sort((a, b) => b.overdue - a.overdue || b.active - a.active || a.name.localeCompare(b.name, 'vi'))
})

function statusOf(item = {}) {
  const status = String(item.Status || item.status || '').trim()
  if (status === 'Pending' || status === 'Borrowed' || status === 'ReturnPending' || status === 'Returned' || status === 'Overdue') return status
  if (item.ReturnedAt || item.returnedAt) return 'Returned'
  if (item.DueAt && new Date(item.DueAt) < new Date()) return 'Overdue'
  return 'Borrowed'
}

function search() {
  searchByCard(cardNumber.value)
}

function searchByCard(card) {
  const normalized = String(card || '').trim()
  if (!normalized) return
  cardNumber.value = normalized
  const txs = libStore.transactions.filter(tx => {
    const txCard = String(tx.CardNumber || tx.cardNumber || '').trim()
    const txName = displayReader(tx).toLowerCase()
    const query = normalized.toLowerCase()
    return txCard === normalized || txName.includes(query)
  })
  result.value = {
    cardNo: normalized,
    readerName: txs[0] ? displayReader(txs[0]) : normalized,
    transactions: txs,
    active: txs.filter(tx => statusOf(tx) !== 'Returned').length,
    overdue: txs.filter(tx => statusOf(tx) === 'Overdue').length,
    returned: txs.filter(tx => statusOf(tx) === 'Returned').length
  }
}

function displayReader(item = {}) {
  return getDisplayReaderName(item, item.CardNumber || item.cardNumber || 'Độc giả')
}

function displayCard(item = {}) {
  return getDisplayCardNumber(item, item.CardNumber || item.cardNumber || '—')
}

function displayBook(item = {}) {
  return getDisplayBookTitle(item, item.BookId || item.bookId || '—')
}

function statusColor(item) {
  const status = statusOf(item)
  if (status === 'Overdue') return 'error'
  if (status === 'Returned') return 'success'
  if (status === 'ReturnPending') return 'purple'
  if (status === 'Pending') return 'warning'
  return 'info'
}

function statusLabel(item) {
  const status = statusOf(item)
  if (status === 'Overdue') return 'Quá hạn'
  if (status === 'Returned') return 'Đã trả'
  if (status === 'ReturnPending') return 'Chờ trả'
  if (status === 'Pending') return 'Chờ duyệt'
  return 'Đang mượn'
}

onMounted(() => libStore.loadTransactions())
</script>
