const ID3_API = 'http://163.223.210.87:5001'
const N3_LOGIN_URL = 'http://163.223.210.87:80/login'
const BASE = 'http://163.223.210.87:5000/api/circulation'
const CATALOG_API = BASE

export { ID3_API, N3_LOGIN_URL, BASE, CATALOG_API }

export function getToken() {
  return localStorage.getItem('authToken') || localStorage.getItem('token')
}

export function getReaderCard() {
  return localStorage.getItem('readerCard') || getCachedUserInfo().cardNumber || null
}

export function getCachedUserInfo() {
  try { return JSON.parse(localStorage.getItem('userInfo') || '{}') }
  catch { return {} }
}

export function parseJwt(token) {
  try {
    const b = token.split('.')[1]
    return JSON.parse(decodeURIComponent(
      atob(b.replace(/-/g, '+').replace(/_/g, '/'))
        .split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join('')
    ))
  } catch { return null }
}

export function getTokenPayload() {
  const t = getToken()
  return t ? parseJwt(t) : null
}

export function forceLogin() {
  clearAuth()
  window.location.href = N3_LOGIN_URL
}

export function clearAuth() {
  localStorage.removeItem('authToken')
  localStorage.removeItem('token')
  localStorage.removeItem('readerCard')
  localStorage.removeItem('userInfo')
}

export async function authFetch(url, opts = {}) {
  const t = getToken()
  const redirectOnAuthError = opts.redirectOnAuthError !== false
  const { redirectOnAuthError: _redirectOnAuthError, ...fetchOptions } = opts
  const headers = {
    'Content-Type': 'application/json',
    ...(t ? { Authorization: `Bearer ${t}` } : {}),
    ...(opts.headers || {})
  }
  const response = await fetch(url, { ...fetchOptions, headers })
  if (redirectOnAuthError && (response.status === 401 || response.status === 403)) {
    forceLogin()
  }
  return response
}

export async function fetchBooks() {
  try {
    const r = await authFetch(`${CATALOG_API}/books`)
    if (!r.ok) return []
    const data = await r.json()
    return Array.isArray(data) ? data.map(normalizeBook) : []
  } catch { return [] }
}

export async function fetchTransactions(cardNumber) {
  if (!cardNumber) return []
  try {
    const r = await authFetch(`${BASE}/transactions?cardNumber=${encodeURIComponent(cardNumber)}`)
    if (!r.ok) return []
    return await r.json()
  } catch { return [] }
}

export async function fetchAllTransactions() {
  try {
    const r = await authFetch(`${BASE}/transactions`)
    if (!r.ok) return []
    return await r.json()
  } catch { return [] }
}

export async function fetchFines() {
  try {
    const r = await authFetch(`${BASE}/fines`)
    if (!r.ok) return []
    return await r.json()
  } catch { return [] }
}

export async function borrowBook(cardNumber, bookId) {
  const r = await authFetch(`${BASE}/borrow`, {
    method: 'POST',
    body: JSON.stringify({ cardNumber, bookId: String(bookId) })
  })
  return r
}

export async function returnBook(cardNumber, bookId) {
  const r = await authFetch(`${BASE}/return`, {
    method: 'POST',
    body: JSON.stringify({ cardNumber, bookId: String(bookId) })
  })
  return r
}

export async function loadUserProfile() {
  const p = getTokenPayload()
  const c = getCachedUserInfo()
  const u = {
    fullName: c.fullName || p?.fullName || p?.name || p?.username || 'Độc giả',
    username: c.username || p?.username || '',
    email: c.email || p?.email || '',
    role: c.role || p?.role || 'Reader',
    cardNumber: c.cardNumber || localStorage.getItem('readerCard') || p?.cardNumber || '',
    createdAt: c.createdAt || p?.createdAt || ''
  }
  localStorage.setItem('userInfo', JSON.stringify(u))
  if (u.cardNumber) localStorage.setItem('readerCard', u.cardNumber)

  try {
    if (!getToken()) return u
    const r = await authFetch(`${ID3_API}/api/User/profile`, { redirectOnAuthError: false })
    if (!r.ok) return u
    const d = await r.json()
    const a = {
      fullName: d.fullName || d.username || u.fullName,
      username: d.username || u.username,
      email: d.email || u.email,
      role: d.role || u.role,
      cardNumber: d.libraryCard?.cardNumber || d.cardNumber || u.cardNumber,
      createdAt: d.createdAt || u.createdAt
    }
    if (a.cardNumber) localStorage.setItem('readerCard', a.cardNumber)
    localStorage.setItem('userInfo', JSON.stringify(a))
    return a
  } catch { return u }
}

export function normalizeBook(b = {}) {
  const id = b.id ?? b.Id ?? b.bookId ?? b.BookId
  const tenSach = b.tenSach ?? b.TenSach ?? b.title ?? b.Title ?? '—'
  const tacGia = b.tacGia ?? b.TacGia ?? b.author ?? b.Author ?? '—'
  const nhaSanXuat = b.nhaSanXuat ?? b.NhaSanXuat ?? b.publisher ?? b.Publisher ?? ''
  const imageUrl = b.imageUrl ?? b.ImageUrl ?? ''
  const isbn = b.isbn ?? b.Isbn ?? b.ISBN ?? ''
  const soLuong = Number(b.soLuong ?? b.SoLuong ?? 0)
  const soBanDaMuon = Number(b.soBanDaMuon ?? b.SoBanDaMuon ?? 0)
  const soBanConLai = Number(b.soBanConLai ?? b.SoBanConLai ?? Math.max(soLuong - soBanDaMuon, 0))
  const moTa = b.moTa ?? b.MoTa ?? b.description ?? b.Description ?? ''
  const giaMuon = Number(b.giaMuon ?? b.GiaMuon ?? b.giaThue ?? b.GiaThue ?? b.price ?? b.Price ?? b.donGia ?? b.DonGia ?? 0)

  return {
    id, tenSach, tacGia, nhaSanXuat, imageUrl, isbn,
    soLuong, soBanDaMuon, soBanConLai, moTa, giaMuon
  }
}

export function initAuth() {
  const p = new URLSearchParams(window.location.search)
  const t = p.get('token')
  const c = p.get('cardNumber')
  if (t) {
    localStorage.setItem('authToken', t)
    localStorage.setItem('token', t)
    if (c) localStorage.setItem('readerCard', c)
    window.history.replaceState({}, '', window.location.pathname)
  }
}

export function logout() {
  clearAuth()
  window.location.href = 'http://163.223.210.87:80/'
}
