<template>
  <div>
    <div class="mb-6">
      <h2 class="text-h5 font-weight-black">Há»“ sÆ¡ cÃ¡ nhÃ¢n</h2>
      <p class="text-body-2 text-grey">ThÃ´ng tin tÃ i khoáº£n vÃ  thá»‘ng kÃª</p>
    </div>

    <v-row>
      <!-- Left -->
      <v-col cols="12" xl="8">
        <!-- Profile Card -->
        <v-card rounded="xl" elevation="1" class="overflow-hidden mb-5">
          <div class="profile-cover">
            <v-avatar size="96" class="profile-avatar" :image="avatarUrl || undefined">
              <span v-if="!avatarUrl">{{ initials }}</span>
            </v-avatar>
            <button class="avatar-upload-btn" type="button" @click="pickAvatar">
              <v-icon size="18">mdi-camera</v-icon>
              Äá»•i áº£nh
            </button>
            <input ref="avatarInput" class="d-none" type="file" accept="image/*" @change="onAvatarPicked" />
          </div>
          <v-card-text class="pt-14 px-7 pb-6">
            <div class="d-flex align-center justify-space-between mb-5">
              <div>
                <h3 class="text-h6 font-weight-black">{{ userInfo.fullName }}</h3>
                <p class="text-body-2 text-grey">{{ roleName }}</p>
              </div>
              <v-btn class="btn-gradient" prepend-icon="mdi-pencil" @click="openEditDialog">
                Chá»‰nh sá»­a
              </v-btn>
            </div>
            <v-row dense>
              <v-col cols="6" md="3">
                <div class="info-box">
                  <span class="info-label">MÃ£ tháº» thÆ° viá»‡n</span>
                  <span class="info-value text-primary font-weight-bold">
                    {{ displayCardNumber }}
                    <v-icon
                      v-if="userInfo.cardNumber || userInfo.CardNumber || userInfo.libraryCard?.cardNumber"
                      size="14"
                      class="ml-1 cursor-pointer"
                      @click="copyCard"
                    >
                      mdi-content-copy
                    </v-icon>
                  </span>
                </div>
              </v-col>
              <v-col cols="6" md="3">
                <div class="info-box">
                  <span class="info-label">EMAIL</span>
                  <span class="info-value">{{ userInfo.email || 'â€”' }}</span>
                </div>
              </v-col>
              <v-col cols="6" md="3">
                <div class="info-box">
                  <span class="info-label">NGÃ€Y THAM GIA</span>
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
            PhÃ­ pháº¡t
            <v-spacer />
            <v-chip v-if="store.totalUnpaidFines > 0" color="error" size="small">Cáº§n thanh toÃ¡n</v-chip>
          </v-card-title>
          <v-card-text>
            <div class="fines-total mb-4">
              <p class="text-caption font-weight-bold" style="color:#e05a2b">Tá»•ng cáº§n thanh toÃ¡n</p>
              <p class="text-h4 font-weight-black text-error">{{ formatMoney(store.totalUnpaidFines) }}</p>
            </div>

            <div v-if="store.myUnpaidFines.length" class="mb-4">
              <p class="text-caption font-weight-bold text-grey mb-2">ChÆ°a thanh toÃ¡n</p>
              <v-list density="compact">
                <v-list-item v-for="(fine, i) in store.myUnpaidFines" :key="`unpaid-${i}`">
                  <v-list-item-title class="text-body-2">{{ translateFineReason(fine.Reason || fine.reason || '') }}</v-list-item-title>
                  <v-list-item-subtitle>{{ formatDate(fine.CreatedAt || fine.createdAt) }}</v-list-item-subtitle>
                  <template #append>
                    <span class="text-error font-weight-bold">{{ formatMoney(fine.Amount || fine.amount || 0) }}</span>
                  </template>
                </v-list-item>
              </v-list>
            </div>

            <div v-if="store.myPendingFinePayments.length" class="mb-4">
              <p class="text-caption font-weight-bold text-grey mb-2">Äang chá» thá»§ thÆ° duyá»‡t</p>
              <v-list density="compact">
                <v-list-item v-for="(fine, i) in store.myPendingFinePayments" :key="`pending-${i}`">
                  <v-list-item-title class="text-body-2">{{ translateFineReason(fine.Reason || fine.reason || '') }}</v-list-item-title>
                  <v-list-item-subtitle>
                    {{ formatDate(fine.PaymentRequestedAt || fine.paymentRequestedAt || fine.CreatedAt || fine.createdAt) }}
                  </v-list-item-subtitle>
                  <template #append>
                    <v-chip size="small" color="warning" variant="flat">Chá» duyá»‡t</v-chip>
                  </template>
                </v-list-item>
              </v-list>
            </div>

            <v-btn
              block
              class="btn-gradient"
              size="large"
              prepend-icon="mdi-credit-card"
              :loading="paying"
              :disabled="!store.myUnpaidFines.length"
              @click="payAllFines"
            >
              Thanh toÃ¡n ngay
            </v-btn>
            <p class="text-caption text-grey text-center mt-3">
              Thanh toÃ¡n trÆ°á»›c ngÃ y 15 hÃ ng thÃ¡ng Ä‘á»ƒ trÃ¡nh bá»‹ khÃ³a tháº».
            </p>
          </v-card-text>
        </v-card>

      </v-col>
    </v-row>

    <!-- Edit Dialog -->
    <v-dialog v-model="editDialog" max-width="500">
      <v-card rounded="xl">
        <v-card-title class="font-weight-bold">Chá»‰nh sá»­a há»“ sÆ¡</v-card-title>
        <v-card-text>
          <v-text-field v-model="editForm.fullName" label="Há» vÃ  tÃªn" class="mb-3" />
          <v-text-field v-model="editForm.email" label="Email" class="mb-3" />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="editDialog = false">Há»§y</v-btn>
          <v-btn class="btn-gradient" @click="saveProfile">LÆ°u thay Ä‘á»•i</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="3000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { ref, computed, reactive } from 'vue'
import { useAppStore } from '@/stores/app'
import { requestFinePayment } from '@/utils/api'
import { getInitials, getDisplayCardNumber, formatDate, formatMoney, translateFineReason } from '@/utils/helpers'

const store = useAppStore()
const editDialog = ref(false)
const paying = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const avatarInput = ref(null)

const userInfo = computed(() => store.userInfo || {})
const initials = computed(() => getInitials(userInfo.value.fullName))
const avatarUrl = computed(() => userInfo.value.avatarUrl || userInfo.value.AvatarUrl || userInfo.value.avatar || userInfo.value.Avatar || '')
const displayCardNumber = computed(() => getDisplayCardNumber(userInfo.value))
const roleName = computed(() => {
  const role = userInfo.value.role
  if (role === 'Admin') return 'Quáº£n trá»‹ viÃªn'
  if (role === 'Librarian') return 'Thá»§ thÆ°'
  return 'ThÃ nh viÃªn'
})

const onTimeCount = computed(() =>
  store.myTransactions.filter(t => {
    const returnedAt = t.ReturnedAt || t.returnedAt
    const dueAt = t.DueAt || t.dueAt
    return store.isReturned(t) && returnedAt && dueAt && new Date(returnedAt) <= new Date(dueAt)
  }).length
)

const stats = computed(() => [
  { label: 'Äang mÆ°á»£n', value: store.activeTransactions.length, color: 'text-primary' },
  { label: 'Tá»•ng mÆ°á»£n', value: store.myTransactions.length, color: '' },
  { label: 'ÄÃºng háº¡n', value: onTimeCount.value, color: 'text-success' },
  { label: 'QuÃ¡ háº¡n', value: store.overdueTransactions.length, color: 'text-error' }
])

const editForm = reactive({ fullName: '', email: '' })

function showMessage(text, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

function openEditDialog() {
  editForm.fullName = userInfo.value.fullName || ''
  editForm.email = userInfo.value.email || ''
  editDialog.value = true
}

function pickAvatar() {
  avatarInput.value?.click()
}

function onAvatarPicked(event) {
  const file = event.target.files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = () => {
    const next = {
      ...userInfo.value,
      avatarUrl: String(reader.result || ''),
      AvatarUrl: String(reader.result || '')
    }
    localStorage.setItem('userInfo', JSON.stringify(next))
    store.userInfo = next
    showMessage('ÄÃ£ cáº­p nháº­t áº£nh Ä‘áº¡i diá»‡n.')
  }
  reader.readAsDataURL(file)
}

function saveProfile() {
  const next = {
    ...userInfo.value,
    fullName: editForm.fullName?.trim() || userInfo.value.fullName || '',
    email: editForm.email?.trim() || userInfo.value.email || ''
  }
  localStorage.setItem('userInfo', JSON.stringify(next))
  store.userInfo = next
  editDialog.value = false
  showMessage('ÄÃ£ lÆ°u thÃ´ng tin há»“ sÆ¡.')
  store.loadAll()
}

async function payAllFines() {
  if (!store.myUnpaidFines.length) return
  paying.value = true
  try {
    for (const fine of store.myUnpaidFines) {
      const id = fine.Id || fine.id
      if (!id) continue
      const response = await requestFinePayment(id)
      if (!response.ok) {
        const data = await response.json().catch(() => null)
        showMessage(data?.message || data?.Message || 'KhÃ´ng gá»­i Ä‘Æ°á»£c yÃªu cáº§u thanh toÃ¡n.', 'error')
        return
      }
    }
    await store.loadAll()
    showMessage('ÄÃ£ gá»­i yÃªu cáº§u thanh toÃ¡n. Chá» thá»§ thÆ° duyá»‡t.')
  } catch {
    showMessage('KhÃ´ng káº¿t ná»‘i Ä‘Æ°á»£c mÃ¡y chá»§. Vui lÃ²ng thá»­ láº¡i.', 'error')
  } finally {
    paying.value = false
  }
}

function copyCard() {
  navigator.clipboard?.writeText(displayCardNumber.value || '')
  showMessage('ÄÃ£ sao chÃ©p mÃ£ tháº».')
}
</script>

<style scoped lang="scss">
.page-title {
  color: #0f172a;
}

.page-subtitle {
  color: #64748b;
}

.profile-card,
.stats-card,
.fines-card,
.edit-card {
  border: 1px solid #e5eef5;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.06) !important;
}

.profile-cover {
  height: 140px;
  background: linear-gradient(135deg, #064e3b 0%, #065f46 45%, #047857 100%);
  position: relative;
}
.profile-avatar {
  position: absolute;
  bottom: -48px;
  left: 28px;
  border: 4px solid white;
  background: linear-gradient(135deg, #0f766e, #047857);
  color: white;
  font-size: 28px;
  font-weight: 800;
  box-shadow: 0 8px 24px rgba(0,0,0,0.2);
}
.avatar-upload-btn {
  position: absolute;
  right: 20px;
  bottom: 18px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 0;
  border-radius: 999px;
  padding: 10px 14px;
  background: rgba(255, 255, 255, 0.92);
  color: #065f46;
  font-weight: 700;
  box-shadow: 0 6px 18px rgba(0,0,0,0.12);
  cursor: pointer;
}
.avatar-upload-btn:hover { background: #fff; }
.info-box {
  background: #f0fdf4;
  border-radius: 12px;
  padding: 12px;
  border: 1px solid #d1fae5;
}
.info-label {
  display: block;
  font-size: 10px;
  font-weight: 700;
  color: #64748b;
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
  background: linear-gradient(135deg, rgba(6, 95, 70, 0.08), rgba(4, 120, 87, 0.12));
  border-radius: 14px;
  border: 1px solid rgba(4, 120, 87, 0.18);
}
.upgrade-card {
  background: linear-gradient(135deg, #064e3b 0%, #065f46 45%, #047857 100%) !important;
}
</style>

