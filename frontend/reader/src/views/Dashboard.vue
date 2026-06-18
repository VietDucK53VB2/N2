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
    <div class="dashboard-sections">
    <!-- My Books Preview -->
    <div class="section-mybooks" :style="{ order: booksFirst ? 2 : 1 }">
      <div class="section-header mb-4">
        <h3 class="section-title"><span class="title-icon">📖</span> Sách của tôi</h3>
        <v-btn variant="text" color="primary" append-icon="mdi-arrow-right" class="btn-slide" @click="$router.push('/mybooks')">
          Xem tất cả
        </v-btn>
      </div>

      <v-row v-if="store.activeTransactions.length" class="mb-8">
        <v-col v-for="(tx, idx) in store.activeTransactions.slice(0, 4)" :key="tx.Id || tx.id" cols="6" sm="4" md="3" lg="2">
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
              <p class="book-authơr">{{ tx.TacGia || tx.tacGia || '—' }}</p>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
      <v-card v-else flat class="empty-card text-center pa-10 mb-8">
        <div class="empty-icon-wrap">
          <v-icon size="56" color="grey-lighten-1">mdi-book-open-variant</v-icon>
        </div>
        <p class="text-body-1 text-grey mt-4 mb-4">Bạn chưa mượn sách nào</p>
      </v-card>
    </div>

    <div class="section-books" :style="{ order: booksFirst ? 1 : 2 }">
      <!-- All Books Grid -->
      <div class="section-header mb-4 books-header">
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
              <v-btn
                icon
                size="x-small"
                class="heart-btn"
                :class="{ 'is-favorite': store.isFavorite(book.id) }"
                type="button"
                :ripple="false"
                @click.stop.prevent="toggleFavorite(book, $event)"
              >
                <v-icon size="18" :color="store.isFavorite(book.id) ? 'pink' : 'grey-darken-1'">
                  {{ store.isFavorite(book.id) ? 'mdi-heart' : 'mdi-heart-outline' }}
                </v-icon>
              </v-btn>
              <v-chip
                v-if="activeCategory === 'popular' && book._borrowCount > 0"
                class="fire-badge"
                size="x-small"
                color="black"
                variant="flat"
              >
                <v-icon size="10" start>mdi-fire</v-icon>{{ book._borrowCount }}
              </v-chip>
            </div>
            <v-card-text class="pa-3">
              <p class="book-title">{{ book.tenSach }}</p>
              <p class="book-authơr">{{ book.tacGia }}</p>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </div>
    </div>

    <!-- Modals -->
    <BookDetailDialog v-model="detailDialog" :book="selectedBook" :borrow-count="selectedBorrowCount" @borrowed="handleBorrowed" />
    <v-snackbar v-model="successSnackbar" color="success" timeout="3500" location="bottom right">
      {{ successMessage }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useAppStore } from '@/stores/app'
import { titleColor, formatMoney, daysLeft, getDisplayName } from '@/utils/helpers'
import BookDetailDialog from '@/components/BookDetailDialog.vue'
import dayjs from 'dayjs'
import 'dayjs/locale/vi'
dayjs.locale('vi')

const props = defineProps({ searchText: String })
const store = useAppStore()

const activeCategory = ref('all')
const detailDialog = ref(false)
const selectedBook = ref(null)
const selectedBorrowCount = ref(0)
const successSnackbar = ref(false)
const successMessage = ref('')

const categories = [
  { label: '📚 Tất cả', value: 'all' },
  { label: '✨ Mới nhất', value: 'new' },
  { label: '🔥 Phổ biến', value: 'popular' }
]
const categoryKeywords = {
  vanhoc: ['văn', 'thơ', 'truyện', 'tiểu thuyết'], khoahoc: ['khoa học', 'science', 'vật lý'],
  lichsu: ['lịch sử', 'history', 'chiến'], kinhte: ['kinh tế', 'business', 'tài chính'],
  tamly: ['tâm lý', 'psychology', 'kỹ năng']
}

const currentDate = computed(() => dayjs().format('dddd, D [tháng] M, YYYY'))
const firstName = computed(() => {
  const candidate = getDisplayName(store.userInfo || {}, 'Độc giả')
  const parts = String(candidate).trim().split(/\s+/).filter(Boolean)
  return parts.length ? parts[parts.length - 1] : candidate
})
const booksFirst = computed(() => activeCategory.value !== 'all')

const heroStats = computed(() => [
  { label: 'ĐANG MƯỢN', value: store.activeTransactions.length, class: '' },
  { label: 'QUÁ HẠN', value: store.overdueTransactions.length, class: 'text-red' },
  { label: 'PHÍ PHẠT', value: formatMoney(store.totalUnpaidFines), class: 'text-amber small-num' },
  { label: 'TỔNG MƯỢN', value: store.myTransactions.length, class: 'text-cyan' }
])

const filteredBooks = computed(() => {
  const ranked = popularBooks.value.map(item => item.book)
  const rankedIds = new Set(ranked.map(book => String(book.id)))
  let books = [
    ...ranked.map((book, index) => ({ ...book, _popularRank: index + 1, _borrowCount: popularCountMap.value[String(book.id)] || 0 })),
    ...store.books.filter(book => !rankedIds.has(String(book.id)))
  ]
  const kw = categoryKeywords[activeCategory.value]
  if (kw) { const f = books.filter(b => kw.some(k => b.tenSach.toLowerCase().includes(k))); if (f.length) books = f }
  const q = props.searchText?.toLowerCase()
  if (q) books = books.filter(b => b.tenSach.toLowerCase().includes(q) || b.tacGia.toLowerCase().includes(q))
  books = books.filter(b => Number(b.soBanConLai ?? 1) > 0)
  return books
})

const popularBooks = computed(() => {
  const now = new Date()
  let txs = store.allTransactions
  txs = txs.filter(t => t.BorrowedAt && new Date(t.BorrowedAt) >= new Date(now - 30 * 864e5))
  const cm = {}
  txs.filter(t => !store.isPending(t)).forEach(t => {
    const id = String(store.bookIdOf(t))
    if (id) cm[id] = (cm[id] || 0) + 1
  })
  let ranked = store.books.map(b => ({ book: b, count: cm[String(b.id)] || 0 })).sort((a, b) => b.count - a.count).slice(0, 8)
  if (ranked.every(r => r.count === 0)) ranked = store.books.map(b => ({ book: b, count: b.soLuong })).sort((a, b) => b.count - a.count).slice(0, 8)
  return ranked
})

const popularCountMap = computed(() => {
  const counts = {}
  store.allTransactions.forEach(tx => {
    if (store.isPending(tx)) return
    const id = String(store.bookIdOf(tx))
    if (!id) return
    counts[id] = (counts[id] || 0) + 1
  })
  return counts
})

function rankColor(i) { return ['amber-darken-1', 'blue-grey', 'deep-orange'][i] || 'grey-darken-1' }
function openDetail(book, count = 0) { selectedBook.value = book; selectedBorrowCount.value = count; detailDialog.value = true }
async function toggleFavorite(book, event) {
  event?.preventDefault?.()
  event?.stopPropagation?.()
  await store.toggleFavorite(book)
}
function handleBorrowed(payload = {}) {
  store.loadAll()
  const quantity = payload.quantity || 1
  const title = payload.title || 'sách'
  const total = payload.totalPrice ? ` - ${formatMoney(payload.totalPrice)}` : ''
  successMessage.value = `Đã gửi yêu cầu mượn ${quantity} cuốn "${title}"${total}. Vui lòng chờ thủ thư duyệt.`
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

// --- Hero ---
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

// --- Categories ---
.category-scroll { display: flex; justify-content: center; }
.category-chip {
  font-weight: 600;
  transition: all 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
  &:hover { transform: translateY(-3px) scale(1.04); box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1); }
}
:deep(.chip-active) { background: linear-gradient(135deg, #047857, #065f46) !important; color: white !important; box-shadow: 0 4px 16px rgba(4, 120, 87, 0.35) !important; transform: scale(1.06); }

// --- Section ---
.section-header { display: flex; align-items: center; justify-content: space-between; }
.section-title { font-size: 16px; font-weight: 800; color: #1e293b; margin: 0; display: flex; align-items: center; gap: 8px; letter-spacing: -0.02em; }
.title-icon { font-size: 20px; }
.dashboard-sections { display: flex; flex-direction: column; }
.section-mybooks, .section-books { width: 100%; }

// --- Book Cards ---
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
.status-badge { position: absolute; top: 6px; right: 6px; font-weight: 600; z-index: 2; }
.heart-btn {
  position: absolute;
  top: 6px;
  left: 6px;
  z-index: 2;
  background: rgba(255,255,255,0.95);
  box-shadow: 0 4px 14px rgba(15, 23, 42, 0.12);
}
.heart-btn.is-favorite {
  background: rgba(255,255,255,0.98);
}

.book-title {
  font-size: 13px; font-weight: 700; line-height: 1.4; letter-spacing: -0.01em;
  overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;
  color: #1a1a1a; transition: color 0.2s;
}
.book-authơr { font-size: 11px; color: #64748b; margin-top: 2px; font-weight: 500; }

// --- Empty ---
.empty-card { border-radius: 16px !important; background: rgba(255,255,255,0.7) !important; border: 2px dashed rgba(4,120,87,0.15) !important; }
.empty-icon-wrap { animation: float 3s ease-in-out infinite; }

// --- Buttons ---
.btn-gradient {
  background: linear-gradient(135deg, #047857, #065f46) !important;
  color: white !important; font-weight: 600; text-transform: none; border-radius: 10px;
  transition: all 0.25s;
  &:hover { transform: translateY(-2px); box-shadow: 0 4px 14px rgba(4,120,87,0.35); }
}
.btn-slide { transition: all 0.2s; &:hover { transform: translateX(3px); } }
.count-chip { animation: none; }

// --- Responsive ---
@media (max-width: 960px) {
  .hero-banner .v-card-text { flex-direction: column; gap: 16px; align-items: flex-start !important; }
  .stats-row { flex-wrap: wrap; }
  .stat-glass { min-width: 80px; }
  .hero-title { font-size: 18px; }
}

// --- Animations ---
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
@keyframes statPop { from { opacity: 0; transform: scale(0.9); } to { opacity: 1; transform: scale(1); } }
@keyframes cardIn { from { opacity: 0; transform: translateY(16px); } to { opacity: 1; transform: translateY(0); } }
@keyframes float { 0%, 100% { transform: translateY(0); } 50% { transform: translateY(-8px); } }
</style>








