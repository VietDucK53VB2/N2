<template>
  <div class="reader-shell">
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
      <div class="sidebar-top">
        <div class="logo-circle" @click="$router.push('/')">
          <v-icon color="white" size="20">mdi-book-open-page-variant</v-icon>
        </div>
        <transition name="fade-text">
          <span v-if="!isRail" class="logo-text">Thư viện số</span>
        </transition>
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
            <transition name="fade-text">
              <span v-if="!isRail" class="nav-label">{{ item.title }}</span>
            </transition>
            <v-chip v-if="item.badge && !isRail" size="x-small" color="error" variant="flat" class="nav-badge pulse-badge">
              {{ item.badge }}
            </v-chip>
          </div>

          <div v-else class="nav-group">
            <div class="nav-item-wrap" :class="{ active: $route.name === item.route }" :style="{ animationDelay: idx * 50 + 'ms' }" @click="toggleGroup(item)">
              <div class="nav-icon-circle">
                <v-icon size="18" color="white">{{ item.icon }}</v-icon>
              </div>
              <transition name="fade-text">
                <span v-if="!isRail" class="nav-label">{{ item.title }}</span>
              </transition>
              <v-icon v-if="!isRail" size="16" class="nav-chevron">
                {{ expandedGroups[item.route] ? 'mdi-chevron-up' : 'mdi-chevron-down' }}
              </v-icon>
            </div>
            <transition name="fade-text">
              <div v-if="expandedGroups[item.route] && !isRail" class="nav-children">
                <button
                  v-for="child in item.children"
                  :key="child.route"
                  type="button"
                  class="nav-child"
                  :class="{ active: $route.name === child.route }"
                  @click.stop="$router.push({ name: 'categories', params: { category: child.route.replace('categories-', '') } })"
                >
                  <span class="nav-child-dot" />
                  <span>{{ child.label }}</span>
                </button>
              </div>
            </transition>
          </div>
        </template>
      </div>

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

    <v-app-bar flat color="white" elevation="0" class="reader-appbar">
      <v-btn icon variant="text" class="d-md-none" @click="drawer = !drawer">
        <v-icon>mdi-menu</v-icon>
      </v-btn>
      <v-text-field
        v-model="searchText"
        prepend-inner-icon="mdi-magnify"
        placeholder="Tìm kiếm sách, tác giả..."
        hide-details
        density="compact"
        variant="solo-filled"
        flat
        rounded="lg"
        class="header-search mx-4"
        style="max-width:480px"
        clearable
        @keyup.enter="onSearch"
      />
      <v-spacer />
      <v-badge :content="store.cartItems.length" :model-value="store.cartItems.length > 0" color="primary" overlap>
        <v-btn icon="mdi-cart-outline" variant="text" class="btn-glow ml-2" @click="$router.push('/cart')" />
      </v-badge>
      <v-menu>
        <template #activator="{ props }">
          <v-avatar v-bind="props" size="36" class="ml-3 header-avatar" :image="avatarUrl || undefined">
            <span v-if="!avatarUrl">{{ initials }}</span>
          </v-avatar>
        </template>
        <v-list density="compact" rounded="lg" width="180">
          <v-list-item prepend-icon="mdi-account-circle" title="Hồ sơ" @click="$router.push('/profile')" />
          <v-divider class="my-1" />
          <v-list-item prepend-icon="mdi-logout" title="Đăng xuất" @click="handleLogout" />
        </v-list>
      </v-menu>
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
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAppStore } from '@/stores/app'
import { logout } from '@/utils/api'
import { getInitials } from '@/utils/helpers'

const router = useRouter()
const route = useRoute()
const store = useAppStore()

const drawer = ref(true)
const hovered = ref(false)
const searchText = ref('')
const expandedGroups = ref({ categories: false })

const isRail = computed(() => !hovered.value)
const initials = computed(() => getInitials(store.userInfo?.fullName))
const avatarUrl = computed(() => store.userInfo?.avatarUrl || store.userInfo?.AvatarUrl || store.userInfo?.avatar || store.userInfo?.Avatar || '')

const menuItems = computed(() => [
  { title: 'Trang chủ', icon: 'mdi-home-variant', route: 'dashboard' },
  { title: 'Sách của tôi', icon: 'mdi-bookshelf', route: 'mybooks', badge: store.activeTransactions.length || null },
  { title: 'Yêu thích', icon: 'mdi-heart', route: 'favorites' },
  { title: 'Giỏ mượn', icon: 'mdi-cart-outline', route: 'cart', badge: 0 },
  {
    title: 'Thể loại',
    icon: 'mdi-shape-outline',
    route: 'categories',
    children: [
      { label: 'Tất cả', route: 'categories-all' },
      { label: 'Văn học', route: 'categories-vanhoc' },
      { label: 'Khoa học', route: 'categories-khoahoc' }
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
</script>

<style scoped lang="scss">
.reader-shell { min-height: 100vh; background: #f7f9f8; }
.glass-sidebar {
  background: linear-gradient(180deg, #064e3b 0%, #065f46 40%, #047857 100%) !important;
  border-radius: 0 20px 20px 0 !important;
  border: none !important;
  box-shadow: 4px 0 20px rgba(6, 78, 59, 0.25) !important;
  display: flex; flex-direction: column; padding: 10px 6px; transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1) !important;
  overflow: visible !important; z-index: 100; position: fixed !important; top: 0; left: 0; height: 100vh;
}
.sidebar-top { display: flex; align-items: center; gap: 12px; padding: 10px 6px 14px; }
.logo-circle { width: 40px; height: 40px; min-width: 40px; border-radius: 12px; background: rgba(255,255,255,0.13); border: 1px solid rgba(255,255,255,0.18); display: flex; align-items: center; justify-content: center; cursor: pointer; transition: all 0.3s cubic-bezier(0.34, 1.56, 0.64, 1); }
.logo-text { font-size: 14px; font-weight: 800; color: #fff; white-space: nowrap; }
.nav-list { flex: 1; display: flex; flex-direction: column; gap: 2px; padding: 6px 0; }
.nav-item-wrap { display: flex; align-items: center; gap: 10px; padding: 7px 8px; border-radius: 12px; cursor: pointer; transition: all 0.2s ease; animation: navIn 0.35s ease both; }
.nav-item-wrap:hover { background: rgba(255,255,255,0.1); transform: translateX(2px); }
.nav-item-wrap.active { background: rgba(255,255,255,0.17); box-shadow: 0 2px 10px rgba(255,255,255,0.08); }
.nav-icon-circle { width: 36px; height: 36px; min-width: 36px; border-radius: 10px; background: rgba(255,255,255,0.08); border: 1px solid rgba(255,255,255,0.1); display: flex; align-items: center; justify-content: center; }
.nav-label { font-size: 12.5px; font-weight: 500; color: rgba(255,255,255,0.7); white-space: nowrap; }
.nav-badge { margin-left: auto; }
.nav-group { display: flex; flex-direction: column; gap: 4px; }
.nav-chevron { margin-left: auto; color: rgba(255,255,255,0.7); }
.nav-children { display: flex; flex-direction: column; gap: 4px; margin: 0 0 0 44px; }
.nav-child { display: flex; align-items: center; gap: 8px; border: 0; background: transparent; color: rgba(255,255,255,0.72); padding: 4px 0; cursor: pointer; text-align: left; }
.nav-child.active, .nav-child:hover { color: #fff; }
.nav-child-dot { width: 6px; height: 6px; border-radius: 999px; background: rgba(255,255,255,0.55); }
.sidebar-bottom {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 10px;
  padding: 8px 12px 2px;
  border-top: 1px solid rgba(255,255,255,0.07);
  background: linear-gradient(180deg, rgba(4,120,87,0), rgba(4,120,87,0.08));
}
.logout-circle { background: rgba(239,68,68,0.15) !important; border-color: rgba(239,68,68,0.25) !important; }
.reader-appbar { border-bottom: 1px solid #f0f0f0 !important; position: fixed !important; top: 0; left: 64px !important; right: 0 !important; width: calc(100% - 64px) !important; z-index: 50; background: #fff !important; }
.reader-main { background: #f7f9f8; min-height: 100vh; margin-left: 64px; padding-top: 64px; }
.header-avatar { background: linear-gradient(135deg, #065f46, #047857) !important; color: white !important; font-weight: 700; font-size: 13px; cursor: pointer; }
.fade-text-enter-active, .fade-text-leave-active { transition: all 0.2s ease; }
.fade-text-enter-from, .fade-text-leave-to { opacity: 0; transform: translateX(-4px); }
@keyframes navIn { from { opacity: 0; transform: translateX(-8px); } to { opacity: 1; transform: translateX(0); } }
</style>
