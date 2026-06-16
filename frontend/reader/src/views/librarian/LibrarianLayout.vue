<template>
  <v-layout>
    <v-navigation-drawer v-model="drawer" permanent width="240" elevation="0" class="lib-sidebar">
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

    <v-main class="lib-main">
      <div class="lib-topbar">
        <div class="lib-topbar__greeting">
          <h2 class="topbar-title">Welcome {{ displayName }} !</h2>
        </div>

        <div class="lib-topbar__actions">
          <v-text-field
            v-model="searchText"
            prepend-inner-icon="mdi-magnify"
            placeholder="Tìm kiếm độc giả, thẻ..."
            hide-details
            density="compact"
            variant="solo-filled"
            flat
            rounded="lg"
            class="topbar-search"
            clearable
          />

          <v-btn icon variant="text" class="topbar-action">
            <v-badge :content="libStore.pendingCount" :model-value="libStore.pendingCount > 0" color="error" offset-x="2" offset-y="2">
              <v-icon size="20">mdi-bell-outline</v-icon>
            </v-badge>
          </v-btn>

          <v-btn variant="flat" rounded="xl" class="profile-pill">
            <v-avatar size="34" class="profile-pill__avatar" :image="avatarUrl || undefined">
              <span v-if="!avatarUrl">{{ initials }}</span>
            </v-avatar>
            <div class="profile-pill__meta">
              <span class="profile-pill__name">{{ displayName }}</span>
              <span class="profile-pill__role">{{ userRoleLabel }}</span>
            </div>
          </v-btn>
        </div>
      </div>

      <v-container fluid class="pa-6">
        <router-view />
      </v-container>
    </v-main>
  </v-layout>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAppStore } from '@/stores/app'
import { useLibrarianStore } from '@/stores/librarian'
import { getCachedUserInfo, logout } from '@/utils/api'
import { getInitials, getDisplayName } from '@/utils/helpers'

const route = useRoute()
const appStore = useAppStore()
const libStore = useLibrarianStore()
const drawer = ref(true)
const searchText = ref('')

const userInfo = computed(() => appStore.userInfo || getCachedUserInfo())
const displayName = computed(() => getDisplayName(userInfo.value, 'Thủ thư'))
const initials = computed(() => getInitials(displayName.value))
const avatarUrl = computed(() => userInfo.value?.avatarUrl || userInfo.value?.AvatarUrl || userInfo.value?.avatar || userInfo.value?.Avatar || '')
const userRoleLabel = computed(() => {
  const role = String(userInfo.value?.role || userInfo.value?.Role || 'Librarian').toLowerCase()
  if (role === 'admin') return 'Admin'
  if (role === 'reader') return 'Reader'
  return 'Librarian'
})

const navItems = computed(() => [
  { title: 'Dashboard', icon: 'mdi-view-dashboard', route: 'lib-overview' },
  { title: 'Phiếu mượn', icon: 'mdi-file-document-outline', route: 'lib-loans', badge: libStore.pendingCount || null },
  { title: 'Tra cứu', icon: 'mdi-magnify', route: 'lib-search' },
  { title: 'Trả sách', icon: 'mdi-book-arrow-left', route: 'lib-return' },
  { title: 'Phí phạt', icon: 'mdi-cash-multiple', route: 'lib-fines' }
])

function handleLogout() {
  logout()
}
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
  justify-content: space-between;
  gap: 18px;
  min-height: 72px;
  padding: 18px 28px;
  background: #fff;
  border-bottom: 1px solid #edf0ea;
  box-shadow: 0 1px 8px rgba(15, 23, 42, 0.04);
}

.lib-topbar__greeting {
  min-width: 0;
}

.topbar-title {
  font-size: 18px;
  font-weight: 700;
  color: #0f172a;
  margin: 0;
  white-space: nowrap;
}

.lib-topbar__actions {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 12px;
}

.topbar-search {
  width: min(42vw, 280px);
  min-width: 220px;
}

.topbar-search :deep(.v-field) {
  border-radius: 14px;
  background: #f7f5ef;
  box-shadow: inset 0 0 0 1px #e7e3d7;
}

.topbar-search :deep(.v-field__outline) {
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
  background: linear-gradient(135deg, #7c3aed, #c026d3) !important;
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
</style>
