<template>
  <div class="page-shell">
    <a-row :gutter="[16, 16]" class="stats-row">
      <a-col :xs="24" :md="6">
        <a-card class="mini-card">
          <a-statistic title="Tổng doanh thu" :value="store.totalRevenue" :precision="0" suffix="đ" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="6">
        <a-card class="mini-card">
          <a-statistic title="Thu từ mượn" :value="store.totalBorrowRevenue" :precision="0" suffix="đ" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="6">
        <a-card class="mini-card">
          <a-statistic title="Thu từ phí phạt" :value="store.totalFineRevenue" :precision="0" suffix="đ" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="6">
        <a-card class="mini-card">
          <a-statistic title="Chờ duyệt phí phạt" :value="store.pendingFineAmount" :precision="0" suffix="đ" />
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="[16, 16]">
      <a-col :xs="24" :lg="14">
        <a-card class="panel-card">
          <template #title>
            <div class="panel-head">
              <div>
                <div class="panel-title">Doanh thu đã ghi nhận</div>
                <div class="panel-subtitle">Chỉ cộng khi duyệt mượn hoặc duyệt thanh toán phí phạt</div>
              </div>
            </div>
          </template>

          <a-alert
            type="success"
            show-icon
            class="mb-4"
            message="Doanh thu đã được tính từ backend"
            description="Mỗi lượt duyệt mượn sẽ ghi nhận theo số ngày mượn. Mỗi lượt duyệt phí phạt sẽ ghi nhận đúng số tiền phí phạt của phiếu đó."
          />

          <a-table
            :columns="columns"
            :data-source="store.recentRevenue"
            :loading="loading"
            :pagination="{ pageSize: 8 }"
            size="middle"
            row-key="Id"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'Amount'">
                <span class="money">{{ money(record.Amount) }}</span>
              </template>
              <template v-else-if="column.key === 'CreatedAt'">
                {{ fmtDateTime(record.CreatedAt) }}
              </template>
              <template v-else-if="column.key === 'SourceType'">
                <a-tag :color="record.SourceType === 'BorrowApproval' ? 'green' : 'orange'">
                  {{ record.SourceType === 'BorrowApproval' ? 'Thu mượn' : 'Thu phí phạt' }}
                </a-tag>
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>

      <a-col :xs="24" :lg="10">
        <a-card class="panel-card h-100">
          <template #title>
            <div class="panel-head">
              <div>
                <div class="panel-title">Tình trạng thu phí phạt</div>
                <div class="panel-subtitle">Phần chưa thu và phần đã chờ duyệt</div>
              </div>
            </div>
          </template>

          <a-space direction="vertical" style="width: 100%" :size="16">
            <a-card class="mini-card">
              <a-statistic title="Phí phạt chưa thanh toán" :value="store.unpaidFineAmount" :precision="0" suffix="đ" />
            </a-card>
            <a-card class="mini-card">
              <a-statistic title="Đã thu phí phạt" :value="store.totalFineRevenue" :precision="0" suffix="đ" />
            </a-card>
            <a-card class="mini-card">
              <a-statistic title="Số khoản đã thu" :value="store.fineRevenueCount" />
            </a-card>
          </a-space>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'
import dayjs from 'dayjs'

const store = useLibrarianStore()
const loading = ref(false)

const columns = [
  { title: 'Loại', key: 'SourceType', width: 120 },
  { title: 'Mã tham chiếu', dataIndex: 'ReferenceId', key: 'ReferenceId', width: 180 },
  { title: 'Diễn giải', dataIndex: 'Description', key: 'Description' },
  { title: 'Số tiền', key: 'Amount', width: 140 },
  { title: 'Thời điểm thu', key: 'CreatedAt', width: 160 }
]

function money(value) {
  return `${Number(value || 0).toLocaleString()} đ`
}

function fmtDateTime(value) {
  return value ? dayjs(value).format('DD/MM/YYYY HH:mm:ss') : '—'
}

onMounted(async () => {
  loading.value = true
  try {
    await store.loadRevenueSummary()
    if (!store.fines.length) {
      await store.loadFines()
    }
  } finally {
    loading.value = false
  }
})
</script>

<style scoped>
.page-shell {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.stats-row {
  margin-bottom: 0;
}

.mini-card,
.panel-card {
  border-radius: 18px !important;
  border: 1px solid #edf1ee !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04) !important;
}

.mini-card :deep(.ant-card-body) {
  padding: 18px 20px;
}

.panel-card :deep(.ant-card-head) {
  border-bottom: none;
  padding: 18px 20px 0;
}

.panel-card :deep(.ant-card-body) {
  padding: 18px 20px 20px;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.panel-title {
  font-size: 16px;
  font-weight: 800;
  color: #103b35;
  letter-spacing: -0.01em;
}

.panel-subtitle {
  font-size: 12px;
  color: #8c98a5;
  margin-top: 2px;
}

.money {
  font-weight: 800;
  color: #dc2626;
}
</style>
