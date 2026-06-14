<template>
  <a-layout class="lib-shell" :class="{ 'is-embedded': isEmbedded }">
    <a-layout-sider
      v-if="!isEmbedded"
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      width="240"
      :collapsed-width="64"
      theme="dark"
      class="lib-sider"
      @mouseenter="collapsed = false"
      @mouseleave="collapsed = true"
    >
      <div class="sider-brand">
        <div class="brand-avatar">
          <ReadOutlined />
        </div>
        <transition name="fade-text">
          <span v-if="!collapsed" class="brand-name">SmartLib</span>
        </transition>
      </div>
      <a-menu
        v-model:selectedKeys="selectedKeys"
        theme="dark"
        mode="inline"
        @click="onMenuClick"
      >
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
        <a-menu-item key="fines">
          <template #icon><DollarOutlined /></template>
          <span>Phí phạt</span>
        </a-menu-item>
      </a-menu>
      <div class="sider-footer">
        <a-button type="text" danger block @click="doLogout">
          <template #icon><LogoutOutlined /></template>
          <span v-if="!collapsed">Đăng xuất</span>
        </a-button>
      </div>
    </a-layout-sider>

    <a-layout class="lib-main">
      <a-layout-header v-if="!isEmbedded" class="lib-header">
        <a-button v-if="!isEmbedded" type="text" class="trigger-btn" @click="collapsed = !collapsed">
          <MenuUnfoldOutlined v-if="collapsed" />
          <MenuFoldOutlined v-else />
        </a-button>
        <a-button v-if="!isEmbedded" type="text" class="trigger-btn" @click="goBack">
          <ArrowLeftOutlined />
        </a-button>
        <div class="header-info">
          <h3 class="header-title">{{ pageTitle }}</h3>
          <p class="header-sub">{{ pageSub }}</p>
        </div>
        <a-space class="header-right" :size="12">
          <a-input-search placeholder="Tìm kiếm..." style="width:220px" size="middle" />
          <a-popover
            v-model:open="notificationOpen"
            trigger="click"
            placement="bottomRight"
            overlay-class-name="lib-notification-popover"
          >
            <template #content>
              <div class="notification-panel">
                <div class="notification-head">
                  <div class="notification-title">
                    <BellOutlined />
                    <span>Thông báo</span>
                  </div>
                  <a-button type="link" size="small" class="refresh-link" @click="refreshNotifications">Làm mới</a-button>
                </div>
                <div class="notification-tags">
                  <button
                    v-for="tag in notificationTags"
                    :key="tag.key"
                    class="notify-tag"
                    :class="{ active: tag.count > 0 }"
                    type="button"
                    @click="goNotification(tag.route)"
                  >
                    {{ tag.label }}
                    <span v-if="tag.count" class="tag-count">{{ tag.count }}</span>
                  </button>
                </div>
                <div class="notification-list">
                  <button
                    v-for="item in notificationItems"
                    :key="item.key"
                    class="notification-item"
                    type="button"
                    @click="goNotification(item.route)"
                  >
                    <span class="notification-icon" :class="item.tone">{{ item.short }}</span>
                    <span class="notification-copy">
                      <strong>{{ item.title }}</strong>
                      <span>{{ item.subtitle }}</span>
                      <small>{{ item.meta }}</small>
                    </span>
                  </button>
                  <div v-if="!notificationItems.length" class="notification-empty">
                    Không có thông báo mới
                  </div>
                </div>
              </div>
            </template>
            <a-badge :count="notificationItems.length" :offset="[-4, 4]">
              <a-button type="text" shape="circle" size="large"><BellOutlined /></a-button>
            </a-badge>
          </a-popover>
          <a-avatar v-if="!isEmbedded" size="36" class="header-avatar">TT</a-avatar>
        </a-space>
      </a-layout-header>
      <a-layout-content class="lib-content">
        <router-view />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useLibrarianStore } from '@/stores/librarian'
import {
  ArrowLeftOutlined,
  BellOutlined,
  DashboardOutlined,
  DollarOutlined,
  FileTextOutlined,
  LogoutOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  ReadOutlined,
  RollbackOutlined,
  SearchOutlined,
  StarOutlined
} from '@ant-design/icons-vue'

const router = useRouter()
const route = useRoute()
const store = useLibrarianStore()
const collapsed = ref(true)
const selectedKeys = ref(['overview'])
const notificationOpen = ref(false)
const isEmbedded = computed(() => {
  const params = new URLSearchParams(window.location.search)
  const hashQuery = window.location.hash.includes('?')
    ? new URLSearchParams(window.location.hash.slice(window.location.hash.indexOf('?') + 1))
    : new URLSearchParams()
  const embedValue = params.get('embed') || hashQuery.get('embed')
  const hideMenuValue = params.get('hideMenu') || hashQuery.get('hideMenu')
  const adminValue = params.get('admin') || hashQuery.get('admin')
  const modeValue = params.get('mode') || hashQuery.get('mode')
  const embeddedValue = params.get('embedded') || hashQuery.get('embedded')
  return window.self !== window.top ||
    embedValue === 'admin' ||
    embedValue === '1' ||
    hideMenuValue === '1' ||
    adminValue === '1' ||
    modeValue === 'admin' ||
    embeddedValue === '1'
})

const titles = {
  overview: 'Dashboard',
  loans: 'Xử lý Phiếu mượn',
  search: 'Tra cứu Tình trạng',
  return: 'Xác nhận Trả sách',
  reviews: 'Đánh giá sách',
  fines: 'Quản lý Phí phạt'
}

const subs = {
  overview: 'Tổng quan hoạt động thư viện',
  loans: 'Duyệt hoặc từ chối yêu cầu mượn sách',
  search: 'Tìm kiếm theo mã thẻ độc giả',
  return: 'Wizard 3 bước xử lý trả sách',
  reviews: 'Xem số sao và bình luận của độc giả',
  fines: 'Theo dõi và thu phí phạt'
}

const pageTitle = computed(() => titles[route.name] || 'Thủ thư')
const pageSub = computed(() => subs[route.name] || '')
const notificationTags = computed(() => [
  { key: 'pending', label: 'Mượn mới', count: store.pendingTx.length, route: 'loans' },
  { key: 'return', label: 'Chờ trả', count: store.returnPendingTx.length, route: 'loans' },
  { key: 'overdue', label: 'Quá hạn', count: store.overdueTx.length, route: 'loans' },
  { key: 'fines', label: 'Phí chưa thu', count: store.unpaidFines.length, route: 'fines' },
  { key: 'reviews', label: 'Đánh giá', count: store.totalReviewCount, route: 'reviews' }
])
const notificationItems = computed(() => {
  const rows = []
  store.pendingTx.slice(0, 3).forEach(tx => {
    rows.push({
      key: `pending-${tx.Id || tx.id || store.bookIdOf(tx)}`,
      short: 'M',
      tone: 'warn',
      title: 'Yêu cầu mượn mới',
      subtitle: `${store.readerNameOf(tx)} - ${store.bookTitleOf(tx)}`,
      meta: 'Cần duyệt hoặc từ chối',
      route: 'loans'
    })
  })
  store.returnPendingTx.slice(0, 3).forEach(tx => {
    rows.push({
      key: `return-${tx.Id || tx.id || store.bookIdOf(tx)}`,
      short: 'T',
      tone: 'info',
      title: 'Chờ kiểm tra trả sách',
      subtitle: `${store.readerNameOf(tx)} - ${store.bookTitleOf(tx)}`,
      meta: 'Cần đánh giá tình trạng sách',
      route: 'loans'
    })
  })
  store.overdueTx.slice(0, 2).forEach(tx => {
    rows.push({
      key: `overdue-${tx.Id || tx.id || store.bookIdOf(tx)}`,
      short: 'Q',
      tone: 'danger',
      title: 'Phiếu mượn quá hạn',
      subtitle: `${store.readerNameOf(tx)} - ${store.bookTitleOf(tx)}`,
      meta: 'Cần nhắc trả sách hoặc xử lý phí',
      route: 'loans'
    })
  })
  if (store.unpaidFines.length) {
    rows.push({
      key: 'fines-unpaid',
      short: 'P',
      tone: 'money',
      title: 'Có phí chưa thu',
      subtitle: `${store.unpaidFines.length} khoản phí đang chờ thu`,
      meta: 'Mở quản lý phí phạt',
      route: 'fines'
    })
  }
  return rows.slice(0, 8)
})

watch(() => route.name, n => {
  selectedKeys.value = [n || 'overview']
  store.loadAll()
}, { immediate: true })

async function onMenuClick({ key }) {
  if (route.name !== key) await router.push({ name: key })
  await store.loadAll()
}

function goBack() {
  router.back()
  window.setTimeout(() => store.loadAll(), 250)
}

async function goNotification(routeName = 'loans') {
  notificationOpen.value = false
  if (route.name !== routeName) await router.push({ name: routeName })
  await store.loadAll()
}

async function refreshNotifications() {
  await store.loadAll()
}

function doLogout() {
  localStorage.clear()
  window.location.href = 'http://163.223.210.87:80/'
}
</script>

<style scoped>
.lib-shell {
  height: 100vh;
  min-height: 100vh;
  overflow: hidden;
  background: #f7f9f8;
}
.lib-main {
  height: 100vh;
  min-width: 0;
  overflow: hidden;
}
.lib-sider {
  background: linear-gradient(180deg, #064e3b 0%, #065f46 40%, #047857 100%) !important;
  display: flex;
  flex-direction: column;
  border-radius: 0 20px 20px 0;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1) !important;
  overflow: hidden;
  box-shadow: 4px 0 20px rgba(6, 78, 59, 0.2);
  height: 100vh;
}
.lib-sider :deep(.ant-layout-sider-children) { display: flex; flex-direction: column; height: 100%; }
.lib-sider :deep(.ant-menu) { background: transparent !important; border: none; flex: 1; padding: 4px 0; }
.lib-sider :deep(.ant-menu-item) {
  border-radius: 12px;
  margin: 3px 8px;
  height: 42px;
  line-height: 42px;
  color: rgba(255,255,255,0.65);
  transition: all 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.lib-sider :deep(.ant-menu-item:hover) {
  background: rgba(255,255,255,0.1) !important;
  color: #fff !important;
  transform: translateX(3px);
}
.lib-sider :deep(.ant-menu-item-selected) {
  background: rgba(255,255,255,0.18) !important;
  color: #fff !important;
  box-shadow: 0 2px 12px rgba(255,255,255,0.1);
  font-weight: 600;
}
.lib-sider :deep(.ant-menu-item-selected .anticon) { color: #fff !important; }

.sider-brand {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 18px 14px;
  border-bottom: 1px solid rgba(255,255,255,0.06);
}
.brand-avatar {
  width: 40px;
  height: 40px;
  min-width: 40px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.12);
  border: 1px solid rgba(255, 255, 255, 0.15);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  color: #fff;
  transition: transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.brand-avatar:hover { transform: scale(1.1) rotate(-5deg); }
.brand-name { font-size: 15px; font-weight: 800; color: #fff; letter-spacing: 0; white-space: nowrap; }
.sider-footer { padding: 12px; border-top: 1px solid rgba(255,255,255,0.06); margin-top: auto; }

.lib-header {
  background: #fff;
  padding: 10px 28px;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.04);
  position: sticky;
  top: 0;
  z-index: 10;
  height: 72px;
  min-height: 72px;
  line-height: normal !important;
  flex: 0 0 72px;
}
.trigger-btn { font-size: 18px; }
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
  font-weight: 800;
  font-size: 17px;
  line-height: 1.25;
  letter-spacing: 0;
  color: #1e293b;
}
.header-sub {
  margin: 0;
  font-size: 12px;
  line-height: 1.35;
  color: #94a3b8;
}
.header-right { margin-left: auto; }
.header-avatar { background: linear-gradient(135deg, #065f46, #047857); color: #fff; font-weight: 700; cursor: pointer; transition: transform 0.25s; }
.header-avatar:hover { transform: scale(1.08); }

.notification-panel {
  width: 360px;
  padding: 2px;
}
.notification-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 10px 12px;
  border-bottom: 1px solid #eef2f7;
}
.notification-title {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #064e3b;
  font-weight: 800;
}
.refresh-link {
  padding: 0;
  font-weight: 600;
}
.notification-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  padding: 12px 10px 10px;
}
.notify-tag {
  border: 0;
  border-radius: 999px;
  background: #eef7f4;
  color: #0f513f;
  cursor: pointer;
  font-size: 11px;
  font-weight: 700;
  line-height: 1;
  padding: 7px 10px;
  transition: all 0.2s ease;
}
.notify-tag:hover,
.notify-tag.active {
  background: #d9f2ea;
  color: #065f46;
}
.tag-count {
  margin-left: 5px;
  color: #dc2626;
}
.notification-list {
  border-top: 1px solid #eef2f7;
  display: grid;
  gap: 8px;
  max-height: 320px;
  overflow-y: auto;
  padding: 10px;
}
.notification-item {
  width: 100%;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  background: #fff;
  cursor: pointer;
  display: grid;
  grid-template-columns: 40px 1fr;
  gap: 10px;
  padding: 10px;
  text-align: left;
  transition: all 0.2s ease;
}
.notification-item:hover {
  border-color: #a7f3d0;
  box-shadow: 0 8px 22px rgba(15, 23, 42, 0.08);
  transform: translateY(-1px);
}
.notification-icon {
  align-items: center;
  border-radius: 11px;
  color: #fff;
  display: flex;
  font-weight: 800;
  height: 36px;
  justify-content: center;
  width: 36px;
}
.notification-icon.warn { background: #f59e0b; }
.notification-icon.info { background: #2563eb; }
.notification-icon.danger { background: #dc2626; }
.notification-icon.money { background: #047857; }
.notification-copy {
  display: grid;
  gap: 2px;
  min-width: 0;
}
.notification-copy strong {
  color: #0f172a;
  font-size: 13px;
}
.notification-copy span {
  color: #475569;
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.notification-copy small {
  color: #94a3b8;
  font-size: 11px;
}
.notification-empty {
  color: #94a3b8;
  font-size: 13px;
  padding: 20px 10px;
  text-align: center;
}

:global(.lib-notification-popover .ant-popover-inner) {
  border-radius: 12px;
  box-shadow: 0 18px 45px rgba(15, 23, 42, 0.16);
  padding: 0;
}
:global(.lib-notification-popover .ant-popover-arrow::before) {
  background: #fff;
}

.lib-content {
  padding: 28px;
  overflow-y: auto;
  background: #f7f9f8;
  height: calc(100vh - 72px);
  min-height: 0;
}

.is-embedded .lib-content {
  height: 100vh;
  padding: 24px;
}

.fade-text-enter-active, .fade-text-leave-active { transition: all 0.2s ease; }
.fade-text-enter-from, .fade-text-leave-to { opacity: 0; transform: translateX(-6px); }
</style>
