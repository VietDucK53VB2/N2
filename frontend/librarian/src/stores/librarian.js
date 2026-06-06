import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

const CIRC_API = 'http://163.223.210.87:5000/api/circulation'
const N3_LOGIN_URL = 'http://163.223.210.87:80/login'

function getToken() {
  return localStorage.getItem('authToken') || localStorage.getItem('token')
}

function clearAuth() {
  localStorage.removeItem('authToken')
  localStorage.removeItem('token')
  localStorage.removeItem('readerCard')
  localStorage.removeItem('userInfo')
}

function forceLogin() {
  clearAuth()
  window.location.href = N3_LOGIN_URL
}

function statusOf(transaction = {}) {
  return String(transaction.Status || transaction.status || '').trim()
}

function isPending(transaction) {
  return statusOf(transaction) === 'Pending'
}

function isBorrowed(transaction) {
  return statusOf(transaction) === 'Borrowed'
}

function isOverdue(transaction) {
  return statusOf(transaction) === 'Overdue'
}

function isReturned(transaction) {
  return statusOf(transaction) === 'Returned'
}

function isActiveLoan(transaction) {
  return isBorrowed(transaction) || isOverdue(transaction)
}

function cardNumberOf(transaction = {}) {
  return transaction.CardNumber || transaction.cardNumber || '—'
}

function bookIdOf(transaction = {}) {
  return transaction.BookId || transaction.bookId || ''
}

async function apiFetch(url, opts = {}) {
  const t = getToken()
  const response = await fetch(url, { ...opts, headers: { 'Content-Type': 'application/json', ...(t ? { Authorization: `Bearer ${t}` } : {}), ...(opts.headers || {}) } })
  if (response.status === 401 || response.status === 403) forceLogin()
  return response
}

export const useLibrarianStore = defineStore('librarian', () => {
  const transactions = ref([])
  const fines = ref([])
  const loading = ref(false)

  const pendingTx = computed(() => transactions.value.filter(isPending))
  const borrowedTx = computed(() => transactions.value.filter(isBorrowed))
  const activeTx = computed(() => transactions.value.filter(isActiveLoan))
  const overdueTx = computed(() => transactions.value.filter(isOverdue))
  const returnedTx = computed(() => transactions.value.filter(isReturned))

  const unpaidFines = computed(() => fines.value.filter(f => !(f.IsPaid || f.isPaid)))
  const totalUnpaid = computed(() => unpaidFines.value.reduce((s, f) => s + Number(f.Amount || f.amount || 0), 0))
  const paidFines = computed(() => fines.value.filter(f => f.IsPaid || f.isPaid))

  async function loadTransactions() {
    loading.value = true
    try { const r = await apiFetch(`${CIRC_API}/transactions`); if (r.ok) transactions.value = await r.json() } catch {}
    finally { loading.value = false }
  }
  async function loadFines() {
    try { const r = await apiFetch(`${CIRC_API}/fines`); if (r.ok) fines.value = await r.json() } catch {}
  }
  async function approve(id) { const r = await apiFetch(`${CIRC_API}/transactions/${id}/approve`, { method: 'POST' }); if (r.ok) await loadTransactions(); return r }
  async function reject(id) { const r = await apiFetch(`${CIRC_API}/transactions/${id}/reject`, { method: 'POST' }); if (r.ok) await loadTransactions(); return r }
  async function returnBook(cardNumber, bookId) { const r = await apiFetch(`${CIRC_API}/return`, { method: 'POST', body: JSON.stringify({ cardNumber, bookId }) }); if (r.ok) { await loadTransactions(); await loadFines() } return r }
  async function payFine(id) { const r = await apiFetch(`${CIRC_API}/fines/${id}/pay`, { method: 'POST' }); if (r.ok) await loadFines(); return r }
  async function loadAll() { await Promise.all([loadTransactions(), loadFines()]) }

  return {
    transactions, fines, loading,
    pendingTx, borrowedTx, activeTx, overdueTx, returnedTx,
    unpaidFines, paidFines, totalUnpaid,
    statusOf, isPending, isBorrowed, isOverdue, isReturned, isActiveLoan, cardNumberOf, bookIdOf,
    loadTransactions, loadFines, loadAll, approve, reject, returnBook, payFine
  }
})
