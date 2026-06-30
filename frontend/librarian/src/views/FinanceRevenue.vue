<template>
  <div class="page-shell">
    <a-card class="hero-card" :bordered="false">
      <div class="hero-layout">
        <div class="hero-copy">
          <div class="eyebrow">QUẢN LÝ DOANH THU</div>
          <h1>Doanh thu {{ scopeTitle }}</h1>
          <p>
            Theo dõi doanh thu theo ngày, tháng, năm hoặc một khoảng tùy chọn.
            Dữ liệu đang lấy trực tiếp từ backend và đồng bộ với các phiếu mượn, phí phạt.
          </p>
          <div class="hero-tags">
            <a-tag color="green">{{ records.length }} giao dịch trong kỳ</a-tag>
            <a-tag color="gold">{{ periodLabel }}</a-tag>
            <a-tag color="blue">Cập nhật từ backend</a-tag>
          </div>
        </div>

        <div class="hero-filters">
          <div class="filter-title">Xem theo</div>
          <a-segmented v-model:value="periodMode" :options="periodOptions" size="large" class="period-segmented" />

          <a-date-picker
            v-if="periodMode === 'day'"
            v-model:value="pickedDay"
            format="DD/MM/YYYY"
            class="time-picker"
            allow-clear
          />
          <a-date-picker
            v-else-if="periodMode === 'month'"
            v-model:value="pickedMonth"
            picker="month"
            format="MM/YYYY"
            class="time-picker"
            allow-clear
          />
          <a-date-picker
            v-else-if="periodMode === 'year'"
            v-model:value="pickedYear"
            picker="year"
            format="YYYY"
            class="time-picker"
            allow-clear
          />
          <a-range-picker
            v-else-if="periodMode === 'range'"
            v-model:value="pickedRange"
            format="DD/MM/YYYY"
            class="time-picker"
            allow-clear
          />
          <div v-else class="all-time-note">
            Xem toàn bộ dữ liệu doanh thu đang có trong hệ thống.
          </div>

          <a-space :size="10" wrap>
            <a-button type="primary" class="refresh-btn" :loading="loading" @click="reloadRevenue">
              Làm mới
            </a-button>
            <a-button class="ghost-btn" @click="setDefaultFilter">Đặt mặc định</a-button>
          </a-space>
        </div>
      </div>
    </a-card>

    <a-row :gutter="[16, 16]" class="stats-row">
      <a-col :xs="24" :md="6">
        <a-card class="mini-card accent-green">
          <a-statistic title="Tổng doanh thu" :value="store.totalRevenue" :precision="0" suffix="đ" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="6">
        <a-card class="mini-card accent-teal">
          <a-statistic title="Thu từ mượn" :value="store.totalBorrowRevenue" :precision="0" suffix="đ" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="6">
        <a-card class="mini-card accent-amber">
          <a-statistic title="Thu từ phí phạt" :value="store.totalFineRevenue" :precision="0" suffix="đ" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="6">
        <a-card class="mini-card accent-red">
          <a-statistic title="Chờ duyệt phí phạt" :value="store.pendingFineAmount" :precision="0" suffix="đ" />
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="[16, 16]">
      <a-col :xs="24" :xl="16">
        <a-card class="panel-card">
          <template #title>
            <div class="panel-head">
              <div>
                <div class="panel-title">Doanh thu đã ghi nhận</div>
                <div class="panel-subtitle">
                  {{ records.length }} dòng dữ liệu trong {{ periodLabel.toLowerCase() }}
                </div>
              </div>
              <a-tag color="green">{{ money(store.totalRevenue) }}</a-tag>
            </div>
          </template>

          <a-alert
            type="success"
            show-icon
            class="mb-4 revenue-alert"
            message="Doanh thu được tính từ backend"
            description="Mỗi lượt duyệt mượn sẽ ghi nhận theo số ngày mượn. Mỗi lượt duyệt phí phạt sẽ ghi nhận đúng số tiền của phiếu đó."
          />

          <a-table
            :columns="columns"
            :data-source="records"
            :loading="loading"
            :pagination="{ pageSize: 8, showSizeChanger: false }"
            size="middle"
            row-key="Id"
            :locale="{ emptyText: 'Chưa có dữ liệu trong khoảng thời gian này' }"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'SourceType'">
                <a-tag :color="record.SourceType === 'BorrowApproval' ? 'green' : 'orange'">
                  {{ record.SourceType === 'BorrowApproval' ? 'Thu mượn' : 'Thu phí phạt' }}
                </a-tag>
              </template>
              <template v-else-if="column.key === 'ReferenceId'">
                <div class="ref-cell">{{ record.ReferenceId }}</div>
              </template>
              <template v-else-if="column.key === 'Description'">
                <div class="desc-cell">
                  <div class="reader-line" v-if="displayCard(record) !== '—' || displayReader(record) !== '—'">
                    {{ displayReader(record) }} · {{ displayCard(record) }}
                  </div>
                  <div>{{ record.Description }}</div>
                </div>
              </template>
              <template v-else-if="column.key === 'Amount'">
                <span class="money">{{ money(record.Amount) }}</span>
              </template>
              <template v-else-if="column.key === 'CreatedAt'">
                {{ fmtDateTime(record.CreatedAt) }}
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>

      <a-col :xs="24" :xl="8">
        <a-space direction="vertical" style="width: 100%" :size="16">
          <a-card class="panel-card">
            <template #title>
              <div class="panel-head">
                <div>
                  <div class="panel-title">Tổng quan trong kỳ</div>
                  <div class="panel-subtitle">Số liệu cập nhật theo bộ lọc hiện tại</div>
                </div>
              </div>
            </template>

            <div class="summary-stack">
              <div class="summary-item">
                <span>Thu mượn</span>
                <strong>{{ money(store.totalBorrowRevenue) }}</strong>
              </div>
              <div class="summary-item">
                <span>Thu phí phạt</span>
                <strong>{{ money(store.totalFineRevenue) }}</strong>
              </div>
              <div class="summary-item">
                <span>Phí phạt chưa thanh toán</span>
                <strong>{{ money(store.unpaidFineAmount) }}</strong>
              </div>
              <div class="summary-item">
                <span>Số khoản đã thu</span>
                <strong>{{ store.fineRevenueCount }}</strong>
              </div>
            </div>
          </a-card>

          <a-card class="panel-card">
            <template #title>
              <div class="panel-head">
                <div>
                  <div class="panel-title">Phân loại giao dịch</div>
                  <div class="panel-subtitle">Tỷ trọng theo loại doanh thu</div>
                </div>
              </div>
            </template>

            <div class="type-list">
              <div v-for="item in typeBreakdown" :key="item.label" class="type-row">
                <div class="type-copy">
                  <span class="type-dot" :style="{ background: item.color }"></span>
                  <span>{{ item.label }}</span>
                </div>
                <strong>{{ item.count }}</strong>
              </div>
            </div>
          </a-card>
        </a-space>
      </a-col>
    </a-row>
  </div>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import dayjs from 'dayjs'
import { useLibrarianStore } from '@/stores/librarian'

const store = useLibrarianStore()
const loading = ref(false)
const periodMode = ref('month')
const pickedDay = ref(dayjs())
const pickedMonth = ref(dayjs())
const pickedYear = ref(dayjs())
const pickedRange = ref([dayjs().subtract(29, 'day'), dayjs()])

const periodOptions = [
  { label: 'Tất cả', value: 'all' },
  { label: 'Ngày', value: 'day' },
  { label: 'Tháng', value: 'month' },
  { label: 'Năm', value: 'year' },
  { label: 'Khoảng', value: 'range' }
]

const columns = [
  { title: 'Loại', key: 'SourceType', width: 120 },
  { title: 'Mã tham chiếu', key: 'ReferenceId', width: 220 },
  { title: 'Diễn giải', key: 'Description' },
  { title: 'Số tiền', key: 'Amount', width: 140 },
  { title: 'Thời điểm thu', key: 'CreatedAt', width: 170 }
]

const records = computed(() => Array.isArray(store.recentRevenue) ? store.recentRevenue : [])

const periodLabel = computed(() => {
  if (periodMode.value === 'all') return 'tất cả thời gian'
  if (periodMode.value === 'day') return pickedDay.value ? dayjs(pickedDay.value).format('DD/MM/YYYY') : 'hôm nay'
  if (periodMode.value === 'month') return pickedMonth.value ? `tháng ${dayjs(pickedMonth.value).format('MM/YYYY')}` : 'tháng này'
  if (periodMode.value === 'year') return pickedYear.value ? `năm ${dayjs(pickedYear.value).format('YYYY')}` : 'năm nay'
  if (periodMode.value === 'range' && Array.isArray(pickedRange.value) && pickedRange.value.length === 2) {
    return `${dayjs(pickedRange.value[0]).format('DD/MM/YYYY')} - ${dayjs(pickedRange.value[1]).format('DD/MM/YYYY')}`
  }
  return 'khoảng tuỳ chọn'
})

const scopeTitle = computed(() => {
  if (periodMode.value === 'all') return 'tổng thể'
  if (periodMode.value === 'day') return 'theo ngày'
  if (periodMode.value === 'month') return 'theo tháng'
  if (periodMode.value === 'year') return 'theo năm'
  return 'theo khoảng tuỳ chọn'
})

const typeBreakdown = computed(() => {
  const items = [
    { label: 'Thu mượn', key: 'BorrowApproval', color: '#1f5f55', count: 0 },
    { label: 'Thu phí phạt', key: 'FinePayment', color: '#d97706', count: 0 }
  ]

  for (const record of records.value) {
    const type = String(record.SourceType || '').trim()
    const match = items.find(item => item.key === type)
    if (match) match.count += 1
  }

  return items
})

function money(value) {
  return `${Number(value || 0).toLocaleString()} đ`
}

function fmtDateTime(value) {
  return value ? dayjs(value).format('DD/MM/YYYY HH:mm:ss') : '—'
}

function displayReader(record = {}) {
  return record.ReaderName || record.readerName || record.FullName || record.fullName || record.ReaderUsername || record.readerUsername || displayCard(record)
}

function displayCard(record = {}) {
  return record.CardNumber || record.cardNumber || '—'
}

function buildRevenueParams() {
  if (periodMode.value === 'all') {
    return { period: 'all', take: 200 }
  }

  if (periodMode.value === 'day') {
    return {
      period: 'day',
      date: pickedDay.value ? dayjs(pickedDay.value).format('YYYY-MM-DD') : dayjs().format('YYYY-MM-DD'),
      take: 200
    }
  }

  if (periodMode.value === 'month') {
    return {
      period: 'month',
      date: pickedMonth.value ? dayjs(pickedMonth.value).format('YYYY-MM-DD') : dayjs().format('YYYY-MM-DD'),
      take: 200
    }
  }

  if (periodMode.value === 'year') {
    return {
      period: 'year',
      date: pickedYear.value ? dayjs(pickedYear.value).format('YYYY-MM-DD') : dayjs().format('YYYY-MM-DD'),
      take: 200
    }
  }

  if (Array.isArray(pickedRange.value) && pickedRange.value.length === 2) {
    return {
      period: 'range',
      from: dayjs(pickedRange.value[0]).format('YYYY-MM-DD'),
      to: dayjs(pickedRange.value[1]).format('YYYY-MM-DD'),
      take: 300
    }
  }

  return { period: 'range', take: 300 }
}

async function reloadRevenue() {
  loading.value = true
  try {
    await store.loadRevenueSummary(buildRevenueParams())
    if (!store.fines.length) {
      await store.loadFines()
    }
  } finally {
    loading.value = false
  }
}

function setDefaultFilter() {
  periodMode.value = 'month'
  pickedDay.value = dayjs()
  pickedMonth.value = dayjs()
  pickedYear.value = dayjs()
  pickedRange.value = [dayjs().subtract(29, 'day'), dayjs()]
}

watch(
  [periodMode, pickedDay, pickedMonth, pickedYear, pickedRange],
  () => {
    void reloadRevenue()
  },
  { deep: true, immediate: true }
)
</script>

<style scoped>
.page-shell {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.hero-card,
.mini-card,
.panel-card {
  border-radius: 18px !important;
  border: 1px solid #edf1ee !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04) !important;
}

.hero-card {
  background:
    radial-gradient(circle at top right, rgba(217, 139, 7, 0.12), transparent 28%),
    linear-gradient(180deg, #ffffff 0%, #fbfcfb 100%);
}

.hero-card :deep(.ant-card-body) {
  padding: 22px 24px;
}

.hero-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(320px, 0.8fr);
  gap: 20px;
  align-items: start;
}

.hero-copy h1 {
  margin: 6px 0 10px;
  font-size: 30px;
  line-height: 1.1;
  font-weight: 900;
  color: #103b35;
  letter-spacing: -0.03em;
}

.hero-copy p {
  max-width: 720px;
  color: #6f7d7a;
  line-height: 1.6;
  margin: 0;
}

.eyebrow {
  font-size: 12px;
  letter-spacing: 0.12em;
  font-weight: 800;
  color: #1f5f55;
}

.hero-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 14px;
}

.hero-filters {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  background: #f8fbf8;
  border: 1px solid #e5eee7;
  border-radius: 16px;
}

.filter-title {
  font-size: 13px;
  font-weight: 800;
  color: #103b35;
}

.period-segmented {
  width: 100%;
}

.time-picker {
  width: 100%;
}

.all-time-note {
  padding: 12px 14px;
  border-radius: 14px;
  background: #fffaf2;
  border: 1px solid #f1dcc0;
  color: #8a5f1b;
  font-size: 13px;
  line-height: 1.5;
}

.refresh-btn {
  background: #1f5f55;
  border-color: #1f5f55;
}

.refresh-btn:hover {
  background: #194d45;
  border-color: #194d45;
}

.ghost-btn {
  border-color: #d7e4dd;
  color: #33514b;
}

.stats-row {
  margin-bottom: 0;
}

.mini-card :deep(.ant-card-body) {
  padding: 18px 20px;
}

.mini-card.accent-green::before,
.mini-card.accent-teal::before,
.mini-card.accent-amber::before,
.mini-card.accent-red::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 5px;
  border-radius: 18px 0 0 18px;
}

.mini-card.accent-green::before {
  background: #1f5f55;
}

.mini-card.accent-teal::before {
  background: #2a7c73;
}

.mini-card.accent-amber::before {
  background: #d97706;
}

.mini-card.accent-red::before {
  background: #dc2626;
}

.panel-card :deep(.ant-card-head) {
  border-bottom: none;
  padding: 18px 20px 0;
}

.panel-card :deep(.ant-card-body) {
  padding: 18px 20px 20px;
}

.panel-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.panel-title {
  font-size: 16px;
  font-weight: 800;
  color: #103b35;
  letter-spacing: -0.01em;
}

.panel-subtitle {
  font-size: 12px;
  color: #8c98a5;
  margin-top: 2px;
}

.revenue-alert {
  margin-bottom: 16px;
  border-radius: 14px;
}

.money {
  font-weight: 800;
  color: #dc2626;
}

.ref-cell,
.desc-cell {
  font-size: 13px;
  color: #33514b;
  word-break: break-word;
}

.reader-line {
  margin-bottom: 4px;
  color: #0f3d35;
  font-weight: 700;
}

.summary-stack,
.type-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.summary-item,
.type-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 14px;
  border-radius: 14px;
  background: #fbfcfb;
  border: 1px solid #edf1ee;
}

.summary-item span,
.type-copy {
  font-size: 13px;
  color: #6f7d7a;
}

.summary-item strong,
.type-row strong {
  font-size: 16px;
  color: #103b35;
}

.type-copy {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.type-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex: 0 0 auto;
}

@media (max-width: 1200px) {
  .hero-layout {
    grid-template-columns: 1fr;
  }
}
</style>
