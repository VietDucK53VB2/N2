import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authFetch, BASE } from '@/utils/api'

const CIRC_API = BASE

export const useLibrarianStore = defineStore('librarian', () => {
  const transactions = ref([])
  const fines = ref([])
  const loading = ref(false)

  const pendingCount = computed(() =>
    transactions.value.filter(t => t.Status === 'Pending').length
  )

  const overdueCount = computed(() =>
    transactions.value.filter(t => t.Status === 'Overdue').length
  )

  const activeCount = computed(() =>
    transactions.value.filter(t => t.Status !== 'Returned').length
  )

  async function loadTransactions() {
    loading.value = true
    try {
      const r = await authFetch(`${CIRC_API}/transactions`)
      if (r.ok) transactions.value = await r.json()
    } catch (e) { console.error(e) }
    finally { loading.value = false }
  }

  async function loadFines() {
    try {
      const r = await authFetch(`${CIRC_API}/fines`)
      if (r.ok) fines.value = await r.json()
    } catch (e) { console.error(e) }
  }

  async function approveTransaction(id) {
    const r = await authFetch(`${CIRC_API}/transactions/${id}/approve`, { method: 'POST' })
    if (r.ok) await loadTransactions()
    return r
  }

  function promptRejectReason(defaultReason = 'Không đủ điều kiện') {
    const text = window.prompt('Nhập lý do từ chối', defaultReason)
    return text?.trim() || ''
  }

  async function rejectTransaction(id, reason = '') {
    const r = await authFetch(`${CIRC_API}/transactions/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ Reason: reason, reason })
    })
    if (r.ok) await loadTransactions()
    return r
  }

  async function renewTransaction(id, extraDays = 7, reason = '') {
    const r = await authFetch(`${CIRC_API}/transactions/${id}/renew`, {
      method: 'POST',
      body: JSON.stringify({ ExtraDays: extraDays, extraDays, Reason: reason, reason })
    })
    if (r.ok) await loadTransactions()
    return r
  }

  async function rejectRenew(id, reason = '') {
    const r = await authFetch(`${CIRC_API}/transactions/${id}/renew/reject`, {
      method: 'POST',
      body: JSON.stringify({ Reason: reason, reason })
    })
    if (r.ok) await loadTransactions()
    return r
  }

  async function approveReturn(id, payload = {}) {
    const r = await authFetch(`${CIRC_API}/transactions/${id}/return/approve`, {
      method: 'POST',
      body: JSON.stringify({
        Condition: payload.condition || payload.Condition || 'Good',
        condition: payload.condition || payload.Condition || 'Good',
        ConditionNote: payload.conditionNote || payload.ConditionNote || '',
        conditionNote: payload.conditionNote || payload.ConditionNote || ''
      })
    })
    if (r.ok) await loadTransactions()
    return r
  }

  async function rejectReturn(id, reason = '') {
    const r = await authFetch(`${CIRC_API}/transactions/${id}/return/reject`, {
      method: 'POST',
      body: JSON.stringify({ Reason: reason, reason })
    })
    if (r.ok) await loadTransactions()
    return r
  }

  async function returnBook(cardNumber, bookId) {
    const r = await authFetch(`${CIRC_API}/return`, {
      method: 'POST',
      body: JSON.stringify({ cardNumber, bookId })
    })
    if (r.ok) { await loadTransactions(); await loadFines() }
    return r
  }

  async function markFinePaid(fineId) {
    const r = await authFetch(`${CIRC_API}/fines/${fineId}/pay`, { method: 'POST' })
    if (r.ok) await loadFines()
    return r
  }

  async function rejectFinePayment(fineId, reason = '') {
    const r = await authFetch(`${CIRC_API}/fines/${fineId}/reject-payment`, {
      method: 'POST',
      body: JSON.stringify({ Reason: reason, reason })
    })
    if (r.ok) await loadFines()
    return r
  }

  async function loadAll() {
    await Promise.all([loadTransactions(), loadFines()])
  }

  return {
    transactions, fines, loading,
    pendingCount, overdueCount, activeCount,
    loadTransactions, loadFines, loadAll,
    approveTransaction, promptRejectReason, rejectTransaction, renewTransaction, rejectRenew,
    approveReturn, rejectReturn, returnBook, markFinePaid, rejectFinePayment
  }
})
