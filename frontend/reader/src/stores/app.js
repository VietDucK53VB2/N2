import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import {
  fetchBooks, fetchTransactions, fetchAllTransactions,
  fetchEvents, fetchFines, getReaderCard, getCachedUserInfo,
  loadUserProfile as loadProfile, normalizeEvent
} from '@/utils/api'

export const useAppStore = defineStore('app', () => {
  const fallbackBooks = [
    {
      id: 90001,
      tenSach: 'Dòng Chảy Dữ Liệu',
      tacGia: 'Nguyễn Minh Khoa',
      imageUrl: '',
      soLuong: 12,
      soBanConLai: 7
    },
    {
      id: 90002,
      tenSach: 'Bí Mật Của Giao Diện',
      tacGia: 'Trần Anh Duy',
      imageUrl: '',
      soLuong: 8,
      soBanConLai: 4
    }
  ]

  const books = ref([])
  const cartItems = ref(JSON.parse(localStorage.getItem('readerCartItems') || '[]'))
  const myTransactions = ref([])
  const allTransactions = ref([])
  const events = ref([])
  const fines = ref([])
  const userInfo = ref(getCachedUserInfo())
  const favorites = ref(loadFavoritesForUser(userInfo.value))
  const loading = ref(false)

  function normalizeIdentity(value = '') {
    return String(value || '').trim().toLowerCase()
  }

  function currentUserKey(info = userInfo.value) {
    return normalizeIdentity(
      info?.username ||
      info?.Username ||
      info?.cardNumber ||
      info?.CardNumber ||
      localStorage.getItem('readerCard') ||
      getCachedUserInfo().username ||
      getCachedUserInfo().cardNumber ||
      'anonymous'
    ) || 'anonymous'
  }

  function favoritesStorage() {
    try {
      return JSON.parse(localStorage.getItem('readerFavoritesByUser') || '{}')
    } catch {
      return {}
    }
  }

  function loadFavoritesForUser(info = {}) {
    const map = favoritesStorage()
    const key = currentUserKey(info)
    const list = map[key]
    return Array.isArray(list) ? list : []
  }

  function persistFavorites(list = favorites.value, info = userInfo.value) {
    const map = favoritesStorage()
    const key = currentUserKey(info)
    map[key] = Array.isArray(list) ? list : []
    localStorage.setItem('readerFavoritesByUser', JSON.stringify(map))
  }

  function normalizeFavoriteBook(book = {}) {
    return {
      id: String(book.id ?? book.Id ?? book.bookId ?? book.BookId ?? ''),
      tenSach: book.tenSach ?? book.TenSach ?? book.title ?? book.Title ?? '—',
      tacGia: book.tacGia ?? book.TacGia ?? book.author ?? book.Author ?? '—',
      imageUrl: book.imageUrl ?? book.ImageUrl ?? '',
      soBanConLai: Number(book.soBanConLai ?? book.SoBanConLai ?? 0),
      theLoai: book.theLoai ?? book.TheLoai ?? ''
    }
  }

  function favoriteIds() {
    return new Set((favorites.value || []).map(item => String(item.id)))
  }

  function isFavorite(bookId) {
    return favoriteIds().has(String(bookId))
  }

  function toggleFavorite(book) {
    if (!book?.id) return false
    const id = String(book.id)
    const list = Array.isArray(favorites.value) ? [...favorites.value] : []
    const idx = list.findIndex(item => String(item.id) === id)
    if (idx >= 0) {
      list.splice(idx, 1)
    } else {
      list.unshift(normalizeFavoriteBook(book))
    }
    favorites.value = list
    persistFavorites(list)
    return true
  }

  function removeFavorite(bookId) {
    const id = String(bookId)
    const list = (favorites.value || []).filter(item => String(item.id) !== id)
    favorites.value = list
    persistFavorites(list)
  }

  function currentRole() {
    return String(
      userInfo.value?.role ||
      getCachedUserInfo().role ||
      localStorage.getItem('role') ||
      ''
    ).trim().toLowerCase()
  }

  function isStaffRole() {
    const role = currentRole()
    return role === 'librarian' || role === 'admin'
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
    const data = await fetchBooks()
    books.value = Array.isArray(data) && data.length ? data : fallbackBooks
  }

  function persistCart() {
    localStorage.setItem('readerCartItems', JSON.stringify(cartItems.value))
  }

  function addDaysToLocalIso(days) {
    const base = new Date()
    base.setDate(base.getDate() + Number(days || 1))
    const pad = value => String(value).padStart(2, '0')
    return `${base.getFullYear()}-${pad(base.getMonth() + 1)}-${pad(base.getDate())}T${pad(base.getHours())}:${pad(base.getMinutes())}`
  }

  function addToCart(book, quantity = 1) {
    if (!book?.id) return
    const existing = cartItems.value.find(item => String(item.id) === String(book.id))
    if (existing) {
      existing.quantity = Math.min(10, (Number(existing.quantity) || 1) + Number(quantity || 1))
    } else {
      cartItems.value.unshift({
        id: book.id,
        tenSach: book.tenSach || book.TenSach || '',
        tacGia: book.tacGia || book.TacGia || '',
        imageUrl: book.imageUrl || book.ImageUrl || '',
        quantity: Math.min(10, Number(quantity || 1)),
        borrowDays: 1,
        borrowDueAt: addDaysToLocalIso(1),
        soBanConLai: Number(book.soBanConLai ?? book.SoBanConLai ?? 0)
      })
    }
    persistCart()
  }

  function removeFromCart(bookId) {
    cartItems.value = cartItems.value.filter(item => String(item.id) !== String(bookId))
    persistCart()
  }

  function clearCart() {
    cartItems.value = []
    persistCart()
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
    if (!isStaffRole()) {
      allTransactions.value = []
      return []
    }
    allTransactions.value = await fetchAllTransactions()
    return allTransactions.value
  }

  async function loadEvents() {
    if (!isStaffRole()) {
      events.value = []
      return []
    }
    const card = getReaderCard()
    const payloads = await fetchEvents()
    const parsed = payloads
      .map(normalizeEvent)
      .map(event => {
        try {
          return { ...event, payload: JSON.parse(event.payloadJson || '{}') }
        } catch {
          return { ...event, payload: {} }
        }
      })
      .filter(event => {
        const payload = event.payload || {}
        const cardNumber = payload.CardNumber || payload.cardNumber || payload.ReaderCardNumber || payload.readerCardNumber || ''
        return !card || !cardNumber || String(cardNumber) === String(card)
      })
    events.value = parsed
    return parsed
  }

  async function loadFines() {
    if (!isStaffRole()) {
      fines.value = []
      return []
    }
    fines.value = await fetchFines()
    return fines.value
  }

  async function loadUserInfo() {
    const info = await loadProfile()
    const cached = getCachedUserInfo()
    const readerCard = localStorage.getItem('readerCard') || ''
    userInfo.value = {
      ...cached,
      ...info,
      fullName:
        info?.fullName ||
        info?.FullName ||
        cached?.fullName ||
        cached?.FullName ||
        cached?.username ||
        readerCard ||
        userInfo.value?.fullName ||
        'Độc giả',
      username:
        info?.username ||
        info?.Username ||
        cached?.username ||
        cached?.Username ||
        readerCard ||
        userInfo.value?.username ||
        '',
      role:
        info?.role ||
        info?.Role ||
        cached?.role ||
        cached?.Role ||
        localStorage.getItem('role') ||
        userInfo.value?.role ||
        'Reader',
      cardNumber:
        info?.cardNumber ||
        info?.CardNumber ||
        cached?.cardNumber ||
        cached?.CardNumber ||
        readerCard ||
        '',
      email:
        info?.email ||
        info?.Email ||
        cached?.email ||
        cached?.Email ||
        '',
      createdAt:
        info?.createdAt ||
        info?.CreatedAt ||
        cached?.createdAt ||
        cached?.CreatedAt ||
        '',
      avatarUrl: info?.avatarUrl || info?.AvatarUrl || cached?.avatarUrl || cached?.AvatarUrl || cached?.avatar || cached?.Avatar || ''
    }
    favorites.value = loadFavoritesForUser(userInfo.value)
    return info
  }

  async function loadAll() {
    loading.value = true
    try {
      await loadBooks()
      await loadMyTransactions()
      if (isStaffRole()) {
        await Promise.all([
          loadAllTransactions(),
          loadEvents(),
          loadFines()
        ])
      } else {
        allTransactions.value = []
        events.value = []
        fines.value = []
      }
    } finally {
      loading.value = false
    }
  }

  return {
    books, myTransactions, allTransactions, events, fines, userInfo, loading,
    cartItems,
    favorites,
    activeTransactions, overdueTransactions, pendingTransactions, returnedTransactions,
    myFines, myUnpaidFines, totalUnpaidFines,
    statusOf, isPending, isBorrowed, isOverdue, isReturned, isReturnPending, isActiveLoan, isFinePaid, cardNumberOf, bookIdOf,
    loadBooks, loadMyTransactions, loadAllTransactions, loadEvents, loadFines,
    addToCart, removeFromCart, clearCart,
    isFavorite, toggleFavorite, removeFavorite,
    loadUserInfo, loadAll
  }
})
