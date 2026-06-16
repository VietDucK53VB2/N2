<template>
  <div class="reader-shell">
    <v-navigation-drawer
      permanent
      :width="220"
      class="glass-sidebar"
      elevation="0"
    >
      <div class="sidebar-top">
        <div class="logo-circle" @click="$router.push('/')">
          <v-icon color="white" size="20">mdi-book-open-page-variant</v-icon>
        </div>
        <span class="logo-text">Thư viện số</span>
      </div>

      <div class="nav-list">
        <template v-for="(item, idx) in menuItems" :key="item.route">
          <div
            v-if="!item.children"
            class="nav-item-wrap"
            :class="{ active: $route.name === item.route }"
            :style="{ animationDelay: idx * 50 + 'ms' }"
            @click="$router.push({ name: item.route })"
          >
            <div class="nav-icon-circle">
              <v-icon size="18" color="white">{{ item.icon }}</v-icon>
            </div>
            <span class="nav-label">{{ item.title }}</span>
            <v-chip
              v-if="item.badge"
              size="x-small"
              color="error"
              variant="flat"
              class="nav-badge pulse-badge"
            >
              {{ item.badge }}
            </v-chip>
          </div>

          <div v-else class="nav-group">
            <div
              class="nav-item-wrap"
              :class="{ active: $route.name === item.route || route.name === 'categories' }"
              :style="{ animationDelay: idx * 50 + 'ms' }"
              @click="toggleGroup(item)"
            >
              <div class="nav-icon-circle">
                <v-icon size="18" color="white">{{ item.icon }}</v-icon>
              </div>
              <span class="nav-label">{{ item.title }}</span>
              <v-icon size="16" class="nav-chevron">
                {{ expandedGroups[item.route] ? 'mdi-chevron-up' : 'mdi-chevron-down' }}
              </v-icon>
            </div>

            <div v-if="expandedGroups[item.route]" class="nav-children">
              <button
                v-for="child in item.children"
                :key="child.value"
                type="button"
                class="nav-child"
                :class="{ active: activeCategorySlug === slugifyCategory(child.value) }"
                @click.stop="$router.push({ name: 'categories', params: { category: child.value } })"
              >
                <span class="nav-child-dot" />
                <span>{{ child.label }}</span>
              </button>
            </div>
          </div>
        </template>
      </div>

      <div class="sidebar-bottom">
        <div class="nav-item-wrap" @click="handleLogout">
          <div class="nav-icon-circle logout-circle">
            <v-icon size="18" color="white">mdi-logout</v-icon>
          </div>
          <span class="nav-label sidebar-logout">Đăng xuất</span>
        </div>
      </div>
    </v-navigation-drawer>

    <v-app-bar flat color="white" elevation="0" class="reader-appbar">
      <div class="reader-appbar__inner">
        <div class="reader-greeting">
          <span class="reader-greeting__text">Welcome {{ displayName }} !</span>
        </div>

        <div class="reader-actions">
          <v-text-field
            v-model="searchText"
            prepend-inner-icon="mdi-magnify"
            placeholder="Tìm kiếm sách, tác giả..."
            hide-details
            density="compact"
            variant="solo-filled"
            flat
            rounded="lg"
            class="header-search"
            clearable
            @keyup.enter="onSearch"
          />

          <v-btn icon variant="text" class="topbar-action" @click="$router.push('/cart')">
            <v-badge :content="store.cartItems.length" :model-value="store.cartItems.length > 0" color="error" offset-x="2" offset-y="2">
              <v-icon size="20">mdi-cart-outline</v-icon>
            </v-badge>
          </v-btn>

          <v-menu>
            <template #activator="{ props }">
              <v-btn v-bind="props" variant="flat" rounded="xl" class="profile-pill">
                <v-avatar size="34" class="profile-pill__avatar" :image="avatarUrl || undefined">
                  <span v-if="!avatarUrl">{{ initials }}</span>
                </v-avatar>
                <div class="profile-pill__meta">
                  <span class="profile-pill__name">{{ displayName }}</span>
                  <span class="profile-pill__role">{{ userRoleLabel }}</span>
                </div>
              </v-btn>
            </template>
            <v-list density="compact" rounded="lg" width="180">
              <v-list-item prepend-icon="mdi-account-circle" title="Hồ sơ" @click="$router.push('/profile')" />
              <v-divider class="my-1" />
              <v-list-item prepend-icon="mdi-logout" title="Đăng xuất" @click="handleLogout" />
            </v-list>
          </v-menu>
        </div>
      </div>
    </v-app-bar>

    <main class="reader-main">
      <v-container fluid class="pa-6">
        <router-view v-slot="{ Component }">
          <component v-if="Component" :is="Component" :search-text="searchText" />
          <v-alert v-else type="warning" variant="tonal">
            Không tải được nội dung trang. Vui lòng tải lại trang.
          </v-alert>
        </router-view>
      </v-container>
    </main>
  </div>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAppStore } from '@/stores/app'
import { logout } from '@/utils/api'
import { getInitials, getDisplayName, buildCatalogCategories, slugifyCategory } from '@/utils/helpers'

const router = useRouter()
const route = useRoute()
const store = useAppStore()

const searchText = ref('')
const expandedGroups = ref({ categories: false })

const displayName = computed(() => getDisplayName(store.userInfo || {}, 'Độc giả'))
const initials = computed(() => getInitials(displayName.value))
const avatarUrl = computed(() => store.userInfo?.avatarUrl || store.userInfo?.AvatarUrl || store.userInfo?.avatar || store.userInfo?.Avatar || '')
const catalogCategories = computed(() => buildCatalogCategories(store.books))
const activeCategorySlug = computed(() => slugifyCategory(route.params.category || 'all'))
const userRoleLabel = computed(() => String(store.userInfo?.role || 'Reader').trim() || 'Reader')

const menuItems = computed(() => [
  { title: 'Trang chủ', icon: 'mdi-home-variant', route: 'dashboard' },
  { title: 'Sách của tôi', icon: 'mdi-bookshelf', route: 'mybooks', badge: store.activeTransactions.length || null },
  { title: 'Yêu thích', icon: 'mdi-heart', route: 'favorites' },
  { title: 'Giỏ mượn', icon: 'mdi-cart-outline', route: 'cart', badge: store.cartItems.length || null },
  {
    title: 'Thể loại',
    icon: 'mdi-shape-outline',
    route: 'categories',
    children: [
      { label: 'Tất cả', value: 'all' },
      ...catalogCategories.value
    ]
  },
  { title: 'Lịch sử', icon: 'mdi-history', route: 'history' },
  { title: 'Hồ sơ', icon: 'mdi-account-circle', route: 'profile' }
])

function onSearch() {
  if (searchText.value) router.push({ name: 'dashboard', query: { q: searchText.value } })
}

function toggleGroup(item) {
  if (!item.children) return
  expandedGroups.value[item.route] = !expandedGroups.value[item.route]
  if (route.name !== item.route) router.push({ name: item.route, params: { category: 'all' } })
}

function handleLogout() {
  logout()
}

watch(
  () => route.name,
  name => {
    if (name === 'categories') {
      expandedGroups.value.categories = true
    }
  },
  { immediate: true }
)
</script>

<style scoped lang="scss">
.reader-shell {
  min-height: 100vh;
  background: #f7f9f8;
}

.glass-sidebar {
  background: linear-gradient(180deg, #064e3b 0%, #065f46 40%, #047857 100%) !important;
  border-radius: 0 20px 20px 0 !important;
  border: none !important;
  box-shadow: 4px 0 20px rgba(6, 78, 59, 0.25) !important;
  display: flex;
  flex-direction: column;
  padding: 10px 6px;
  overflow: hidden !important;
  z-index: 100;
  position: fixed !important;
  top: 0;
  left: 0;
  height: 100vh;
}

.sidebar-top {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 6px 14px;
}

.logo-circle {
  width: 40px;
  height: 40px;
  min-width: 40px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.13);
  border: 1px solid rgba(255, 255, 255, 0.18);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
}

.logo-circle:hover {
  transform: scale(1.06);
  background: rgba(255, 255, 255, 0.2);
}

.logo-text {
  font-size: 13px;
  font-weight: 800;
  color: #fff;
  white-space: nowrap;
}

.nav-list {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 6px 0;
  overflow-y: auto;
  overflow-x: hidden;
  scrollbar-width: thin;
}

.nav-item-wrap {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 8px;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.2s ease;
  animation: navIn 0.35s ease both;
}

.nav-item-wrap:hover {
  background: rgba(255, 255, 255, 0.1);
  transform: translateX(2px);
}

.nav-item-wrap.active {
  background: rgba(255, 255, 255, 0.17);
  box-shadow: 0 2px 10px rgba(255, 255, 255, 0.08);
}

.nav-icon-circle {
  width: 34px;
  height: 34px;
  min-width: 34px;
  border-radius: 10px;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  align-items: center;
  justify-content: center;
}

.nav-label {
  font-size: 11px;
  font-weight: 600;
  color: rgba(255, 255, 255, 0.72);
  white-space: nowrap;
  min-width: 0;
}

.nav-badge {
  margin-left: auto;
}

.nav-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.nav-chevron {
  margin-left: auto;
  color: rgba(255, 255, 255, 0.7);
}

.nav-children {
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin: 0 0 0 40px;
  max-height: 260px;
  overflow-y: auto;
  overflow-x: hidden;
  padding-right: 2px;
}

.nav-child {
  display: flex;
  align-items: center;
  gap: 7px;
  border: 0;
  background: transparent;
  color: rgba(255, 255, 255, 0.72);
  padding: 2px 0;
  cursor: pointer;
  text-align: left;
  font-size: 10px;
  line-height: 1.25;
}

.nav-child.active,
.nav-child:hover {
  color: #fff;
}

.nav-child-dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.55);
  flex-shrink: 0;
}

.sidebar-bottom {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 10px;
  padding: 8px 12px 2px;
  border-top: 1px solid rgba(255, 255, 255, 0.07);
  background: linear-gradient(180deg, rgba(4, 120, 87, 0), rgba(4, 120, 87, 0.08));
}

.logout-circle {
  background: rgba(239, 68, 68, 0.15) !important;
  border-color: rgba(239, 68, 68, 0.25) !important;
}

.sidebar-logout {
  color: rgba(255, 255, 255, 0.6) !important;
}

.reader-appbar {
  position: fixed !important;
  top: 0;
  left: 220px !important;
  right: 0 !important;
  width: calc(100% - 220px) !important;
  z-index: 50;
  background: #fff !important;
  border-bottom: 1px solid #edf0ea !important;
  box-shadow: 0 1px 8px rgba(15, 23, 42, 0.04) !important;
  min-height: 72px !important;
  padding: 0 18px 0 16px;
}

.reader-appbar__inner {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 18px;
}

.reader-greeting {
  min-width: 0;
  display: flex;
  align-items: center;
}

.reader-greeting__text {
  font-size: 18px;
  font-weight: 700;
  color: #0f172a;
  white-space: nowrap;
}

.reader-actions {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-search {
  width: min(42vw, 260px);
  min-width: 220px;
}

.header-search :deep(.v-field) {
  border-radius: 14px;
  background: #f7f5ef;
  box-shadow: inset 0 0 0 1px #e7e3d7;
}

.header-search :deep(.v-field__outline) {
  display: none;
}

.topbar-action {
  width: 42px;
  height: 42px;
  border-radius: 12px !important;
  background: #f7f7f4;
  border: 1px solid #e7e3d7;
  color: #334155;
}

.profile-pill {
  min-width: 0;
  height: 42px;
  padding: 0 12px 0 8px;
  background: #f7fbf8;
  border: 1px solid #dbe8de;
  box-shadow: 0 2px 8px rgba(15, 23, 42, 0.05);
  text-transform: none;
}

.profile-pill__avatar {
  margin-right: 10px;
  background: linear-gradient(135deg, #065f46, #047857) !important;
  color: white !important;
  font-weight: 700;
  font-size: 12px;
}

.profile-pill__meta {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  line-height: 1.05;
  min-width: 0;
}

.profile-pill__name {
  font-size: 13px;
  font-weight: 700;
  color: #0f172a;
  white-space: nowrap;
}

.profile-pill__role {
  font-size: 11px;
  font-weight: 600;
  color: #64748b;
}

.reader-main {
  background: #f7f9f8;
  min-height: 100vh;
  margin-left: 220px;
  padding-top: 72px;
}

.fade-text-enter-active,
.fade-text-leave-active {
  transition: all 0.2s ease;
}

.fade-text-enter-from,
.fade-text-leave-to {
  opacity: 0;
  transform: translateX(-4px);
}

@keyframes navIn {
  from {
    opacity: 0;
    transform: translateX(-8px);
  }

  to {
    opacity: 1;
    transform: translateX(0);
  }
}

@media (max-width: 960px) {
  .reader-appbar {
    left: 0 !important;
    width: 100% !important;
  }

  .reader-main {
    margin-left: 0;
  }
}
</style>
