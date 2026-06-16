<template>
  <v-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)" max-width="760" scrollable>
    <v-card v-if="book" rounded="xl" class="overflow-hidden">
      <div class="detail-hero" :style="heroBg">
        <div class="hero-overlay"></div>
        <v-btn class="favorite-btn" icon variant="flat" size="small">
          <v-icon size="20">mdi-heart-outline</v-icon>
        </v-btn>

        <div class="hero-content">
          <div class="hero-cover" :style="{ backgroundColor: titleColor(book.tenSach) }">
            <v-img v-if="book.imageUrl" :src="book.imageUrl" cover height="100%" />
            <v-icon v-else size="36" color="white" style="opacity:0.4">mdi-book-open-variant</v-icon>
          </div>

          <div class="flex-grow-1">
            <div class="d-flex ga-2 mb-2 flex-wrap">
              <v-chip size="small" :color="book.soBanConLai > 0 ? 'success' : 'error'" variant="flat">
                {{ book.soBanConLai > 0 ? `Còn ${book.soBanConLai} quyển` : 'Hết bản' }}
              </v-chip>
              <v-chip v-if="borrowCount" size="small" color="deep-orange" variant="flat">
                <v-icon start size="12">mdi-fire</v-icon>{{ borrowCount }} lượt mượn
              </v-chip>
            </div>
            <h2 class="text-h5 font-weight-black text-white mb-1">{{ book.tenSach }}</h2>
            <p class="text-body-2 text-white-70"><v-icon size="14">mdi-account-edit</v-icon> {{ book.tacGia }}</p>
            <p v-if="book.nhaSanXuat" class="text-caption text-white-50"><v-icon size="12">mdi-domain</v-icon> {{ book.nhaSanXuat }}</p>
            <p v-if="book.isbn" class="text-caption text-white-50"><v-icon size="12">mdi-barcode</v-icon> ISBN: {{ book.isbn }}</p>
          </div>
        </div>
      </div>

      <v-card-text class="pa-6 detail-scroll">
        <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4">
          {{ errorMessage }}
        </v-alert>

        <h4 class="text-subtitle-2 font-weight-bold mb-2">
          <v-icon size="16" color="primary">mdi-text</v-icon> Tóm tắt nội dung
        </h4>
        <p class="text-body-2 text-grey">
          {{ book.moTa || `"${book.tenSach}" là một trong những cuốn sách được độc giả yêu thích.` }}
        </p>

        <div class="reviews-panel mt-5">
          <div class="d-flex align-center justify-space-between mb-3">
            <div>
              <h4 class="text-subtitle-2 font-weight-bold mb-1">Đánh giá độc giả</h4>
              <p class="text-caption text-grey">Một vài nhận xét mẫu để bạn test giao diện.</p>
            </div>
            <div class="rating-summary">
              <div class="rating-stars">
                <v-icon v-for="n in 5" :key="n" size="18" color="amber">mdi-star</v-icon>
              </div>
              <strong>{{ avgRating }}/5</strong>
              <span class="text-caption text-grey">({{ reviews.length }} lượt)</span>
            </div>
          </div>

          <div class="review-list">
            <div v-for="review in reviews" :key="review.name" class="review-item">
              <v-avatar size="34" class="review-avatar">{{ review.initials }}</v-avatar>
              <div class="review-body">
                <div class="d-flex align-center justify-space-between">
                  <strong>{{ review.name }}</strong>
                  <div class="review-stars">
                    <v-icon v-for="n in 5" :key="n" size="12" :color="n <= review.rating ? 'amber' : 'grey-lighten-2'">mdi-star</v-icon>
                  </div>
                </div>
                <div class="review-meta">{{ review.date }}</div>
                <p class="review-text">{{ review.comment }}</p>
              </div>
            </div>
          </div>
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
            <div class="text-caption text-grey mt-1">{{ durationSummary }}</div>
          </div>
        </div>

        <div class="return-field mt-4">
          <v-text-field
            v-model="borrowReturnAt"
            type="datetime-local"
            label="Ngày giờ trả sách"
            density="comfortable"
            variant="outlined"
            hide-details
          />
        </div>
      </v-card-text>

      <v-card-actions class="pa-4 border-t">
        <v-btn variant="text" @click="$emit('update:modelValue', false)">Đóng</v-btn>
        <v-spacer />
        <v-btn
          variant="tonal"
          color="primary"
          class="mr-2"
          :disabled="book.soBanConLai <= 0"
          prepend-icon="mdi-cart-plus"
          @click="handleAddToCart"
        >
          Thêm vào giỏ hàng
        </v-btn>
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
import { borrowBook, getReaderCard } from '@/utils/api'
import { titleColor, formatMoney } from '@/utils/helpers'

const props = defineProps({
  modelValue: Boolean,
  book: Object,
  borrowCount: { type: Number, default: 0 }
})
const emit = defineEmits(['update:modelValue', 'borrowed'])
const store = useAppStore()
const borrowing = ref(false)
const errorMessage = ref('')
const quantity = ref(1)
const borrowDuration = ref(14)
const borrowReturnAt = ref('')

const reviews = [
  { name: 'LIB-199999', initials: 'L', rating: 5, date: '16:31:44 7/6/2026', comment: 'Nội dung cuốn hút, rất hợp để test giao diện đánh giá.' }
]

const avgRating = computed(() => {
  const total = reviews.reduce((s, r) => s + r.rating, 0)
  return (total / reviews.length).toFixed(1)
})

const heroBg = computed(() => {
  if (props.book?.imageUrl) return { backgroundImage: `url(${props.book.imageUrl})` }
  return { backgroundColor: titleColor(props.book?.tenSach) }
})
const maxQuantity = computed(() => Math.max(1, Math.min(Number(props.book?.soBanConLai || 1), 10)))
const unitPrice = computed(() => getBorrowUnitPrice(props.book))
const totalPrice = computed(() => {
  const days = Math.max(1, Number(borrowDuration.value || 1))
  return unitPrice.value * quantity.value * days
})
const borrowStart = computed(() => new Date())
const durationSummary = computed(() => {
  const duration = Math.max(1, Number(borrowDuration.value || 1))
  return `${formatMoney(unitPrice.value)} x ${duration} ngày x ${quantity.value} cuốn`
})

watch(() => props.book?.id, () => {
  quantity.value = 1
  borrowDuration.value = 14
  const now = new Date()
  borrowReturnAt.value = new Date(now.getTime() + 14 * 24 * 60 * 60 * 1000).toISOString().slice(0, 16)
  errorMessage.value = ''
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
    const response = await borrowBook(card, bookId, quantity.value)
    if (!response.ok) {
      const data = await response.json().catch(() => null)
      errorMessage.value = data?.message || data?.Message || 'Không mượn được sách. Vui lòng thử lại.'
      return
    }
    await store.loadAll()
    emit('borrowed', {
      title: props.book?.tenSach,
      quantity: quantity.value,
      totalPrice: totalPrice.value,
      borrowAt: borrowStart.value.toISOString(),
      borrowDuration: Math.max(1, Number(borrowDuration.value || 1))
    })
    emit('update:modelValue', false)
  } catch {
    errorMessage.value = 'Không kết nối được máy chủ. Vui lòng thử lại.'
  } finally {
    borrowing.value = false
  }
}

function handleAddToCart() {
  errorMessage.value = ''
  if (!props.book?.id) {
    errorMessage.value = 'Không tìm thấy mã sách.'
    return
  }
  store.addToCart(props.book, quantity.value)
  emit('update:modelValue', false)
}

function getBorrowUnitPrice(book = {}) {
  return 5000
}
</script>

<style scoped lang="scss">
.detail-hero {
  min-height: 240px;
  position: relative;
  background-size: cover;
  background-position: center;
}
.hero-overlay {
  position: absolute;
  inset: 0;
  background: linear-gradient(to top, rgba(0,0,0,0.82), rgba(0,0,0,0.34));
  backdrop-filter: blur(18px);
}
.hero-content {
  position: relative;
  z-index: 10;
  display: flex;
  align-items: flex-end;
  gap: 20px;
  padding: 24px;
  min-height: 240px;
}
.hero-cover {
  width: 128px;
  height: 176px;
  border-radius: 16px;
  overflow: hidden;
  flex-shrink: 0;
  box-shadow: 0 12px 36px rgba(0,0,0,0.5);
  display: flex;
  align-items: center;
  justify-content: center;
}
.favorite-btn {
  position: absolute;
  top: 16px;
  right: 16px;
  z-index: 20;
  background: rgba(255,255,255,0.92);
  color: #334155;
}
.detail-scroll {
  max-height: 520px;
  overflow-y: auto;
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
.rating-summary {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  justify-content: flex-end;
}
.rating-stars {
  display: flex;
  gap: 2px;
}
.reviews-panel {
  border: 1px solid #e8efe7;
  border-radius: 14px;
  background: #fff;
  padding: 14px;
}
.review-list { display: grid; gap: 12px; }
.review-item {
  display: flex;
  gap: 10px;
  padding: 10px;
  border-radius: 12px;
  background: #fbfdff;
  border: 1px solid #edf2f7;
}
.review-avatar {
  background: linear-gradient(135deg, #065f46, #047857);
  color: white;
  font-size: 12px;
  font-weight: 800;
  flex-shrink: 0;
}
.review-body { min-width: 0; flex: 1; }
.review-stars { display: flex; gap: 2px; margin: 2px 0 4px; }
.review-meta { font-size: 11px; color: #94a3b8; }
.review-text { font-size: 12px; color: #475569; line-height: 1.45; margin: 0; }
.return-field {
  border: 1px solid #e5e7eb;
  border-radius: 14px;
  padding: 12px;
  background: #fff;
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
@media (max-width: 640px) {
  .borrow-panel {
    grid-template-columns: 1fr;
  }
  .borrow-panel .text-right {
    text-align: left;
  }
  .hero-content {
    flex-direction: column;
    align-items: flex-start;
  }
  .hero-cover {
    width: 108px;
    height: 150px;
  }
}
</style>
