<template>
  <v-layout>
    <!-- Sidebar -->
    <v-navigation-drawer v-model="drawer" permanent width="240" elevation="0" class="lib-sidebar">
      <!-- Brand -->
      <div class="brand-area">
        <span class="brand-icon">📚</span>
        <span class="brand-text">SmartLib</span>
      </div>

      <v-list nav density="compact" class="px-3 mt-2">
        <v-list-item
          v-for="item in navItems"
          :key="item.route"
          :prepend-icon="item.icon"
          :title="item.title"
          :value="item.route"
          :active="$route.name === item.route"
          rounded="lg"
          class="nav-link mb-1"
          @click="$router.push({ name: item.route })"
        >
          <template v-if="item.badge" #append>
            <v-chip size="x-small" color="error" variant="flat" class="font-weight-bold">{{ item.badge }}</v-chip>
          </template>
        </v-list-item>
      </v-list>

      <v-spacer />

      <template #append>
        <v-divider class="mx-4 mb-2" />
        <v-list nav density="compact" class="px-3">
          <v-list-item prepend-icon="mdi-cog" title="Cài đặt" rounded="lg" class="nav-link" />
          <v-list-item prepend-icon="mdi-help-circle-outline" title="Trợ giúp" rounded="lg" class="nav-link" />
          <v-list-item prepend-icon="mdi-logout" title="Đăng xuất" rounded="lg" class="nav-link text-error" @click="handleLogout" />
        </v-list>
      </template>
    </v-navigation-drawer>

    <!-- Main area -->
    <v-main class="lib-main">
      <!-- Top bar -->
      <div class="lib-topbar">
        <div>
          <h2 class="topbar-title">{{ greeting }}</h2>
          <p class="topbar-sub">{{ subtitle }}</p>
        </div>
        <v-spacer />
        <v-text-field
          v-model="searchText"
          prepend-inner-icon="mdi-magnify"
          placeholder="Tìm kiếm..."
          hide-details
          density="compact"
          variant="solo-filled"
          flat
          rounded="lg"
          class="topbar-search"
          style="max-width:280px"
        />
        <v-badge :content="libStore.pendingCount" :model-value="libStore.pendingCount > 0" color="error" overlap>
          <v-btn icon="mdi-bell-outline" variant="text" class="ml-3" />
        </v-badge>
        <v-avatar size="36" class="ml-3 topbar-avatar">{{ initials }}</v-avatar>
      </div>

      <!-- Content -->
      <v-container fluid class="pa-6">
        <router-view />
      </v-container>
    </v-main>
  </v-layout>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { useLibrarianStore } from '@/stores/librarian'
import { getCachedUserInfo, logout } from '@/utils/api'
import { getInitials } from '@/utils/helpers'

const route = useRoute()
const libStore = useLibrarianStore()
const drawer = ref(true)
const searchText = ref('')

const userInfo = computed(() => getCachedUserInfo())
const initials = computed(() => getInitials(userInfo.value.fullName))

const greeting = computed(() => `Xin chào, ${(userInfo.value.fullName || 'Thủ thư').split(' ').pop()}!`)
const subtitle = computed(() => 'Quản lý thư viện hiệu quả mỗi ngày')

const navItems = computed(() => [
  { title: 'Dashboard', icon: 'mdi-view-dashboard', route: 'lib-overview' },
  { title: 'Phiếu mượn', icon: 'mdi-file-document-outline', route: 'lib-loans', badge: libStore.pendingCount || null },
  { title: 'Tra cứu', icon: 'mdi-magnify', route: 'lib-search' },
  { title: 'Trả sách', icon: 'mdi-book-arrow-left', route: 'lib-return' },
  { title: 'Phí phạt', icon: 'mdi-cash-multiple', route: 'lib-fines' }
])

function handleLogout() { logout() }
</script>

<style scoped lang="scss">
.lib-sidebar {
  background: #fff !important;
  border-right: 1px solid #f0f0f5 !important;
}

.brand-area {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 20px 20px 16px;
}

.brand-icon {
  font-size: 24px;
}

.brand-text {
  font-size: 18px;
  font-weight: 800;
  color: #6c3fb5;
}

.nav-link {
  font-weight: 500;
  color: #64748b;
  transition: all 0.2s;
}

:deep(.v-list-item--active) {
  background: linear-gradient(135deg, #7c3aed, #c026d3) !important;
  color: white !important;
  font-weight: 600;

  .v-list-item__prepend .v-icon {
    color: white !important;
  }
}

.lib-main {
  background: #f8f7fc;
}

.lib-topbar {
  display: flex;
  align-items: center;
  padding: 20px 28px;
  background: transparent;
}

.topbar-title {
  font-size: 22px;
  font-weight: 800;
  color: #1e1b4b;
  margin: 0;
}

.topbar-sub {
  font-size: 13px;
  color: #94a3b8;
  margin: 2px 0 0;
}

.topbar-search {
  background: #fff;
  border-radius: 12px;
}

.topbar-avatar {
  background: linear-gradient(135deg, #7c3aed, #c026d3);
  color: white;
  font-weight: 700;
  font-size: 13px;
  cursor: pointer;
}
</style>
