import dayjs from 'dayjs'
import 'dayjs/locale/vi'
dayjs.locale('vi')

export function formatDate(d) {
  if (!d) return '—'
  return dayjs(d).format('DD/MM/YYYY')
}

export function formatMoney(v) {
  return new Intl.NumberFormat('vi-VN').format(v) + ' đ'
}

export function daysLeft(dueDate) {
  if (!dueDate) return null
  return Math.ceil((new Date(dueDate) - new Date()) / (1000 * 60 * 60 * 24))
}

export function titleColor(t) {
  const colors = [
    '#1B3A5F', '#7B2D3B', '#1F4E3D', '#5C4A1E', '#3D2B5E',
    '#1F5C4E', '#5C3D1E', '#3B4A7B', '#5E2B3B', '#2B5E3D'
  ]
  if (!t) return colors[0]
  let h = 0
  for (let i = 0; i < t.length; i++) h = t.charCodeAt(i) + ((h << 5) - h)
  return colors[Math.abs(h) % colors.length]
}

export function getInitials(name) {
  if (!name) return 'DG'
  return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2)
}

function firstNonEmpty(...values) {
  for (const value of values) {
    if (value === null || value === undefined) continue
    const text = String(value).trim()
    if (text) return text
  }
  return ''
}

export function getDisplayName(user = {}, fallback = 'Độc giả') {
  return firstNonEmpty(
    user.fullName,
    user.FullName,
    user.username,
    user.Username,
    user.name,
    user.Name,
    user.full_name,
    user.fullName,
    fallback
  ) || fallback
}

export function getDisplayCardNumber(user = {}, fallback = 'Chưa liên kết') {
  return firstNonEmpty(
    user.cardNumber,
    user.CardNumber,
    user.readerCardNumber,
    user.ReaderCardNumber,
    user.libraryCardNumber,
    user.LibraryCardNumber,
    user.libraryCard?.cardNumber,
    user.libraryCard?.CardNumber,
    user.user?.cardNumber,
    user.user?.CardNumber,
    fallback
  ) || fallback
}

export function getGenreTags(tenSach) {
  const tags = []
  const t = (tenSach || '').toLowerCase()
  if (t.includes('lập trình') || t.includes('code') || t.includes('python') || t.includes('java')) tags.push('Lập trình')
  if (t.includes('toán')) tags.push('Toán học')
  if (t.includes('văn') || t.includes('thơ') || t.includes('truyện')) tags.push('Văn học')
  if (t.includes('lịch sử') || t.includes('history')) tags.push('Lịch sử')
  if (t.includes('khoa học') || t.includes('science')) tags.push('Khoa học')
  if (t.includes('kinh tế') || t.includes('business')) tags.push('Kinh tế')
  if (t.includes('tâm lý')) tags.push('Tâm lý')
  if (!tags.length) tags.push('Sách hay', 'Đề xuất')
  return tags.slice(0, 3)
}

function normalizeCategoryLabel(value) {
  return String(value || '')
    .replace(/\s+/g, ' ')
    .replace(/\s*,\s*/g, ', ')
    .trim()
}

export function slugifyCategory(value) {
  return normalizeCategoryLabel(value)
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

export function extractBookCategories(book = {}) {
  const raw = [
    book.theLoai,
    book.TheLoai,
    book.genre,
    book.Genre,
    book.category,
    book.Category,
    book.chuDe,
    book.ChuDe,
    book.type,
    book.Type
  ]

  const categories = []
  for (const value of raw) {
    if (Array.isArray(value)) {
      categories.push(...value)
      continue
    }
    if (value === null || value === undefined) continue
    const text = String(value).trim()
    if (!text) continue
    categories.push(...text.split(/[;,/|•]+/))
  }

  const cleaned = categories
    .map(normalizeCategoryLabel)
    .filter(Boolean)

  if (cleaned.length) return [...new Set(cleaned)]

  return []
}

export function buildCatalogCategories(books = []) {
  const seen = new Map()
  for (const book of books || []) {
    for (const label of extractBookCategories(book)) {
      const value = slugifyCategory(label)
      if (!value) continue
      if (!seen.has(value)) {
        seen.set(value, {
          label,
          value,
          count: 0
        })
      }
      seen.get(value).count += 1
    }
  }
  if (!seen.size) {
    for (const book of books || []) {
      for (const label of getGenreTags(book.tenSach || book.TenSach || '')) {
        const value = slugifyCategory(label)
        if (!value) continue
        if (!seen.has(value)) {
          seen.set(value, {
            label,
            value,
            count: 0
          })
        }
        seen.get(value).count += 1
      }
    }
  }
  return [...seen.values()].sort((a, b) => a.label.localeCompare(b.label, 'vi'))
}

export function bookMatchesCategory(book = {}, categoryValue = '') {
  const target = slugifyCategory(categoryValue)
  if (!target || target === 'all') return true
  return extractBookCategories(book).some(label => slugifyCategory(label) === target)
}
