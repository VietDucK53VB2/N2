import { createApp } from 'vue'
import { createPinia } from 'pinia'
import Antd from 'ant-design-vue'
import 'ant-design-vue/dist/reset.css'
import App from './App.vue'
import router from './router'
import './style.css'

const HANDOFF_REDEEM_URL = 'http://163.223.210.87:5000/api/identity/Auth/handoff/redeem'

function parseJwt(token) {
  try {
    const b = token.split('.')[1]
    return JSON.parse(decodeURIComponent(
      atob(b.replace(/-/g, '+').replace(/_/g, '/'))
        .split('').map(c => `%${(`00${c.charCodeAt(0).toString(16)}`).slice(-2)}`).join('')
    ))
  } catch {
    return {}
  }
}

function extractRole(source = {}) {
  const role =
    source.role ||
    source.Role ||
    source.roles ||
    source.Roles ||
    source['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
    source['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role'] ||
    ''
  return Array.isArray(role) ? role[0] || '' : String(role || '')
}

function extractName(source = {}) {
  return source.fullName ||
    source.FullName ||
    source.name ||
    source.Name ||
    source.username ||
    source.Username ||
    source['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ||
    source['http://schemas.microsoft.com/ws/2008/06/identity/claims/name'] ||
    ''
}

function extractEmail(source = {}) {
  return source.email ||
    source.Email ||
    source.emailAddress ||
    source.EmailAddress ||
    source['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
    source['http://schemas.microsoft.com/ws/2008/06/identity/claims/emailaddress'] ||
    ''
}

function extractUserId(source = {}) {
  return source.id ||
    source.Id ||
    source.userId ||
    source.UserId ||
    source.sub ||
    source.nameid ||
    source['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
    source['http://schemas.microsoft.com/ws/2008/06/identity/claims/nameidentifier'] ||
    ''
}

function extractCardNumber(source = {}) {
  return source.cardNumber ||
    source.CardNumber ||
    source.libraryCardNumber ||
    source.LibraryCardNumber ||
    source.readerCard ||
    source.ReaderCard ||
    source.libraryCard?.cardNumber ||
    source.libraryCard?.CardNumber ||
    source.LibraryCard?.cardNumber ||
    source.LibraryCard?.CardNumber ||
    ''
}

function pickToken(session = {}) {
  return session.accessToken ||
    session.token ||
    session.authToken ||
    session.access_token ||
    session.jwt ||
    session.jwtToken ||
    ''
}

function removeAuthQueryFromUrl() {
  const url = new URL(window.location.href)
  for (const key of ['code', 'token', 'accessToken', 'access_token', 'jwt', 'jwtToken', 'cardNumber', 'card', 'readerCard']) {
    url.searchParams.delete(key)
  }
  window.history.replaceState({}, '', `${url.pathname}${url.search}${window.location.hash || ''}`)
}

function saveAuthSession(session = {}) {
  const token = pickToken(session)
  if (token) {
    localStorage.setItem('authToken', token)
    localStorage.setItem('token', token)
    localStorage.setItem('accessToken', token)
  }

  const refreshToken = session.refreshToken || session.refresh_token
  if (refreshToken) localStorage.setItem('refreshToken', refreshToken)

  const payload = token ? parseJwt(token) : {}
  const user = session.user || session.userInfo || session.profile || session
  const userInfo = {
    id: extractUserId(user) || extractUserId(payload) || '',
    fullName: extractName(user) || extractName(payload) || '',
    username: user.username || user.Username || payload.username || extractName(payload) || '',
    email: extractEmail(user) || extractEmail(payload) || '',
    role: extractRole(user) || extractRole(payload) || 'Librarian',
    cardNumber: extractCardNumber(user) || session.cardNumber || session.readerCard || extractCardNumber(payload) || '',
    createdAt: user.createdAt || user.CreatedAt || ''
  }

  if (userInfo.cardNumber) localStorage.setItem('readerCard', userInfo.cardNumber)
  if (userInfo.role) localStorage.setItem('role', userInfo.role)
  if (userInfo.email) localStorage.setItem('email', userInfo.email)
  if (userInfo.username) localStorage.setItem('username', userInfo.username)
  localStorage.setItem('userInfo', JSON.stringify(userInfo))
}

async function redeemAuthHandoffCode(code) {
  const response = await fetch(HANDOFF_REDEEM_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code })
  })
  if (!response.ok) throw new Error(`Redeem auth code failed: ${response.status}`)
  return response.json()
}

async function initAuth() {
  const params = new URLSearchParams(window.location.search)
  const code = params.get('code')
  if (code) {
    try {
      const session = await redeemAuthHandoffCode(code)
      saveAuthSession(session)
    } finally {
      removeAuthQueryFromUrl()
    }
    return
  }


}

async function bootstrap() {
  await initAuth()

  const app = createApp(App)
  app.use(createPinia())
  app.use(router)
  app.use(Antd)
  app.mount('#app')
}

bootstrap()

