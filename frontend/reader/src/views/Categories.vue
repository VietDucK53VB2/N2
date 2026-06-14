<template>
  <div class="categories-page">
    <div class="page-head">
      <div>
        <h2>Thể loại sách</h2>
        <p>Chọn thể loại ở thanh menu để xem nhanh các đầu sách phù hợp.</p>
      </div>
      <v-chip color="primary" variant="tonal">
        <v-icon start size="16">mdi-shape-outline</v-icon>{{ filteredBooks.length }} cuốn
      </v-chip>
    </div>

    <section class="category-content">
      <div class="content-head">
        <h3>{{ categoryLabel(selectedCategory, store.books) }}</h3>
        <v-text-field
          v-model="localSearch"
          prepend-inner-icon="mdi-magnify"
          placeholder="Tìm trong thể loại..."
          density="compact"
          variant="solo-filled"
          flat
          rounded="xl"
          hide-details
          clearable
          class="category-search"
        />
      </div>

      <v-row v-if="filteredBooks.length">
        <v-col v-for="book in filteredBooks" :key="book.id" cols="6" sm="4" md="3" lg="2" class="d-flex">
          <v-card class="book-card" hover @click="openDetail(book)">
            <div class="book-cover" :style="{ backgroundColor: titleColor(book.tenSach) }">
              <v-img v-if="book.imageUrl" :src="book.imageUrl" cover class="cover-img" />
              <v-icon v-else size="48" color="white" class="empty-cover-icon">mdi-book-open-variant</v-icon>
              <div class="cover-shine"></div>
              <v-btn
                class="favorite-btn"
                :class="{ 'favorite-active': isFavorite(book) }"
                :icon="isFavorite(book) ? 'mdi-heart' : 'mdi-heart-outline'"
                size="x-small"
                variant="flat"
                @click.stop="toggleFavorite(book)"
              />
              <v-chip v-if="book.soBanConLai <= 0" class="avail-badge" size="x-small" color="error" variant="flat">
                Hết
              </v-chip>
            </div>
            <v-card-text class="pa-3">
              <p class="book-title">{{ book.tenSach }}</p>
              <p class="book-author">{{ book.tacGia }}</p>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <v-card v-else flat class="empty-card text-center pa-10">
        <v-icon size="52" color="grey-lighten-1">mdi-book-search-outline</v-icon>
        <p class="text-body-1 text-grey mt-4 mb-0">Không có sách phù hợp.</p>
      </v-card>
    </section>

    <BookDetailDialog
      v-model="detailDialog"
      :book="selectedBook"
      @favorite-changed="favoriteIds = loadFavoriteIds()"
      @borrowed="handleBorrowed"
    />
    <v-snackbar v-model="snackbar" color="success" timeout="3000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useAppStore } from '@/stores/app'
import { titleColor } from '@/utils/helpers'
import { categoryLabel, matchesCategory } from '@/utils/categories'
import { bookKey, loadFavoriteIds, toggleFavoriteBook } from '@/utils/favorites'
import BookDetailDialog from '@/components/BookDetailDialog.vue'

const route = useRoute()
const store = useAppStore()

const selectedCategory = ref(String(route.params.category || 'all'))
const localSearch = ref('')
const favoriteIds = ref(loadFavoriteIds())
const detailDialog = ref(false)
const selectedBook = ref(null)
const snackbar = ref(false)
const snackbarText = ref('')

const filteredBooks = computed(() => {
  const q = localSearch.value?.trim().toLowerCase()
  return store.books.filter(book => {
    if (!matchesCategory(book, selectedCategory.value)) return false
    if (!q) return true
    return String(book.tenSach || '').toLowerCase().includes(q) ||
      String(book.tacGia || '').toLowerCase().includes(q)
  })
})

watch(() => route.params.category, value => {
  selectedCategory.value = String(value || 'all')
})

function isFavorite(book) {
  return favoriteIds.value.has(bookKey(book))
}

function toggleFavorite(book) {
  const wasFavorite = isFavorite(book)
  favoriteIds.value = toggleFavoriteBook(book, favoriteIds.value).ids
  snackbarText.value = wasFavorite
    ? `Đã bỏ yêu thích "${book.tenSach || 'sách'}".`
    : `Đã thêm "${book.tenSach || 'sách'}" vào yêu thích.`
  snackbar.value = true
}

function openDetail(book) {
  selectedBook.value = book
  detailDialog.value = true
}

async function handleBorrowed() {
  await store.loadAll()
  snackbarText.value = 'Đã gửi yêu cầu mượn sách. Vui lòng chờ thủ thư duyệt.'
  snackbar.value = true
}
</script>

<style scoped lang="scss">
.categories-page {
  animation: fadeIn 0.35s ease;
}
.page-head {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 16px;
  margin-bottom: 22px;
}
.page-head h2 {
  margin: 0;
  font-size: 26px;
  font-weight: 900;
  color: #0f172a;
}
.page-head p {
  margin: 4px 0 0;
  color: #94a3b8;
}
.content-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
}
.content-head h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 900;
  color: #0f172a;
}
.category-search {
  max-width: 360px;
}
.book-card {
  border-radius: 12px !important;
  overflow: hidden;
  border: none !important;
  box-shadow: 0 4px 12px rgba(0,0,0,0.08) !important;
  background: #fff !important;
  display: flex !important;
  flex-direction: column;
  width: 100%;
  height: 100%;
  min-height: 286px;
  transition: transform 0.25s ease, box-shadow 0.25s ease;
}
.book-card:hover {
  transform: translateY(-7px);
  box-shadow: 0 16px 40px rgba(0,0,0,0.14) !important;
}
.book-cover {
  position: relative;
  width: 100%;
  aspect-ratio: 2 / 3;
  overflow: hidden;
}
.cover-img {
  position: absolute !important;
  inset: 0;
  width: 100% !important;
  height: 100% !important;
  object-fit: cover;
}
:deep(.book-cover .v-img) {
  position: absolute !important;
  inset: 0;
  width: 100% !important;
  height: 100% !important;
}
:deep(.book-cover .v-img__img) {
  object-fit: cover !important;
}
.cover-shine {
  position: absolute;
  inset: 0;
  background: linear-gradient(105deg, transparent 40%, rgba(255,255,255,0.15) 45%, transparent 50%);
  pointer-events: none;
}
.empty-cover-icon {
  opacity: 0.3;
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}
.favorite-btn {
  position: absolute !important;
  top: 6px;
  left: 6px;
  z-index: 3;
  width: 28px !important;
  height: 28px !important;
  background: rgba(255,255,255,0.92) !important;
  color: #64748b !important;
  box-shadow: 0 4px 14px rgba(15,23,42,0.22) !important;
}
.favorite-btn.favorite-active {
  color: #ec4899 !important;
}
.avail-badge {
  position: absolute;
  top: 6px;
  right: 6px;
  font-weight: 700;
  z-index: 2;
}
.book-title {
  font-size: 13px;
  font-weight: 800;
  line-height: 1.35;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  min-height: 35px;
  color: #1a1a1a;
}
.book-author {
  font-size: 11px;
  color: #64748b;
  margin-top: 2px;
  font-weight: 500;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}
.empty-card {
  border-radius: 16px !important;
  background: rgba(255,255,255,0.7) !important;
  border: 2px dashed rgba(4,120,87,0.2) !important;
}
@media (max-width: 900px) {
  .category-search {
    max-width: none;
  }
  .content-head {
    align-items: stretch;
    flex-direction: column;
  }
}
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>
