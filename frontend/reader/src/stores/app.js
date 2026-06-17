import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import {
  fetchBooks, fetchTransactions, fetchAllTransactions,
  fetchEvents, fetchFines, fetchFavorites, saveFavoriteBook, removeFavoriteBook,
  getReaderCard, getCachedUserInfo,
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
      nhaSanXuat: book.nhaSanXuat ?? book.NhaSanXuat ?? '',
      isbn: book.isbn ?? book.Isbn ?? book.ISBN ?? '',
      namXuatBan: Number(book.namXuatBan ?? book.NamXuatBan ?? 0),
      tomTat: book.tomTat ?? book.TomTat ?? book.moTa ?? book.MoTa ?? '',
      soBanConLai: Number(book.soBanConLai ?? book.SoBanConLai ?? 0),
      soLuong: Number(book.soLuong ?? book.SoLuong ?? 0),
      soBanDaMuon: Number(book.soBanDaMuon ?? book.SoBanDaMuon ?? 0),
      theLoai: book.theLoai ?? book.TheLoai ?? '',
      trangThai: book.trangThai ?? book.TrangThai ?? '',
      danhGiaTrungBinh: Number(book.danhGiaTrungBinh ?? book.DanhGiaTrungBinh ?? 0)
    }
  }

  function favoriteIds() {
    return new Set((favorites.value || []).map(item => String(item.id)))
  }

  function isFavorite(bookId) {
    return favoriteIds().has(String(bookId))
  }

  async function loadFavoritesFromServer(info = userInfo.value) {
    const remote = await fetchFavorites()
    if (Array.isArray(remote)) {
      const normalized = remote.map(normalizeFavoriteBook)
      favorites.value = normalized
      persistFavorites(normalized, info)
      return normalized
    }

    const local = loadFavoritesForUser(info)
    favorites.value = local
    return local
  }

  async function toggleFavorite(book) {
    if (!book?.id) return false
    const id = String(book.id)
    const list = Array.isArray(favorites.value) ? [...favorites.value] : []
    const idx = list.findIndex(item => String(item.id) === id)
    const nextList = idx >= 0
      ? list.filter(item => String(item.id) !== id)
      : [normalizeFavoriteBook(book), ...list]

    favorites.value = nextList
    persistFavorites(nextList)

    if (idx >= 0) {
      removeFavoriteBook(id).catch(() => false)
      return true
    }

    const normalizedBook = normalizeFavoriteBook(book)
    saveFavoriteBook(normalizedBook)
      .then(saved => {
        if (!saved) return
        const current = Array.isArray(favorites.value) ? [...favorites.value] : []
        const currentIdx = current.findIndex(item => String(item.id) === id)
        if (currentIdx >= 0) {
          current[currentIdx] = normalizeFavoriteBook(saved)
          favorites.value = current
          persistFavorites(current)
        }
      })
      .catch(() => {})
    return true
  }

  async function removeFavorite(bookId) {
    const id = String(bookId)
    const previousList = Array.isArray(favorites.value) ? [...favorites.value] : []
    const list = previousList.filter(item => String(item.id) !== id)
    favorites.value = list
    persistFavorites(list)
    removeFavoriteBook(id).catch(() => false)
    return true
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

  function borrowedAtOf(transaction = {}) {
    return (
      transaction.BorrowedAt ||
      transaction.borrowedAt ||
      transaction.RequestDate ||
      transaction.requestDate ||
      transaction.CreatedAt ||
      transaction.createdAt ||
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
    myFines.value.filter(f => !isFinePaid(f) && !isFinePaymentPending(f))
  )

  const myPendingFinePayments = computed(() =>
    myFines.value.filter(isFinePaymentPending)
  )

  const myPaidFines = computed(() =>
    myFines.value.filter(isFinePaid)
  )

  const totalUnpaidFines = computed(() =>
    myFines.value
      .filter(f => !isFinePaid(f))
      .reduce((s, f) => s + Number(f.Amount || f.amount || 0), 0)
  )

  async function loadBooks() {
    const data = await fetchBooks()
    books.value = Array.isArray(data) && data.length ? data : fallbackBooks
  }

  function persistCart() {
    localStorage.setItem('readerCartItems', JSON.stringify(cartItems.value))
  }

  function addHoursToLocalIso(hours) {
    const base = new Date()
    base.setTime(base.getTime() + Number(hours || 1) * 60 * 60 * 1000)
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
        nhaSanXuat: book.nhaSanXuat || book.NhaSanXuat || '',
        isbn: book.isbn || book.Isbn || book.ISBN || '',
        namXuatBan: Number(book.namXuatBan ?? book.NamXuatBan ?? 0),
        tomTat: book.tomTat || book.TomTat || book.moTa || book.MoTa || '',
        quantity: Math.min(10, Number(quantity || 1)),
        borrowDays: 1,
        borrowDueAt: addHoursToLocalIso(1),
        soLuong: Number(book.soLuong ?? book.SoLuong ?? 0),
        soBanDaMuon: Number(book.soBanDaMuon ?? book.SoBanDaMuon ?? 0),
        soBanConLai: Number(book.soBanConLai ?? book.SoBanConLai ?? 0),
        theLoai: book.theLoai || book.TheLoai || '',
        trangThai: book.trangThai || book.TrangThai || '',
        danhGiaTrungBinh: Number(book.danhGiaTrungBinh ?? book.DanhGiaTrungBinh ?? 0)
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
          Isbn: tx.Isbn || tx.isbn || bk.isbn,
          NamXuatBan: tx.NamXuatBan || tx.namXuatBan || bk.namXuatBan,
          TomTat: tx.TomTat || tx.tomTat || bk.tomTat,
          TheLoai: tx.TheLoai || tx.theLoai || bk.theLoai,
          TrangThai: tx.TrangThai || tx.trangThai || bk.trangThai,
          DanhGiaTrungBinh: tx.DanhGiaTrungBinh || tx.danhGiaTrungBinh || bk.danhGiaTrungBinh,
          SoLuong: tx.SoLuong || tx.soLuong || bk.soLuong,
          SoBanDaMuon: tx.SoBanDaMuon || tx.soBanDaMuon || bk.soBanDaMuon,
          SoBanConLai: tx.SoBanConLai || tx.soBanConLai || bk.soBanConLai
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
    events.value = parsed
    return parsed
  }

  async function loadFines() {
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
    await loadFavoritesFromServer(userInfo.value)
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
        await loadEvents()
        await loadFines()
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
    myFines, myUnpaidFines, myPendingFinePayments, myPaidFines, totalUnpaidFines,
    statusOf, isPending, isBorrowed, isOverdue, isReturned, isReturnPending, isRenewPending, isActiveLoan, isFinePaid, isFinePaymentPending, cardNumberOf, bookIdOf,
    loadBooks, loadMyTransactions, loadAllTransactions, loadEvents, loadFines,
    addToCart, removeFromCart, clearCart,
    isFavorite, toggleFavorite, removeFavorite,
    loadFavoritesFromServer,
    loadUserInfo, loadAll
  }
})
