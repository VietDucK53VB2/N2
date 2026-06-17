<template>
  <div class="cart-page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Xin chào, {{ userName }}!</h1>
        <p class="page-subtitle">Chọn sách bạn muốn đọc hôm nay.</p>
      </div>
    </div>

    <div class="cart-grid">
      <div class="cart-main-col">
        <v-card rounded="xl" class="cart-panel" elevation="1">
          <div class="panel-head">
            <h3 class="panel-title">Danh sách trong giỏ</h3>
            <v-btn variant="text" color="error" prepend-icon="mdi-trash-can-outline" :disabled="!items.length" @click="clearAll">
              Xóa giỏ
            </v-btn>
          </div>

          <v-divider class="my-4" />

          <template v-if="items.length">
            <div class="cart-item" v-for="item in items" :key="item.id">
              <div class="item-cover-wrap">
                <v-avatar rounded="lg" size="100" class="book-avatar" :image="item.imageUrl || undefined">
                  <v-icon v-if="!item.imageUrl" size="30" color="white">mdi-book-open-variant</v-icon>
                </v-avatar>
              </div>

              <div class="item-body">
                <div class="item-top-row">
                  <div>
                    <h4 class="item-title">{{ item.tenSach }}</h4>
                    <p class="item-author">{{ item.tacGia || '—' }}</p>
                  </div>
                  <v-btn icon="mdi-close" variant="text" color="error" size="small" @click="remove(item.id)" />
                </div>

                <div class="item-fields">
                  <div class="field-box">
                    <span class="field-label">SL</span>
                    <strong class="field-value">{{ item.quantity }}</strong>
                  </div>

                  <div class="field-box">
                    <span class="field-label">Thời lượng</span>
                    <strong class="field-value">{{ item.borrowDurationText }}</strong>
                  </div>

                  <div class="field-box full-width">
                    <span class="field-label">Phí tạm tính</span>
                    <strong class="field-value accent">{{ formatMoney(lineTotal(item)) }}</strong>
                  </div>

                  <div class="field-box full-width">
                    <span class="field-label">Hạn trả</span>
                    <v-text-field
                      v-model="item.borrowDueAt"
                      type="datetime-local"
                      step="1"
                      density="compact"
                      variant="outlined"
                      hide-details
                      class="due-field"
                      @update:model-value="syncItem(item)"
                    />
                  </div>
                </div>
              </div>
            </div>
          </template>

          <v-card v-else flat class="empty-state text-center pa-12">
            <v-icon size="56" color="grey-lighten-1">mdi-cart-outline</v-icon>
            <h4 class="mt-4 mb-2">Giỏ đang trống</h4>
            <p class="text-body-2 text-grey mb-4">Thêm sách từ trang chi tiết để gom lại trước khi mượn.</p>
            <v-btn class="btn-gradient" prepend-icon="mdi-book-plus" @click="$router.push('/')">Quay về trang chủ</v-btn>
          </v-card>
        </v-card>
      </div>

      <div class="cart-summary-col">
        <v-card rounded="xl" class="summary-panel" elevation="1">
          <div class="summary-head">
            <v-icon color="deep-orange" size="22">mdi-credit-card-outline</v-icon>
            <h3 class="panel-title">Tóm tắt thanh toán</h3>
          </div>

          <div class="summary-box">
            <span class="summary-label">Tổng sách trong giỏ</span>
            <strong class="summary-value">{{ items.length }}</strong>
          </div>
          <div class="summary-box">
            <span class="summary-label">Tổng phí tạm tính</span>
            <strong class="summary-money">{{ formatMoney(totalPrice) }}</strong>
          </div>
          <div class="summary-box">
            <span class="summary-label">Còn lại trong tháng</span>
            <strong class="summary-value">{{ remainingInMonth }}</strong>
          </div>

          <v-btn
            block
            size="large"
            class="btn-gradient summary-action"
            prepend-icon="mdi-check-circle-outline"
            :loading="submitting"
            :disabled="!items.length"
            @click="submitCart"
          >
            Mượn tất cả
          </v-btn>

          <p class="summary-note">
            Hệ thống sẽ gửi từng yêu cầu mượn trong giỏ. Mỗi lượt vẫn bị giới hạn theo số sách trong tháng.
          </p>
        </v-card>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, watch, ref } from 'vue'
import { useAppStore } from '@/stores/app'
import { formatMoney, getDisplayName, formatDurationText } from '@/utils/helpers'

const store = useAppStore()
const submitting = ref(false)

const items = computed(() => store.cartItems)
const userName = computed(() => getDisplayName(store.userInfo || {}, 'b?n'))
const totalQuantity = computed(() => items.value.reduce((sum, item) => sum + Number(item.quantity || 0), 0))
const totalPrice = computed(() => items.value.reduce((sum, item) => sum + lineTotal(item), 0))
const remainingInMonth = computed(() => Math.max(0, 10 - totalQuantity.value))

function ensureItem(item) {
  if (!item) return
  if (!item.quantity || item.quantity < 1) item.quantity = 1
  if (!item.borrowDays || item.borrowDays < 1) item.borrowDays = 1
  if (!item.borrowDays || Number.isNaN(Number(item.borrowDays))) item.borrowDays = 1
  if (!item.borrowDueAt) item.borrowDueAt = addHoursToLocalIso(1)
  item.borrowDurationText = formatDurationText(new Date(), item.borrowDueAt)
}

function syncItem(item) {
  ensureItem(item)
  const days = daysFromDueAt(item.borrowDueAt)
  item.borrowDays = days
  item.borrowDurationText = formatDurationText(new Date(), item.borrowDueAt)
  store.cartItems = [...store.cartItems]
}

function pricePerDay() {
  return 5000
}

function lineTotal(item) {
  ensureItem(item)
  return pricePerDay() * Number(item.quantity || 1) * Number(item.borrowDays || 1)
}

function addHoursToLocalIso(hours) {
  const base = new Date()
  base.setTime(base.getTime() + Number(hours || 1) * 60 * 60 * 1000)
  const pad = value => String(value).padStart(2, '0')
  return `${base.getFullYear()}-${pad(base.getMonth() + 1)}-${pad(base.getDate())}T${pad(base.getHours())}:${pad(base.getMinutes())}`
}

function daysFromDueAt(dueAt) {
  const value = new Date(dueAt)
  if (Number.isNaN(value.getTime())) return 1
  const diff = value.getTime() - Date.now()
  return Math.max(1, Math.ceil(diff / (24 * 60 * 60 * 1000)))
}

function normalizeCart() {
  let changed = false
  store.cartItems.forEach(item => {
    const beforeDays = item.borrowDays
    const beforeQty = item.quantity
    const beforeDueAt = item.borrowDueAt
    ensureItem(item)
    item.borrowDays = daysFromDueAt(item.borrowDueAt)
    item.borrowDurationText = formatDurationText(new Date(), item.borrowDueAt)
    if (beforeDays !== item.borrowDays || beforeQty !== item.quantity) changed = true
    if (beforeDueAt !== item.borrowDueAt) changed = true
  })
  if (changed) store.cartItems = [...store.cartItems]
}

function remove(id) {
  store.removeFromCart(id)
}

function clearAll() {
  store.clearCart()
}

async function submitCart() {
  if (!items.value.length) return
  submitting.value = true
  try {
    // Tạm thời giữ nút ở mức mô phỏng để bạn test giao diện trước.
    await new Promise(resolve => setTimeout(resolve, 600))
    store.clearCart()
  } finally {
    submitting.value = false
  }
}

watch(items, normalizeCart, { deep: true, immediate: true })
onMounted(() => store.loadAll())
</script>

<style scoped lang="scss">
.cart-page {
  padding-bottom: 12px;
}
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 18px;
}
.page-title {
  font-size: 24px;
  font-weight: 900;
  line-height: 1.1;
  color: #111827;
}
.page-subtitle {
  font-size: 14px;
  color: #6b7280;
  margin-top: 4px;
}
.cart-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 360px;
  gap: 18px;
  align-items: start;
}
.cart-main-col,
.cart-summary-col {
  min-width: 0;
}
.cart-panel,
.summary-panel {
  border-radius: 20px !important;
  background: #fff !important;
  box-shadow: 0 8px 24px rgba(15, 23, 42, 0.08) !important;
}
.cart-panel {
  padding: 18px !important;
}
.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}
.panel-title {
  font-size: 18px;
  font-weight: 800;
  color: #1f2937;
  margin: 0;
}
.cart-item {
  display: grid;
  grid-template-columns: 110px minmax(0, 1fr);
  gap: 14px;
  padding: 14px;
  border-radius: 18px;
  border: 1px solid #edf2f7;
  background: #fff;
  margin-bottom: 14px;
}
.item-cover-wrap {
  display: flex;
  align-items: flex-start;
  justify-content: center;
}
.book-avatar {
  background: linear-gradient(135deg, #065f46, #047857);
}
.item-body {
  min-width: 0;
}
.item-top-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 10px;
}
.item-title {
  font-size: 18px;
  font-weight: 800;
  color: #111827;
  line-height: 1.2;
}
.item-author {
  margin-top: 3px;
  color: #6b7280;
}
.item-fields {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
.field-box {
  border-radius: 14px;
  border: 1px solid #ebe7de;
  background: #fcfbf7;
  padding: 12px 14px;
  min-width: 0;
}
.field-box.full-width {
  grid-column: 1 / -1;
}
.field-label {
  display: block;
  font-size: 12px;
  color: #9ca3af;
  font-weight: 700;
  margin-bottom: 4px;
}
.field-value {
  display: block;
  font-size: 18px;
  font-weight: 900;
  color: #111827;
}
.field-value.accent {
  color: #111827;
}
.field-input :deep(.v-field__input) {
  min-height: 32px;
  padding-top: 0;
  padding-bottom: 0;
}
.due-field :deep(.v-field__input) {
  color: #374151;
}
.summary-panel {
  position: sticky;
  top: 88px;
  padding: 18px !important;
}
.summary-head {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 14px;
}
.summary-box {
  border-radius: 16px;
  border: 1px solid #edf2f7;
  background: #fbfbff;
  padding: 16px 14px;
  margin-bottom: 14px;
}
.summary-label {
  display: block;
  font-size: 13px;
  font-weight: 700;
  color: #6b7280;
  margin-bottom: 8px;
}
.summary-value {
  display: block;
  font-size: 28px;
  font-weight: 900;
  color: #111827;
}
.summary-money {
  display: block;
  font-size: 28px;
  font-weight: 900;
  color: #111827;
}
.summary-action {
  margin-top: 8px;
}
.summary-note {
  margin-top: 14px;
  font-size: 13px;
  line-height: 1.55;
  color: #9ca3af;
  text-align: center;
}
.btn-gradient {
  background: linear-gradient(135deg, #047857, #065f46) !important;
  color: white !important;
  font-weight: 700;
}
.empty-state {
  border: 2px dashed rgba(4, 120, 87, 0.16) !important;
  border-radius: 18px !important;
  background: #fbfffd !important;
}
@media (max-width: 1100px) {
  .cart-grid {
    grid-template-columns: 1fr;
  }
  .summary-panel {
    position: static;
  }
}
@media (max-width: 700px) {
  .page-title {
    font-size: 20px;
  }
  .cart-panel,
  .summary-panel {
    border-radius: 18px !important;
  }
  .cart-item {
    grid-template-columns: 1fr;
  }
  .item-fields {
    grid-template-columns: 1fr;
  }
  .field-box.full-width {
    grid-column: auto;
  }
}
</style>
