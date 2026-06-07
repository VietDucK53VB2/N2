import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import {
  fetchBooks, fetchTransactions, fetchAllTransactions,
  fetchFines, getReaderCard, getCachedUserInfo,
  loadUserProfile as loadProfile
} from '@/utils/api'

export const useAppStore = defineStore('app', () => {
  const books = ref([])
  const myTransactions = ref([])
  const allTransactions = ref([])
  const fines = ref([])
  const userInfo = ref(getCachedUserInfo())
  const loading = ref(false)

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

  function isReturnPending(transaction) {
    return statusOf(transaction) === 'ReturnPending'
  }

  function isActiveLoan(transaction) {
    return isBorrowed(transaction) || isOverdue(transaction) || isReturnPending(transaction)
  }

  function isFinePaid(fine = {}) {
    return Boolean(fine.IsPaid || fine.isPaid || fine.PaidAt || fine.paidAt)
  }

  function cardNumberOf(record = {}) {
    return record.CardNumber || record.cardNumber || ''
  }

  function bookIdOf(record = {}) {
    return record.BookId || record.bookId || ''
  }

  const activeTransactions = computed(() =>
    myTransactions.value.filter(isActiveLoan)
  )

  const overdueTransactions = computed(() =>
    activeTransactions.value.filter(isOverdue)
  )

  const pendingTransactions = computed(() =>
    myTransactions.value.filter(isPending)
  )

  const returnedTransactions = computed(() =>
    myTransactions.value.filter(isReturned)
  )

  const myFines = computed(() => {
    const card = getReaderCard()
    return fines.value.filter(f => cardNumberOf(f) === card)
  })

  const myUnpaidFines = computed(() =>
    myFines.value.filter(f => !isFinePaid(f))
  )

  const totalUnpaidFines = computed(() =>
    myUnpaidFines.value.reduce((s, f) => s + Number(f.Amount || f.amount || 0), 0)
  )

  async function loadBooks() {
    books.value = await fetchBooks()
  }

  async function loadMyTransactions() {
    const card = getReaderCard()
    if (!card) { myTransactions.value = []; return }
    const txs = await fetchTransactions(card)
    // Enrich with book info
    if (books.value.length) {
      const bMap = new Map(books.value.map(b => [String(b.id), b]))
      myTransactions.value = txs.map(tx => {
        const bk = bMap.get(String(bookIdOf(tx)))
        if (!bk) return tx
        return {
          ...tx,
          TenSach: tx.TenSach || tx.tenSach || tx.Title || tx.title || bk.tenSach,
          TacGia: tx.TacGia || tx.tacGia || bk.tacGia,
          ImageUrl: tx.ImageUrl || tx.imageUrl || bk.imageUrl,
          Isbn: tx.Isbn || tx.isbn || bk.isbn
        }
      })
    } else {
      myTransactions.value = txs
    }
  }

  async function loadAllTransactions() {
    allTransactions.value = await fetchAllTransactions()
  }

  async function loadFines() {
    fines.value = await fetchFines()
  }

  async function loadUserInfo() {
    const info = await loadProfile()
    userInfo.value = info
    return info
  }

  async function loadAll() {
    loading.value = true
    try {
      await loadBooks()
      await Promise.all([
        loadMyTransactions(),
        loadAllTransactions(),
        loadFines()
      ])
    } finally {
      loading.value = false
    }
  }

  return {
    books, myTransactions, allTransactions, fines, userInfo, loading,
    activeTransactions, overdueTransactions, pendingTransactions, returnedTransactions,
    myFines, myUnpaidFines, totalUnpaidFines,
    statusOf, isPending, isBorrowed, isOverdue, isReturned, isReturnPending, isActiveLoan, isFinePaid, cardNumberOf, bookIdOf,
    loadBooks, loadMyTransactions, loadAllTransactions, loadFines,
    loadUserInfo, loadAll
  }
})
