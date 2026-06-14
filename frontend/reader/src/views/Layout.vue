<template>
  <div class="reader-shell">
    <!-- Sidebar: hover to expand, mouse leave to collapse -->
    <v-navigation-drawer
      v-model="drawer"
      :rail="isRail"
      permanent
      :width="220"
      :rail-width="64"
      class="glass-sidebar"
      elevation="0"
      @mouseenter="hovered = true"
      @mouseleave="hovered = false"
    >
      <!-- Logo -->
      <div class="sidebar-top">
        <div class="logo-circle" @click="goRoute('dashboard')">
          <v-icon color="white" size="20">mdi-book-open-page-variant</v-icon>
        </div>
        <transition name="fade-text">
          <span v-if="!isRail" class="logo-text">Thư viện số</span>
        </transition>
      </div>

      <!-- Nav Items -->
      <div class="nav-list">
        <div v-for="(item, idx) in menuItems" :key="item.route" class="nav-group">
          <div
            class="nav-item-wrap"
            :class="{ active: isMenuActive(item) }"
            :style="{ animationDelay: idx * 50 + 'ms' }"
            @click="goMenu(item)"
          >
            <div class="nav-icon-circle">
              <v-icon size="18" color="white">{{ item.icon }}</v-icon>
            </div>
            <transition name="fade-text">
              <span v-if="!isRail" class="nav-label">{{ item.title }}</span>
            </transition>
            <v-chip
              v-if="item.badge && !isRail"
              size="x-small" color="error" variant="flat"
              class="nav-badge pulse-badge"
            >
              {{ item.badge }}
            </v-chip>
            <v-icon v-if="item.children && !isRail" size="16" class="nav-chevron">
              {{ expandedMenus[item.route] ? 'mdi-chevron-up' : 'mdi-chevron-down' }}
            </v-icon>
          </div>
          <transition name="fade-text">
            <div v-if="item.children && expandedMenus[item.route] && !isRail" class="nav-children">
              <button
                v-for="child in item.children"
                :key="child.value"
                type="button"
                class="nav-child"
                :class="{ active: $route.name === item.route && $route.params.category === child.value }"
                @click.stop="goCategory(child.value)"
              >
                <span class="nav-child-dot"></span>
                <span>{{ child.label }}</span>
              </button>
            </div>
          </transition>
        </div>
      </div>

      <!-- Expand indicator -->
      <div class="sidebar-bottom">
        <div class="nav-item-wrap" @click="handleLogout">
          <div class="nav-icon-circle logout-circle">
            <v-icon size="18" color="white">mdi-logout</v-icon>
          </div>
          <transition name="fade-text">
            <span v-if="!isRail" class="nav-label" style="color:rgba(255,255,255,0.6)">Đăng xuất</span>
          </transition>
        </div>
      </div>
    </v-navigation-drawer>

    <!-- App Bar -->
    <v-app-bar flat color="white" elevation="0" class="reader-appbar">
      <v-btn icon variant="text" class="d-md-none" @click="drawer = !drawer">
        <v-icon>mdi-menu</v-icon>
      </v-btn>
      <v-text-field
        v-model="searchText"
        prepend-inner-icon="mdi-magnify"
        placeholder="Tìm kiếm sách, tác giả..."
        hide-details density="compact"
        variant="solo-filled" flat rounded="xl"
        class="header-search mx-4"
        style="max-width:480px"
        clearable
        @keyup.enter="onSearch"
      />

      <v-spacer />

      <v-menu location="bottom end" :close-on-content-click="false">
        <template #activator="{ props }">
          <v-badge :content="notificationItems.length" :model-value="notificationItems.length > 0" color="error" overlap>
            <v-btn v-bind="props" icon="mdi-bell-outline" variant="text" class="btn-glow" />
          </v-badge>
        </template>
        <v-card width="320" rounded="xl" elevation="8" class="notification-card">
          <v-card-title class="text-subtitle-1 font-weight-bold d-flex align-center justify-space-between">
            <span>Thông báo</span>
            <v-chip v-if="notificationItems.length" size="x-small" color="error" variant="flat">
              {{ notificationItems.length }}
            </v-chip>
          </v-card-title>
          <v-divider />
          <v-list v-if="notificationItems.length" density="compact" lines="two" class="py-1">
            <v-list-item
              v-for="item in notificationItems"
              :key="item.key"
              :prepend-icon="item.icon"
              :title="item.title"
              :subtitle="item.subtitle"
              @click="goNotification(item)"
            />
          </v-list>
          <div v-else class="pa-5 text-center text-grey">
            <v-icon size="32" color="grey-lighten-1" class="mb-2">mdi-bell-check-outline</v-icon>
            <p class="text-body-2 mb-0">Không có thông báo mới</p>
          </div>
        </v-card>
      </v-menu>

      <v-menu>
        <template #activator="{ props }">
          <v-avatar v-bind="props" size="36" class="ml-3 header-avatar">
            {{ initials }}
          </v-avatar>
        </template>
        <v-list density="compact" rounded="xl" width="180">
          <v-list-item prepend-icon="mdi-account-circle" title="Hồ sơ" @click="$router.push('/profile')" />
          <v-divider class="my-1" />
          <v-list-item prepend-icon="mdi-logout" title="Đăng xuất" @click="handleLogout" />
        </v-list>
      </v-menu>
    </v-app-bar>

    <!-- Main -->
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
import { ref, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAppStore } from '@/stores/app'
import { logout } from '@/utils/api'
import { getInitials } from '@/utils/helpers'

const router = useRouter()
const route = useRoute()
const store = useAppStore()

const drawer = ref(true)
const hovered = ref(false)
const searchText = ref('')
const expandedMenus = ref({ categories: true })

// Rail mode: collapsed when not hovered
const isRail = computed(() => !hovered.value)

const initials = computed(() => getInitials(store.userInfo?.fullName))

const notificationItems = computed(() => {
  const items = []
  store.overdueTransactions.slice(0, 4).forEach(tx => {
    items.push({
      key: `overdue-${tx.Id || tx.id || store.bookIdOf(tx)}`,
      icon: 'mdi-alert-circle-outline',
      title: tx.TenSach || tx.tenSach || 'Sách quá hạn',
      subtitle: 'Sách đã quá hạn trả',
      route: 'mybooks'
    })
  })
  store.activeTransactions.filter(store.isReturnPending).slice(0, 3).forEach(tx => {
    items.push({
      key: `return-${tx.Id || tx.id || store.bookIdOf(tx)}`,
      icon: 'mdi-book-clock-outline',
      title: tx.TenSach || tx.tenSach || 'Yêu cầu trả sách',
      subtitle: 'Đang chờ thủ thư kiểm tra trả sách',
      route: 'mybooks'
    })
  })
  store.pendingTransactions.slice(0, 3).forEach(tx => {
    items.push({
      key: `pending-${tx.Id || tx.id || store.bookIdOf(tx)}`,
      icon: 'mdi-clock-outline',
      title: tx.TenSach || tx.tenSach || 'Yêu cầu mượn sách',
      subtitle: 'Đang chờ thủ thư duyệt mượn',
      route: 'mybooks'
    })
  })
  return items.slice(0, 8)
})

const menuItems = computed(() => [
  { title: 'Trang chủ', icon: 'mdi-home-variant', route: 'dashboard' },
  { title: 'Sách của tôi', icon: 'mdi-bookshelf', route: 'mybooks', badge: store.activeTransactions.length || null },
  { title: 'Yêu thích', icon: 'mdi-heart', route: 'favorites' },
  { title: 'Thể loại', icon: 'mdi-shape-outline', route: 'categories', children: store.categories },
  { title: 'Lịch sử', icon: 'mdi-history', route: 'history' },
  { title: 'Hồ sơ', icon: 'mdi-account-circle', route: 'profile' }
])

async function refreshData() {
  if (typeof store.loadAll === 'function') await store.loadAll()
}

async function goRoute(routeName) {
  if (route.name !== routeName) await router.push({ name: routeName })
  await refreshData()
}

function isMenuActive(item) {
  return route.name === item.route
}

async function goMenu(item) {
  if (item.children) {
    expandedMenus.value = {
      ...expandedMenus.value,
      [item.route]: !expandedMenus.value[item.route]
    }
    if (route.name !== item.route) await router.push({ name: item.route, params: { category: 'all' } })
    await refreshData()
    return
  }
  await goRoute(item.route)
}

async function goCategory(category) {
  await router.push({ name: 'categories', params: { category } })
  await refreshData()
}

async function goNotification(item) {
  await goRoute(item.route || 'mybooks')
}

async function onSearch() {
  if (!searchText.value) return
  await router.push({ name: 'dashboard', query: { q: searchText.value } })
  await refreshData()
}

watch(() => route.name, () => {
  refreshData()
})

function handleLogout() { logout() }
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
  transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1) !important;
  overflow: visible !important;
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

  &:hover {
    transform: scale(1.1) rotate(-5deg);
    background: rgba(255, 255, 255, 0.22);
    box-shadow: 0 4px 14px rgba(255, 255, 255, 0.12);
  }
}

.logo-text {
  font-size: 14px;
  font-weight: 800;
  color: #fff;
  white-space: nowrap;
  letter-spacing: 0;
}

.nav-list {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 6px 0;
}

.nav-group {
  display: flex;
  flex-direction: column;
}

.nav-item-wrap {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 7px 8px;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.2s ease;
  animation: navIn 0.35s ease both;

  &:hover {
    background: rgba(255, 255, 255, 0.1);
    transform: translateX(2px);
  }

  &.active {
    background: rgba(255, 255, 255, 0.17);
    box-shadow: 0 2px 10px rgba(255, 255, 255, 0.08);

    .nav-icon-circle {
      background: rgba(255, 255, 255, 0.22);
      box-shadow: 0 0 10px rgba(255, 255, 255, 0.15);
    }
    .nav-label { font-weight: 700; color: #fff; }
  }
}

.nav-icon-circle {
  width: 36px;
  height: 36px;
  min-width: 36px;
  border-radius: 10px;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.25s;
}

.nav-label {
  font-size: 12.5px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.7);
  white-space: nowrap;
  letter-spacing: 0;
}

.nav-badge { margin-left: auto; }
.nav-chevron {
  margin-left: auto;
  color: rgba(255, 255, 255, 0.58);
}
.nav-children {
  margin: 2px 0 6px 24px;
  padding: 2px 0 2px 18px;
  border-left: 2px solid rgba(255, 255, 255, 0.18);
}
.nav-child {
  width: 100%;
  border: 0;
  background: transparent;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  border-radius: 8px;
  color: rgba(255, 255, 255, 0.66);
  font-size: 12px;
  font-weight: 700;
  text-align: left;
  cursor: pointer;
  transition: all 0.2s ease;
}
.nav-child:hover,
.nav-child.active {
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
}
.nav-child-dot {
  width: 5px;
  height: 5px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.45);
  flex-shrink: 0;
}
.nav-child.active .nav-child-dot {
  background: #fbbf24;
}
.pulse-badge { animation: pulse 2s infinite; }

.sidebar-bottom {
  margin-top: auto;
  padding-top: 6px;
  border-top: 1px solid rgba(255, 255, 255, 0.07);
}

.logout-circle {
  background: rgba(239, 68, 68, 0.15) !important;
  border-color: rgba(239, 68, 68, 0.25) !important;
}

// ─── App bar ───
.reader-appbar {
  border-bottom: 1px solid #f0f0f0 !important;
  position: fixed !important;
  top: 0;
  left: 64px !important;
  right: 0 !important;
  width: calc(100% - 64px) !important;
  z-index: 50;
  background: #fff !important;
}

.header-search {
  transition: box-shadow 0.3s;
  &:focus-within { box-shadow: 0 2px 12px rgba(4, 120, 87, 0.1); }
}

.header-avatar {
  background: linear-gradient(135deg, #065f46, #047857) !important;
  color: white !important;
  font-weight: 700;
  font-size: 13px;
  cursor: pointer;
  transition: transform 0.25s;
  &:hover { transform: scale(1.08); }
}

.btn-glow {
  transition: all 0.25s;
  &:hover { color: #047857 !important; }
}

.reader-main {
  background: #f7f9f8;
  min-height: 100vh;
  margin-left: 64px;
  padding-top: 64px;
}

// ─── Transitions ───
.page-slide-enter-active, .page-slide-leave-active { transition: all 0.25s ease; }
.page-slide-enter-from { opacity: 0; transform: translateY(12px); }
.page-slide-leave-to { opacity: 0; transform: translateY(-6px); }

.fade-text-enter-active, .fade-text-leave-active { transition: all 0.2s ease; }
.fade-text-enter-from, .fade-text-leave-to { opacity: 0; transform: translateX(-4px); }

@keyframes navIn { from { opacity: 0; transform: translateX(-8px); } to { opacity: 1; transform: translateX(0); } }
@keyframes pulse { 0%, 100% { transform: scale(1); } 50% { transform: scale(1.12); } }
</style>
