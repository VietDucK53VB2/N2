<template>
  <v-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)" max-width="680" scrollable>
    <v-card v-if="book" rounded="xl" class="overflow-hidden">
      <!-- Hero -->
      <div class="detail-hero" :style="heroBg">
        <div class="hero-overlay"></div>
        <div class="hero-content">
          <div class="hero-cover" :style="{ backgroundColor: titleColor(book.tenSach) }">
            <v-img v-if="book.imageUrl" :src="book.imageUrl" cover height="100%" />
            <v-icon v-else size="36" color="white" style="opacity:0.4">mdi-book-open-variant</v-icon>
          </div>
          <div class="flex-grow-1">
            <div class="d-flex ga-2 mb-2">
              <v-chip size="small" :color="book.soBanConLai > 0 ? 'success' : 'error'" variant="flat">
                {{ book.soBanConLai > 0 ? `Còn ${book.soBanConLai} quyển` : 'Hết bản' }}
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
      <v-card-text class="pa-6" style="max-height:300px;overflow-y:auto">
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
import { borrowBook, getReaderCard } from '@/utils/api'
import { titleColor, getGenreTags, formatMoney } from '@/utils/helpers'

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

const heroBg = computed(() => {
  if (props.book?.imageUrl) return { backgroundImage: `url(${props.book.imageUrl})` }
  return { backgroundColor: titleColor(props.book?.tenSach) }
})
const genres = computed(() => getGenreTags(props.book?.tenSach))
const maxQuantity = computed(() => Math.max(1, Math.min(Number(props.book?.soBanConLai || 1), 10)))
const unitPrice = computed(() => getBorrowUnitPrice(props.book))
const totalPrice = computed(() => unitPrice.value * quantity.value)

watch(() => props.book?.id, () => {
  quantity.value = 1
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
      totalPrice: totalPrice.value
    })
    emit('update:modelValue', false)
  } catch {
    errorMessage.value = 'Không kết nối được máy chủ. Vui lòng thử lại.'
  } finally {
    borrowing.value = false
  }
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
