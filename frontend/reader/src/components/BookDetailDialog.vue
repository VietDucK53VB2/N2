<template>
  <v-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)" max-width="680" scrollable>
    <v-card v-if="book" rounded="xl" class="overflow-hidden">
      <!-- Hero -->
      <div class="detail-hero" :style="heroBg">
        <div class="hero-overlay"></div>
        <v-btn
          class="detail-favorite"
          :class="{ 'favorite-active': isFavorite }"
          :icon="isFavorite ? 'mdi-heart' : 'mdi-heart-outline'"
          variant="flat"
          size="small"
          @click="toggleFavorite"
        />
        <div class="hero-content">
          <div class="hero-cover" :style="{ backgroundColor: titleColor(book.tenSach) }">
            <v-img v-if="book.imageUrl" :src="book.imageUrl" cover height="100%" />
            <v-icon v-else size="36" color="white" style="opacity:0.4">mdi-book-open-variant</v-icon>
          </div>
          <div class="flex-grow-1">
            <div class="d-flex ga-2 mb-2">
              <v-chip size="small" :color="book.soBanConLai > 0 ? 'success' : 'error'" variant="flat">
                {{ book.soBanConLai > 0 ? 'Có thể mượn' : 'Hết bản' }}
              </v-chip>
              <v-chip v-if="borrowCount" size="small" color="deep-orange" variant="flat">
                <v-icon start size="12">mdi-fire</v-icon>{{ borrowCount }} lượt mượn
              </v-chip>
            </div>
            <h2 class="text-h6 font-weight-black text-white">{{ book.tenSach }}</h2>
            <p class="text-body-2 text-white-70"><v-icon size="14">mdi-account-edit</v-icon> {{ book.tacGia }}</p>
            <p v-if="book.nhaSanXuat" class="text-caption text-white-50"><v-icon size="12">mdi-domain</v-icon> {{ book.nhaSanXuat }}</p>
            <p v-if="book.isbn" class="text-caption text-white-50"><v-icon size="12">mdi-barcode</v-icon> ISBN: {{ book.isbn }}</p>
          </div>
        </div>
      </div>

      <!-- Body -->
      <v-card-text class="pa-6" style="max-height:460px;overflow-y:auto">
        <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4">
          {{ errorMessage }}
        </v-alert>
        <div class="mb-3">
          <v-chip v-for="tag in genres" :key="tag" size="small" color="orange" variant="tonal" class="mr-1 mb-1">
            {{ tag }}
          </v-chip>
        </div>
        <h4 class="text-subtitle-2 font-weight-bold mb-2">
          <v-icon size="16" color="primary">mdi-text</v-icon> Tóm tắt nội dung
        </h4>
        <p class="text-body-2 text-grey">
          {{ book.moTa || `"${book.tenSach}" là một trong những cuốn sách được độc giả yêu thích.` }}
        </p>
        <div class="review-panel mt-5">
          <div class="review-head">
            <div>
              <p class="panel-label mb-1">Đánh giá độc giả</p>
              <div class="rating-line">
                <v-rating :model-value="averageRating" density="compact" size="18" color="amber" half-increments readonly />
                <span class="rating-score">{{ averageRating.toFixed(1) }}/5</span>
                <span class="review-count">({{ reviewCount }} lượt)</span>
              </div>
            </div>
            <v-btn-toggle v-model="reviewSort" density="compact" mandatory rounded="lg" variant="outlined" class="review-sort">
              <v-btn size="x-small" value="desc">Cao nhất</v-btn>
              <v-btn size="x-small" value="asc">Thấp nhất</v-btn>
            </v-btn-toggle>
          </div>

          <v-progress-linear v-if="reviewsLoading" indeterminate color="primary" height="2" class="mt-3" />
          <div v-else-if="sortedReviews.length" class="review-list">
            <div v-for="review in sortedReviews" :key="review.id" class="review-item">
              <div class="review-avatar">{{ review.initial }}</div>
              <div class="review-content">
                <div class="review-meta">
                  <strong>{{ review.name }}</strong>
                  <v-rating :model-value="review.rating" density="compact" size="14" color="amber" readonly />
                </div>
                <p v-if="review.comment" class="review-comment">{{ review.comment }}</p>
                <p class="review-date">{{ review.createdAt }}</p>
              </div>
            </div>
          </div>
          <div v-else class="review-empty">Chưa có đánh giá cho cuốn sách này.</div>
        </div>
        <div class="borrow-panel mt-5">
          <div>
            <p class="panel-label">Giá mượn</p>
            <p class="panel-value">{{ formatMoney(unitPrice) }} / cuốn</p>
          </div>
          <div class="qty-control">
            <v-btn icon="mdi-minus" size="x-small" variant="tonal" :disabled="quantity <= 1" @click="quantity--" />
            <span class="qty-value">{{ quantity }}</span>
            <v-btn icon="mdi-plus" size="x-small" variant="tonal" :disabled="quantity >= maxQuantity" @click="quantity++" />
          </div>
          <div class="text-right">
            <p class="panel-label">Tổng tiền</p>
            <p class="panel-value text-primary">{{ formatMoney(totalPrice) }}</p>
          </div>
        </div>
        <v-text-field
          v-model="returnAt"
          type="datetime-local"
          label="Ngày giờ trả sách"
          prepend-inner-icon="mdi-calendar-clock"
          density="comfortable"
          variant="outlined"
          :min="minReturnAt"
          hide-details
          class="mt-4"
        />
      </v-card-text>

      <!-- Footer -->
      <v-card-actions class="pa-4 border-t">
        <v-btn variant="text" @click="$emit('update:modelValue', false)">Đóng</v-btn>
        <v-spacer />
        <v-btn
          class="btn-gradient"
          size="large"
          :disabled="book.soBanConLai <= 0"
          :loading="borrowing"
          prepend-icon="mdi-book-plus"
          @click="handleBorrow"
        >
          Mượn sách ngay
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { useAppStore } from '@/stores/app'
import { borrowBook, fetchBookReviews, getReaderCard } from '@/utils/api'
import { titleColor, getGenreTags, formatMoney } from '@/utils/helpers'
import { bookKey, loadFavoriteIds, toggleFavoriteBook } from '@/utils/favorites'

const props = defineProps({
  modelValue: Boolean,
  book: Object,
  borrowCount: { type: Number, default: 0 }
})
const emit = defineEmits(['update:modelValue', 'borrowed', 'favorite-changed'])
const store = useAppStore()
const borrowing = ref(false)
const errorMessage = ref('')
const quantity = ref(1)
const returnAt = ref(defaultReturnAt())
const reviews = ref([])
const reviewsLoading = ref(false)
const reviewSort = ref('desc')
const favoriteBooks = ref(loadFavoriteIds())

const heroBg = computed(() => {
  if (props.book?.imageUrl) return { backgroundImage: `url(${props.book.imageUrl})` }
  return { backgroundColor: titleColor(props.book?.tenSach) }
})
const genres = computed(() => getGenreTags(props.book?.tenSach))
const maxQuantity = computed(() => Math.max(1, Math.min(Number(props.book?.soBanConLai || 1), 10)))
const unitPrice = computed(() => getBorrowUnitPrice(props.book))
const totalPrice = computed(() => unitPrice.value * quantity.value)
const minReturnAt = computed(() => toDateTimeLocal(new Date(Date.now() + 60 * 60 * 1000)))
const averageRating = computed(() => {
  if (!reviews.value.length) return 0
  return reviews.value.reduce((sum, review) => sum + Number(review.rating || 0), 0) / reviews.value.length
})
const reviewCount = computed(() => reviews.value.length)
const sortedReviews = computed(() => [...reviews.value].sort((a, b) => {
  const diff = reviewSort.value === 'asc' ? a.rating - b.rating : b.rating - a.rating
  if (diff) return diff
  return new Date(b.rawCreatedAt || 0) - new Date(a.rawCreatedAt || 0)
}))
const isFavorite = computed(() => {
  const id = bookKey(props.book)
  return !!id && favoriteBooks.value.has(id)
})

watch(() => props.book?.id, () => {
  quantity.value = 1
  returnAt.value = defaultReturnAt()
  errorMessage.value = ''
  reviews.value = []
  if (props.modelValue) loadReviews()
})

watch(() => props.modelValue, open => {
  if (open) loadReviews()
})

async function handleBorrow() {
  errorMessage.value = ''
  const card = getReaderCard()
  const bookId = props.book?.id

  if (!card) {
    errorMessage.value = 'Không tìm thấy mã thẻ độc giả. Vui lòng đăng nhập lại.'
    return
  }
  if (!bookId) {
    errorMessage.value = 'Không tìm thấy mã sách.'
    return
  }

  borrowing.value = true
  try {
    const response = await borrowBook(card, bookId, quantity.value, returnAt.value)
    if (!response.ok) {
      const data = await response.json().catch(() => null)
      errorMessage.value = borrowErrorMessage(data)
      return
    }
    await store.loadAll()
    emit('borrowed', {
      title: props.book?.tenSach,
      quantity: quantity.value,
      totalPrice: totalPrice.value
    })
    emit('update:modelValue', false)
  } catch {
    errorMessage.value = 'Không kết nối được máy chủ. Vui lòng thử lại.'
  } finally {
    borrowing.value = false
  }
}

async function loadReviews() {
  const id = bookKey(props.book)
  if (!id) return
  reviewsLoading.value = true
  try {
    const data = await fetchBookReviews(id)
    reviews.value = normalizeReviews(data, id)
  } finally {
    reviewsLoading.value = false
  }
}

function normalizeReviews(data, bookId) {
  const groups = Array.isArray(data) ? data : [data]
  const group = groups.find(item => String(item?.bookId ?? item?.BookId ?? item?.id ?? '') === String(bookId)) || groups[0] || {}
  const items = group.reviews || group.Reviews || (Array.isArray(data) ? [] : data?.items) || []
  return items.map((review, index) => {
    const card = review.CardNumber || review.cardNumber || ''
    const name = review.FullName || review.fullName || review.UserName || review.userName || review.Username || review.username || card || 'Độc giả'
    const created = review.CreatedAt || review.createdAt || ''
    return {
      id: review.ReviewId || review.reviewId || review.id || `${bookId}-${index}`,
      name,
      initial: String(name || 'Đ').trim().charAt(0).toUpperCase(),
      rating: Number(review.Rating ?? review.rating ?? 0),
      comment: review.Comment || review.comment || '',
      rawCreatedAt: created,
      createdAt: created ? new Date(created).toLocaleString('vi-VN') : ''
    }
  }).filter(review => review.rating > 0)
}

function borrowErrorMessage(data) {
  const raw = String(data?.message || data?.Message || '')
  if (raw.includes('Borrow limit exceeded') || raw.includes('Monthly borrow limit exceeded')) {
    const limit = data?.monthlyBorrowLimit ?? data?.MonthlyBorrowLimit ?? 5
    const active = data?.activeBorrowCount ?? data?.ActiveBorrowCount
    const activeText = Number.isFinite(Number(active)) ? ` Hiện bạn đang giữ ${active} cuốn.` : ''
    return `Bạn đã đạt giới hạn mượn. Mỗi độc giả chỉ được giữ tối đa ${limit} cuốn đang mượn hoặc chờ xử lý. Sách đã trả sẽ không tính vào giới hạn.${activeText}`
  }
  return raw || 'Không mượn được sách. Vui lòng thử lại.'
}

function toggleFavorite() {
  favoriteBooks.value = toggleFavoriteBook(props.book, favoriteBooks.value).ids
  emit('favorite-changed')
}

function defaultReturnAt() {
  return toDateTimeLocal(new Date(Date.now() + 14 * 24 * 60 * 60 * 1000))
}

function toDateTimeLocal(date) {
  const local = new Date(date)
  local.setMinutes(local.getMinutes() - local.getTimezoneOffset())
  return local.toISOString().slice(0, 16)
}

function getBorrowUnitPrice(book = {}) {
  const explicitPrice = Number(
    book.giaMuon ?? book.GiaMuon ??
    book.giaThue ?? book.GiaThue ??
    book.price ?? book.Price ??
    book.donGia ?? book.DonGia ?? 0
  )
  if (explicitPrice > 0) return explicitPrice

  const title = String(book.tenSach || book.TenSach || '').toLowerCase()
  const publisher = String(book.nhaSanXuat || book.NhaSanXuat || '').toLowerCase()
  const text = `${title} ${publisher}`

  if (text.includes('lập trình') || text.includes('lap trinh') || text.includes('code') || text.includes('java') || text.includes('python')) return 12000
  if (text.includes('khoa học') || text.includes('khoa hoc') || text.includes('kinh tế') || text.includes('kinh te')) return 10000
  if (text.includes('tâm lý') || text.includes('tam ly') || text.includes('kỹ năng') || text.includes('ky nang')) return 9000
  if (text.includes('thiếu nhi') || text.includes('thieu nhi') || text.includes('truyện tranh') || text.includes('truyen tranh')) return 4000
  if (text.includes('văn học') || text.includes('van hoc') || text.includes('truyện') || text.includes('truyen')) return 6000
  return 5000
}
</script>

<style scoped lang="scss">
.detail-hero {
  height: 240px;
  position: relative;
  background-size: cover;
  background-position: center;
}
.hero-overlay {
  position: absolute;
  inset: 0;
  background: linear-gradient(to top, rgba(0,0,0,0.85), rgba(0,0,0,0.3));
  backdrop-filter: blur(20px);
}
.hero-content {
  position: relative;
  z-index: 10;
  display: flex;
  align-items: flex-end;
  gap: 20px;
  padding: 24px;
  height: 100%;
}
.hero-cover {
  width: 110px;
  height: 160px;
  border-radius: 12px;
  overflow: hidden;
  flex-shrink: 0;
  box-shadow: 0 12px 36px rgba(0,0,0,0.5);
  display: flex;
  align-items: center;
  justify-content: center;
}
.detail-favorite {
  position: absolute !important;
  top: 16px;
  right: 16px;
  z-index: 20;
  background: rgba(255,255,255,0.9) !important;
  color: #64748b !important;
  box-shadow: 0 8px 22px rgba(0,0,0,0.22) !important;
  backdrop-filter: blur(10px);
}
.detail-favorite.favorite-active {
  color: #ec4899 !important;
}
.text-white-70 { color: rgba(255,255,255,0.7) !important; }
.text-white-50 { color: rgba(255,255,255,0.5) !important; }
.border-t { border-top: 1px solid #f0e8de; }
.borrow-panel {
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  gap: 14px;
  padding: 14px;
  border: 1px solid #f0e8de;
  border-radius: 14px;
  background: #fffaf4;
}
.review-panel {
  padding: 14px;
  border: 1px solid #e7ece8;
  border-radius: 14px;
  background: linear-gradient(180deg, #ffffff, #f8fafc);
}
.review-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}
.rating-line {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}
.rating-score {
  font-weight: 800;
  color: #0f172a;
  font-size: 13px;
}
.review-count {
  color: #94a3b8;
  font-size: 12px;
}
.review-sort {
  flex-shrink: 0;
}
.review-list {
  display: grid;
  gap: 10px;
  margin-top: 12px;
}
.review-item {
  display: flex;
  gap: 10px;
  padding: 10px;
  border-radius: 12px;
  background: #fff;
  border: 1px solid #eef2f7;
}
.review-avatar {
  width: 32px;
  height: 32px;
  border-radius: 999px;
  display: grid;
  place-items: center;
  flex-shrink: 0;
  background: #ecfdf5;
  color: #047857;
  font-weight: 900;
}
.review-content {
  min-width: 0;
  flex: 1;
}
.review-meta {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  align-items: center;
  font-size: 13px;
}
.review-comment {
  margin: 4px 0 0;
  color: #475569;
  font-size: 13px;
  line-height: 1.5;
}
.review-date {
  margin: 4px 0 0;
  color: #94a3b8;
  font-size: 11px;
}
.review-empty {
  margin-top: 12px;
  padding: 14px;
  text-align: center;
  border: 1px dashed #dbe5dd;
  border-radius: 12px;
  color: #94a3b8;
  font-size: 13px;
}
.panel-label {
  font-size: 11px;
  font-weight: 700;
  color: #9ca3af;
  margin-bottom: 2px;
}
.panel-value {
  font-size: 14px;
  font-weight: 800;
  color: #1f2937;
}
.qty-control {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px;
  border-radius: 999px;
  background: #fff;
  border: 1px solid #eee2d6;
}
.qty-value {
  width: 28px;
  text-align: center;
  font-weight: 800;
  color: #1f2937;
}
</style>
