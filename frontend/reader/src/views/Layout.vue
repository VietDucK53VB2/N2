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
        <div class="logo-circle" @click="$router.push('/')">
          <v-icon color="white" size="20">mdi-book-open-page-variant</v-icon>
        </div>
        <transition name="fade-text">
          <span v-if="!isRail" class="logo-text">Thư viện số</span>
        </transition>
      </div>

      <!-- Nav Items -->
      <div class="nav-list">
        <div
          v-for="(item, idx) in menuItems"
          :key="item.route"
          class="nav-item-wrap"
          :class="{ active: $route.name === item.route }"
          :style="{ animationDelay: idx * 50 + 'ms' }"
          @click="$router.push({ name: item.route })"
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

      <v-badge :content="store.overdueTransactions.length" :model-value="store.overdueTransactions.length > 0" color="error" overlap>
        <v-btn icon="mdi-bell-outline" variant="text" class="btn-glow" />
      </v-badge>

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
import { ref, computed } from 'vue'
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

// Rail mode: collapsed when not hovered
const isRail = computed(() => !hovered.value)

const initials = computed(() => getInitials(store.userInfo?.fullName))

const menuItems = computed(() => [
  { title: 'Trang chủ', icon: 'mdi-home-variant', route: 'dashboard' },
  { title: 'Sách của tôi', icon: 'mdi-bookshelf', route: 'mybooks', badge: store.activeTransactions.length || null },
  { title: 'Lịch sử', icon: 'mdi-history', route: 'history' },
  { title: 'Hồ sơ', icon: 'mdi-account-circle', route: 'profile' }
])

function onSearch() { if (searchText.value) router.push({ name: 'dashboard', query: { q: searchText.value } }) }
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
