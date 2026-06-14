const ID3_API = 'http://163.223.210.87:5001'
const N3_LOGIN_URL = 'http://163.223.210.87:80/login'
const BASE = 'http://163.223.210.87:5000/api/circulation'
const CATALOG_API = BASE
const SAME_ORIGIN_BASE = `${window.location.origin}/api/circulation`
const HANDOFF_REDEEM_URL = 'http://163.223.210.87:5000/api/identity/Auth/handoff/redeem'

export { ID3_API, N3_LOGIN_URL, BASE, CATALOG_API }

export function getToken() {
  return localStorage.getItem('authToken') ||
    localStorage.getItem('token') ||
    localStorage.getItem('accessToken') ||
    localStorage.getItem('access_token') ||
    localStorage.getItem('jwt') ||
    localStorage.getItem('jwtToken')
}

export function getReaderCard() {
  const cached = getCachedUserInfo()
  const payload = getTokenPayload() || {}
  const card =
    localStorage.getItem('readerCard') ||
    extractCardNumber(cached) ||
    extractCardNumber(payload) ||
    null
  if (card) localStorage.setItem('readerCard', card)
  return card
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

  let cleanHash = window.location.hash || '#/'
  if (cleanHash.includes('?')) {
    const [hashPath, hashSearch] = cleanHash.split('?')
    const hashParams = new URLSearchParams(hashSearch)
    for (const key of ['code', 'token', 'accessToken', 'access_token', 'jwt', 'jwtToken', 'cardNumber', 'card', 'readerCard']) {
      hashParams.delete(key)
    }
    const query = hashParams.toString()
    cleanHash = query ? `${hashPath}?${query}` : hashPath
  }

  window.history.replaceState({}, '', `${url.pathname}${url.search}${cleanHash}`)
}

export function saveAuthSession(session = {}) {
  const token = pickToken(session)
  if (token) {
    localStorage.setItem('authToken', token)
    localStorage.setItem('token', token)
    localStorage.setItem('accessToken', token)
  }

  const refreshToken = session.refreshToken || session.refresh_token
  if (refreshToken) localStorage.setItem('refreshToken', refreshToken)

  const payload = token ? parseJwt(token) || {} : {}
  const user = session.user || session.userInfo || session.profile || session
  const cached = getCachedUserInfo()
  const nextUser = {
    ...cached,
    id: extractUserId(user) || extractUserId(payload) || cached.id || '',
    fullName: extractName(user) || extractName(payload) || cached.fullName || '',
    username: user.username || user.Username || payload.username || extractName(payload) || cached.username || '',
    email: extractEmail(user) || extractEmail(payload) || cached.email || '',
    role: extractRole(user) || extractRole(payload) || cached.role || 'Reader',
    cardNumber: extractCardNumber(user) || session.cardNumber || session.readerCard || cached.cardNumber || extractCardNumber(payload) || '',
    createdAt: user.createdAt || user.CreatedAt || cached.createdAt || ''
  }

  if (nextUser.cardNumber) localStorage.setItem('readerCard', nextUser.cardNumber)
  if (nextUser.role) localStorage.setItem('role', nextUser.role)
  if (nextUser.email) localStorage.setItem('email', nextUser.email)
  if (nextUser.username) localStorage.setItem('username', nextUser.username)
  localStorage.setItem('userInfo', JSON.stringify(nextUser))

  return nextUser
}

export async function redeemAuthHandoffCode(code) {
  const response = await fetch(HANDOFF_REDEEM_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code })
  })
  if (!response.ok) {
    const text = await response.text().catch(() => '')
    throw new Error(text || `Redeem auth code failed: ${response.status}`)
  }
  return response.json()
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

export function getUserRole() {
  const cached = getCachedUserInfo()
  const payload = getTokenPayload() || {}
  return extractRole(cached) || extractRole(payload) || 'Reader'
}

export function isStaffRole(role = getUserRole()) {
  const normalized = String(role || '').toLowerCase()
  return normalized === 'admin' || normalized === 'librarian'
}

export function forceLogin() {
  clearAuth()
  window.location.href = N3_LOGIN_URL
}

export function clearAuth() {
  localStorage.removeItem('authToken')
  localStorage.removeItem('token')
  localStorage.removeItem('accessToken')
  localStorage.removeItem('access_token')
  localStorage.removeItem('jwt')
  localStorage.removeItem('jwtToken')
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
  if (redirectOnAuthError && response.status === 401 && !await isTokenStillValid(t)) {
    forceLogin()
  }
  return response
}

async function isTokenStillValid(token) {
  if (!token) return false
  try {
    const response = await fetch('http://163.223.210.87:5000/api/identity/Auth/validate', {
      headers: { Authorization: `Bearer ${token}` }
    })
    return response.ok
  } catch {
    return false
  }
}

async function authFetchWithFallback(path, opts = {}, options = {}) {
  const primary = `${BASE}${path}`
  const fallback = `${SAME_ORIGIN_BASE}${path}`
  if (options.preferSameOrigin) {
    try {
      const response = await authFetch(fallback, opts)
      if (response.ok || response.status !== 404) return response
    } catch {
      // Fall back to the gateway if the hosted N2 API cannot be reached.
    }
    return authFetch(primary, opts)
  }
  try {
    const response = await authFetch(primary, opts)
    if (response.ok || (response.status >= 400 && response.status < 500 && response.status !== 404)) return response
  } catch {
    // Try the hosted N2 API if the gateway is temporarily unavailable.
  }
  return authFetch(fallback, opts)
}

export async function fetchBooks() {
  try {
    const r = await authFetchWithFallback('/books')
    if (!r.ok) return []
    const data = await r.json()
    return Array.isArray(data) ? data.map(normalizeBook) : []
  } catch { return [] }
}

export async function fetchCategories() {
  try {
    const r = await authFetchWithFallback('/categories')
    if (!r.ok) return []
    const data = await r.json()
    return Array.isArray(data) ? data : []
  } catch { return [] }
}

export async function fetchTransactions(cardNumber) {
  if (!cardNumber) return []
  try {
    const r = await authFetchWithFallback(`/transactions?cardNumber=${encodeURIComponent(cardNumber)}&pageSize=200`)
    if (!r.ok) return []
    return await r.json()
  } catch { return [] }
}

export async function fetchAllTransactions() {
  try {
    const r = await authFetchWithFallback('/transactions?pageSize=200', { redirectOnAuthError: false })
    if (!r.ok) return []
    return await r.json()
  } catch { return [] }
}

export async function fetchFines() {
  try {
    const r = await authFetchWithFallback('/fines', { redirectOnAuthError: false })
    if (!r.ok) return []
    return await r.json()
  } catch { return [] }
}

export async function borrowBook(cardNumber, bookId, quantity = 1, dueAt = null) {
  const user = getCachedUserInfo()
  const payload = getTokenPayload()
  const dueAtIso = dueAt ? new Date(dueAt).toISOString() : null
  const r = await authFetchWithFallback('/borrow', {
    method: 'POST',
    body: JSON.stringify({
      cardNumber,
      bookId: String(bookId),
      quantity,
      ...(dueAtIso ? { dueAt: dueAtIso } : {}),
      userId: user.id || payload?.sub || payload?.userId || payload?.nameid || user.username || '',
      readerName: user.fullName || payload?.fullName || payload?.name || payload?.username || '',
      readerUsername: user.username || payload?.username || ''
    })
  })
  return r
}

export async function returnBook(cardNumber, bookId) {
  const r = await authFetchWithFallback('/return', {
    method: 'POST',
    body: JSON.stringify({ cardNumber, bookId: String(bookId) })
  })
  return r
}

export async function returnTransaction(transactionId) {
  return authFetchWithFallback(`/transactions/${transactionId}/return`, { method: 'POST' }, { preferSameOrigin: true })
}

export async function payFine(fineId) {
  return authFetchWithFallback(`/fines/${fineId}/pay`, { method: 'POST' })
}

export async function reviewBook(bookId, { transactionId, cardNumber, userId, rating, comment = '' }) {
  const user = getCachedUserInfo()
  const payload = getTokenPayload() || {}
  const username =
    user.username ||
    payload.username ||
    payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ||
    ''
  const fullName = user.fullName || extractName(payload) || username

  return authFetchWithFallback(`/books/${encodeURIComponent(bookId)}/reviews`, {
    method: 'POST',
    body: JSON.stringify({
      transactionId,
      cardNumber,
      userId: userId || user.id || extractUserId(payload),
      username,
      fullName,
      rating,
      comment,
      createdAt: new Date().toISOString()
    })
  }, { preferSameOrigin: true })
}

export async function fetchBookReviews(bookId = '') {
  const query = bookId ? `?bookId=${encodeURIComponent(bookId)}` : ''
  try {
    const r = await authFetchWithFallback(`/books/reviews${query}`, {}, { preferSameOrigin: true })
    if (!r.ok) return []
    return await r.json()
  } catch {
    return []
  }
}

export async function loadUserProfile() {
  const p = getTokenPayload()
  const c = getCachedUserInfo()
  const u = {
    fullName: c.fullName || p?.fullName || p?.name || p?.username || 'Độc giả',
    username: c.username || p?.username || '',
    email: c.email || p?.email || '',
    role: extractRole(c) || extractRole(p || {}) || 'Reader',
    cardNumber: extractCardNumber(c) || localStorage.getItem('readerCard') || extractCardNumber(p || {}) || '',
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
      role: extractRole(d) || u.role,
      cardNumber: extractCardNumber(d) || u.cardNumber,
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
  const theLoai =
    b.theLoai ?? b.TheLoai ??
    b.tenTheLoai ?? b.TenTheLoai ??
    b.category ?? b.Category ??
    b.categoryName ?? b.CategoryName ??
    b.genre ?? b.Genre ??
    b.loaiSach ?? b.LoaiSach ??
    b.nhomSach ?? b.NhomSach ??
    b.catalogCategory ?? b.CatalogCategory ??
    ''
  const maTheLoai =
    b.maTheLoai ?? b.MaTheLoai ??
    b.categoryId ?? b.CategoryId ??
    b.genreId ?? b.GenreId ??
    b.loaiSachId ?? b.LoaiSachId ??
    ''
  const categories =
    b.categories ?? b.Categories ??
    b.genres ?? b.Genres ??
    b.theLoais ?? b.TheLoais ??
    b.categoryList ?? b.CategoryList ??
    []

  return {
    id, tenSach, tacGia, nhaSanXuat, imageUrl, isbn,
    soLuong, soBanDaMuon, soBanConLai, moTa, giaMuon,
    theLoai,
    tenTheLoai: theLoai,
    category: theLoai,
    categoryName: theLoai,
    maTheLoai,
    categories
  }
}

export async function initAuth() {
  const p = new URLSearchParams(window.location.search)
  const hashQuery = window.location.hash.includes('?')
    ? new URLSearchParams(window.location.hash.slice(window.location.hash.indexOf('?') + 1))
    : new URLSearchParams()
  const code = p.get('code') || hashQuery.get('code')
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

export function logout() {
  clearAuth()
  window.location.href = 'http://163.223.210.87:80/'
}

