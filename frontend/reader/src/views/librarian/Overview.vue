<template>
  <div>
    <!-- Stat Cards Row -->
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
              style="opacity:0.4"
            />
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Middle Row: Chart + Activity -->
    <v-row class="mb-6">
      <!-- Biểu đồ phiếu mượn -->
      <v-col cols="12" md="8">
        <v-card rounded="xl" elevation="0" class="pa-5 chart-card">
          <div class="d-flex align-center justify-space-between mb-4">
            <h4 class="card-title">Thống kê mượn trả</h4>
            <v-btn-toggle v-model="chartPeriod" mandatory density="compact" variant="outlined" rounded="lg" divided>
              <v-btn value="week" size="x-small">Tuần</v-btn>
              <v-btn value="month" size="x-small">Tháng</v-btn>
            </v-btn-toggle>
          </div>
          <!-- Simple chart visualization -->
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

      <!-- Hoạt động gần đây -->
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

    <!-- Bottom: Recent Transactions Table -->
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
          <template #item.CardNumber="{ item }">
            <div class="d-flex align-center ga-2">
              <v-avatar size="28" color="primary" variant="tonal">
                <v-icon size="14">mdi-account</v-icon>
              </v-avatar>
              <span class="font-weight-medium">{{ item.CardNumber || item.cardNumber }}</span>
            </div>
          </template>
          <template #item.BorrowedAt="{ item }">
            {{ formatDate(item.BorrowedAt) }}
          </template>
          <template #item.DueAt="{ item }">
            {{ formatDate(item.DueAt) }}
          </template>
          <template #item.Status="{ item }">
            <v-chip size="small" :color="statusColor(item.Status)" variant="flat" class="font-weight-bold">
              {{ statusLabel(item.Status) }}
            </v-chip>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import { formatDate } from '@/utils/helpers'

const libStore = useLibrarianStore()
const chartPeriod = ref('week')

const stats = computed(() => [
  {
    label: 'Tổng phiếu mượn', value: libStore.transactions.length,
    trend: '+12%', trendIcon: 'mdi-trending-up', trendClass: 'trend-up',
    color: '#7c3aed', progress: 75, class: 'stat-purple'
  },
  {
    label: 'Chờ duyệt', value: libStore.pendingCount,
    trend: libStore.pendingCount + ' mới', trendIcon: 'mdi-clock', trendClass: 'trend-warn',
    color: '#f59e0b', progress: 40, class: 'stat-amber'
  },
  {
    label: 'Quá hạn', value: libStore.overdueCount,
    trend: 'Cần xử lý', trendIcon: 'mdi-alert', trendClass: 'trend-down',
    color: '#ef4444', progress: 25, class: 'stat-red'
  },
  {
    label: 'Đang mượn', value: libStore.activeCount,
    trend: 'Bình thường', trendIcon: 'mdi-check', trendClass: 'trend-up',
    color: '#22c55e', progress: 60, class: 'stat-green'
  }
])

const chartData = computed(() => {
  const labels = chartPeriod.value === 'week'
    ? ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']
    : ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9', 'T10', 'T11', 'T12']
  return labels.map(l => ({
    label: l,
    borrowed: Math.floor(Math.random() * 80) + 20,
    returned: Math.floor(Math.random() * 60) + 10
  }))
})

const recentActivities = computed(() => {
  const recent = [...libStore.transactions]
    .sort((a, b) => new Date(b.BorrowedAt) - new Date(a.BorrowedAt))
    .slice(0, 5)
  return recent.map(t => ({
    icon: t.Status === 'Returned' ? 'mdi-check' : t.Status === 'Overdue' ? 'mdi-alert' : 'mdi-book-plus',
    color: t.Status === 'Returned' ? 'success' : t.Status === 'Overdue' ? 'error' : 'primary',
    text: `${t.CardNumber || '—'} — ${t.Status === 'Returned' ? 'Đã trả' : t.Status === 'Overdue' ? 'Quá hạn' : 'Mượn sách'}`,
    time: formatDate(t.BorrowedAt)
  }))
})

const recentTransactions = computed(() =>
  [...libStore.transactions].sort((a, b) => new Date(b.BorrowedAt) - new Date(a.BorrowedAt)).slice(0, 8)
)

const headers = [
  { title: 'Độc giả', key: 'CardNumber', width: '160px' },
  { title: 'Book ID', key: 'BookId', width: '100px' },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: '120px' },
  { title: 'Hạn trả', key: 'DueAt', width: '120px' },
  { title: 'Trạng thái', key: 'Status', width: '120px' }
]

function statusColor(s) {
  if (s === 'Pending') return 'warning'
  if (s === 'Overdue') return 'error'
  if (s === 'Returned') return 'success'
  return 'info'
}
function statusLabel(s) {
  if (s === 'Pending') return 'Chờ duyệt'
  if (s === 'Overdue') return 'Quá hạn'
  if (s === 'Returned') return 'Đã trả'
  return 'Đang mượn'
}

onMounted(() => libStore.loadAll())
</script>

<style scoped lang="scss">
.stat-card {
  border: 1px solid #f0f0f5;
  transition: transform 0.2s, box-shadow 0.2s;

  &:hover {
    transform: translateY(-4px);
    box-shadow: 0 12px 32px rgba(0, 0, 0, 0.08);
  }
}

.stat-label {
  font-size: 12px;
  font-weight: 600;
  color: #94a3b8;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.stat-value {
  font-size: 32px;
  font-weight: 800;
  color: #1e1b4b;
  margin: 0;
}

.stat-trend {
  font-size: 11px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 2px;
}

.trend-up { color: #22c55e; }
.trend-down { color: #ef4444; }
.trend-warn { color: #f59e0b; }

.card-title {
  font-size: 15px;
  font-weight: 700;
  color: #1e1b4b;
  margin: 0;
}

.chart-card, .activity-card {
  border: 1px solid #f0f0f5;
}

.chart-area {
  height: 200px;
  display: flex;
  flex-direction: column;
}

.chart-bars {
  flex: 1;
  display: flex;
  align-items: flex-end;
  gap: 8px;
  padding-bottom: 24px;
}

.chart-bar-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 3px;
  position: relative;
}

.chart-bar {
  width: 60%;
  border-radius: 4px 4px 0 0;
  transition: height 0.5s ease;
  min-height: 4px;

  &.borrowed { background: linear-gradient(to top, #7c3aed, #a855f7); }
  &.returned { background: linear-gradient(to top, #c026d3, #e879f9); width: 40%; }
}

.chart-label {
  position: absolute;
  bottom: -20px;
  font-size: 10px;
  color: #94a3b8;
  font-weight: 600;
}

.chart-legend {
  display: flex;
  gap: 16px;
  justify-content: center;
  margin-top: 8px;
}

.legend-item {
  font-size: 11px;
  color: #64748b;
  display: flex;
  align-items: center;
  gap: 6px;
}

.legend-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;

  &.borrowed { background: #7c3aed; }
  &.returned { background: #c026d3; }
}

.activity-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.activity-item {
  display: flex;
  align-items: center;
}

.activity-text {
  font-size: 13px;
  font-weight: 500;
  color: #334155;
  margin: 0;
}

.activity-time {
  font-size: 11px;
  color: #94a3b8;
  margin: 0;
}
</style>
