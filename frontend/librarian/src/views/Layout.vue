<template>
  <a-layout class="lib-shell">
    <a-layout-sider
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
        <div class="brand-avatar">📚</div>
        <transition name="fade-text">
          <span v-if="!collapsed" class="brand-name">SmartLib</span>
        </transition>
      </div>
      <a-menu v-model:selectedKeys="selectedKeys" theme="dark" mode="inline" @click="onMenuClick">
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
        <a-button type="text" danger block @click="doLogout">
          <template #icon><LogoutOutlined /></template>
          <span v-if="!collapsed">Đăng xuất</span>
        </a-button>
      </div>
    </a-layout-sider>

    <a-layout class="lib-main">
      <a-layout-header class="lib-header">
        <a-button type="text" class="trigger-btn" @click="collapsed = !collapsed">
          <MenuUnfoldOutlined v-if="collapsed" />
          <MenuFoldOutlined v-else />
        </a-button>
        <div class="header-info">
          <h3 class="header-title">{{ pageTitle }}</h3>
          <p class="header-sub">{{ pageSub }}</p>
        </div>
        <a-space class="header-right" :size="12">
          <a-input-search placeholder="Tìm kiếm..." style="width:220px" size="middle" />
          <a-badge :count="store.pendingTx.length" :offset="[-4, 4]">
            <a-button type="text" shape="circle" size="large"><BellOutlined /></a-button>
          </a-badge>
          <a-avatar size="36" class="header-avatar">TT</a-avatar>
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
  DashboardOutlined,
  FileTextOutlined,
  SearchOutlined,
  RollbackOutlined,
  DollarOutlined,
  LogoutOutlined,
  BellOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  StarOutlined
} from '@ant-design/icons-vue'

const router = useRouter()
const route = useRoute()
const store = useLibrarianStore()
const collapsed = ref(true)
const selectedKeys = ref(['overview'])

const titles = {
  overview: 'Dashboard',
  loans: 'Xử lý Phiếu mượn',
  search: 'Tra cứu Tình trạng',
  return: 'Xác nhận Trả sách',
  reviews: 'Đánh giá sách',
  'finance-prices': 'Giá & phí',
  'finance-revenue': 'Doanh thu',
  fines: 'Quản lý Phí phạt'
}

const subs = {
  overview: 'Tổng quan hoạt động thư viện',
  loans: 'Duyệt hoặc từ chối yêu cầu mượn sách',
  search: 'Tìm kiếm theo mã thẻ độc giả',
  return: 'Wizard 3 bước xử lý trả sách',
  reviews: 'Theo dõi và tổng hợp đánh giá sách',
  'finance-prices': 'Thiết lập và theo dõi khung giá, phí dịch vụ',
  'finance-revenue': 'Tổng hợp doanh thu từ hoạt động thư viện',
  fines: 'Theo dõi và thu phí phạt'
}

const pageTitle = computed(() => titles[route.name] || 'Thủ thư')
const pageSub = computed(() => subs[route.name] || '')

watch(() => route.name, n => { selectedKeys.value = [n || 'overview'] }, { immediate: true })
function onMenuClick({ key }) { router.push({ name: key }) }
function doLogout() { localStorage.clear(); window.location.href = window.location.origin + '/login' }
</script>

<style scoped>
.lib-shell { height: 100vh; min-height: 100vh; overflow: hidden; background: #f7f9f8; }
.lib-main { height: 100vh; min-width: 0; overflow: hidden; }
.lib-sider {
  background: linear-gradient(180deg, #064e3b 0%, #065f46 40%, #047857 100%) !important;
  display: flex; flex-direction: column; border-radius: 0 20px 20px 0;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1) !important; overflow: hidden;
  box-shadow: 4px 0 20px rgba(6, 78, 59, 0.2); height: 100vh;
}
.lib-sider :deep(.ant-layout-sider-children) { display: flex; flex-direction: column; height: 100%; }
.lib-sider :deep(.ant-menu) { background: transparent !important; border: none; flex: 1; padding: 4px 0; }
.lib-sider :deep(.ant-menu-item) {
  border-radius: 12px; margin: 3px 8px; height: 42px; line-height: 42px;
  color: rgba(255,255,255,0.65); transition: all 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.lib-sider :deep(.ant-menu-item:hover) { background: rgba(255,255,255,0.1) !important; color: #fff !important; transform: translateX(3px); }
.lib-sider :deep(.ant-menu-item-selected) { background: rgba(255,255,255,0.18) !important; color: #fff !important; box-shadow: 0 2px 12px rgba(255,255,255,0.1); font-weight: 600; }
.lib-sider :deep(.ant-menu-item-selected .anticon) { color: #fff !important; }
.sider-brand { display: flex; align-items: center; gap: 12px; padding: 18px 14px; border-bottom: 1px solid rgba(255,255,255,0.06); }
.brand-avatar { width: 40px; height: 40px; min-width: 40px; border-radius: 12px; background: rgba(255, 255, 255, 0.12); border: 1px solid rgba(255, 255, 255, 0.15); display: flex; align-items: center; justify-content: center; font-size: 20px; transition: transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1); }
.brand-avatar:hover { transform: scale(1.1) rotate(-5deg); }
.brand-name { font-size: 15px; font-weight: 800; color: #fff; letter-spacing: -0.02em; white-space: nowrap; }
.sider-footer { padding: 12px; border-top: 1px solid rgba(255,255,255,0.06); margin-top: auto; }
.lib-header {
  background: #fff; padding: 10px 28px; display: flex; align-items: center; gap: 16px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.04); position: sticky; top: 0; z-index: 10;
  height: 72px; min-height: 72px; line-height: normal !important; flex: 0 0 72px;
}
.trigger-btn { font-size: 18px; }
.header-info { flex: 1; min-width: 0; display: flex; flex-direction: column; justify-content: center; gap: 4px; height: 100%; }
.header-title { margin: 0; font-weight: 800; font-size: 17px; line-height: 1.25; color: #1e293b; }
.header-sub { margin: 0; font-size: 12px; line-height: 1.35; color: #94a3b8; }
.header-right { margin-left: auto; }
.header-avatar { background: linear-gradient(135deg, #065f46, #047857); color: #fff; font-weight: 700; cursor: pointer; transition: transform 0.25s; }
.header-avatar:hover { transform: scale(1.08); }
.lib-content { padding: 28px; overflow-y: auto; background: #f7f9f8; height: calc(100vh - 72px); min-height: 0; }
.fade-text-enter-active, .fade-text-leave-active { transition: all 0.2s ease; }
.fade-text-enter-from, .fade-text-leave-to { opacity: 0; transform: translateX(-6px); }
</style>
