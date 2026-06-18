<template>
  <div class="overview-page">
    <a-row :gutter="[16, 16]" class="stats-grid">
      <a-col v-for="(stat, idx) in stats" :key="stat.title" :xs="12" :lg="6">
        <a-card class="stat-card" :class="stat.className" :style="{ animationDelay: `${idx * 60}ms` }">
          <div class="stat-icon" :style="{ background: stat.bg }">
            <component :is="stat.icon" :style="{ color: stat.color, fontSize: '20px' }" />
          </div>
          <div class="stat-copy">
            <div class="stat-value" :style="{ color: stat.color }">{{ stat.value }}</div>
            <div class="stat-title">{{ stat.title }}</div>
          </div>
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="[16, 16]" class="dashboard-grid">
      <a-col :xs="24" :lg="16">
        <a-card class="panel-card">
          <template #title>
            <div class="panel-head">
              <div>
                <div class="panel-title">Lượt mượn sách</div>
                <div class="panel-subtitle">Theo tháng</div>
              </div>
            </div>
          </template>

          <div class="monthly-chart">
            <div class="chart-axis">
              <div v-for="tick in [400, 300, 200, 100, 0]" :key="tick" class="axis-row">
                <span>{{ tick }}</span>
                <div class="axis-line"></div>
              </div>
            </div>

            <div class="chart-bars">
              <div
                v-for="item in monthlyBorrowSeries"
                :key="item.label"
                class="bar-item"
              >
                <div class="bar-wrap">
                  <div class="bar-label">{{ item.value }}</div>
                  <div class="bar" :style="{ height: `${item.height}%` }"></div>
                </div>
                <div class="bar-month">{{ item.label }}</div>
              </div>
            </div>
          </div>
        </a-card>
      </a-col>

      <a-col :xs="24" :lg="8">
        <a-card class="panel-card full-height">
          <template #title>
            <div class="panel-head">
              <div>
                <div class="panel-title">Thống kê phiếu</div>
                <div class="panel-subtitle">Theo trạng thái</div>
              </div>
            </div>
          </template>

          <div class="donut-card">
            <div class="donut" :style="{ background: statusRingStyle }">
              <div class="donut-center">
                <span class="donut-label">Tổng phiếu</span>
                <strong>{{ store.transactions.length }}</strong>
              </div>
            </div>
            <div class="legend-list">
              <div v-for="item in statusStats" :key="item.label" class="legend-item">
                <span class="legend-dot" :style="{ background: item.color }"></span>
                <span class="legend-label">{{ item.label }}</span>
                <strong>{{ item.count }}</strong>
              </div>
            </div>
          </div>
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="[16, 16]" class="dashboard-grid">
      <a-col :xs="24" :lg="16">
        <a-card class="panel-card">
          <template #title>
            <div class="panel-head">
              <div>
                <div class="panel-title">Phân loại đầu sách</div>
                <div class="panel-subtitle">Theo thể loại</div>
              </div>
            </div>
          </template>

          <div class="category-layout">
            <div class="donut-panel">
              <div class="donut big" :style="{ background: categoryRingStyle }">
                <div class="donut-center">
                  <span class="donut-label">Tổng số</span>
                  <strong>{{ totalBooks }}</strong>
                </div>
              </div>
            </div>

            <div class="category-legend">
              <div
                v-for="(item, index) in topCategories"
                :key="item.label"
                class="category-row"
              >
                <div class="category-name">
                  <span class="legend-dot" :style="{ background: palette[index % palette.length] }"></span>
                  <span>{{ item.label }}</span>
                </div>
                <strong>{{ item.count }}</strong>
              </div>
            </div>
          </div>
        </a-card>
      </a-col>

      <a-col :xs="24" :lg="8">
        <a-card class="panel-card full-height">
          <template #title>
            <div class="panel-head">
              <div>
                <div class="panel-title">Top sách mượn nhiều</div>
                <div class="panel-subtitle">Xếp hạng phổ biến</div>
              </div>
            </div>
          </template>

          <div class="top-book-list">
            <div v-for="(book, index) in topBorrowedBooks" :key="book.id" class="top-book-item">
              <div class="rank" :class="{ alt: index === 1, alt2: index >= 2 }">{{ index + 1 }}</div>
              <div class="top-book-info">
                <div class="top-book-title">{{ book.title }}</div>
                <div class="top-book-author">{{ book.author }}</div>
                <div class="progress-line">
                  <span :style="{ width: `${book.percent}%`, background: index === 0 ? '#d98b07' : '#2a7c73' }"></span>
                </div>
              </div>
              <div class="top-book-count">{{ book.count }} lượt</div>
            </div>

            <a-empty v-if="!topBorrowedBooks.length" description="Chưa có dữ liệu" />
          </div>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import dayjs from 'dayjs'
import {
  FileTextOutlined,
  ClockCircleOutlined,
  BookOutlined,
  WarningOutlined
} from '@ant-design/icons-vue'
import { useLibrarianStore } from '@/stores/librarian'

const store = useLibrarianStore()
const catalogBooks = ref([])
const loadingCatalog = ref(false)

const palette = [
  '#275f58',
  '#d98b07',
  '#2aa197',
  '#f4c35b',
  '#3d5a80',
  '#f08a5d',
  '#8d6cab',
  '#1f7a72',
  '#ff9f68',
  '#3d7db3'
]

function token() {
  return localStorage.getItem('authToken') || localStorage.getItem('token') || ''
}

function headers() {
  const t = token()
  return {
    'Content-Type': 'application/json',
    ...(t ? { Authorization: `Bearer ${t}` } : {})
  }
}

function normalizeBook(item = {}) {
  const id = item.id ?? item.Id ?? item.bookId ?? item.BookId ?? item.ma ?? item.Ma ?? ""
  const title = item.tenSach ?? item.TenSach ?? item.tenSanPham ?? item.TenSanPham ?? item.title ?? item.Title ?? '?'
  const author = item.tacGia ?? item.TacGia ?? item.author ?? item.Author ?? '?'
  const category = item.theLoai ?? item.TheLoai ?? item.genre ?? item.Genre ?? item.category ?? item.Category ?? 'Ch?a ph?n lo?i'
  const imageUrl = item.imageUrl ?? item.ImageUrl ?? item.anhUrl ?? item.AnhUrl ?? item.anhBia ?? item.AnhBia ?? ''
  const available = Number(item.soBanConLai ?? item.SoBanConLai ?? 0)
  const status = item.trangThai ?? item.TrangThai ?? item.status ?? item.Status ?? ''
  const rating = Number(item.danhGiaTrungBinh ?? item.DanhGiaTrungBinh ?? item.averageRating ?? item.AverageRating ?? 0)
  const year = Number(item.namXuatBan ?? item.NamXuatBan ?? 0)
  return { id: String(id), title, author, category, imageUrl, available, status, rating, year }
}

async function loadCatalogBooks() {
  loadingCatalog.value = true
  try {
    const response = await fetch(`${window.location.origin}/api/catalog/books`, { headers: headers() })
    if (!response.ok) {
      catalogBooks.value = []
      return
    }
    const data = await response.json()
    catalogBooks.value = Array.isArray(data) ? data.map(normalizeBook) : []
  } catch {
    catalogBooks.value = []
  } finally {
    loadingCatalog.value = false
  }
}

const allTx = computed(() => [...store.transactions])

const totalReaders = computed(() => new Set(allTx.value.map(tx => String(tx.CardNumber || tx.cardNumber || '')).filter(Boolean)).size)
const totalCards = computed(() => totalReaders.value)
const totalLoans = computed(() => allTx.value.length)
const totalBooks = computed(() => catalogBooks.value.length)

const stats = computed(() => ([
  { title: 'Tổng số độc giả', value: totalReaders.value, color: '#275f58', bg: '#e8f4f1', icon: FileTextOutlined, className: 'accent-left' },
  { title: 'Tổng số thẻ', value: totalCards.value, color: '#d98b07', bg: '#fff1d5', icon: ClockCircleOutlined, className: 'accent-left' },
  { title: 'Tổng lượt mượn', value: totalLoans.value, color: '#2a7c73', bg: '#e5f4f2', icon: BookOutlined, className: 'accent-left' },
  { title: 'Tổng số lượng sách', value: totalBooks.value, color: '#d98b07', bg: '#fff3d7', icon: WarningOutlined, className: 'accent-right' }
]))

const statusStats = computed(() => {
  const map = [
    { label: 'Chờ duyệt', color: '#d98b07', count: 0 },
    { label: 'Đang mượn', color: '#275f58', count: 0 },
    { label: 'Chờ trả', color: '#8d8f9b', count: 0 },
    { label: 'Quá hạn', color: '#e05656', count: 0 },
    { label: 'Đã trả', color: '#3d7db3', count: 0 }
  ]
  for (const tx of allTx.value) {
    if (store.isPending(tx)) map[0].count += 1
    else if (store.isBorrowed(tx)) map[1].count += 1
    else if (store.isReturnPending(tx)) map[2].count += 1
    else if (store.isOverdue(tx)) map[3].count += 1
    else if (store.isReturned(tx)) map[4].count += 1
  }
  return map.filter(item => item.count > 0)
})

const topCategories = computed(() => {
  const grouped = new Map()
  for (const book of catalogBooks.value) {
    const label = String(book.category || 'Chưa phân loại').trim() || 'Chưa phân loại'
    grouped.set(label, (grouped.get(label) || 0) + 1)
  }
  return [...grouped.entries()]
    .map(([label, count]) => ({ label, count }))
    .sort((a, b) => b.count - a.count)
})

const monthlyBorrowSeries = computed(() => {
  const year = dayjs().year()
  const months = Array.from({ length: 6 }, (_, i) => {
    const month = i + 1
    const count = allTx.value.filter(tx => {
      const date = tx.BorrowedAt || tx.borrowedAt || tx.CreatedAt || tx.createdAt
      if (!date) return false
      const dt = dayjs(date)
      return dt.isValid() && dt.year() === year && dt.month() + 1 === month
    }).length
    return { label: dayjs(`${year}-${String(month).padStart(2, '0')}-01`).format('MMM'), value: count, height: 0 }
  })
  const max = Math.max(1, ...months.map(m => m.value))
  return months.map(item => ({
    ...item,
    height: Math.max(8, Math.round((item.value / max) * 100))
  }))
})

const topBorrowedBooks = computed(() => {
  const counts = new Map()
  for (const tx of allTx.value) {
    const id = String(tx.BookId || tx.bookId || '')
    if (!id) continue
    counts.set(id, (counts.get(id) || 0) + 1)
  }

  const max = Math.max(1, ...counts.values())
  return [...counts.entries()]
    .map(([id, count]) => {
      const match = catalogBooks.value.find(book => String(book.id) === id)
      return {
        id,
        title: match?.title || `Sách #${id}`,
        author: match?.author || 'Không rõ tác giả',
        count,
        percent: Math.max(12, Math.round((count / max) * 100))
      }
    })
    .sort((a, b) => b.count - a.count)
    .slice(0, 5)
})

const statusRingStyle = computed(() => {
  const total = Math.max(1, statusStats.value.reduce((sum, item) => sum + item.count, 0))
  if (!statusStats.value.length) return 'conic-gradient(#dbe3df 0% 100%)'
  let start = 0
  const parts = statusStats.value
    .map((item, index) => {
      const size = (item.count / total) * 100
      const end = start + size
      const color = item.color || palette[index % palette.length]
      const piece = `${color} ${start}% ${end}%`
      start = end
      return piece
    })
  return `conic-gradient(${parts.join(', ')})`
})

const categoryRingStyle = computed(() => {
  const total = Math.max(1, topCategories.value.reduce((sum, item) => sum + item.count, 0))
  let start = 0
  const parts = topCategories.value.slice(0, 10).map((item, index) => {
    const size = (item.count / total) * 100
    const end = start + size
    const color = palette[index % palette.length]
    const piece = `${color} ${start}% ${end}%`
    start = end
    return piece
  })
  return parts.length ? `conic-gradient(${parts.join(', ')})` : 'conic-gradient(#dbe3df 0% 100%)'
})

onMounted(async () => {
  await Promise.all([
    loadCatalogBooks(),
    store.loadAll()
  ])
})
</script>

<style scoped>
.overview-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  animation: fadeIn 0.35s ease;
}

.stats-grid,
.dashboard-grid {
  margin-bottom: 0;
}

.stat-card {
  border-radius: 16px !important;
  border: 1px solid #edf1ee !important;
  box-shadow: 0 10px 22px rgba(15, 23, 42, 0.04) !important;
  min-height: 96px;
  overflow: hidden;
  position: relative;
}

.stat-card::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 5px;
  background: #275f58;
}

.stat-card.accent-right::before {
  background: #d98b07;
}

.stat-card :deep(.ant-card-body) {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 18px 16px;
}

.stat-icon {
  width: 52px;
  height: 52px;
  min-width: 52px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.stat-copy {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.stat-value {
  font-size: 26px;
  font-weight: 800;
  line-height: 1;
  letter-spacing: -0.03em;
}

.stat-title {
  font-size: 13px;
  font-weight: 600;
  color: #77858d;
}

.panel-card {
  border-radius: 18px !important;
  border: 1px solid #edf1ee !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04) !important;
  height: 100%;
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
  align-items: center;
  justify-content: space-between;
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

.monthly-chart {
  display: grid;
  grid-template-columns: 72px 1fr;
  gap: 16px;
  align-items: end;
  min-height: 330px;
}

.chart-axis {
  padding-top: 8px;
  height: 300px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.axis-row {
  display: flex;
  align-items: center;
  gap: 10px;
  color: #7d8a83;
  font-size: 12px;
}

.axis-line {
  flex: 1;
  height: 1px;
  border-bottom: 1px dashed #e6ece8;
}

.chart-bars {
  height: 300px;
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  gap: 24px;
  align-items: end;
}

.bar-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
}

.bar-wrap {
  position: relative;
  height: 240px;
  width: 100%;
  display: flex;
  align-items: end;
  justify-content: center;
}

.bar {
  width: 68%;
  max-width: 74px;
  min-height: 14px;
  border-radius: 10px 10px 4px 4px;
  background: linear-gradient(180deg, #4c7f79 0%, #2f6761 100%);
  box-shadow: 0 10px 18px rgba(47, 103, 97, 0.22);
}

.bar-label {
  position: absolute;
  top: -22px;
  font-size: 12px;
  font-weight: 700;
  color: #1f5f55;
}

.bar-month {
  font-size: 13px;
  color: #7d8a83;
}

.donut-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 14px;
  min-height: 330px;
}

.donut {
  width: 188px;
  height: 188px;
  border-radius: 50%;
  position: relative;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.03);
}

.donut::after {
  content: '';
  position: absolute;
  inset: 24px;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 0 0 1px #eef2ee inset;
}

.donut.big {
  width: 220px;
  height: 220px;
}

.donut-center {
  position: absolute;
  inset: 0;
  z-index: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  text-align: center;
  color: #275f58;
  pointer-events: none;
}

.donut-label {
  font-size: 13px;
  color: #7d8a83;
}

.donut-center strong {
  font-size: 28px;
  line-height: 1.1;
}

.legend-list,
.category-legend {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.legend-item,
.category-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  color: #33514b;
  font-size: 13px;
  font-weight: 600;
}

.legend-item strong,
.category-row strong {
  font-size: 13px;
  color: #103b35;
}

.legend-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex: 0 0 auto;
}

.category-layout {
  display: grid;
  grid-template-columns: minmax(260px, 1fr) 240px;
  gap: 20px;
  align-items: center;
  min-height: 300px;
}

.donut-panel {
  display: flex;
  justify-content: center;
}

.category-name {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.top-book-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.top-book-item {
  display: grid;
  grid-template-columns: 36px minmax(0, 1fr) auto;
  gap: 12px;
  align-items: center;
  padding: 12px;
  border-radius: 14px;
  background: #fffaf3;
  border: 1px solid #edf1ee;
}

.rank {
  width: 36px;
  height: 36px;
  border-radius: 10px;
  background: linear-gradient(180deg, #d98b07 0%, #eb9f23 100%);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 800;
}

.rank.alt {
  background: linear-gradient(180deg, #8d94a8 0%, #9da6b7 100%);
}

.rank.alt2 {
  background: linear-gradient(180deg, #d9790b 0%, #e4881d 100%);
}

.top-book-info {
  min-width: 0;
}

.top-book-title {
  font-size: 14px;
  font-weight: 800;
  color: #103b35;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.top-book-author {
  font-size: 12px;
  color: #7d8a83;
  margin-top: 2px;
}

.progress-line {
  margin-top: 8px;
  height: 6px;
  background: #ecf1ee;
  border-radius: 999px;
  overflow: hidden;
}

.progress-line span {
  display: block;
  height: 100%;
  border-radius: inherit;
}

.top-book-count {
  font-size: 12px;
  font-weight: 800;
  color: #2f6761;
  background: #edf7f5;
  border-radius: 999px;
  padding: 5px 10px;
  white-space: nowrap;
}

.full-height {
  min-height: 100%;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

@media (max-width: 992px) {
  .category-layout {
    grid-template-columns: 1fr;
  }

  .monthly-chart {
    grid-template-columns: 1fr;
  }

  .chart-axis {
    display: none;
  }
}
</style>
