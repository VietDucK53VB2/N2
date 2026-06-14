import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

const CIRC_API = 'http://163.223.210.87:5000/api/circulation'
const N3_LOGIN_URL = 'http://163.223.210.87:80/login'

function getToken() {
  return localStorage.getItem('authToken') || localStorage.getItem('token') || localStorage.getItem('accessToken')
}

function clearAuth() {
  localStorage.removeItem('authToken')
  localStorage.removeItem('token')
  localStorage.removeItem('accessToken')
  localStorage.removeItem('readerCard')
  localStorage.removeItem('userInfo')
}

function forceLogin() {
  clearAuth()
  window.location.href = N3_LOGIN_URL
}

async function apiFetch(url, opts = {}) {
  const token = getToken()
  const redirectOnAuthError = opts.redirectOnAuthError !== false
  const { redirectOnAuthError: _redirectOnAuthError, ...fetchOptions } = opts
  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(opts.headers || {})
  }
  const response = await fetch(url, { ...fetchOptions, headers })
  if (redirectOnAuthError && (response.status === 401 || response.status === 403)) forceLogin()
  return response
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

function isReturnPending(transaction) {
  return statusOf(transaction) === 'ReturnPending'
}

function isActiveLoan(transaction) {
  return isBorrowed(transaction) || isOverdue(transaction)
}

function cardNumberOf(transaction = {}) {
  return transaction.CardNumber || transaction.cardNumber || ''
}

function bookIdOf(transaction = {}) {
  return String(transaction.BookId || transaction.bookId || '')
}

function normalizeBook(book = {}) {
  const id = book.id ?? book.Id ?? book.bookId ?? book.BookId
  return {
    id: String(id || ''),
    title: book.tenSach ?? book.TenSach ?? book.title ?? book.Title ?? '',
    author: book.tacGia ?? book.TacGia ?? book.author ?? book.Author ?? '',
    isbn: book.isbn ?? book.Isbn ?? book.ISBN ?? '',
    imageUrl: book.imageUrl ?? book.ImageUrl ?? ''
  }
}

function normalizeReviewGroup(group = {}) {
  const bookId = String(group.bookId || group.BookId || '')
  const reviews = group.reviews || group.Reviews || []
  return {
    bookId,
    averageRating: Number(group.averageRating ?? group.AverageRating ?? 0),
    reviewCount: Number(group.reviewCount ?? group.ReviewCount ?? reviews.length ?? 0),
    reviews: reviews.map(review => ({
      reviewId: review.reviewId || review.ReviewId || '',
      userId: review.userId || review.UserId || '',
      cardNumber: review.cardNumber || review.CardNumber || '',
      rating: Number(review.rating ?? review.Rating ?? 0),
      comment: review.comment ?? review.Comment ?? '',
      createdAt: review.createdAt || review.CreatedAt || ''
    }))
  }
}

export const useLibrarianStore = defineStore('librarian', () => {
  const transactions = ref([])
  const fines = ref([])
  const books = ref([])
  const reviews = ref([])
  const borrowPolicy = ref({ monthlyBorrowLimit: 5 })
  const loading = ref(false)

  const pendingTx = computed(() => transactions.value.filter(isPending))
  const borrowedTx = computed(() => transactions.value.filter(isBorrowed))
  const activeTx = computed(() => transactions.value.filter(isActiveLoan))
  const overdueTx = computed(() => transactions.value.filter(isOverdue))
  const returnPendingTx = computed(() => transactions.value.filter(isReturnPending))
  const returnedTx = computed(() => transactions.value.filter(isReturned))

  const unpaidFines = computed(() => fines.value.filter(f => !(f.IsPaid || f.isPaid)))
  const totalUnpaid = computed(() => unpaidFines.value.reduce((sum, fine) => sum + Number(fine.Amount || fine.amount || 0), 0))
  const paidFines = computed(() => fines.value.filter(f => f.IsPaid || f.isPaid))

  const booksById = computed(() => {
    const map = new Map()
    for (const book of books.value) {
      if (book.id) map.set(String(book.id), book)
    }
    return map
  })

  const reviewRows = computed(() => reviews.value.flatMap(group => {
    const book = booksById.value.get(String(group.bookId))
    return group.reviews.map(review => ({
      ...review,
      bookId: group.bookId,
      bookTitle: book?.title || `Sách #${group.bookId || '—'}`,
      bookAuthor: book?.author || '',
      averageRating: group.averageRating,
      reviewCount: group.reviewCount,
      readerName: readerNameByCard(review.cardNumber)
    }))
  }))

  const totalReviewCount = computed(() => reviewRows.value.length)
  const averageReviewRating = computed(() => {
    if (!reviewRows.value.length) return 0
    return Math.round((reviewRows.value.reduce((sum, review) => sum + review.rating, 0) / reviewRows.value.length) * 10) / 10
  })

  async function loadBooks() {
    try {
      const response = await apiFetch(`${CIRC_API}/books`)
      if (!response.ok) return
      const data = await response.json()
      books.value = Array.isArray(data) ? data.map(normalizeBook) : []
    } catch {
      books.value = []
    }
  }

  async function enrichTransactions(items) {
    if (!books.value.length) await loadBooks()

    return items.map(item => ({
      ...item,
      ReaderName: readerNameOf(item),
      ReaderUsername: readerUsernameOf(item),
      TenSach: bookTitleOf(item),
      TacGia: bookAuthorOf(item)
    }))
  }

  async function loadTransactions() {
    loading.value = true
    try {
      const response = await apiFetch(`${CIRC_API}/transactions?pageSize=200`)
      if (!response.ok) return
      const data = await response.json()
      transactions.value = await enrichTransactions(Array.isArray(data) ? data : [])
    } catch {
      transactions.value = []
    } finally {
      loading.value = false
    }
  }

  async function loadFines() {
    try {
      const response = await apiFetch(`${CIRC_API}/fines`)
      if (response.ok) fines.value = await response.json()
    } catch {
      fines.value = []
    }
  }

  async function loadReviews() {
    try {
      if (!books.value.length) await loadBooks()
      const response = await apiFetch(`${CIRC_API}/books/reviews`)
      if (!response.ok) return
      const data = await response.json()
      reviews.value = Array.isArray(data) ? data.map(normalizeReviewGroup) : []
    } catch {
      reviews.value = []
    }
  }

  async function loadBorrowPolicy() {
    try {
      const response = await apiFetch(`${CIRC_API}/settings/borrow-policy`, { redirectOnAuthError: false })
      if (!response.ok) return
      const data = await response.json()
      borrowPolicy.value = {
        monthlyBorrowLimit: Number(data.monthlyBorrowLimit ?? data.MonthlyBorrowLimit ?? 5)
      }
    } catch {
      borrowPolicy.value = { monthlyBorrowLimit: 5 }
    }
  }

  async function saveBorrowPolicy(monthlyBorrowLimit) {
    const response = await apiFetch(`${CIRC_API}/settings/borrow-policy`, {
      method: 'PUT',
      body: JSON.stringify({ monthlyBorrowLimit: Number(monthlyBorrowLimit) || 5 })
    })
    if (response.ok) {
      const data = await response.json().catch(() => null)
      borrowPolicy.value = {
        monthlyBorrowLimit: Number(data?.monthlyBorrowLimit ?? data?.MonthlyBorrowLimit ?? monthlyBorrowLimit)
      }
    }
    return response
  }

  async function approve(id) {
    const response = await apiFetch(`${CIRC_API}/transactions/${id}/approve`, { method: 'POST' })
    if (response.ok) await loadTransactions()
    return response
  }

  async function reject(id) {
    const response = await apiFetch(`${CIRC_API}/transactions/${id}/reject`, { method: 'POST' })
    if (response.ok) await loadTransactions()
    return response
  }

  async function requestReturn(cardNumber, bookId) {
    const response = await apiFetch(`${CIRC_API}/return`, {
      method: 'POST',
      body: JSON.stringify({ cardNumber, bookId })
    })
    if (response.ok) await loadTransactions()
    return response
  }

  async function approveReturn(id, payload = {}) {
    const response = await apiFetch(`${CIRC_API}/transactions/${id}/return/approve`, {
      method: 'POST',
      body: JSON.stringify(payload)
    })
    if (response.ok) {
      await loadTransactions()
      await loadFines()
    }
    return response
  }

  async function rejectReturn(id) {
    const response = await apiFetch(`${CIRC_API}/transactions/${id}/return/reject`, { method: 'POST' })
    if (response.ok) await loadTransactions()
    return response
  }

  async function payFine(id) {
    const response = await apiFetch(`${CIRC_API}/fines/${id}/pay`, { method: 'POST' })
    if (response.ok) await loadFines()
    return response
  }

  async function loadAll() {
    loading.value = true
    try {
      await loadBooks()
      await Promise.all([loadTransactions(), loadFines(), loadReviews(), loadBorrowPolicy()])
    } finally {
      loading.value = false
    }
  }

  function bookTitleOf(record = {}) {
    const direct = record.TenSach || record.tenSach || record.Title || record.title || ''
    if (direct && direct !== '—') return direct
    const book = booksById.value.get(bookIdOf(record))
    return book?.title || `Sách #${bookIdOf(record) || '—'}`
  }

  function bookAuthorOf(record = {}) {
    const direct = record.TacGia || record.tacGia || record.Author || record.author || ''
    if (direct && direct !== '—') return direct
    return booksById.value.get(bookIdOf(record))?.author || ''
  }

  function readerNameOf(record = {}) {
    const card = cardNumberOf(record)
    const direct = record.ReaderName || record.readerName || record.FullName || record.fullName || ''
    if (direct && direct !== '—' && direct !== card) return direct
    return card || '—'
  }

  function readerUsernameOf(record = {}) {
    const direct = record.ReaderUsername || record.readerUsername || record.Username || record.username || ''
    return direct && direct !== '—' ? direct : ''
  }

  function readerNameByCard(cardNumber = '') {
    if (!cardNumber) return '—'
    const found = transactions.value.find(item => cardNumberOf(item) === cardNumber)
    return found ? readerNameOf(found) : cardNumber
  }

  return {
    transactions, fines, books, reviews, reviewRows, borrowPolicy, loading,
    pendingTx, borrowedTx, activeTx, overdueTx, returnPendingTx, returnedTx,
    unpaidFines, paidFines, totalUnpaid,
    totalReviewCount, averageReviewRating,
    statusOf, isPending, isBorrowed, isOverdue, isReturned, isReturnPending, isActiveLoan,
    cardNumberOf, bookIdOf, bookTitleOf, bookAuthorOf, readerNameOf, readerUsernameOf, readerNameByCard,
    loadBooks, loadTransactions, loadFines, loadReviews, loadBorrowPolicy, loadAll,
    saveBorrowPolicy,
    approve, reject, requestReturn, approveReturn, rejectReturn, payFine
  }
})
