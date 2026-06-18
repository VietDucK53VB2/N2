<template>
  <a-layout v-if="showChrome" class="lib-shell">
    <a-layout-sider
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      width="248"
      :collapsed-width="80"
      theme="light"
      class="lib-sider"
    >
      <div class="sider-brand">
        <div class="brand-avatar"><BookOutlined /></div>
        <transition name="fade-text">
          <div v-if="!collapsed" class="brand-copy">
            <span class="brand-name">SmartLib</span>
            <span class="brand-subtitle">Library System</span>
          </div>
        </transition>
      </div>

      <a-menu v-model:selectedKeys="selectedKeys" theme="light" mode="inline" @click="onMenuClick">
        <a-menu-item key="overview">
          <template #icon><DashboardOutlined /></template>
          <span>Dashboard</span>
        </a-menu-item>
        <a-menu-item key="loans">
          <template #icon><FileTextOutlined /></template>
          <span>Phiếu mượn</span>
          <a-badge v-if="store.pendingTx.length" :count="store.pendingTx.length" :number-style="{ marginLeft: '8px' }" />
        </a-menu-item>
        <a-menu-item key="search">
          <template #icon><SearchOutlined /></template>
          <span>Tra cứu</span>
        </a-menu-item>
        <a-menu-item key="return">
          <template #icon><RollbackOutlined /></template>
          <span>Trả sách</span>
        </a-menu-item>
        <a-menu-item key="reviews">
          <template #icon><StarOutlined /></template>
          <span>Đánh giá</span>
        </a-menu-item>
        <a-sub-menu key="finance">
          <template #icon><DollarOutlined /></template>
          <template #title>Quản lý Tài chính</template>
          <a-menu-item key="finance-prices">Giá &amp; phí</a-menu-item>
          <a-menu-item key="finance-revenue">Doanh thu</a-menu-item>
          <a-menu-item key="fines">Phí phạt</a-menu-item>
        </a-sub-menu>
      </a-menu>

      <div class="sider-footer">
        <div class="portal-pill">THỦ THƯ PORTAL</div>
        <a-button class="logout-btn" block @click="doLogout">
          <template #icon><LogoutOutlined /></template>
          <span v-if="!collapsed">Đăng xuất</span>
        </a-button>
      </div>
    </a-layout-sider>

    <a-layout class="lib-main">
      <a-layout-header class="lib-header">
        <div class="header-left">
          <a-button type="text" class="trigger-btn" @click="collapsed = !collapsed">
            <MenuUnfoldOutlined v-if="collapsed" />
            <MenuFoldOutlined v-else />
          </a-button>
          <div class="header-info">
            <h3 class="header-title">Chào mừng {{ loginName }}!</h3>
            <p v-if="pageSub" class="header-subtitle">{{ pageSub }}</p>
          </div>
        </div>

        <a-space class="header-right" :size="12">
          <a-input-search
            placeholder="Tìm kiếm Độc giả, Thẻ..."
            style="width: 260px"
            size="middle"
            class="header-search"
          />
          <a-dropdown trigger="click" :menu="{ items: settingsMenuItems, onClick: onToolbarMenuClick }">
            <a-button type="text" shape="circle" size="large" class="topbar-action">
              <SettingOutlined />
            </a-button>
          </a-dropdown>
          <a-popover
            trigger="click"
            placement="bottomRight"
            overlay-class-name="notifications-popover"
          >
            <template #content>
              <div class="popover-panel">
                <div class="popover-head">
                  <div>
                    <div class="popover-title">Thông báo nhanh</div>
                    <div class="popover-subtitle">{{ store.pendingTx.length }} phiếu đang chờ xử lý</div>
                  </div>
                  <a-button type="link" size="small" @click="router.push({ name: 'loans' })">
                    Xem phiếu mượn
                  </a-button>
                </div>
                <a-empty v-if="!pendingNotices.length" description="Không có thông báo mới" />
                <div v-else class="notice-list">
                  <div v-for="item in pendingNotices" :key="item.key" class="notice-item">
                    <div class="notice-badge" :class="item.variant">{{ item.variantLabel }}</div>
                    <div class="notice-body">
                      <div class="notice-title">{{ item.title }}</div>
                      <div class="notice-meta">{{ item.meta }}</div>
                    </div>
                  </div>
                </div>
              </div>
            </template>
            <a-badge :count="store.pendingTx.length" :offset="[-4, 4]">
              <a-button type="text" shape="circle" size="large" class="topbar-action">
                <BellOutlined />
              </a-button>
            </a-badge>
          </a-popover>
          <a-button class="lang-pill">EN</a-button>
          <div class="header-profile">
            <a-avatar size="40" class="header-avatar" :src="avatarUrl || undefined">{{ initials }}</a-avatar>
            <div class="profile-meta">
              <span class="profile-name">{{ loginName }}</span>
              <span class="profile-role">{{ roleLabel }}</span>
            </div>
          </div>
        </a-space>
      </a-layout-header>

      <a-layout-content class="lib-content">
        <router-view />
      </a-layout-content>
    </a-layout>

    <a-modal
      v-model:open="accountModalVisible"
      title="Thông tin tài khoản"
      centered
      :footer="null"
      width="420px"
    >
      <div class="account-card">
        <a-avatar size="56" class="account-avatar" :src="avatarUrl || undefined">{{ initials }}</a-avatar>
        <div class="account-copy">
          <div class="account-name">{{ loginName }}</div>
          <div class="account-role">{{ roleLabel }}</div>
        </div>
      </div>

      <a-descriptions :column="1" size="small" bordered class="account-desc">
        <a-descriptions-item label="Tên đăng nhập">{{ loginName }}</a-descriptions-item>
        <a-descriptions-item label="Họ và tên">{{ realName }}</a-descriptions-item>
        <a-descriptions-item label="Vai trò">{{ roleLabel }}</a-descriptions-item>
      </a-descriptions>
    </a-modal>
  </a-layout>

  <router-view v-else />
</template>

<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { clearLibrarianAuth, getLibrarianToken, getLibrarianUserInfo, useLibrarianStore } from '@/stores/librarian'
import {
  DashboardOutlined,
  FileTextOutlined,
  SearchOutlined,
  RollbackOutlined,
  DollarOutlined,
  LogoutOutlined,
  BellOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  StarOutlined,
  SettingOutlined,
  BookOutlined
} from '@ant-design/icons-vue'

const router = useRouter()
const route = useRoute()
const store = useLibrarianStore()
const collapsed = ref(false)
const selectedKeys = ref(['overview'])
const sessionUserInfo = ref(readSessionUserInfo())
const accountModalVisible = ref(false)

function readStoredUserInfo() {
  return getLibrarianUserInfo()
}

function parseJwt(token) {
  try {
    const b = String(token || '').split('.')[1]
    if (!b) return null
    return JSON.parse(decodeURIComponent(
      atob(b.replace(/-/g, '+').replace(/_/g, '/'))
        .split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join('')
    ))
  } catch {
    return null
  }
}

function readTokenInfo() {
  const token = getLibrarianToken()
  return token ? (parseJwt(token) || {}) : {}
}

function isGenericName(value = '') {
  const normalized = String(value || '').trim().toLowerCase()
  return [
    'độc giả',
    'reader',
    'thủ thư',
    'librarian',
    'thành viên',
    'bạn',
    'guest',
    'user',
    'admin',
    'quản trị viên'
  ].includes(normalized)
}

function firstMeaningful(...values) {
  for (const value of values) {
    if (value === null || value === undefined) continue
    const text = String(value).trim()
    if (text && !isGenericName(text)) return text
  }
  return ''
}

function extractDisplayName(info = {}) {
  return firstMeaningful(
    info.username,
    info.Username,
    info.fullName,
    info.FullName,
    info.displayName,
    info.DisplayName,
    info.name,
    info.Name,
    info.preferred_username,
    info.preferredUsername,
    info.unique_name,
    info.uniqueName,
    info.sub,
    info.Subject,
    'Thủ thư'
  ) || 'Thủ thư'
}

function getInitialsFromName(name = '') {
  const parts = String(name).trim().split(/\s+/).filter(Boolean)
  if (!parts.length) return 'TT'
  return parts.slice(-2).map(p => p[0]).join('').toUpperCase()
}

function readSessionUserInfo() {
  const stored = readStoredUserInfo()
  const tokenInfo = readTokenInfo()
  const merged = {
    ...stored,
    ...tokenInfo,
    fullName: firstMeaningful(
      stored.fullName,
      stored.FullName,
      tokenInfo.fullName,
      tokenInfo.FullName,
      stored.displayName,
      stored.DisplayName,
      tokenInfo.displayName,
      tokenInfo.DisplayName,
      tokenInfo.name,
      tokenInfo.Name,
      tokenInfo.username,
      tokenInfo.Username,
      stored.username,
      stored.Username,
      'Thủ thư'
    ),
    username: firstMeaningful(
      stored.username,
      stored.Username,
      tokenInfo.username,
      tokenInfo.Username,
      tokenInfo.name,
      tokenInfo.Name,
      tokenInfo.unique_name,
      tokenInfo.uniqueName,
      tokenInfo.preferred_username,
      tokenInfo.preferredUsername
    ),
    role: firstMeaningful(
      stored.role,
      stored.Role,
      tokenInfo.role,
      tokenInfo.Role,
      tokenInfo['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
      tokenInfo['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role'],
      'Admin'
    ) || 'Admin',
    avatarUrl: stored.avatarUrl || stored.AvatarUrl || stored.avatar || stored.Avatar || ''
  }

  if (!merged.fullName || isGenericName(merged.fullName)) {
    merged.fullName = merged.username || stored.cardNumber || tokenInfo.cardNumber || 'Thủ thư'
  }

  return merged
}

function syncSessionUser() {
  sessionUserInfo.value = readSessionUserInfo()
}

const userInfo = computed(() => sessionUserInfo.value)
const displayName = computed(() => extractDisplayName(userInfo.value))
const loginName = computed(() => {
  return firstMeaningful(
    userInfo.value?.username,
    userInfo.value?.Username,
    userInfo.value?.unique_name,
    userInfo.value?.uniqueName,
    userInfo.value?.preferred_username,
    userInfo.value?.preferredUsername,
    userInfo.value?.fullName,
    userInfo.value?.FullName,
    'Thủ thư'
  ) || 'Thủ thư'
})
const realName = computed(() => {
  return firstMeaningful(
    userInfo.value?.fullName,
    userInfo.value?.FullName,
    userInfo.value?.displayName,
    userInfo.value?.DisplayName,
    userInfo.value?.name,
    userInfo.value?.Name,
    loginName.value,
    'Thủ thư'
  ) || loginName.value
})
const initials = computed(() => getInitialsFromName(displayName.value))
const avatarUrl = computed(() => userInfo.value?.avatarUrl || userInfo.value?.AvatarUrl || userInfo.value?.avatar || userInfo.value?.Avatar || '')
const roleLabel = computed(() => {
  const role = String(userInfo.value?.role || '').trim().toLowerCase()
  if (role === 'librarian' || role === 'admin') return 'Thủ thư'
  if (role === 'reader') return 'Độc giả'
  return userInfo.value?.role || 'Thủ thư'
})

const pageSub = computed(() => {
  const sub = {
    overview: 'Tổng quan hoạt động thư viện',
    loans: 'Duyệt và xử lý phiếu mượn',
    search: 'Tra cứu theo mã thẻ độc giả',
    return: 'Xử lý trả sách và gia hạn',
    reviews: 'Theo dõi đánh giá sách',
    'finance-prices': 'Giá và phí dịch vụ',
    'finance-revenue': 'Tổng hợp doanh thu',
    fines: 'Quản lý phí phạt'
  }
  return sub[route.name] || ''
})

watch(
  () => route.name,
  n => { selectedKeys.value = [n || 'overview'] },
  { immediate: true }
)

function onMenuClick({ key }) {
  router.push({ name: key })
}

const settingsMenuItems = computed(() => ([
  { key: 'profile', label: 'Thông tin tài khoản' },
  { key: 'refresh', label: 'Làm mới dữ liệu' },
  { key: 'logout', label: 'Đăng xuất' }
]))

const pendingNotices = computed(() => {
  return store.pendingTx.slice(0, 5).map(tx => {
    const bookTitle = store.bookTitleOf(tx)
    return {
      key: tx.Id || tx.id || `${store.cardNumberOf(tx)}-${store.bookIdOf(tx)}`,
      variant: 'warning',
      variantLabel: 'Chờ duyệt',
      title: `${bookTitle}`,
      meta: `${store.cardNumberOf(tx)} · ${store.bookIdOf(tx)}`
    }
  })
})
function readEmbedMode() {
  const search = new URLSearchParams(window.location.search)
  if (search.get('embed') === '1' || search.get('embed') === 'true') return true

  const pathname = window.location.pathname || ''
  if (pathname.startsWith('/ui/librarian/embed/')) return true

  const hash = window.location.hash || ''
  if (!hash) return false

  const idx = hash.indexOf('?')
  if (idx === -1) return false

  const hashParams = new URLSearchParams(hash.slice(idx + 1))
  return hashParams.get('embed') === '1' || hashParams.get('embed') === 'true'
}

const isEmbedMode = ref(readEmbedMode())
const showChrome = computed(() =>
  window.location.pathname.startsWith('/ui/librarian/') &&
  !route.path.startsWith('/embed/') &&
  !isEmbedMode.value
)
function syncEmbedMode() {
  isEmbedMode.value = readEmbedMode()
}

async function onToolbarMenuClick({ key }) {
  if (key === 'refresh') {
    await store.loadAll()
    return
  }

  if (key === 'profile') {
    accountModalVisible.value = true
    return
  }

  if (key === 'logout') {
    doLogout()
  }
}

function doLogout() {
  clearLibrarianAuth()
  window.location.href = window.location.origin.replace(/:\d+$/, '') + '/login'
}

onMounted(() => {
  syncSessionUser()
  window.addEventListener('storage', syncSessionUser)
  window.addEventListener('hashchange', syncEmbedMode)
  window.addEventListener('popstate', syncEmbedMode)
})

onBeforeUnmount(() => {
  window.removeEventListener('storage', syncSessionUser)
  window.removeEventListener('hashchange', syncEmbedMode)
  window.removeEventListener('popstate', syncEmbedMode)
})

watch(
  () => route.fullPath,
  () => {
    syncSessionUser()
  },
  { immediate: true }
)
</script>

<style scoped>
.lib-shell {
  height: 100vh;
  min-height: 100vh;
  overflow: hidden;
  background: linear-gradient(180deg, #f7faf7 0%, #f3f6f0 100%);
}

.lib-main {
  height: 100vh;
  min-width: 0;
  overflow: hidden;
}

.lib-sider {
  background: linear-gradient(180deg, #eef7ef 0%, #edf7ec 100%) !important;
  display: flex;
  flex-direction: column;
  border-radius: 0 20px 20px 0;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1) !important;
  overflow: hidden;
  box-shadow: 4px 0 20px rgba(15, 23, 42, 0.08);
  height: 100vh;
}

.lib-sider :deep(.ant-layout-sider-children) {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.lib-sider :deep(.ant-menu) {
  background: transparent !important;
  border: none;
  flex: 1;
  padding: 8px 0 4px;
}

.lib-sider :deep(.ant-menu-item),
.lib-sider :deep(.ant-menu-submenu-title) {
  border-radius: 12px;
  margin: 4px 10px;
  height: 44px;
  line-height: 44px;
  color: #33514b !important;
  transition: all 0.22s cubic-bezier(0.34, 1.56, 0.64, 1);
}

.lib-sider :deep(.ant-menu-item:hover),
.lib-sider :deep(.ant-menu-submenu-title:hover) {
  background: rgba(7, 94, 67, 0.08) !important;
  color: #064e3b !important;
  transform: translateX(3px);
}

.lib-sider :deep(.ant-menu-item-selected) {
  background: #1f5f55 !important;
  color: #fff !important;
  box-shadow: 0 8px 20px rgba(31, 95, 85, 0.18);
  font-weight: 600;
}

.lib-sider :deep(.ant-menu-item-selected .anticon) {
  color: #fff !important;
}

.sider-brand {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 18px 14px;
  border-bottom: 1px solid rgba(31, 95, 85, 0.08);
}

.brand-avatar {
  width: 40px;
  height: 40px;
  min-width: 40px;
  border-radius: 12px;
  background: #1f5f55;
  color: #ffd97d;
  border: 1px solid rgba(15, 23, 42, 0.05);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  transition: transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
}

.brand-avatar:hover {
  transform: scale(1.1) rotate(-5deg);
}

.brand-copy {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.brand-name {
  font-size: 15px;
  font-weight: 800;
  color: #173f39;
  letter-spacing: -0.02em;
  white-space: nowrap;
}

.brand-subtitle {
  font-size: 12px;
  font-weight: 600;
  color: #7d8a83;
}

.sider-footer {
  padding: 16px 12px 12px;
  border-top: 1px solid rgba(31, 95, 85, 0.08);
  margin-top: auto;
}

.portal-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 6px 14px;
  margin: 0 auto 12px;
  border-radius: 999px;
  color: #d97706;
  border: 1px solid #f2d089;
  background: #fff5da;
  font-size: 12px;
  font-weight: 800;
  width: fit-content;
}

.logout-btn {
  border-radius: 16px;
  height: 42px;
  border-color: #cfe0d8;
  color: #33514b;
  background: #f4faf5;
  box-shadow: none;
}

.lib-header {
  background: #fff;
  margin: 16px 20px 0 20px;
  border-radius: 20px;
  padding: 16px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  box-shadow: 0 8px 24px rgba(15, 23, 42, 0.05);
  position: sticky;
  top: 16px;
  z-index: 10;
  height: 76px;
  min-height: 76px;
  line-height: normal !important;
  flex: 0 0 76px;
}

.header-left {
  display: flex;
  align-items: center;
  min-width: 0;
  gap: 14px;
}

.trigger-btn {
  font-size: 18px;
  width: 42px;
  height: 42px;
  border-radius: 12px;
  background: #f7f7f4;
  border: 1px solid #e7e3d7;
}

.header-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 4px;
  height: 100%;
}

.header-title {
  margin: 0;
  font-weight: 700;
  font-size: 18px;
  line-height: 1.25;
  color: #0f172a;
  white-space: nowrap;
}

.header-subtitle {
  margin: 2px 0 0;
  font-size: 12px;
  color: #8c98a5;
  white-space: nowrap;
}

.header-right {
  margin-left: auto;
}

.header-search {
  border-radius: 14px;
}

.header-search :deep(.ant-input-affix-wrapper) {
  border-radius: 14px;
  background: #f7f5ef;
  border-color: #e7e3d7;
  box-shadow: inset 0 0 0 1px #e7e3d7;
}

.header-search :deep(.ant-input) {
  background: transparent;
}

.topbar-action {
  width: 42px;
  height: 42px;
  border-radius: 12px !important;
  background: #f7f7f4;
  border: 1px solid #e7e3d7;
  color: #334155;
}

.lang-pill {
  height: 42px;
  padding: 0 14px;
  border-radius: 12px;
  background: #f7f7f4;
  border: 1px solid #e7e3d7;
  color: #33514b;
  font-weight: 700;
}

.header-profile {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 4px 10px 4px 6px;
  border-radius: 999px;
  border: 1px solid #dbe8de;
  background: #f7fbf8;
  box-shadow: 0 2px 8px rgba(15, 23, 42, 0.05);
}

.header-avatar {
  background: linear-gradient(135deg, #065f46, #047857);
  color: #fff;
  font-weight: 700;
  cursor: pointer;
  transition: transform 0.25s;
}

.header-avatar:hover {
  transform: scale(1.08);
}

.profile-meta {
  display: flex;
  flex-direction: column;
  line-height: 1.05;
  min-width: 0;
}

.profile-name {
  font-size: 13px;
  font-weight: 700;
  color: #0f172a;
  white-space: nowrap;
}

.profile-role {
  font-size: 11px;
  font-weight: 600;
  color: #64748b;
}

.popover-panel {
  width: 320px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.popover-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.popover-title {
  font-size: 14px;
  font-weight: 800;
  color: #103b35;
}

.popover-subtitle {
  margin-top: 2px;
  font-size: 12px;
  color: #7d8a83;
}

.notice-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-height: 280px;
  overflow: auto;
}

.notice-item {
  display: flex;
  gap: 10px;
  padding: 10px;
  border-radius: 14px;
  background: #f8fbf8;
  border: 1px solid #e5eee7;
}

.notice-badge {
  flex: 0 0 auto;
  border-radius: 999px;
  padding: 4px 8px;
  font-size: 11px;
  font-weight: 800;
  height: fit-content;
}

.notice-badge.warning {
  color: #b45309;
  background: #fff4df;
}

.notice-body {
  min-width: 0;
}

.notice-title {
  font-size: 13px;
  font-weight: 700;
  color: #173f39;
  line-height: 1.3;
}

.notice-meta {
  margin-top: 2px;
  font-size: 12px;
  color: #7d8a83;
}

.account-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 4px 0 16px;
}

.account-avatar {
  background: linear-gradient(135deg, #065f46, #047857);
  color: #fff;
  font-weight: 800;
}

.account-copy {
  display: flex;
  flex-direction: column;
}

.account-name {
  font-size: 16px;
  font-weight: 800;
  color: #103b35;
}

.account-role {
  font-size: 12px;
  color: #7d8a83;
}

.account-desc {
  margin-top: 6px;
}

.lib-content {
  padding: 20px;
  overflow-y: auto;
  background: linear-gradient(180deg, #f7faf7 0%, #f3f6f0 100%);
  height: calc(100vh - 92px);
  min-height: 0;
}

.fade-text-enter-active,
.fade-text-leave-active {
  transition: all 0.2s ease;
}

.fade-text-enter-from,
.fade-text-leave-to {
  opacity: 0;
  transform: translateX(-6px);
}
</style>
