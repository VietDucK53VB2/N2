const FAVORITE_STORAGE_KEY = 'readerFavoriteBooks'

export function bookKey(book = {}) {
  return String(book?.id ?? book?.Id ?? book?.bookId ?? book?.BookId ?? '')
}

export function loadFavoriteIds() {
  try {
    return new Set(JSON.parse(localStorage.getItem(FAVORITE_STORAGE_KEY) || '[]').map(String))
  } catch {
    return new Set()
  }
}

export function saveFavoriteIds(ids) {
  localStorage.setItem(FAVORITE_STORAGE_KEY, JSON.stringify([...ids]))
  window.dispatchEvent(new CustomEvent('reader-favorites-changed'))
}

export function isFavoriteBook(book, ids = loadFavoriteIds()) {
  const id = bookKey(book)
  return !!id && ids.has(id)
}

export function toggleFavoriteBook(book, ids = loadFavoriteIds()) {
  const id = bookKey(book)
  if (!id) return { ids, active: false }
  const next = new Set(ids)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  saveFavoriteIds(next)
  return { ids: next, active: next.has(id) }
}
