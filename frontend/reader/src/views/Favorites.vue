<template>
  <v-card rounded="xl" class="pa-6" elevation="1">
    <h3 class="text-h6 font-weight-bold mb-2">Yêu thích</h3>
    <p class="text-body-2 text-grey">Bản snapshot cũ có menu này. Hiện tại hệ thống chưa lưu danh sách yêu thích riêng nên trang này hiển thị từ kho sách hiện có.</p>
    <v-row class="mt-4">
      <v-col v-for="book in books" :key="book.id" cols="12" sm="6" md="4" lg="3">
        <v-card variant="tonal" rounded="xl" class="pa-4">
          <div class="text-subtitle-1 font-weight-bold">{{ book.tenSach }}</div>
          <div class="text-caption text-grey">{{ book.tacGia }}</div>
        </v-card>
      </v-col>
    </v-row>
  </v-card>
</template>

<script setup>
import { computed, onMounted } from 'vue'
import { useAppStore } from '@/stores/app'

const store = useAppStore()
const books = computed(() => store.books.slice(0, 8))
onMounted(() => store.loadBooks())
</script>
