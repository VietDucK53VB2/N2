import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

const CIRC_API = `${window.location.origin}/api/circulation`
const CATALOG_API = `${window.location.origin}/api/catalog/books`
const N3_LOGIN_URL = `${window.location.origin}/login`
const HANDOFF_REDEEM_URL = `${window.location.origin.replace(/:\d+$/, ':5000')}/api/identity/Auth/handoff/redeem`

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

function bookIdOf(transaction = {}) {
  return transaction.BookId || transaction.bookId || ''
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
  const revenueSummary = ref(null)
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

  async function loadTransactions() {
    loading.value = true
    try { const r = await apiFetch(`${CIRC_API}/transactions`); if (r.ok) transactions.value = await r.json() } catch {}
    finally { loading.value = false }
  }
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
    try { const r = await apiFetch(`${CIRC_API}/fines`); if (r.ok) fines.value = await r.json() } catch {}
  }
  async function loadRevenueSummary() {
    try {
      const r = await apiFetch(`${CIRC_API}/revenue`)
      if (r.ok) revenueSummary.value = await r.json()
    } catch {}
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
  async function loadAll() { await Promise.all([loadBooks(), loadTransactions(), loadFines(), loadRevenueSummary()]) }

  function bookTitleOf(record = {}) {
    const bookId = String(bookIdOf(record))
    const match = bookMap.value.get(bookId)
    return (
      record.TenSach ||
      record.tenSach ||
      record.Title ||
      record.title ||
      record.BookTitle ||
      record.bookTitle ||
      record.BookName ||
      record.bookName ||
      match?.tenSach ||
      (bookId ? `Book #${bookId}` : '—')
    )
  }

  return {
    books, transactions, fines, revenueSummary, loading,
    pendingTx, borrowedTx, activeTx, overdueTx, returnPendingTx, returnedTx,
    unpaidFines, paidFines, totalUnpaid, totalRevenue, totalBorrowRevenue, totalFineRevenue, pendingFineAmount, unpaidFineAmount, borrowRevenueCount, fineRevenueCount, recentRevenue,
    statusOf, isPending, isBorrowed, isOverdue, isReturned, isReturnPending, isActiveLoan, cardNumberOf, bookIdOf, bookTitleOf,
    loadBooks, loadTransactions, loadFines, loadRevenueSummary, loadAll, approve, reject, requestReturn, approveReturn, renew, rejectRenew, rejectReturn, payFine
  }
})
