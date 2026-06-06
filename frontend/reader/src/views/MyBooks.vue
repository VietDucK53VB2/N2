<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <div>
        <h2 class="text-h5 font-weight-black">Sách của tôi</h2>
        <p class="text-body-2 text-grey">Quản lý sách đang mượn</p>
      </div>
      <v-btn class="btn-gradient" size="large" prepend-icon="mdi-plus" @click="borrowDialog = true">
        Mượn sách mới
      </v-btn>
    </div>

    <!-- Filter -->
    <v-chip-group v-model="filter" mandatory selected-class="text-white bg-primary" class="mb-6">
      <v-chip value="all" variant="outlined" filter>Tất cả</v-chip>
      <v-chip value="soon" variant="outlined" filter>Sắp hết hạn</v-chip>
      <v-chip value="overdue" variant="outlined" filter>Quá hạn</v-chip>
    </v-chip-group>

    <!-- Books -->
    <v-row v-if="filteredBooks.length">
      <v-col v-for="tx in filteredBooks" :key="tx.Id || tx.id" cols="12" sm="6" lg="4" xl="3">
        <v-card class="book-item" hover>
          <div class="book-cover" :style="{ backgroundColor: titleColor(tx.TenSach || tx.tenSach) }">
            <v-img v-if="tx.ImageUrl || tx.imageUrl" :src="tx.ImageUrl || tx.imageUrl" cover height="100%" />
            <v-icon v-else size="48" color="white" style="opacity:0.3">mdi-book-open-variant</v-icon>
            <div class="cover-gradient"></div>
            <div class="cover-info">
              <p class="text-body-2 font-weight-bold text-white">{{ tx.TenSach || tx.tenSach || '—' }}</p>
              <p class="text-caption text-white-50">{{ tx.TacGia || tx.tacGia || '—' }}</p>
            </div>
            <v-chip class="status-chip" size="x-small" :color="statusColor(tx)" variant="flat">
              <v-icon start size="10">{{ statusIcon(tx) }}</v-icon>
              {{ statusText(tx) }}
            </v-chip>
          </div>

          <v-card-text class="pa-4">
            <v-row dense class="mb-3">
              <v-col cols="6">
                <div class="date-box">
                  <span class="date-label">NGÀY MƯỢN</span>
                  <span class="date-value">{{ formatDate(tx.BorrowedAt) }}</span>
                </div>
              </v-col>
              <v-col cols="6">
                <div class="date-box">
                  <span class="date-label">HẠN TRẢ</span>
                  <span class="date-value" :class="{ 'text-error': store.isOverdue(tx) }">
                    {{ formatDate(tx.DueAt) }}
                  </span>
                </div>
              </v-col>
            </v-row>

            <v-progress-linear
              :model-value="progressPercent(tx)"
              :color="progressColor(tx)"
              rounded
              height="6"
              class="mb-4"
            />

            <v-btn
              block
              variant="tonal"
              color="success"
              prepend-icon="mdi-undo"
              @click="openReturn(tx)"
            >
              Trả sách
            </v-btn>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Empty State -->
    <v-card v-else flat color="transparent" class="text-center pa-12">
      <v-icon size="72" color="grey-lighten-2" class="mb-4">mdi-book-open-variant</v-icon>
      <p class="text-body-1 text-grey mb-4">Không có sách nào</p>
      <v-btn class="btn-gradient" prepend-icon="mdi-plus" @click="borrowDialog = true">Mượn sách mới</v-btn>
    </v-card>

    <!-- Return Dialog -->
    <v-dialog v-model="returnDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title class="text-h6 font-weight-bold">Xác nhận trả sách</v-card-title>
        <v-card-text>
          <v-alert v-if="returnError" type="error" variant="tonal" density="compact" class="mb-3">
            {{ returnError }}
          </v-alert>
          <v-alert type="info" variant="tonal" class="mb-0">
            Bạn muốn trả sách: <strong>{{ returnBook?.TenSach || returnBook?.tenSach }}</strong>
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="returnDialog = false">Hủy</v-btn>
          <v-btn color="success" :loading="returning" @click="submitReturn">Xác nhận trả</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Borrow Dialog -->
    <BorrowDialog v-model="borrowDialog" @success="loadData" />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAppStore } from '@/stores/app'
import { getReaderCard, returnBook as apiReturn } from '@/utils/api'
import { titleColor, formatDate, daysLeft } from '@/utils/helpers'
import BorrowDialog from '@/components/BorrowDialog.vue'

const store = useAppStore()
const filter = ref('all')
const returnDialog = ref(false)
const returnBookData = ref(null)
const returning = ref(false)
const borrowDialog = ref(false)
const returnError = ref('')

const filteredBooks = computed(() => {
  const active = store.activeTransactions
  if (filter.value === 'overdue') return active.filter(store.isOverdue)
  if (filter.value === 'soon') return active.filter(t => {
    if (store.isOverdue(t)) return false
    const d = daysLeft(t.DueAt)
    return d !== null && d <= 3
  })
  return active
})

const returnBook = computed(() => returnBookData.value)

function statusColor(tx) {
  if (store.isOverdue(tx)) return 'error'
  const d = daysLeft(tx.DueAt)
  return d !== null && d <= 3 ? 'warning' : 'info'
}
function statusIcon(tx) {
  if (store.isOverdue(tx)) return 'mdi-alert'
  return 'mdi-calendar'
}
function statusText(tx) {
  if (store.isOverdue(tx)) return 'Qu\u00e1 h\u1ea1n'
  if (tx.Status === 'Overdue') return 'Quá hạn'
  const d = daysLeft(tx.DueAt)
  return d !== null ? `Còn ${d} ngày` : 'Đang mượn'
}
function progressPercent(tx) {
  const d = daysLeft(tx.DueAt)
  if (d === null) return 50
  return Math.min(100, Math.max(0, (1 - d / 14) * 100))
}
function progressColor(tx) {
  if (store.isOverdue(tx)) return 'error'
  const d = daysLeft(tx.DueAt)
  return d !== null && d <= 3 ? 'warning' : 'primary'
}
function openReturn(tx) {
  returnError.value = ''
  returnBookData.value = tx
  returnDialog.value = true
}
async function submitReturn() {
  const tx = returnBookData.value
  if (!tx) return
  const cardNumber = store.cardNumberOf(tx) || getReaderCard()
  const bookId = store.bookIdOf(tx)
  if (!cardNumber || !bookId) {
    returnError.value = 'Thiếu mã thẻ hoặc mã sách, không thể trả sách.'
    return
  }
  returnError.value = ''
  returning.value = true
  try {
    const r = await apiReturn(cardNumber, bookId)
    if (r.ok) {
      returnDialog.value = false
      loadData()
    } else {
      const data = await r.json().catch(() => null)
      returnError.value = data?.message || data?.Message || 'Không trả được sách. Vui lòng thử lại.'
    }
  } catch {
    returnError.value = 'Không kết nối được máy chủ. Vui lòng thử lại.'
  } finally { returning.value = false }
}
async function loadData() {
  await store.loadMyTransactions()
}
onMounted(loadData)
</script>

<style scoped lang="scss">
.book-item {
  overflow: hidden;
  transition: transform 0.25s, box-shadow 0.25s;
  &:hover {
    transform: translateY(-4px);
  }
}
.book-cover {
  position: relative;
  height: 200px;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}
.cover-gradient {
  position: absolute;
  inset: 0;
  background: linear-gradient(to top, rgba(0,0,0,0.75), transparent 60%);
  z-index: 1;
}
.cover-info {
  position: absolute;
  bottom: 12px;
  left: 12px;
  right: 12px;
  z-index: 2;
}
.status-chip {
  position: absolute;
  top: 10px;
  right: 10px;
  z-index: 2;
}
.date-box {
  background: #faf7f2;
  border-radius: 10px;
  padding: 8px 10px;
}
.date-label {
  display: block;
  font-size: 10px;
  font-weight: 700;
  color: #bbb;
  letter-spacing: 0;
  margin-bottom: 3px;
  text-transform: none;
}
.date-value {
  font-size: 12px;
  font-weight: 600;
}
.text-white-50 {
  color: rgba(255,255,255,0.6) !important;
}
</style>
