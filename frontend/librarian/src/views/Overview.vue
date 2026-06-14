<template>
  <div class="overview">
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

    <a-row :gutter="[20, 20]">
      <a-col :xs="24" :lg="16">
        <a-card class="content-card" :body-style="{ padding: 0 }">
          <template #title>
            <div class="card-header">
              <span class="card-header-title">Phiếu mượn gần đây</span>
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
            :custom-row="rowProps"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'reader'">
                <div class="cell-card">
                  <a-avatar size="small" :style="{ background: '#e6f7ef', color: '#047857' }">
                    {{ store.readerNameOf(record).slice(0, 1) }}
                  </a-avatar>
                  <div>
                    <div class="cell-card-text">{{ store.readerNameOf(record) }}</div>
                    <div class="muted">{{ store.cardNumberOf(record) }}</div>
                  </div>
                </div>
              </template>

              <template v-else-if="column.key === 'book'">
                <div>
                  <div class="cell-card-text">{{ store.bookTitleOf(record) }}</div>
                  <div class="muted">
                    {{ store.bookAuthorOf(record) || `Mã sách: ${store.bookIdOf(record)}` }}
                  </div>
                </div>
              </template>

              <template v-else-if="column.key === 'Status'">
                <a-tag :color="statusColor(record.Status)" class="status-tag">{{ statusLabel(record.Status) }}</a-tag>
              </template>

              <template v-else-if="column.key === 'BorrowedAt'">
                {{ fmtDate(record.BorrowedAt || record.borrowedAt) }}
              </template>

              <template v-else-if="column.key === 'DueAt'">
                {{ fmtDate(record.DueAt || record.dueAt) }}
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>

      <a-col :xs="24" :lg="8">
        <a-card class="content-card">
          <template #title>
            <span class="card-header-title">Hoạt động</span>
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

    <a-modal v-model:open="detailDialog" title="Chi tiết phiếu mượn" :footer="null" width="560px">
      <a-descriptions v-if="detailTx" bordered size="small" :column="1">
        <a-descriptions-item label="Người dùng">{{ store.readerNameOf(detailTx) }}</a-descriptions-item>
        <a-descriptions-item label="Mã thẻ">{{ store.cardNumberOf(detailTx) }}</a-descriptions-item>
        <a-descriptions-item label="Sách">{{ store.bookTitleOf(detailTx) }}</a-descriptions-item>
        <a-descriptions-item label="Mã sách">{{ store.bookIdOf(detailTx) }}</a-descriptions-item>
        <a-descriptions-item label="Ngày mượn">{{ fmtDate(detailTx.BorrowedAt || detailTx.borrowedAt) }}</a-descriptions-item>
        <a-descriptions-item label="Hạn trả">{{ fmtDate(detailTx.DueAt || detailTx.dueAt) }}</a-descriptions-item>
        <a-descriptions-item label="Trạng thái">{{ statusLabel(detailTx.Status) }}</a-descriptions-item>
      </a-descriptions>
      <div class="modal-footer">
        <a-button @click="detailDialog = false">Trở ra</a-button>
      </div>
    </a-modal>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import {
  FileTextOutlined,
  ClockCircleOutlined,
  WarningOutlined,
  CheckCircleOutlined,
  DollarOutlined,
  BookOutlined
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'

const store = useLibrarianStore()
const detailDialog = ref(false)
const detailTx = ref(null)

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
  { title: 'Độc giả', key: 'reader', width: 190 },
  { title: 'Sách', key: 'book', width: 220 },
  { title: 'Ngày mượn', key: 'BorrowedAt', width: 110 },
  { title: 'Hạn trả', key: 'DueAt', width: 110 },
  { title: 'Trạng thái', key: 'Status', width: 120 }
]

const recentTx = computed(() =>
  [...store.transactions]
    .sort((a, b) => new Date(b.BorrowedAt || b.borrowedAt) - new Date(a.BorrowedAt || a.borrowedAt))
    .slice(0, 8)
)

const activities = computed(() =>
  [...store.transactions]
    .sort((a, b) => new Date(b.BorrowedAt || b.borrowedAt) - new Date(a.BorrowedAt || a.borrowedAt))
    .slice(0, 8)
    .map(transaction => ({
      text: `${store.readerNameOf(transaction)} — ${store.bookTitleOf(transaction)} — ${statusLabel(transaction.Status)}`,
      time: fmtDate(transaction.BorrowedAt || transaction.borrowedAt),
      color: statusColor(transaction.Status)
    }))
)

function fmtDate(date) {
  return date ? dayjs(date).format('DD/MM/YYYY HH:mm:ss') : '—'
}

function rowProps(record) {
  return {
    class: 'clickable-row',
    onClick: () => {
      detailTx.value = record
      detailDialog.value = true
    }
  }
}

function statusColor(status) {
  if (status === 'Pending') return 'orange'
  if (status === 'ReturnPending') return 'purple'
  if (status === 'Overdue') return 'red'
  if (status === 'Returned') return 'green'
  return 'blue'
}

function statusLabel(status) {
  if (status === 'Pending') return 'Chờ duyệt'
  if (status === 'ReturnPending') return 'Chờ trả'
  if (status === 'Overdue') return 'Quá hạn'
  if (status === 'Returned') return 'Đã trả'
  return 'Đang mượn'
}
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
.stat-value { margin: 0; font-size: 20px; font-weight: 800; letter-spacing: 0; }

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
.card-header-title { font-weight: 700; font-size: 15px; letter-spacing: 0; }

.cell-card { display: flex; align-items: center; gap: 8px; }
.cell-card-text { font-weight: 600; font-size: 13px; }
.muted {
  color: #94a3b8;
  font-size: 12px;
  margin-top: 2px;
}
.status-tag { font-weight: 600; border-radius: 6px; }

.activity-timeline { padding-top: 4px; }
.activity-item { padding-bottom: 4px; }
.activity-text { margin: 0; font-size: 13px; font-weight: 500; color: #334155; }
.activity-time { margin: 0; font-size: 11px; color: #94a3b8; margin-top: 2px; }
.modal-footer {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
:deep(.clickable-row) {
  cursor: pointer;
}
:deep(.clickable-row:hover td) {
  background: #f0fdf4 !important;
}

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
@keyframes cardIn { from { opacity: 0; transform: translateY(12px); } to { opacity: 1; transform: translateY(0); } }
</style>
