<template>
  <div class="overview">
    <!-- Stat Cards -->
    <a-row :gutter="[16, 16]" class="mb-6">
      <a-col :xs="12" :sm="8" :md="4" v-for="(stat, idx) in stats" :key="stat.title">
        <a-card hoverable class="stat-card" :style="{ animationDelay: idx * 60 + 'ms' }">
          <div class="stat-icon-wrap" :style="{ background: stat.bgColor }">
            <component :is="stat.icon" :style="{ color: stat.color, fontSize: '18px' }" />
          </div>
          <div class="stat-info">
            <p class="stat-title">{{ stat.title }}</p>
            <p class="stat-value" :style="{ color: stat.color }">{{ stat.value }}</p>
          </div>
        </a-card>
      </a-col>
    </a-row>

    <!-- Main Content -->
    <a-row :gutter="[20, 20]">
      <!-- Table -->
      <a-col :xs="24" :lg="16">
        <a-card class="content-card" :body-style="{ padding: 0 }">
          <template #title>
            <div class="card-header">
              <span class="card-header-title">📋 Phiếu mượn gần đây</span>
              <a-button type="link" size="small" @click="$router.push({ name: 'loans' })">
                Xem tất cả →
              </a-button>
            </div>
          </template>
          <a-table
            :columns="columns"
            :data-source="recentTx"
            :loading="store.loading"
            :pagination="false"
            size="middle"
            row-key="Id"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'CardNumber'">
                <div class="cell-card">
                  <a-avatar size="small" :style="{ background: '#e6f7ef', color: '#047857' }">
                    {{ (record.CardNumber || '?')[0] }}
                  </a-avatar>
                  <span class="cell-card-text">{{ record.CardNumber || '—' }}</span>
                </div>
              </template>
              <template v-if="column.key === 'Status'">
                <a-tag :color="statusColor(record.Status)" class="status-tag">{{ statusLabel(record.Status) }}</a-tag>
              </template>
              <template v-if="column.key === 'BorrowedAt'">{{ fmtDate(record.BorrowedAt) }}</template>
              <template v-if="column.key === 'DueAt'">{{ fmtDate(record.DueAt) }}</template>
            </template>
          </a-table>
        </a-card>
      </a-col>

      <!-- Activity Timeline -->
      <a-col :xs="24" :lg="8">
        <a-card class="content-card">
          <template #title>
            <span class="card-header-title">⚡ Hoạt động</span>
          </template>
          <a-timeline class="activity-timeline">
            <a-timeline-item v-for="(act, i) in activities" :key="i" :color="act.color">
              <div class="activity-item">
                <p class="activity-text">{{ act.text }}</p>
                <p class="activity-time">{{ act.time }}</p>
              </div>
            </a-timeline-item>
          </a-timeline>
          <a-empty v-if="!activities.length" description="Chưa có hoạt động" />
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import { FileTextOutlined, ClockCircleOutlined, WarningOutlined, CheckCircleOutlined, DollarOutlined, BookOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'

const store = useLibrarianStore()

const stats = computed(() => [
  { title: 'Tổng phiếu', value: store.transactions.length, color: '#047857', bgColor: '#e6f7ef', icon: FileTextOutlined },
  { title: 'Chờ duyệt', value: store.pendingTx.length, color: '#d97706', bgColor: '#fef3c7', icon: ClockCircleOutlined },
  { title: 'Đang mượn', value: store.activeTx.length, color: '#2563eb', bgColor: '#dbeafe', icon: BookOutlined },
  { title: 'Chờ trả', value: store.returnPendingTx.length, color: '#7c3aed', bgColor: '#ede9fe', icon: ClockCircleOutlined },
  { title: 'Quá hạn', value: store.overdueTx.length, color: '#dc2626', bgColor: '#fee2e2', icon: WarningOutlined },
  { title: 'Đã trả', value: store.returnedTx.length, color: '#0f766e', bgColor: '#ccfbf1', icon: CheckCircleOutlined },
  { title: 'Phí chưa thu', value: store.totalUnpaid.toLocaleString() + 'đ', color: '#b45309', bgColor: '#fef3c7', icon: DollarOutlined }
])

const columns = [
  { title: 'Độc giả', key: 'CardNumber', width: 160 },
  { title: 'Book ID', dataIndex: 'BookId', key: 'BookId', width: 90 },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: 110 },
  { title: 'Hạn trả', key: 'DueAt', width: 110 },
  { title: 'Trạng thái', key: 'Status', width: 100 }
]

const recentTx = computed(() => [...store.transactions].sort((a, b) => new Date(b.BorrowedAt) - new Date(a.BorrowedAt)).slice(0, 8))

const activities = computed(() =>
  [...store.transactions].sort((a, b) => new Date(b.BorrowedAt) - new Date(a.BorrowedAt)).slice(0, 8).map(t => ({
    text: `${t.CardNumber || '—'} — ${t.Status === 'Returned' ? 'Đã trả sách' : t.Status === 'ReturnPending' ? 'Chờ xác nhận trả' : t.Status === 'Overdue' ? 'Quá hạn' : t.Status === 'Pending' ? 'Chờ duyệt' : 'Mượn sách'}`,
    time: fmtDate(t.BorrowedAt),
    color: t.Status === 'Returned' ? 'green' : t.Status === 'ReturnPending' ? 'purple' : t.Status === 'Overdue' ? 'red' : t.Status === 'Pending' ? 'orange' : 'blue'
  }))
)

function fmtDate(d) { return d ? dayjs(d).format('DD/MM/YYYY') : '—' }
function statusColor(s) { return s === 'Pending' ? 'orange' : s === 'ReturnPending' ? 'purple' : s === 'Overdue' ? 'red' : s === 'Returned' ? 'green' : 'blue' }
function statusLabel(s) { return s === 'Pending' ? 'Chờ duyệt' : s === 'ReturnPending' ? 'Chờ trả' : s === 'Overdue' ? 'Quá hạn' : s === 'Returned' ? 'Đã trả' : 'Đang mượn' }
</script>

<style scoped>
.overview { animation: fadeIn 0.4s ease; }
.mb-6 { margin-bottom: 24px; }

.stat-card {
  border-radius: 14px !important;
  border: none !important;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04) !important;
  transition: transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1), box-shadow 0.3s;
  animation: cardIn 0.4s ease both;
}
.stat-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.1) !important;
}
.stat-card :deep(.ant-card-body) {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 18px 16px;
}

.stat-icon-wrap {
  width: 44px;
  height: 44px;
  min-width: 44px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.stat-info { flex: 1; min-width: 0; }
.stat-title { margin: 0; font-size: 12px; font-weight: 600; color: #94a3b8; }
.stat-value { margin: 0; font-size: 20px; font-weight: 800; letter-spacing: -0.02em; }

.content-card {
  border-radius: 16px !important;
  border: none !important;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04) !important;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.card-header-title { font-weight: 700; font-size: 15px; letter-spacing: -0.01em; }

.cell-card { display: flex; align-items: center; gap: 8px; }
.cell-card-text { font-weight: 600; font-size: 13px; }

.status-tag { font-weight: 600; border-radius: 6px; }

.activity-timeline { padding-top: 4px; }
.activity-item { padding-bottom: 4px; }
.activity-text { margin: 0; font-size: 13px; font-weight: 500; color: #334155; }
.activity-time { margin: 0; font-size: 11px; color: #94a3b8; margin-top: 2px; }

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
@keyframes cardIn { from { opacity: 0; transform: translateY(12px); } to { opacity: 1; transform: translateY(0); } }
</style>
