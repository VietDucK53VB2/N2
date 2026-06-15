<template>
  <div class="dashboard">
    <!-- Hero Banner -->
    <v-card class="hero-banner mb-6" flat>
      <v-card-text class="d-flex align-center justify-space-between pa-6 position-relative" style="z-index:1">
        <div class="hero-text">
          <p class="hero-date">{{ currentDate }}</p>
          <h2 class="hero-title">
            Xin chào, <span class="hero-name">{{ firstName }}</span>! 👋
          </h2>
          <p class="hero-sub">Hôm nay bạn muốn khám phá cuốn sách nào?</p>
          <v-btn class="btn-gradient mt-3" size="default" prepend-icon="mdi-plus" @click="borrowDialog = true">
            Mượn sách mới
          </v-btn>
        </div>
        <div class="hero-stats-wrap">
          <div class="stats-row">
            <div v-for="(stat, idx) in heroStats" :key="stat.label" class="stat-glass" :style="{ animationDelay: idx * 80 + 'ms' }">
              <span class="stat-label">{{ stat.label }}</span>
              <span class="stat-num" :class="stat.class">{{ stat.value }}</span>
            </div>
          </div>
        </div>
      </v-card-text>
    </v-card>

    <!-- Category Chips -->
    <div class="category-scroll mb-6">
      <v-chip-group v-model="activeCategory" mandatory selected-class="chip-active" center-active>
        <v-chip
          v-for="cat in categories"
          :key="cat.value"
          :value="cat.value"
          variant="elevated"
          filter
          class="category-chip"
        >
          {{ cat.label }}
        </v-chip>
      </v-chip-group>
    </div>

    <!-- Popular Books -->
    <div class="section-header mb-4">
      <h3 class="section-title">
        <span class="title-icon">🔥</span> Sách phổ biến
      </h3>
      <v-btn-toggle v-model="topTab" mandatory density="compact" variant="outlined" rounded="xl" divided class="toggle-animated">
        <v-btn value="week" size="small">Tuần này</v-btn>
        <v-btn value="month" size="small">Tháng</v-btn>
        <v-btn value="all" size="small">Tất cả</v-btn>
      </v-btn-toggle>
    </div>

    <div class="books-row mb-8">
      <v-row>
        <v-col
          v-for="(item, index) in topBooks"
          :key="item.book.id"
          cols="6" sm="4" md="3" lg="2"
        >
          <v-card
            class="book-card"
            hover
            :style="{ animationDelay: index * 80 + 'ms' }"
            @click="openDetail(item.book, item.count)"
          >
            <div class="book-cover" :style="{ backgroundColor: titleColor(item.book.tenSach) }">
              <v-img v-if="item.book.imageUrl" :src="item.book.imageUrl" cover class="cover-img" />
              <v-icon v-else size="48" color="white" style="opacity:0.3;position:absolute;top:50%;left:50%;transform:translate(-50%,-50%)">mdi-book-open-variant</v-icon>
              <div class="cover-shine"></div>
              <v-chip class="rank-badge" size="x-small" :color="rankColor(index)" variant="flat">
                {{ index + 1 }}
              </v-chip>
              <v-chip v-if="item.count > 0" class="fire-badge" size="x-small" color="black" variant="flat">
                <v-icon size="10" start>mdi-fire</v-icon>{{ item.count }}
              </v-chip>
            </div>
            <v-card-text class="pa-3">
              <p class="book-title">{{ item.book.tenSach }}</p>
              <p class="book-author">{{ item.book.tacGia }}</p>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </div>

    <!-- My Books Preview -->
    <div class="section-header mb-4">
      <h3 class="section-title"><span class="title-icon">📖</span> Sách của tôi</h3>
      <v-btn variant="text" color="primary" append-icon="mdi-arrow-right" class="btn-slide" @click="$router.push('/mybooks')">
        Xem tất cả
      </v-btn>
    </div>

    <v-row v-if="store.activeTransactions.length" class="mb-8">
      <v-col v-for="(tx, idx) in store.activeTransactions.slice(0, 4)" :key="tx.Id || tx.id" cols="6" sm="4" md="3">
        <v-card class="book-card" :style="{ animationDelay: idx * 80 + 'ms' }" hover @click="$router.push('/mybooks')">
          <div class="book-cover" :style="{ backgroundColor: titleColor(tx.TenSach || tx.tenSach) }">
            <v-img v-if="tx.ImageUrl || tx.imageUrl" :src="tx.ImageUrl || tx.imageUrl" cover class="cover-img" />
            <div class="cover-shine"></div>
            <v-chip class="status-badge" size="x-small" :color="getStatusColor(tx)" variant="flat">
              {{ getTransactionStatusText(tx) }}
            </v-chip>
          </div>
          <v-card-text class="pa-3">
            <p class="book-title">{{ tx.TenSach || tx.tenSach || '—' }}</p>
            <p class="book-author">{{ tx.TacGia || tx.tacGia || '—' }}</p>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
    <v-card v-else flat class="empty-card text-center pa-10 mb-8">
      <div class="empty-icon-wrap">
        <v-icon size="56" color="grey-lighten-1">mdi-book-open-variant</v-icon>
      </div>
      <p class="text-body-1 text-grey mt-4 mb-4">Bạn chưa mượn sách nào</p>
      <v-btn class="btn-gradient" prepend-icon="mdi-plus" @click="borrowDialog = true">Mượn ngay</v-btn>
    </v-card>

    <!-- All Books Grid -->
    <div class="section-header mb-4">
      <h3 class="section-title"><span class="title-icon">🗂️</span> Kho sách</h3>
      <v-chip variant="tonal" color="primary" size="small" class="count-chip">
        <v-icon start size="14">mdi-bookshelf</v-icon>{{ filteredBooks.length }} cuốn
      </v-chip>
    </div>

    <v-row>
      <v-col
        v-for="(book, idx) in filteredBooks"
        :key="book.id"
        cols="6" sm="4" md="3" lg="2"
      >
        <v-card
          class="book-card"
          hover
          :style="{ animationDelay: Math.min(idx * 50, 600) + 'ms' }"
          @click="openDetail(book)"
        >
          <div class="book-cover" :style="{ backgroundColor: titleColor(book.tenSach) }">
            <v-img v-if="book.imageUrl" :src="book.imageUrl" cover class="cover-img" />
            <v-icon v-else size="48" color="white" style="opacity:0.3;position:absolute;top:50%;left:50%;transform:translate(-50%,-50%)">mdi-book-open-variant</v-icon>
            <div class="cover-shine"></div>
            <v-chip class="avail-badge" size="x-small" :color="book.soBanConLai > 0 ? 'success' : 'error'" variant="flat">
              {{ book.soBanConLai > 0 ? `Còn ${book.soBanConLai}` : 'Hết' }}
            </v-chip>
          </div>
          <v-card-text class="pa-3">
            <p class="book-title">{{ book.tenSach }}</p>
            <p class="book-author">{{ book.tacGia }}</p>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Modals -->
    <BookDetailDialog v-model="detailDialog" :book="selectedBook" :borrow-count="selectedBorrowCount" @borrowed="handleBorrowed" />
    <BorrowDialog v-model="borrowDialog" @success="handleQuickBorrowSuccess" />
    <v-snackbar v-model="successSnackbar" color="success" timeout="3500" location="bottom right">
      {{ successMessage }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useAppStore } from '@/stores/app'
import { titleColor, formatMoney, daysLeft } from '@/utils/helpers'
import BookDetailDialog from '@/components/BookDetailDialog.vue'
import BorrowDialog from '@/components/BorrowDialog.vue'
import dayjs from 'dayjs'
import 'dayjs/locale/vi'
dayjs.locale('vi')

const props = defineProps({ searchText: String })
const store = useAppStore()

const activeCategory = ref('all')
const topTab = ref('week')
const detailDialog = ref(false)
const selectedBook = ref(null)
const selectedBorrowCount = ref(0)
const borrowDialog = ref(false)
const successSnackbar = ref(false)
const successMessage = ref('')

const categories = [
  { label: '📚 Tất cả', value: 'all' },
  { label: '✨ Mới nhất', value: 'new' },
  { label: '🔥 Phổ biến', value: 'popular' },
  { label: '🖊️ Văn học', value: 'vanhoc' },
  { label: '🔬 Khoa học', value: 'khoahoc' },
  { label: '🏛️ Lịch sử', value: 'lichsu' },
  { label: '💼 Kinh tế', value: 'kinhte' },
  { label: '🧠 Tâm lý', value: 'tamly' }
]
const categoryKeywords = {
  vanhoc: ['văn', 'thơ', 'truyện', 'tiểu thuyết'], khoahoc: ['khoa học', 'science', 'vật lý'],
  lichsu: ['lịch sử', 'history', 'chiến'], kinhte: ['kinh tế', 'business', 'tài chính'],
  tamly: ['tâm lý', 'psychology', 'kỹ năng']
}

const currentDate = computed(() => dayjs().format('dddd, D [tháng] M, YYYY'))
const firstName = computed(() => (store.userInfo?.fullName || 'Độc giả').split(' ').pop())

const heroStats = computed(() => [
  { label: 'ĐANG MƯỢN', value: store.activeTransactions.length, class: '' },
  { label: 'QUÁ HẠN', value: store.overdueTransactions.length, class: 'text-red' },
  { label: 'PHÍ PHẠT', value: formatMoney(store.totalUnpaidFines), class: 'text-amber small-num' },
  { label: 'TỔNG MƯỢN', value: store.myTransactions.length, class: 'text-cyan' }
])

const filteredBooks = computed(() => {
  let books = store.books
  const kw = categoryKeywords[activeCategory.value]
  if (kw) { const f = books.filter(b => kw.some(k => b.tenSach.toLowerCase().includes(k))); if (f.length) books = f }
  const q = props.searchText?.toLowerCase()
  if (q) books = books.filter(b => b.tenSach.toLowerCase().includes(q) || b.tacGia.toLowerCase().includes(q))
  return books
})

const topBooks = computed(() => {
  const now = new Date()
  let txs = store.allTransactions
  if (topTab.value === 'week') txs = txs.filter(t => t.BorrowedAt && new Date(t.BorrowedAt) >= new Date(now - 7 * 864e5))
  else if (topTab.value === 'month') txs = txs.filter(t => t.BorrowedAt && new Date(t.BorrowedAt) >= new Date(now - 30 * 864e5))
  const cm = {}
  txs.filter(t => !store.isPending(t)).forEach(t => {
    const id = String(store.bookIdOf(t))
    if (id) cm[id] = (cm[id] || 0) + 1
  })
  let ranked = store.books.map(b => ({ book: b, count: cm[String(b.id)] || 0 })).sort((a, b) => b.count - a.count).slice(0, 8)
  if (ranked.every(r => r.count === 0)) ranked = store.books.map(b => ({ book: b, count: b.soLuong })).sort((a, b) => b.count - a.count).slice(0, 8)
  return ranked
})

function rankColor(i) { return ['amber-darken-1', 'blue-grey', 'deep-orange'][i] || 'grey-darken-1' }
function openDetail(book, count = 0) { selectedBook.value = book; selectedBorrowCount.value = count; detailDialog.value = true }
function handleBorrowed(payload = {}) {
  store.loadAll()
  const quantity = payload.quantity || 1
  const title = payload.title || 'sách'
  const total = payload.totalPrice ? ` - ${formatMoney(payload.totalPrice)}` : ''
  successMessage.value = `Đã gửi yêu cầu mượn ${quantity} cuốn "${title}"${total}. Vui lòng chờ thủ thư duyệt.`
  successSnackbar.value = true
}
async function handleQuickBorrowSuccess() {
  await store.loadAll()
  successMessage.value = 'Đã gửi yêu cầu mượn sách. Vui lòng chờ thủ thư duyệt.'
  successSnackbar.value = true
}
function getTransactionStatusText(tx) { return getStatusText(tx) }
function getStatusColor(tx) {
  if (store.isPending(tx)) return 'warning'
  if (store.isReturnPending(tx)) return 'deep-purple'
  if (store.isOverdue(tx)) return 'error'
  const d = daysLeft(tx.DueAt)
  return d !== null && d <= 3 ? 'warning' : 'info'
}
function getStatusText(tx) {
  if (store.isPending(tx)) return 'Chờ duyệt'
  if (store.isReturnPending(tx)) return 'Chờ trả'
  if (tx.Status === 'Overdue' || store.isOverdue(tx)) return 'Quá hạn'
  const d = daysLeft(tx.DueAt)
  return d !== null ? `Còn ${d} ngày` : 'Đang mượn'
}
</script>

<style scoped lang="scss">
.dashboard {
  animation: fadeIn 0.4s ease;
}

// ─── Hero ───
.hero-banner {
  background: linear-gradient(135deg, #064e3b 0%, #065f46 50%, #047857 100%) !important;
  border-radius: 20px !important;
  overflow: hidden;
  position: relative;
  box-shadow: 0 8px 32px rgba(6, 78, 59, 0.2);
}

.hero-date { font-size: 11px; color: rgba(255,255,255,0.5); font-weight: 600; }
.hero-title { font-size: 22px; font-weight: 800; color: #fff; margin: 4px 0; letter-spacing: -0.02em; line-height: 1.3; }
.hero-name { color: #fbbf24; }
.hero-sub { font-size: 13px; color: rgba(255,255,255,0.55); margin-top: 2px; }

.hero-stats-wrap {
  display: flex;
  align-items: center;
}

.stats-row {
  display: flex;
  gap: 10px;
}

.stat-glass {
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 14px;
  padding: 14px 20px;
  min-width: 110px;
  text-align: center;
  transition: all 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
  animation: statPop 0.4s ease both;
  backdrop-filter: blur(8px);

  &:hover { background: rgba(255, 255, 255, 0.15); transform: translateY(-3px) scale(1.03); box-shadow: 0 6px 20px rgba(0,0,0,0.15); }
}

.stat-label { display: block; font-size: 10px; font-weight: 700; color: rgba(255,255,255,0.5); letter-spacing: 0.03em; margin-bottom: 4px; text-transform: uppercase; }
.stat-num { display: block; font-size: 22px; font-weight: 800; color: #fff; letter-spacing: -0.02em; }
.stat-num.text-red { color: #fca5a5; }
.stat-num.text-amber { color: #fde68a; font-size: 14px; line-height: 2; }
.stat-num.text-cyan { color: #67e8f9; }

// ─── Categories ───
.category-scroll { display: flex; justify-content: center; }
.category-chip {
  font-weight: 600;
  transition: all 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
  &:hover { transform: translateY(-3px) scale(1.04); box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1); }
}
:deep(.chip-active) { background: linear-gradient(135deg, #047857, #065f46) !important; color: white !important; box-shadow: 0 4px 16px rgba(4, 120, 87, 0.35) !important; transform: scale(1.06); }

// ─── Section ───
.section-header { display: flex; align-items: center; justify-content: space-between; }
.section-title { font-size: 16px; font-weight: 800; color: #1e293b; margin: 0; display: flex; align-items: center; gap: 8px; letter-spacing: -0.02em; }
.title-icon { font-size: 20px; }

// ─── Book Cards ───
.book-card {
  border-radius: 12px !important;
  overflow: hidden;
  border: none !important;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08) !important;
  animation: cardIn 0.4s ease both;
  transition: transform 0.35s cubic-bezier(0.34, 1.56, 0.64, 1), box-shadow 0.3s;
  background: #fff !important;

  &:hover {
    transform: translateY(-8px) scale(1.02);
    box-shadow: 0 16px 40px rgba(0, 0, 0, 0.15) !important;
    .cover-img { transform: scale(1.06); }
    .cover-shine { opacity: 1; transform: translateX(100%); }
    .book-title { color: #047857; }
  }
}

.book-cover {
  position: relative;
  width: 100%;
  aspect-ratio: 2 / 3;
  overflow: hidden;
  border-radius: 0 0 0 0;
}

.cover-img {
  position: absolute !important;
  inset: 0;
  width: 100% !important;
  height: 100% !important;
  object-fit: cover;
  transition: transform 0.4s ease !important;
}

// Override Vuetify v-img to fill
:deep(.book-cover .v-img) {
  position: absolute !important;
  inset: 0;
  width: 100% !important;
  height: 100% !important;
}

:deep(.book-cover .v-img__img) {
  object-fit: cover !important;
}

.cover-shine {
  position: absolute; inset: 0;
  background: linear-gradient(105deg, transparent 40%, rgba(255,255,255,0.15) 45%, transparent 50%);
  opacity: 0; transition: opacity 0.3s, transform 0.5s; pointer-events: none;
}

.rank-badge { position: absolute; top: 6px; left: 6px; font-weight: 800; z-index: 2; }
.fire-badge { position: absolute; bottom: 6px; right: 6px; z-index: 2; }
.status-badge, .avail-badge { position: absolute; top: 6px; right: 6px; font-weight: 600; z-index: 2; }

.book-title {
  font-size: 13px; font-weight: 700; line-height: 1.4; letter-spacing: -0.01em;
  overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;
  color: #1a1a1a; transition: color 0.2s;
}
.book-author { font-size: 11px; color: #64748b; margin-top: 2px; font-weight: 500; }

// ─── Empty ───
.empty-card { border-radius: 16px !important; background: rgba(255,255,255,0.7) !important; border: 2px dashed rgba(4,120,87,0.15) !important; }
.empty-icon-wrap { animation: float 3s ease-in-out infinite; }

// ─── Buttons ───
.btn-gradient {
  background: linear-gradient(135deg, #047857, #065f46) !important;
  color: white !important; font-weight: 600; text-transform: none; border-radius: 10px;
  transition: all 0.25s;
  &:hover { transform: translateY(-2px); box-shadow: 0 4px 14px rgba(4,120,87,0.35); }
}
.btn-slide { transition: all 0.2s; &:hover { transform: translateX(3px); } }
.count-chip { animation: none; }

// ─── Responsive ───
@media (max-width: 960px) {
  .hero-banner .v-card-text { flex-direction: column; gap: 16px; align-items: flex-start !important; }
  .stats-row { flex-wrap: wrap; }
  .stat-glass { min-width: 80px; }
  .hero-title { font-size: 18px; }
}

// ─── Animations ───
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
@keyframes statPop { from { opacity: 0; transform: scale(0.9); } to { opacity: 1; transform: scale(1); } }
@keyframes cardIn { from { opacity: 0; transform: translateY(16px); } to { opacity: 1; transform: translateY(0); } }
@keyframes float { 0%, 100% { transform: translateY(0); } 50% { transform: translateY(-8px); } }
</style>
