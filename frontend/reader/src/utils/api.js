const ID3_API = `${window.location.origin.replace(/:\d+$/, ':5001')}`
const N3_LOGIN_URL = `${window.location.origin.replace(/:\d+$/, '')}/login`
const BASE = `${window.location.origin}/api/circulation`
const CATALOG_API = BASE
const SAME_ORIGIN_BASE = `${window.location.origin}/api/circulation`
const GATEWAY_CATALOG_BOOKS = `${window.location.origin.replace(/:\d+$/, ':5000')}/api/catalog/books`
const HANDOFF_REDEEM_URL = `${window.location.origin.replace(/:\d+$/, ':5000')}/api/identity/Auth/handoff/redeem`
let authBootstrapComplete = false

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

function extractRoleFromPayload(payload = {}) {
  const role =
    payload.role ||
    payload.Role ||
    payload.roles?.[0] ||
    payload.Roles?.[0] ||
    payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
    payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role'] ||
    ''
  return Array.isArray(role) ? role[0] || '' : String(role || '')
}

function extractFirstNonEmpty(...values) {
  for (const value of values) {
    if (value === null || value === undefined) continue
    const text = String(value).trim()
    if (text) return text
  }
  return ''
}

function isGenericDisplayName(value) {
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

function extractDisplayNameFromPayload(payload = {}, fallback = '') {
  const givenName = extractFirstNonEmpty(
    payload.given_name,
    payload.givenName,
    payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'],
    payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/given_name']
  )
  const familyName = extractFirstNonEmpty(
    payload.family_name,
    payload.familyName,
    payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname'],
    payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/family_name']
  )

  const candidates = [
    payload.fullName,
    payload.FullName,
    [givenName, familyName].filter(Boolean).join(' ').trim(),
    payload.name,
    payload.Name,
    payload.unique_name,
    payload.uniqueName,
    payload.preferred_username,
    payload.preferredUsername,
    payload.username,
    payload.Username,
    payload.sub,
    payload.Subject,
    fallback
  ]

  for (const candidate of candidates) {
    const text = extractFirstNonEmpty(candidate)
    if (text && !isGenericDisplayName(text)) return text
  }

  return extractFirstNonEmpty(fallback)
}

function extractCardNumberFromPayload(payload = {}, fallback = '') {
  return extractFirstNonEmpty(
    payload.cardNumber,
    payload.CardNumber,
    payload.readerCardNumber,
    payload.ReaderCardNumber,
    payload.libraryCardNumber,
    payload.LibraryCardNumber,
    payload.libraryCard?.cardNumber,
    payload.libraryCard?.CardNumber,
    payload.user?.cardNumber,
    payload.user?.CardNumber,
    fallback
  )
}

function saveAuthSession(token, cardNumber = '') {
  if (!token) return false
  const payload = parseJwt(token) || {}
  const role = extractRoleFromPayload(payload)
  const username = extractDisplayNameFromPayload(payload, '')
  const normalizedCardNumber = extractCardNumberFromPayload(payload, cardNumber)

  localStorage.setItem('authToken', token)
  localStorage.setItem('token', token)
  if (normalizedCardNumber) localStorage.setItem('readerCard', normalizedCardNumber)
  if (role) localStorage.setItem('role', role)
  const cached = getCachedUserInfo()
  localStorage.setItem('userInfo', JSON.stringify({
    ...cached,
    fullName: extractDisplayNameFromPayload(cached, username || normalizedCardNumber),
    username: cached.username || username || normalizedCardNumber,
    role: cached.role || role,
    cardNumber: cached.cardNumber || normalizedCardNumber || '',
  }))
  return true
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
  localStorage.removeItem('role')
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
  // Only treat 401 as a broken session. Some reader endpoints can legally return 403
  // for forbidden aggregate data, and that should not bounce the user back to login.
  if (authBootstrapComplete && redirectOnAuthError && t && response.status === 401) {
    forceLogin()
  }
  return response
}

async function redeemCode(code) {
  const response = await fetch(HANDOFF_REDEEM_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code })
  })

  if (!response.ok) return false

  const raw = (await response.text()).trim()
  let payload = null

  try {
    payload = raw ? JSON.parse(raw) : null
  } catch {
    payload = raw ? { token: raw } : null
  }

  const token =
    payload?.token ||
    payload?.Token ||
    payload?.accessToken ||
    payload?.AccessToken ||
    payload?.jwt ||
    payload?.Jwt ||
    (typeof payload === 'string' ? payload : '')

  const cardNumber =
    payload?.cardNumber ||
    payload?.CardNumber ||
    payload?.readerCardNumber ||
    payload?.ReaderCardNumber ||
    payload?.user?.cardNumber ||
    payload?.user?.CardNumber ||
    ''

  return saveAuthSession(token, cardNumber)
}

async function authFetchWithFallback(path, opts = {}) {
  const primary = `${BASE}${path}`
  const fallback = `${SAME_ORIGIN_BASE}${path}`
  try {
    const response = await authFetch(primary, opts)
    if (response.ok || (response.status >= 400 && response.status < 500)) return response
  } catch {
    // Try the hosted N2 API if the gateway is temporarily unavailable.
  }
  return authFetch(fallback, opts)
}

async function fetchJsonFromCandidates(paths = []) {
  let lastResponse = null
  for (const path of paths) {
    try {
      const response = await authFetch(path, { redirectOnAuthError: false })
      lastResponse = response
      if (!response.ok) continue
      return await response.json()
    } catch {
      // Try the next candidate.
    }
  }
  if (lastResponse && lastResponse.status === 401 && authBootstrapComplete) {
    forceLogin()
  }
  return null
}

export async function fetchBooks() {
  try {
    const data = await fetchJsonFromCandidates([
      `${window.location.origin}/api/books`,
      GATEWAY_CATALOG_BOOKS,
      `${BASE}/books`
    ])
    return Array.isArray(data) ? data.map(normalizeBook) : []
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

export async function fetchEvents() {
  try {
    const r = await authFetchWithFallback('/events?pageSize=200')
    if (!r.ok) return []
    return await r.json()
  } catch {
    return []
  }
}

export async function fetchFines() {
  try {
    const r = await authFetchWithFallback('/fines')
    if (!r.ok) return []
    return await r.json()
  } catch { return [] }
}

export async function borrowBook(cardNumber, bookId, quantity = 1) {
  const r = await authFetchWithFallback('/borrow', {
    method: 'POST',
    body: JSON.stringify({ cardNumber, bookId: String(bookId), quantity })
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
  return authFetchWithFallback(`/transactions/${transactionId}/return`, { method: 'POST' })
}

export async function payFine(fineId) {
  return authFetchWithFallback(`/fines/${fineId}/pay`, { method: 'POST' })
}

export async function reviewBook(bookId, { cardNumber, userId, rating, comment = '' }) {
  return authFetchWithFallback(`/books/${encodeURIComponent(bookId)}/reviews`, {
    method: 'POST',
    body: JSON.stringify({ cardNumber, userId, rating, comment })
  })
}

export async function fetchBookReviews(bookId = '') {
  const query = bookId ? `?bookId=${encodeURIComponent(bookId)}` : ''
  try {
    const r = await authFetchWithFallback(`/books/reviews${query}`)
    if (!r.ok) return []
    return await r.json()
  } catch {
    return []
  }
}

export async function loadUserProfile() {
  const p = getTokenPayload()
  const c = getCachedUserInfo()
  const fallbackName = extractDisplayNameFromPayload(c, '')
  const fallbackCardNumber = extractCardNumberFromPayload(c, '')
  const u = {
    fullName: extractDisplayNameFromPayload(p, fallbackName || fallbackCardNumber || 'Độc giả'),
    username: extractFirstNonEmpty(
      c.username,
      p?.username,
      p?.Username,
      p?.preferred_username,
      p?.preferredUsername,
      p?.name,
      p?.Name,
      p?.unique_name,
      p?.uniqueName,
      p?.sub,
      p?.Subject,
      fallbackName,
      fallbackCardNumber
    ),
    email: extractFirstNonEmpty(c.email, p?.email, p?.Email),
    role: extractFirstNonEmpty(c.role, p?.role, p?.Role, 'Reader'),
    cardNumber: extractCardNumberFromPayload(p, c.cardNumber || localStorage.getItem('readerCard') || ''),
    createdAt: extractFirstNonEmpty(c.createdAt, p?.createdAt, p?.CreatedAt)
  }
  localStorage.setItem('userInfo', JSON.stringify(u))
  if (u.cardNumber) localStorage.setItem('readerCard', u.cardNumber)

  try {
    if (!getToken()) return u
    const r = await authFetch(`${ID3_API}/api/User/profile`, { redirectOnAuthError: false })
    if (!r.ok) return u
    const d = await r.json()
    const a = {
      fullName: extractDisplayNameFromPayload(d, u.fullName),
      username: extractFirstNonEmpty(d.username, d.Username, d.preferred_username, d.preferredUsername, d.name, d.Name, u.username),
      email: extractFirstNonEmpty(d.email, d.Email, u.email),
      role: extractFirstNonEmpty(d.role, d.Role, u.role),
      cardNumber: extractCardNumberFromPayload(d, u.cardNumber),
      createdAt: extractFirstNonEmpty(d.createdAt, d.CreatedAt, u.createdAt)
    }
    if (!a.fullName) a.fullName = a.username || a.cardNumber || u.fullName
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

export function normalizeEvent(e = {}) {
  return {
    id: e.Id || e.id || '',
    sourceService: e.SourceService || e.sourceService || '',
    eventType: e.EventType || e.eventType || '',
    payloadJson: e.PayloadJson || e.payloadJson || '{}',
    publishedAt: e.PublishedAt || e.publishedAt || ''
  }
}

export async function initAuth() {
  try {
    const p = new URLSearchParams(window.location.search)
    const t = p.get('token')
    const code = p.get('code')
    const c = p.get('cardNumber')

    if (t) {
      saveAuthSession(t, c || '')
      window.history.replaceState({}, '', window.location.pathname + window.location.hash)
      return true
    }

    if (code) {
      const ok = await redeemCode(code)
      window.history.replaceState({}, '', window.location.pathname + window.location.hash)
      return ok
    }

    if (getToken()) return true

    clearAuth()
    window.location.href = N3_LOGIN_URL
    return false
  } finally {
    authBootstrapComplete = true
  }
}

export function logout() {
  clearAuth()
  window.location.href = N3_LOGIN_URL
}
