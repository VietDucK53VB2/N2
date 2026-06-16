<template>
  <a-card title="Giá &amp; phí" :body-style="{ padding: '24px' }">
    <a-row :gutter="16">
      <a-col :xs="24" :md="8">
        <a-statistic title="Giá mượn mặc định" value="5,000" suffix="đ / cuốn" />
      </a-col>
      <a-col :xs="24" :md="8">
        <a-statistic title="Phí quá hạn" value="5,000" suffix="đ / ngày" />
      </a-col>
      <a-col :xs="24" :md="8">
        <a-statistic title="Giới hạn mượn" value="5" suffix="cuốn / tháng" />
      </a-col>
    </a-row>

    <a-divider />

    <p>
      Trang này được khôi phục như một điểm vào chức năng tài chính của thủ thư.
      Phần cấu hình chi tiết có thể nối tiếp từ dữ liệu hệ thống hoặc API riêng nếu bạn đã có backend cho nó.
    </p>
  </a-card>
</template>

<script setup>
</script>
