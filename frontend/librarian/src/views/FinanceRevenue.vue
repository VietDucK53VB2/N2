<template>
  <a-card title="Doanh thu" :body-style="{ padding: '24px' }">
    <a-row :gutter="16" class="mb-4">
      <a-col :xs="24" :md="8">
        <a-statistic title="Tổng phí phạt chưa thu" :value="totalUnpaid" suffix="đ" />
      </a-col>
      <a-col :xs="24" :md="8">
        <a-statistic title="Số khoản đã thu" :value="paidCount" />
      </a-col>
      <a-col :xs="24" :md="8">
        <a-statistic title="Tổng khoản phạt" :value="totalCount" />
      </a-col>
    </a-row>

    <a-alert
      type="info"
      show-icon
      message="Doanh thu được tổng hợp từ dữ liệu hiện có"
      description="Bản nguồn hiện tại chưa có API doanh thu riêng, nên màn này dùng dữ liệu thống kê phí phạt để khôi phục điều hướng cũ."
    />
  </a-card>
</template>

<script setup>
import { computed, onMounted } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'

const store = useLibrarianStore()
const totalUnpaid = computed(() =>
  store.fines.filter(f => !(f.IsPaid || f.isPaid)).reduce((s, f) => s + Number(f.Amount || f.amount || 0), 0)
)
const paidCount = computed(() => store.fines.filter(f => f.IsPaid || f.isPaid).length)
const totalCount = computed(() => store.fines.length)

onMounted(() => store.loadFines())
</script>
