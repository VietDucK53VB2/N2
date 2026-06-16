<template>
  <div class="favorites-page">
    <section class="page-hero mb-6">
      <div>
        <p class="eyebrow">Đã đánh dấu bằng tim</p>
        <h2 class="page-title">Sách yêu thích</h2>
        <p class="page-subtitle">Danh sách này gắn theo từng tài khoản, nên mỗi người sẽ thấy bộ yêu thích riêng của mình.</p>
      </div>
      <v-chip color="pink-lighten-4" variant="flat" class="count-chip">
        <v-icon start size="14" color="pink-darken-1">mdi-heart</v-icon>{{ favorites.length }} cuốn
      </v-chip>
    </section>

    <v-row v-if="favorites.length">
      <v-col v-for="book in favorites" :key="book.id" cols="6" sm="4" md="3" lg="2">
        <v-card class="book-card" rounded="xl" hover @click="$router.push({ name: 'dashboard', query: { q: book.tenSach } })">
          <div class="book-cover" :style="{ backgroundColor: titleColor(book.tenSach) }">
            <v-img v-if="book.imageUrl" :src="book.imageUrl" cover class="cover-img" />
            <v-icon v-else size="44" color="white" style="opacity:.35">mdi-book-open-variant</v-icon>
            <v-btn icon size="x-small" class="heart-btn is-favorite" @click.stop="toggleFavorite(book)">
              <v-icon size="18" color="pink">mdi-heart</v-icon>
            </v-btn>
          </div>
          <v-card-text class="pa-4">
            <p class="book-title">{{ book.tenSach }}</p>
            <p class="book-author">{{ book.tacGia }}</p>
            <v-chip size="x-small" variant="tonal" color="primary" class="mt-3">
              Còn {{ book.soBanConLai ?? '—' }}
            </v-chip>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-card v-else flat class="empty-card text-center pa-10">
      <v-icon size="64" color="pink-lighten-2" class="mb-3">mdi-heart-outline</v-icon>
      <p class="text-body-1 text-grey">Chưa có sách yêu thích nào</p>
      <p class="text-body-2 text-grey mt-1">Bấm vào biểu tượng tim trên thẻ sách để thêm vào đây.</p>
    </v-card>
  </div>
</template>

<script setup>
import { computed, onMounted } from 'vue'
import { useAppStore } from '@/stores/app'
import { titleColor } from '@/utils/helpers'

const store = useAppStore()
const favorites = computed(() => store.favorites || [])

function toggleFavorite(book) {
  store.toggleFavorite(book)
}

onMounted(async () => {
  await store.loadBooks()
  await store.loadFavoritesFromServer()
})
</script>

<style scoped lang="scss">
.favorites-page {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.page-hero {
  display: flex;
  justify-content: space-between;
  align-items: end;
  gap: 16px;
  border-radius: 24px;
  padding: 22px 24px;
  background: linear-gradient(135deg, #fff1f7 0%, #fff 45%, #fdf2f8 100%);
  border: 1px solid #f9dbe6;
}

.eyebrow {
  margin: 0 0 6px;
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #be185d;
  font-weight: 800;
}

.page-title {
  margin: 0;
  font-size: 28px;
  font-weight: 900;
  color: #0f172a;
}

.page-subtitle {
  margin: 8px 0 0;
  color: #64748b;
  font-size: 14px;
  max-width: 700px;
}

.count-chip {
  font-weight: 700;
  color: #be185d;
}

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
  top: 10px;
  left: 10px;
  z-index: 2;
  background: rgba(255, 255, 255, 0.95);
  border: 1px solid rgba(219, 39, 119, 0.15);
  box-shadow: 0 4px 12px rgba(0,0,0,0.08);
}

.heart-btn.is-favorite {
  background: rgba(255, 255, 255, 0.98);
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

.empty-card {
  border-radius: 24px;
  border: 1px dashed #f3b7cc;
  background: #fff;
}

@media (max-width: 960px) {
  .page-hero {
    flex-direction: column;
    align-items: flex-start;
  }

  .page-title {
    font-size: 22px;
  }
}
</style>
