import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

const CIRC_API = `${window.location.origin}/api/circulation`
const CATALOG_API = `${window.location.origin}/api/catalog/books`
const N3_API_ORIGIN = window.location.origin.replace(/:\d+$/, ':5000')
const IDENTITY_REPORT_API = `${N3_API_ORIGIN}/api/identity/Report/dashboard`
const N3_LOGIN_URL = `${window.location.origin}/login`
const HANDOFF_REDEEM_URL = `${N3_API_ORIGIN}/api/identity/Auth/handoff/redeem`

function getToken() {
  return localStorage.getItem('authToken') || localStorage.getItem('token')
}

function parseJwt(token) {
  try {
    const b = token.split('.')[1]
    return JSON.parse(decodeURIComponent(
      atob(b.replace(/-/g, '+').replace(/_/g, '/'))
        .split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join('')
    ))
  } catch {
    return null
  }
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
  const source = payload && typeof payload === 'object' ? payload : {}
  const givenName = extractFirstNonEmpty(
    source.given_name,
    source.givenName,
    source['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'],
    source['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/given_name']
  )
  const familyName = extractFirstNonEmpty(
    source.family_name,
    source.familyName,
    source['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname'],
    source['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/family_name']
  )

  const candidates = [
    source.fullName,
    source.FullName,
    [givenName, familyName].filter(Boolean).join(' ').trim(),
    source.name,
    source.Name,
    source.unique_name,
    source.uniqueName,
    source.preferred_username,
    source.preferredUsername,
    source.username,
    source.Username,
    source.sub,
    source.Subject,
    fallback
  ]

  for (const candidate of candidates) {
    const text = extractFirstNonEmpty(candidate)
    if (text && !isGenericDisplayName(text)) return text
  }

  return extractFirstNonEmpty(fallback)
}

function extractCardNumberFromPayload(payload = {}, fallback = '') {
  const source = payload && typeof payload === 'object' ? payload : {}
  return extractFirstNonEmpty(
    source.cardNumber,
    source.CardNumber,
    source.readerCardNumber,
    source.ReaderCardNumber,
    source.libraryCardNumber,
    source.LibraryCardNumber,
    source.libraryCard?.cardNumber,
    source.libraryCard?.CardNumber,
    source.user?.cardNumber,
    source.user?.CardNumber,
    fallback
  )
}

function getCachedUserInfo() {
  try {
    const parsed = JSON.parse(localStorage.getItem('userInfo') || '{}')
    return parsed && typeof parsed === 'object' ? parsed : {}
  } catch {
    return {}
  }
}

function clearAuth() {
  localStorage.removeItem('authToken')
  localStorage.removeItem('token')
  localStorage.removeItem('readerCard')
  localStorage.removeItem('role')
  localStorage.removeItem('userInfo')
}

function forceLogin() {
  clearAuth()
  window.location.href = N3_LOGIN_URL
}

function storeAuthToken(token, cardNumber = '') {
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

function statusOf(transaction = {}) {
  return String(transaction.Status || transaction.status || '').trim()
}

function dueAtOf(transaction = {}) {
  return (
    transaction.DueAt ||
    transaction.dueAt ||
    transaction.due_at ||
    transaction.returnAt ||
    transaction.ReturnAt ||
    ''
  )
}

function parseTime(value) {
  if (!value) return null
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

function isTimeBasedOverdue(transaction = {}) {
  if (isPending(transaction) || isReturned(transaction) || isReturnPending(transaction) || isRenewPending(transaction)) {
    return false
  }
  const dueAt = parseTime(dueAtOf(transaction))
  return Boolean(dueAt && dueAt.getTime() < Date.now())
}

function isPending(transaction) {
  return statusOf(transaction) === 'Pending'
}

function isBorrowed(transaction) {
  return statusOf(transaction) === 'Borrowed' && !isTimeBasedOverdue(transaction)
}

function isOverdue(transaction) {
  return statusOf(transaction) === 'Overdue' || isTimeBasedOverdue(transaction)
}

function isReturned(transaction) {
  return statusOf(transaction) === 'Returned'
}

function isReturnPending(transaction) {
  return statusOf(transaction) === 'ReturnPending'
}

function isRenewPending(transaction) {
  return statusOf(transaction) === 'RenewPending'
}

function isActiveLoan(transaction) {
  return isBorrowed(transaction) || isOverdue(transaction) || isReturnPending(transaction) || isRenewPending(transaction)
}

function cardNumberOf(transaction = {}) {
  return transaction.CardNumber || transaction.cardNumber || '—'
}

function readerNameOf(record = {}) {
  return (
    record.ReaderName ||
    record.readerName ||
    record.FullName ||
    record.fullName ||
    record.ReaderUsername ||
    record.readerUsername ||
    record.Username ||
    record.username ||
    cardNumberOf(record)
  )
}

function normalizeSearchText(value = '') {
  return String(value || '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim()
    .toLowerCase()
}

function matchesReaderQuery(record = {}, query = '', extraValues = []) {
  const needle = normalizeSearchText(query)
  if (!needle) return true

  const haystack = [
    cardNumberOf(record),
    readerNameOf(record),
    record.FullName,
    record.fullName,
    record.ReaderName,
    record.readerName,
    record.ReaderUsername,
    record.readerUsername,
    record.Username,
    record.username,
    ...extraValues
  ].map(normalizeSearchText).join(' ')

  return haystack.includes(needle)
}

function bookIdOf(transaction = {}) {
  return transaction.BookId || transaction.bookId || ''
}

function normalizeBookId(value = '') {
  return String(value || '').trim()
}

function normalizeBook(book = {}) {
  return {
    id: String(book.id ?? book.Id ?? book.bookId ?? book.BookId ?? ''),
    tenSach: book.tenSach ?? book.TenSach ?? book.title ?? book.Title ?? '',
    tacGia: book.tacGia ?? book.TacGia ?? book.author ?? book.Author ?? '',
    imageUrl: book.imageUrl ?? book.ImageUrl ?? '',
    theLoai: book.theLoai ?? book.TheLoai ?? '',
    trangThai: book.trangThai ?? book.TrangThai ?? ''
  }
}

async function apiFetch(url, opts = {}) {
  const t = getToken()
  const response = await fetch(url, { ...opts, headers: { 'Content-Type': 'application/json', ...(t ? { Authorization: `Bearer ${t}` } : {}), ...(opts.headers || {}) } })
  return response
}

function isEmbedMode() {
  try {
    const search = new URLSearchParams(window.location.search)
    if (search.get('embed') === '1' || search.get('embed') === 'true') return true

    const pathname = window.location.pathname || ''
    if (pathname.startsWith('/ui/librarian/embed/')) return true

    const hash = window.location.hash || ''
    const idx = hash.indexOf('?')
    if (idx === -1) return false

    const hashParams = new URLSearchParams(hash.slice(idx + 1))
    return hashParams.get('embed') === '1' || hashParams.get('embed') === 'true'
  } catch {
    return false
  }
}

function isEmbedRevenueRoute() {
  const pathname = window.location.pathname || ''
  if (pathname.startsWith('/ui/librarian/embed/revenue')) return true

  const hash = window.location.hash || ''
  return hash.includes('/finance/revenue')
}

function isEmbedLoansRoute() {
  const pathname = window.location.pathname || ''
  if (pathname.startsWith('/ui/librarian/embed/loans')) return true

  const hash = window.location.hash || ''
  return hash.includes('/loans')
}

function isEmbedFinesRoute() {
  const pathname = window.location.pathname || ''
  if (pathname.startsWith('/ui/librarian/embed/fines')) return true

  const hash = window.location.hash || ''
  return hash.includes('/fines')
}

function isEmbedPricesRoute() {
  const pathname = window.location.pathname || ''
  if (pathname.startsWith('/ui/librarian/embed/prices')) return true

  const hash = window.location.hash || ''
  return hash.includes('/finance/prices')
}

function isEmbedReviewsRoute() {
  const pathname = window.location.pathname || ''
  if (pathname.startsWith('/ui/librarian/embed/reviews')) return true

  const hash = window.location.hash || ''
  return hash.includes('/reviews')
}

function isPublicEmbedRoute() {
  return isEmbedRevenueRoute() || isEmbedLoansRoute() || isEmbedFinesRoute() || isEmbedPricesRoute() || isEmbedReviewsRoute()
}

function hasAuthToken() {
  return Boolean(getToken())
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

  return storeAuthToken(token, cardNumber)
}

export async function initAuth() {
  const params = new URLSearchParams(window.location.search)
  const token = params.get('token')
  const code = params.get('code')
  const cardNumber = params.get('cardNumber')

  if (token) {
    storeAuthToken(token, cardNumber || '')
    window.history.replaceState({}, '', window.location.pathname + window.location.hash)
    return true
  }

  if (code) {
    const ok = await redeemCode(code)
    window.history.replaceState({}, '', window.location.pathname + window.location.hash)
    return ok
  }

  if (isEmbedMode() && isPublicEmbedRoute()) {
    return true
  }

  return Boolean(getToken())
}

function isFinePaid(fine = {}) {
  return Boolean(
    fine.IsPaid ||
    fine.isPaid ||
    fine.PaymentStatus === 'Paid' ||
    fine.paymentStatus === 'Paid' ||
    fine.PaidAt ||
    fine.paidAt
  )
}

function isFinePaymentPending(fine = {}) {
  if (isFinePaid(fine)) return false
  return Boolean(
    fine.IsPaymentPending ||
    fine.isPaymentPending ||
    fine.PaymentRequestedAt ||
    fine.paymentRequestedAt ||
    fine.PaymentStatus === 'PendingApproval' ||
    fine.paymentStatus === 'PendingApproval'
  )
}

export const useLibrarianStore = defineStore('librarian', () => {
  const books = ref([])
  const transactions = ref([])
  const fines = ref([])
  const dashboardStats = ref(null)
  const identityStats = ref(null)
  const revenueSummary = ref(null)
  const priceSettings = ref(null)
  const loading = ref(false)

  const bookMap = computed(() => {
    const map = new Map()
    for (const book of books.value) {
      const normalized = normalizeBook(book)
      if (!normalized.id) continue
      map.set(normalized.id, normalized)
    }
    return map
  })

  const pendingTx = computed(() => transactions.value.filter(isPending))
  const borrowedTx = computed(() => transactions.value.filter(isBorrowed))
  const activeTx = computed(() => transactions.value.filter(isActiveLoan))
  const overdueTx = computed(() => transactions.value.filter(isOverdue))
  const returnPendingTx = computed(() => transactions.value.filter(isReturnPending))
  const returnedTx = computed(() => transactions.value.filter(isReturned))

  const unpaidFines = computed(() => fines.value.filter(f => !isFinePaid(f)))
  const totalUnpaid = computed(() => unpaidFines.value.reduce((s, f) => s + Number(f.Amount || f.amount || 0), 0))
  const paidFines = computed(() => fines.value.filter(isFinePaid))
  const totalRevenue = computed(() => Number(revenueSummary.value?.totalRevenue || 0))
  const totalBorrowRevenue = computed(() => Number(revenueSummary.value?.totalBorrowRevenue || 0))
  const totalFineRevenue = computed(() => Number(revenueSummary.value?.totalFineRevenue || 0))
  const pendingFineAmount = computed(() => Number(revenueSummary.value?.pendingFineAmount || 0))
  const unpaidFineAmount = computed(() => Number(revenueSummary.value?.unpaidFineAmount || 0))
  const borrowRevenueCount = computed(() => Number(revenueSummary.value?.borrowRevenueCount || 0))
  const fineRevenueCount = computed(() => Number(revenueSummary.value?.fineRevenueCount || 0))
  const recentRevenue = computed(() => Array.isArray(revenueSummary.value?.recentRevenue) ? revenueSummary.value.recentRevenue : [])

  async function loadBooks() {
    try {
      const r = await apiFetch(CATALOG_API)
      if (!r.ok) {
        books.value = []
        return []
      }
      const data = await r.json()
      books.value = Array.isArray(data) ? data.map(normalizeBook) : []
      return books.value
    } catch {
      books.value = []
      return []
    }
  }
  async function loadFines() {
    try {
      const basePath = isEmbedMode() ? `${CIRC_API}/fines/embed` : `${CIRC_API}/fines`
      const r = await apiFetch(basePath)
      if (r.ok) fines.value = await r.json()
    } catch {}
  }
  async function loadPriceSettings() {
    try {
      const basePath = isEmbedMode() ? `${CIRC_API}/settings/prices/embed` : `${CIRC_API}/settings/prices`
      const r = await apiFetch(basePath)
      if (r.ok) priceSettings.value = await r.json()
      return priceSettings.value
    } catch {
      return priceSettings.value
    }
  }
  async function savePriceSettings(payload = {}) {
    const r = await apiFetch(`${CIRC_API}/settings/prices`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    })
    if (r.ok) {
      priceSettings.value = await r.json()
    }
    return r
  }
  function serializeDate(value) {
    if (!value) return ''
    if (typeof value === 'string') return value
    if (value instanceof Date) return value.toISOString().slice(0, 10)
    if (typeof value.format === 'function') return value.format('YYYY-MM-DD')
    return String(value)
  }

  async function loadRevenueSummary(params = {}) {
    try {
      const search = new URLSearchParams()
      if (params.period) search.set('period', String(params.period))
      const date = serializeDate(params.date)
      const from = serializeDate(params.from)
      const to = serializeDate(params.to)
      if (date) search.set('date', date)
      if (from) search.set('from', from)
      if (to) search.set('to', to)
      if (params.take) search.set('take', String(params.take))

      const basePath = isEmbedMode() ? `${CIRC_API}/revenue/embed` : `${CIRC_API}/revenue`
      const url = search.toString() ? `${basePath}?${search.toString()}` : basePath
      const r = await apiFetch(url)
      if (r.ok) revenueSummary.value = await r.json()
    } catch {}
  }
  async function loadDashboardStats() {
    try {
      const r = await apiFetch(`${CIRC_API}/reports/dashboard`)
      if (r.ok) dashboardStats.value = await r.json()
      return dashboardStats.value
    } catch {
      return dashboardStats.value
    }
  }
  async function loadIdentityStats() {
    try {
      const r = await apiFetch(IDENTITY_REPORT_API)
      if (r.ok) identityStats.value = await r.json()
      return identityStats.value
    } catch {
      return identityStats.value
    }
  }
  async function loadTransactions() {
    loading.value = true
    try {
      const basePath = isEmbedMode() ? `${CIRC_API}/transactions/embed` : `${CIRC_API}/transactions`
      const r = await apiFetch(basePath)
      if (r.ok) transactions.value = await r.json()
    } catch {}
    finally { loading.value = false }
  }
  async function approve(id) {
    const r = await apiFetch(`${CIRC_API}/transactions/${id}/approve`, { method: 'POST' })
    if (r.ok) await loadTransactions()
    return r
  }
  async function reject(id, reason = '') {
    const r = await apiFetch(`${CIRC_API}/transactions/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason })
    })
    if (r.ok) await loadTransactions()
    return r
  }
  async function requestReturn(cardNumber, bookId) { const r = await apiFetch(`${CIRC_API}/return`, { method: 'POST', body: JSON.stringify({ cardNumber, bookId }) }); if (r.ok) await loadTransactions(); return r }
  async function approveReturn(id, payload = {}) {
    const r = await apiFetch(`${CIRC_API}/transactions/${id}/return/approve`, {
      method: 'POST',
      body: JSON.stringify(payload)
    })
    if (r.ok) { await loadTransactions(); await loadFines() }
    return r
  }
  async function renew(id, payload = {}) {
    const r = await apiFetch(`${CIRC_API}/transactions/${id}/renew`, {
      method: 'POST',
      body: JSON.stringify(payload)
    })
    if (r.ok) await loadTransactions()
    return r
  }
  async function rejectRenew(id, reason = '') {
    const r = await apiFetch(`${CIRC_API}/transactions/${id}/renew/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason })
    })
    if (r.ok) await loadTransactions()
    return r
  }
  async function rejectReturn(id, reason = '') {
    const r = await apiFetch(`${CIRC_API}/transactions/${id}/return/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason })
    })
    if (r.ok) await loadTransactions()
    return r
  }
  async function payFine(id) { const r = await apiFetch(`${CIRC_API}/fines/${id}/pay`, { method: 'POST' }); if (r.ok) await loadFines(); return r }
  async function loadAll() { await Promise.all([loadBooks(), loadTransactions(), loadFines(), loadRevenueSummary(), loadPriceSettings(), loadDashboardStats(), loadIdentityStats()]) }

  function bookTitleOf(record = {}) {
    const bookId = normalizeBookId(bookIdOf(record))
    const match = bookMap.value.get(bookId) || [...bookMap.value.values()].find(item => normalizeBookId(item.id) === bookId)
    return (
      record.TenSach ||
      record.tenSach ||
      record.Title ||
      record.title ||
      record.BookTitle ||
      record.bookTitle ||
      record.BookName ||
      record.bookName ||
      record.book?.TenSach ||
      record.book?.tenSach ||
      record.book?.Title ||
      record.book?.title ||
      record.catalogBook?.TenSach ||
      record.catalogBook?.tenSach ||
      match?.tenSach ||
      match?.TenSach ||
      match?.title ||
      match?.Title ||
      (bookId ? `Book #${bookId}` : '—')
    )
  }

  return {
    books, transactions, fines, dashboardStats, identityStats, revenueSummary, priceSettings, loading,
    embedMode: isEmbedMode(),
    hasAuthToken,
    pendingTx, borrowedTx, activeTx, overdueTx, returnPendingTx, returnedTx,
    unpaidFines, paidFines, totalUnpaid, totalRevenue, totalBorrowRevenue, totalFineRevenue, pendingFineAmount, unpaidFineAmount, borrowRevenueCount, fineRevenueCount, recentRevenue,
    statusOf, isPending, isBorrowed, isOverdue, isReturned, isReturnPending, isActiveLoan, cardNumberOf, readerNameOf, matchesReaderQuery, bookIdOf, bookTitleOf,
    loadBooks, loadTransactions, loadFines, loadDashboardStats, loadIdentityStats, loadRevenueSummary, loadPriceSettings, savePriceSettings, loadAll, approve, reject, requestReturn, approveReturn, renew, rejectRenew, rejectReturn, payFine
  }
})
