import { createApp } from 'vue'
import { createPinia } from 'pinia'
import Antd from 'ant-design-vue'
import 'ant-design-vue/dist/reset.css'
import App from './App.vue'
import router from './router'
import './style.css'

function migrateLegacyHashRoute() {
  const pathname = window.location.pathname || ''
  const hash = window.location.hash || ''

  if (!pathname.startsWith('/ui/librarian/') || !hash.startsWith('#/')) {
    return
  }

  const rawRoute = hash.slice(1)
  const [routePath] = rawRoute.split('?')
  const normalizedRoute = routePath.replace(/^#?\/+/, '/').replace(/\/+$/, '')
  const search = window.location.search || ''

  const routeMap = {
    '/finance/revenue': '/embed/revenue',
    '/loans': '/embed/loans',
    '/fines': '/embed/fines',
    '/finance/prices': '/embed/prices',
    '/reviews': '/embed/reviews'
  }

  const target = routeMap[normalizedRoute] || normalizedRoute
  if (!target) return

  const nextPath = target.startsWith('/embed/')
    ? `${pathname.replace(/\/?$/, '')}${target}${search || ''}`
    : `${pathname.replace(/\/?$/, '')}${target}${search || ''}`

  if (window.location.pathname + window.location.search !== nextPath) {
    window.location.replace(nextPath)
  }
}

migrateLegacyHashRoute()

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.use(Antd)
app.mount('#app')
