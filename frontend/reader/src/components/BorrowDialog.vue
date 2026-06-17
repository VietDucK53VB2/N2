<template>
  <v-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)" max-width="520" scrollable>
    <v-card rounded="xl">
      <v-card-title class="font-weight-bold">Mượn sách mới</v-card-title>
      <v-card-text>
        <v-text-field
          v-model="keyword"
          prepend-inner-icon="mdi-magnify"
          placeholder="Tìm sách theo tên, tác giả..."
          hide-details
          clearable
          density="compact"
          class="mb-4"
        />

        <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
          {{ errorMessage }}
        </v-alert>

        <v-list lines="two" class="book-list">
          <v-list-item
            v-for="item in filteredBooks"
            :key="item.id"
            :title="item.tenSach"
            :subtitle="item.tacGia"
          >
            <template #prepend>
              <v-avatar :color="titleColor(item.tenSach)" rounded="lg" size="44">
                <v-icon color="white" size="20">mdi-book-open-variant</v-icon>
              </v-avatar>
            </template>
            <template #append>
              <v-btn
                size="small"
                :color="item.soBanConLai > 0 ? 'primary' : 'grey'"
                :disabled="item.soBanConLai <= 0"
                :loading="borrowingId === item.id"
                variant="flat"
                @click="handleBorrow(item)"
              >
                {{ item.soBanConLai > 0 ? 'Mượn' : 'Hết' }}
              </v-btn>
            </template>
          </v-list-item>
        </v-list>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useAppStore } from '@/stores/app'
import { borrowBook, getReaderCard } from '@/utils/api'
import { titleColor } from '@/utils/helpers'

const props = defineProps({ modelValue: Boolean })
const emit = defineEmits(['update:modelValue', 'success'])

const store = useAppStore()
const keyword = ref('')
const borrowingId = ref(null)
const errorMessage = ref('')

const filteredBooks = computed(() => {
  const kw = keyword.value?.toLowerCase() || ''
  if (!kw) return store.books
  return store.books.filter(b =>
    b.tenSach.toLowerCase().includes(kw) || b.tacGia.toLowerCase().includes(kw)
  )
})

async function handleBorrow(book) {
  errorMessage.value = ''
  const card = getReaderCard()
  if (!card) {
    errorMessage.value = 'Không tìm thấy mã thẻ độc giả. Vui lòng đăng nhập lại.'
    return
  }
  borrowingId.value = book.id
  try {
    const borrowedAt = new Date().toISOString()
    const dueAt = new Date(Date.now() + 60 * 60 * 1000).toISOString()
    const r = await borrowBook(card, book.id, 1, { borrowedAt, dueAt })
    if (r.ok) {
      emit('success')
      emit('update:modelValue', false)
      store.loadAll()
    } else {
      const data = await r.json().catch(() => null)
      errorMessage.value = data?.message || data?.Message || 'Không mượn được sách. Vui lòng thử lại.'
    }
  } catch {
    errorMessage.value = 'Không kết nối được máy chủ. Vui lòng thử lại.'
  } finally {
    borrowingId.value = null
  }
}
</script>

<style scoped>
.book-list {
  max-height: 400px;
  overflow-y: auto;
}
</style>
