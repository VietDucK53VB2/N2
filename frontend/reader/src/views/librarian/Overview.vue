<template>
  <div class="page-shell">
    <v-row class="mb-6">
      <v-col v-for="stat in stats" :key="stat.label" cols="12" sm="6" md="3">
        <v-card rounded="xl" elevation="0" class="stat-card" :class="stat.class">
          <v-card-text class="pa-5">
            <div class="d-flex align-center justify-space-between mb-3">
              <span class="stat-label">{{ stat.label }}</span>
              <span class="stat-trend" :class="stat.trendClass">
                <v-icon size="12">{{ stat.trendIcon }}</v-icon> {{ stat.trend }}
              </span>
            </div>
            <p class="stat-value">{{ stat.value }}</p>
            <v-progress-linear
              :model-value="stat.progress"
              :color="stat.color"
              rounded
              height="4"
              class="mt-3"
              bg-color="transparent"
              style="opacity: 0.4"
            />
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-row class="mb-6">
      <v-col cols="12" md="8">
        <v-card rounded="xl" elevation="0" class="pa-5 chart-card">
          <div class="d-flex align-center justify-space-between mb-4">
            <h4 class="card-title">Thống kê mượn trả</h4>
            <v-btn-toggle v-model="chartPeriod" mandatory density="compact" variant="outlined" rounded="lg" divided>
              <v-btn value="week" size="x-small">Tuần</v-btn>
              <v-btn value="month" size="x-small">Tháng</v-btn>
            </v-btn-toggle>
          </div>
          <div class="chart-area">
            <div class="chart-bars">
              <div v-for="(bar, i) in chartData" :key="i" class="chart-bar-group">
                <div class="chart-bar borrowed" :style="{ height: bar.borrowed + '%' }"></div>
                <div class="chart-bar returned" :style="{ height: bar.returned + '%' }"></div>
                <span class="chart-label">{{ bar.label }}</span>
              </div>
            </div>
            <div class="chart-legend">
              <span class="legend-item"><span class="legend-dot borrowed"></span> Mượn</span>
              <span class="legend-item"><span class="legend-dot returned"></span> Trả</span>
            </div>
          </div>
        </v-card>
      </v-col>

      <v-col cols="12" md="4">
        <v-card rounded="xl" elevation="0" class="pa-5 activity-card">
          <h4 class="card-title mb-4">Hoạt động gần đây</h4>
          <div class="activity-list">
            <div v-for="(act, i) in recentActivities" :key="i" class="activity-item">
              <v-avatar :color="act.color" variant="tonal" size="32" class="mr-3">
                <v-icon size="16">{{ act.icon }}</v-icon>
              </v-avatar>
              <div class="flex-grow-1">
                <p class="activity-text">{{ act.text }}</p>
                <p class="activity-time">{{ act.time }}</p>
              </div>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="0">
      <v-card-text class="pa-5">
        <div class="d-flex align-center justify-space-between mb-4">
          <h4 class="card-title">Phiếu mượn gần đây</h4>
          <v-btn variant="text" color="primary" size="small" @click="$router.push({ name: 'lib-loans' })">
            Xem tất cả <v-icon end size="14">mdi-arrow-right</v-icon>
          </v-btn>
        </div>

        <v-data-table
          :headers="headers"
          :items="recentTransactions"
          :loading="libStore.loading"
          density="comfortable"
          :items-per-page="5"
          :hide-default-footer="recentTransactions.length <= 5"
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
          <template #item.BorrowedAt="{ item }">{{ formatDate(item.BorrowedAt) }}</template>
          <template #item.DueAt="{ item }">{{ formatDate(item.DueAt) }}</template>
          <template #item.Status="{ item }">
            <v-chip size="small" :color="statusColor(item)" variant="flat" class="font-weight-bold">
              {{ statusLabel(item) }}
            </v-chip>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import { formatDate, getDisplayBookTitle, getDisplayCardNumber, getDisplayReaderName } from '@/utils/helpers'

const libStore = useLibrarianStore()
const chartPeriod = ref('week')

const stats = computed(() => [
  {
    label: 'Tổng phiếu mượn',
    value: libStore.transactions.length,
    trend: '+12%',
    trendIcon: 'mdi-trending-up',
    trendClass: 'trend-up',
    color: '#7c3aed',
    progress: 75,
    class: 'stat-purple'
  },
  {
    label: 'Chờ duyệt',
    value: libStore.pendingCount,
    trend: `${libStore.pendingCount} mới`,
    trendIcon: 'mdi-clock',
    trendClass: 'trend-warn',
    color: '#f59e0b',
    progress: 40,
    class: 'stat-amber'
  },
  {
    label: 'Quá hạn',
    value: libStore.overdueCount,
    trend: 'Cần xử lý',
    trendIcon: 'mdi-alert',
    trendClass: 'trend-down',
    color: '#ef4444',
    progress: 25,
    class: 'stat-red'
  },
  {
    label: 'Đang mượn',
    value: libStore.activeCount,
    trend: 'Bình thường',
    trendIcon: 'mdi-check',
    trendClass: 'trend-up',
    color: '#22c55e',
    progress: 60,
    class: 'stat-green'
  }
])

const chartData = computed(() => {
  const labels = chartPeriod.value === 'week'
    ? ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']
    : ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9', 'T10', 'T11', 'T12']
  return labels.map(label => ({
    label,
    borrowed: Math.floor(Math.random() * 80) + 20,
    returned: Math.floor(Math.random() * 60) + 10
  }))
})

const recentActivities = computed(() => {
  const recent = [...libStore.transactions]
    .sort((a, b) => new Date(b.BorrowedAt) - new Date(a.BorrowedAt))
    .slice(0, 5)
  return recent.map(tx => ({
    icon: statusOf(tx) === 'Returned' ? 'mdi-check' : statusOf(tx) === 'Overdue' ? 'mdi-alert' : 'mdi-book-plus',
    color: statusOf(tx) === 'Returned' ? 'success' : statusOf(tx) === 'Overdue' ? 'error' : 'primary',
    text: `${displayReader(tx)} · ${displayBook(tx)} · ${statusLabel(tx)}`,
    time: formatDate(tx.BorrowedAt)
  }))
})

const recentTransactions = computed(() =>
  [...libStore.transactions].sort((a, b) => new Date(b.BorrowedAt) - new Date(a.BorrowedAt)).slice(0, 8)
)

const headers = [
  { title: 'Độc giả', key: 'reader', width: '220px', sortable: false },
  { title: 'Sách', key: 'book', width: '280px', sortable: false },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: '140px' },
  { title: 'Hạn trả', key: 'DueAt', width: '140px' },
  { title: 'Trạng thái', key: 'Status', width: '140px' }
]

function statusOf(item = {}) {
  const status = String(item.Status || item.status || '').trim()
  if (status === 'Pending' || status === 'Borrowed' || status === 'ReturnPending' || status === 'Returned' || status === 'Overdue') return status
  if (item.ReturnedAt || item.returnedAt) return 'Returned'
  if (item.DueAt && new Date(item.DueAt) < new Date()) return 'Overdue'
  return 'Borrowed'
}

function statusColor(item) {
  const status = statusOf(item)
  if (status === 'Pending') return 'warning'
  if (status === 'Overdue') return 'error'
  if (status === 'Returned') return 'success'
  if (status === 'ReturnPending') return 'purple'
  return 'info'
}

function statusLabel(item) {
  const status = statusOf(item)
  if (status === 'Pending') return 'Chờ duyệt'
  if (status === 'Overdue') return 'Quá hạn'
  if (status === 'Returned') return 'Đã trả'
  if (status === 'ReturnPending') return 'Chờ trả'
  return 'Đang mượn'
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

onMounted(() => libStore.loadAll())
</script>
