<template>
  <div>
    <div class="mb-6">
      <h2 class="text-h5 font-weight-black">Hồ sơ cá nhân</h2>
      <p class="text-body-2 text-grey">Thông tin tài khoản và thống kê</p>
    </div>

    <v-row>
      <!-- Left -->
      <v-col cols="12" xl="8">
        <!-- Profile Card -->
        <v-card rounded="xl" elevation="1" class="overflow-hidden mb-5">
          <div class="profile-cover">
            <v-avatar size="96" class="profile-avatar">
              {{ initials }}
            </v-avatar>
          </div>
          <v-card-text class="pt-14 px-7 pb-6">
            <div class="d-flex align-center justify-space-between mb-5">
              <div>
                <h3 class="text-h6 font-weight-black">{{ userInfo.fullName }}</h3>
                <p class="text-body-2 text-grey">{{ roleName }}</p>
              </div>
              <v-btn class="btn-gradient" prepend-icon="mdi-pencil" @click="editDialog = true">
                Chỉnh sửa
              </v-btn>
            </div>
            <v-row dense>
              <v-col cols="6" md="3">
                <div class="info-box">
                  <span class="info-label">MÃ ĐỘC GIẢ</span>
                  <span class="info-value text-primary font-weight-bold">
                    {{ userInfo.cardNumber || '—' }}
                    <v-icon size="14" class="ml-1 cursor-pointer" @click="copyCard">mdi-content-copy</v-icon>
                  </span>
                </div>
              </v-col>
              <v-col cols="6" md="3">
                <div class="info-box">
                  <span class="info-label">MÃ THẺ TV</span>
                  <span class="info-value text-primary font-weight-bold">{{ userInfo.cardNumber || '—' }}</span>
                </div>
              </v-col>
              <v-col cols="6" md="3">
                <div class="info-box">
                  <span class="info-label">EMAIL</span>
                  <span class="info-value">{{ userInfo.email || '—' }}</span>
                </div>
              </v-col>
              <v-col cols="6" md="3">
                <div class="info-box">
                  <span class="info-label">NGÀY THAM GIA</span>
                  <span class="info-value">{{ formatDate(userInfo.createdAt) }}</span>
                </div>
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>

        <!-- Mini Stats -->
        <v-row dense>
          <v-col v-for="stat in stats" :key="stat.label" cols="6" sm="3">
            <v-card rounded="xl" elevation="1" class="pa-4">
              <p class="text-caption text-grey font-weight-bold">{{ stat.label }}</p>
              <p class="text-h5 font-weight-black" :class="stat.color">{{ stat.value }}</p>
            </v-card>
          </v-col>
        </v-row>
      </v-col>

      <!-- Right - Fines -->
      <v-col cols="12" xl="4">
        <v-card rounded="xl" elevation="1" class="mb-4">
          <v-card-title class="d-flex align-center ga-2">
            <v-icon color="primary">mdi-wallet</v-icon>
            Phí phạt
            <v-spacer />
            <v-chip v-if="store.totalUnpaidFines > 0" color="error" size="small">Cần thanh toán</v-chip>
          </v-card-title>
          <v-card-text>
            <div class="fines-total mb-4">
              <p class="text-caption font-weight-bold" style="color:#e05a2b">Tổng cần thanh toán</p>
              <p class="text-h4 font-weight-black text-error">{{ formatMoney(store.totalUnpaidFines) }}</p>
            </div>

            <v-list v-if="store.myUnpaidFines.length" density="compact" class="mb-4">
              <v-list-item v-for="(fine, i) in store.myUnpaidFines" :key="i">
                <v-list-item-title class="text-body-2">{{ fine.Reason || fine.reason || 'Phạt quá hạn' }}</v-list-item-title>
                <v-list-item-subtitle>{{ formatDate(fine.CreatedAt || fine.createdAt) }}</v-list-item-subtitle>
                <template #append>
                  <span class="text-error font-weight-bold">{{ formatMoney(fine.Amount || fine.amount || 0) }}</span>
                </template>
              </v-list-item>
            </v-list>

            <v-btn block class="btn-gradient" size="large" prepend-icon="mdi-credit-card">
              Thanh toán ngay
            </v-btn>
            <p class="text-caption text-grey text-center mt-3">
              Thanh toán trước ngày 15 hàng tháng để tránh bị khóa thẻ.
            </p>
          </v-card-text>
        </v-card>

        <!-- Upgrade -->
        <v-card rounded="xl" class="upgrade-card pa-5">
          <p class="text-overline" style="color:rgba(255,255,255,0.6)">NÂNG CẤP TÀI KHOẢN</p>
          <h4 class="text-subtitle-1 font-weight-bold text-white mb-4">Thư viện số Premium</h4>
          <v-btn variant="outlined" color="white" size="small" append-icon="mdi-arrow-right">
            Khám phá ngay
          </v-btn>
        </v-card>
      </v-col>
    </v-row>

    <!-- Edit Dialog -->
    <v-dialog v-model="editDialog" max-width="500">
      <v-card rounded="xl">
        <v-card-title class="font-weight-bold">Chỉnh sửa hồ sơ</v-card-title>
        <v-card-text>
          <v-text-field v-model="editForm.fullName" label="Họ và tên" class="mb-3" />
          <v-text-field v-model="editForm.email" label="Email" class="mb-3" />
          <v-text-field v-model="editForm.cardNumber" label="Mã thẻ thư viện" />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="editDialog = false">Hủy</v-btn>
          <v-btn class="btn-gradient" @click="editDialog = false">Lưu thay đổi</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, computed, reactive } from 'vue'
import { useAppStore } from '@/stores/app'
import { getInitials, formatDate, formatMoney } from '@/utils/helpers'

const store = useAppStore()
const editDialog = ref(false)

const userInfo = computed(() => store.userInfo || {})
const initials = computed(() => getInitials(userInfo.value.fullName))
const roleName = computed(() => {
  const role = userInfo.value.role
  if (role === 'Admin') return 'Quản trị viên'
  if (role === 'Librarian') return 'Thủ thư'
  return 'Thành viên'
})

const onTimeCount = computed(() =>
  store.myTransactions.filter(t => {
    const returnedAt = t.ReturnedAt || t.returnedAt
    const dueAt = t.DueAt || t.dueAt
    return store.isReturned(t) && returnedAt && dueAt && new Date(returnedAt) <= new Date(dueAt)
  }).length
)

const stats = computed(() => [
  { label: 'Đang mượn', value: store.activeTransactions.length, color: 'text-primary' },
  { label: 'Tổng mượn', value: store.myTransactions.length, color: '' },
  { label: 'Đúng hạn', value: onTimeCount.value, color: 'text-success' },
  { label: 'Quá hạn', value: store.overdueTransactions.length, color: 'text-error' }
])

const editForm = reactive({ fullName: '', email: '', cardNumber: '' })

function copyCard() {
  navigator.clipboard?.writeText(userInfo.value.cardNumber || '')
}
</script>

<style scoped lang="scss">
.profile-cover {
  height: 140px;
  background: linear-gradient(135deg, #e8855a, #7c5cbf);
  position: relative;
}
.profile-avatar {
  position: absolute;
  bottom: -48px;
  left: 28px;
  border: 4px solid white;
  background: linear-gradient(135deg, #e8855a, #7c5cbf);
  color: white;
  font-size: 28px;
  font-weight: 800;
  box-shadow: 0 8px 24px rgba(0,0,0,0.2);
}
.info-box {
  background: #faf7f2;
  border-radius: 12px;
  padding: 12px;
}
.info-label {
  display: block;
  font-size: 10px;
  font-weight: 700;
  color: #bbb;
  letter-spacing: 0;
  margin-bottom: 4px;
  text-transform: none;
}
.info-value {
  font-size: 13px;
  font-weight: 600;
  word-break: break-all;
}
.fines-total {
  text-align: center;
  padding: 20px;
  background: #fde8dc;
  border-radius: 14px;
  border: 1px solid #f5c0a0;
}
.upgrade-card {
  background: linear-gradient(135deg, #e8855a, #7c5cbf) !important;
}
</style>
