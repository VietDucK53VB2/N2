<template>
  <div class="categories-page">
    <section class="page-hero mb-6">
      <div class="hero-copy">
        <p class="eyebrow">Khám phá theo chủ đề</p>
        <h2 class="page-title">Thể loại sách</h2>
        <p class="page-subtitle">
          Chọn một nhóm sách để xem nhanh những đầu sách phù hợp nhất.
        </p>
      </div>

      <v-chip-group v-model="selected" selected-class="active-chip" class="hero-chips" mandatory>
        <v-chip
          v-for="cat in categoryChips"
          :key="cat.value"
          :value="cat.value"
          variant="elevated"
          filter
        >
          {{ cat.label }}
        </v-chip>
      </v-chip-group>
    </section>

    <section class="books-panel">
      <div class="panel-head mb-4">
        <div>
          <h3 class="panel-title">Danh sách sách</h3>
          <p class="panel-subtitle">{{ filtered.length }} cuốn phù hợp với lựa chọn hiện tại</p>
        </div>
      </div>

      <v-row>
        <v-col v-for="book in filtered" :key="book.id" cols="6" sm="4" md="3" lg="2">
          <v-card class="book-card" rounded="xl" elevation="2" hover @click="$router.push({ name: 'dashboard', query: { q: book.tenSach } })">
            <div class="book-cover" :style="{ backgroundColor: titleColor(book.tenSach) }">
              <v-img v-if="book.imageUrl" :src="book.imageUrl" cover class="cover-img" />
              <v-icon v-else size="44" color="white" style="opacity:.35">mdi-book-open-variant</v-icon>
              <v-btn
                icon
                size="x-small"
                class="heart-btn"
                :class="{ 'is-favorite': store.isFavorite(book.id) }"
                @click.stop="store.toggleFavorite(book)"
              >
                <v-icon size="18" :color="store.isFavorite(book.id) ? 'pink' : 'grey-darken-1'">
                  {{ store.isFavorite(book.id) ? 'mdi-heart' : 'mdi-heart-outline' }}
                </v-icon>
              </v-btn>
            </div>
            <v-card-text class="pa-4">
              <p class="book-title">{{ book.tenSach }}</p>
              <p class="book-author">{{ book.tacGia }}</p>
              <div class="book-meta">
                <v-chip size="x-small" color="primary" variant="tonal">
                  Còn {{ book.soBanConLai ?? '—' }}
                </v-chip>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </section>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAppStore } from '@/stores/app'
import { titleColor, buildCatalogCategories, bookMatchesCategory } from '@/utils/helpers'

const route = useRoute()
const router = useRouter()
const store = useAppStore()
const selected = ref(String(route.params.category || 'all'))

watch(
  () => route.params.category,
  value => {
    selected.value = String(value || 'all')
  },
  { immediate: true }
)

watch(selected, value => {
  if (String(route.params.category || 'all') !== String(value || 'all')) {
    router.replace({ name: 'categories', params: { category: value } })
  }
})

const categoryChips = computed(() => {
  const books = Array.isArray(store.books) ? store.books : []
  return [
    { label: 'Tất cả', value: 'all' },
    ...buildCatalogCategories(books)
  ]
})

const filtered = computed(() => {
  const category = String(selected.value || 'all')
  const books = Array.isArray(store.books) ? store.books : []
  if (category === 'all') return books.slice(0, 12)
  return books.filter(book => bookMatchesCategory(book, category)).slice(0, 12)
})

onMounted(() => store.loadBooks())
</script>

<style scoped lang="scss">
.categories-page {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.page-hero {
  border-radius: 24px;
  padding: 22px 24px;
  background: linear-gradient(135deg, #064e3b 0%, #065f46 45%, #047857 100%);
  color: #fff;
  box-shadow: 0 14px 30px rgba(6, 78, 59, 0.18);
}

.hero-copy { margin-bottom: 18px; }

.eyebrow {
  margin: 0 0 6px;
  font-size: 12px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.62);
  font-weight: 700;
}

.page-title {
  margin: 0;
  font-size: 28px;
  font-weight: 900;
  letter-spacing: -0.03em;
}

.page-subtitle,
.panel-subtitle {
  margin: 8px 0 0;
  color: rgba(255, 255, 255, 0.72);
  font-size: 14px;
  max-width: 720px;
}

.hero-chips { gap: 10px; flex-wrap: wrap; }

:deep(.hero-chips .v-chip) {
  background: rgba(255, 255, 255, 0.14) !important;
  color: #fff !important;
  border: 1px solid rgba(255, 255, 255, 0.15);
  font-weight: 700;
}

:deep(.active-chip) {
  background: #fff !important;
  color: #065f46 !important;
}

.books-panel {
  background: #fff;
  border-radius: 24px;
  padding: 20px 20px 8px;
  box-shadow: 0 10px 26px rgba(15, 23, 42, 0.06);
}

.panel-head {
  display: flex;
  align-items: end;
  justify-content: space-between;
}

.panel-title {
  margin: 0;
  font-size: 18px;
  font-weight: 800;
  color: #0f172a;
}

.panel-subtitle { color: #64748b; }

.book-card {
  overflow: hidden;
  border: 1px solid #eef2f7;
  transition: transform .22s ease, box-shadow .22s ease;
}

.book-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 14px 28px rgba(15, 23, 42, 0.12);
}

.book-cover {
  aspect-ratio: 2 / 3;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #0f766e, #22c55e);
}

.cover-img {
  position: absolute !important;
  inset: 0;
  width: 100% !important;
  height: 100% !important;
}

.heart-btn {
  position: absolute;
  top: 8px;
  left: 8px;
  z-index: 2;
  background: rgba(255,255,255,0.95);
  box-shadow: 0 4px 14px rgba(15, 23, 42, 0.12);
}

.heart-btn.is-favorite {
  background: rgba(255,255,255,0.98);
}

.book-title {
  margin: 0;
  font-size: 15px;
  font-weight: 800;
  color: #0f172a;
  line-height: 1.35;
}

.book-author {
  margin: 6px 0 0;
  color: #64748b;
  font-size: 12px;
}

.book-meta {
  margin-top: 12px;
}

@media (max-width: 960px) {
  .page-title { font-size: 22px; }
  .books-panel { padding: 16px 14px 6px; }
}
</style>
