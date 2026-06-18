<template>
  <div class="page-shell">
    <a-card class="hero-card" :bordered="false">
      <div class="hero-layout">
        <div class="hero-copy">
          <div class="eyebrow">GIÁ & PHÍ DỊCH VỤ</div>
          <h1>Thiết lập mức mượn và phí xử lý sách</h1>
          <p>
            Cấu hình này được dùng cho luồng thủ thư khi duyệt mượn, duyệt trả và tính phí.
            Hư nặng hoặc mất sách sẽ tính phí riêng khi xác nhận trả.
          </p>
          <div class="hero-tags">
            <a-tag color="green">Đồng bộ từ backend</a-tag>
            <a-tag v-if="!embedMode" color="blue">Có thể chỉnh trực tiếp</a-tag>
            <a-tag v-else color="gold">Chế độ nhúng công khai</a-tag>
            <a-tag color="gold">Áp dụng cho toàn hệ thống</a-tag>
          </div>
        </div>

        <div class="hero-summary">
          <div class="summary-pill">
            <span>Giới hạn mượn</span>
            <strong>{{ form.monthlyBorrowLimit }} cuốn / tháng</strong>
          </div>
          <div class="summary-pill">
            <span>Phí quá hạn</span>
            <strong>{{ money(form.dailyOverdueFine) }} / ngày</strong>
          </div>
          <div class="summary-pill">
            <span>Phí hư nặng / mất sách</span>
            <strong>{{ money(Math.max(form.heavyDamageFine, form.lostFine)) }}</strong>
          </div>
        </div>
      </div>
    </a-card>

    <a-row :gutter="[16, 16]" class="content-grid">
      <a-col :xs="24" :xl="16">
        <a-card class="panel-card" :loading="loading" :bordered="false">
          <template #title>
            <div class="panel-head">
              <div>
                <div class="panel-title">Cấu hình giá và phí</div>
                <div class="panel-subtitle">Lưu xong là backend dùng ngay cho mượn, trả và tính phí.</div>
              </div>
              <a-tag color="geekblue">{{ lastSavedLabel }}</a-tag>
            </div>
          </template>

          <a-alert
            type="warning"
            show-icon
            class="mb-4"
            message="Phân loại trả sách"
            description="Khi thủ thư xác nhận trả với tình trạng Hư nặng hoặc Mất sách, hệ thống sẽ tự cộng phí riêng theo cấu hình bên dưới."
          />

          <a-form layout="vertical" :model="form" class="pricing-form">
            <a-row :gutter="16">
              <a-col :xs="24" :md="12">
                <a-form-item label="Giới hạn mượn trong tháng">
                  <a-input-number
                    v-model:value="form.monthlyBorrowLimit"
                    :min="1"
                    :max="100"
                    :disabled="embedMode"
                    class="full-width"
                  />
                  <div class="field-unit">cuốn / tháng</div>
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item label="Giá mượn mặc định">
                  <a-input-number
                    v-model:value="form.borrowPricePerBook"
                    :min="0"
                    :step="1000"
                    :disabled="embedMode"
                    class="full-width"
                  />
                  <div class="field-unit">đ / cuốn</div>
                </a-form-item>
              </a-col>
            </a-row>

            <a-row :gutter="16">
              <a-col :xs="24" :md="12">
                <a-form-item label="Phí quá hạn">
                  <a-input-number
                    v-model:value="form.dailyOverdueFine"
                    :min="0"
                    :step="1000"
                    :disabled="embedMode"
                    class="full-width"
                  />
                  <div class="field-unit">đ / ngày</div>
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item label="Phí hư nhẹ">
                  <a-input-number
                    v-model:value="form.lightDamageFine"
                    :min="0"
                    :step="1000"
                    :disabled="embedMode"
                    class="full-width"
                  />
                  <div class="field-unit">đ / phiếu</div>
                </a-form-item>
              </a-col>
            </a-row>

            <a-row :gutter="16">
              <a-col :xs="24" :md="12">
                <a-form-item label="Phí hư nặng">
                  <a-input-number
                    v-model:value="form.heavyDamageFine"
                    :min="0"
                    :step="5000"
                    :disabled="embedMode"
                    class="full-width"
                  />
                  <div class="field-unit">đ / phiếu</div>
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item label="Phí mất sách">
                  <a-input-number
                    v-model:value="form.lostFine"
                    :min="0"
                    :step="5000"
                    :disabled="embedMode"
                    class="full-width"
                  />
                  <div class="field-unit">đ / phiếu</div>
                </a-form-item>
              </a-col>
            </a-row>

            <div class="actions">
              <a-space :size="12" wrap>
                <a-button :loading="loading" @click="reloadSettings">Làm mới</a-button>
                <a-button v-if="!embedMode" type="primary" :loading="saving" @click="saveSettings">Lưu cấu hình</a-button>
              </a-space>
              <div class="helper-note">
                Mức phí hư nặng và mất sách đang được để mặc định 100.000 đ nếu chưa cấu hình.
              </div>
            </div>
          </a-form>
        </a-card>
      </a-col>

      <a-col :xs="24" :xl="8">
        <a-card class="side-card" :bordered="false">
          <template #title>
            <div class="panel-head">
              <div>
                <div class="panel-title">Xem nhanh</div>
                <div class="panel-subtitle">Giá trị hiện tại đang áp dụng</div>
              </div>
            </div>
          </template>

          <div class="quick-stack">
            <div class="quick-item">
              <span>Giới hạn mượn</span>
              <strong>{{ form.monthlyBorrowLimit }} cuốn / tháng</strong>
            </div>
            <div class="quick-item">
              <span>Giá mượn</span>
              <strong>{{ money(form.borrowPricePerBook) }} / cuốn</strong>
            </div>
            <div class="quick-item">
              <span>Phí quá hạn</span>
              <strong>{{ money(form.dailyOverdueFine) }} / ngày</strong>
            </div>
            <div class="quick-item">
              <span>Phí hư nhẹ</span>
              <strong>{{ money(form.lightDamageFine) }}</strong>
            </div>
            <div class="quick-item">
              <span>Phí hư nặng</span>
              <strong>{{ money(form.heavyDamageFine) }}</strong>
            </div>
            <div class="quick-item">
              <span>Phí mất sách</span>
              <strong>{{ money(form.lostFine) }}</strong>
            </div>
          </div>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup>
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import { useLibrarianStore } from '../stores/librarian'

const store = useLibrarianStore()
const embedMode = store.embedMode
const loading = ref(false)
const saving = ref(false)
const lastSavedAt = ref('')

const form = reactive({
  monthlyBorrowLimit: 5,
  borrowPricePerBook: 5000,
  dailyOverdueFine: 5000,
  lightDamageFine: 20000,
  heavyDamageFine: 100000,
  lostFine: 100000
})

const lastSavedLabel = computed(() => {
  if (!lastSavedAt.value) return 'Chưa lưu'
  return `Đã lưu ${lastSavedAt.value}`
})

function normalizeNumber(value, fallback) {
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : fallback
}

function syncForm(payload = {}) {
  form.monthlyBorrowLimit = normalizeNumber(payload.monthlyBorrowLimit ?? payload.MonthlyBorrowLimit, 5)
  form.borrowPricePerBook = normalizeNumber(payload.borrowPricePerBook ?? payload.BorrowPricePerBook, 5000)
  form.dailyOverdueFine = normalizeNumber(payload.dailyOverdueFine ?? payload.DailyOverdueFine, 5000)
  form.lightDamageFine = normalizeNumber(payload.lightDamageFine ?? payload.LightDamageFine, 20000)
  form.heavyDamageFine = normalizeNumber(payload.heavyDamageFine ?? payload.HeavyDamageFine, 100000)
  form.lostFine = normalizeNumber(payload.lostFine ?? payload.LostFine, 100000)
}

async function reloadSettings() {
  loading.value = true
  try {
    await store.loadPriceSettings()
    syncForm(store.priceSettings || {})
    message.success('Đã tải lại cấu hình giá và phí.')
  } catch {
    message.error('Không tải được cấu hình giá và phí.')
  } finally {
    loading.value = false
  }
}

async function saveSettings() {
  saving.value = true
  try {
    const payload = {
      monthlyBorrowLimit: form.monthlyBorrowLimit,
      borrowPricePerBook: form.borrowPricePerBook,
      dailyOverdueFine: form.dailyOverdueFine,
      lightDamageFine: form.lightDamageFine,
      heavyDamageFine: form.heavyDamageFine,
      lostFine: form.lostFine
    }

    const response = await store.savePriceSettings(payload)
    if (!response.ok) {
      const text = await response.text()
      throw new Error(text || 'Không thể lưu cấu hình.')
    }

    syncForm(store.priceSettings || payload)
    lastSavedAt.value = new Date().toLocaleString('vi-VN')
    message.success('Đã lưu cấu hình giá và phí.')
  } catch (error) {
    message.error(error instanceof Error ? error.message : 'Không thể lưu cấu hình.')
  } finally {
    saving.value = false
  }
}

function money(value) {
  const number = Number(value || 0)
  return new Intl.NumberFormat('vi-VN').format(number) + ' đ'
}

onMounted(reloadSettings)
</script>

<style scoped>
.page-shell {
  display: grid;
  gap: 16px;
}

.hero-card,
.panel-card,
.side-card {
  border-radius: 20px;
  box-shadow: 0 10px 30px rgba(12, 34, 26, 0.06);
}

.hero-layout {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(300px, 1fr);
  gap: 20px;
  align-items: center;
}

.hero-copy h1 {
  margin: 6px 0 10px;
  font-size: clamp(28px, 3vw, 40px);
  line-height: 1.1;
  color: #12352b;
}

.hero-copy p {
  margin: 0;
  max-width: 760px;
  color: #5b6b66;
  line-height: 1.7;
}

.eyebrow {
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.16em;
  color: #0f766e;
}

.hero-tags {
  margin-top: 16px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.hero-summary {
  display: grid;
  gap: 12px;
}

.summary-pill,
.quick-item {
  padding: 16px 18px;
  border-radius: 16px;
  background: linear-gradient(180deg, #f8fffd 0%, #f2f7f5 100%);
  border: 1px solid #d9ebe5;
}

.summary-pill span,
.quick-item span {
  display: block;
  font-size: 13px;
  color: #6b7f79;
  margin-bottom: 6px;
}

.summary-pill strong,
.quick-item strong {
  display: block;
  color: #173b31;
  font-size: 17px;
}

.content-grid {
  align-items: stretch;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.panel-title {
  font-size: 18px;
  font-weight: 800;
  color: #12352b;
}

.panel-subtitle {
  font-size: 13px;
  color: #6d7e78;
  margin-top: 2px;
}

.pricing-form {
  margin-top: 4px;
}

.full-width {
  width: 100%;
}

.actions {
  margin-top: 8px;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.helper-note {
  color: #6b7f79;
  font-size: 13px;
}

.field-unit {
  margin-top: 6px;
  font-size: 12px;
  color: #7f908a;
}

.quick-stack {
  display: grid;
  gap: 12px;
}

:deep(.ant-input-number-affix-wrapper),
:deep(.ant-input-number) {
  width: 100%;
}

@media (max-width: 992px) {
  .hero-layout {
    grid-template-columns: 1fr;
  }
}
</style>
