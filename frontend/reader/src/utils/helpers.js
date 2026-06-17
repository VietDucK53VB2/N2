import dayjs from 'dayjs'
import 'dayjs/locale/vi'
dayjs.locale('vi')

export function formatDate(d) {
  if (!d) return '—'
  return dayjs(d).format('DD/MM/YYYY')
}

export function formatDateTime(d) {
  if (!d) return '—'
  const parsed = dayjs(d)
  return parsed.isValid() ? parsed.format('DD/MM/YYYY HH:mm:ss') : String(d)
}

export function formatDurationParts(from, to = new Date()) {
  if (!from || !to) return null
  const start = new Date(from)
  const end = new Date(to)
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return null
  const diff = Math.max(0, Math.abs(end.getTime() - start.getTime()))
  const days = Math.floor(diff / 86400000)
  const hours = Math.floor((diff % 86400000) / 3600000)
  const minutes = Math.floor((diff % 3600000) / 60000)
  const seconds = Math.floor((diff % 60000) / 1000)
  return { days, hours, minutes, seconds, totalMs: diff }
}

export function formatDurationText(from, to = new Date()) {
  const parts = formatDurationParts(from, to)
  if (!parts) return '—'
  return `${parts.days} ngày ${String(parts.hours).padStart(2, '0')}:${String(parts.minutes).padStart(2, '0')}:${String(parts.seconds).padStart(2, '0')}`
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

export function getDisplayReaderName(record = {}, fallback = 'Độc giả') {
  return firstNonEmpty(
    record.readerName,
    record.ReaderName,
    record.fullName,
    record.FullName,
    record.readerFullName,
    record.ReaderFullName,
    record.username,
    record.Username,
    record.readerUsername,
    record.ReaderUsername,
    record.cardNumber,
    record.CardNumber,
    fallback
  ) || fallback
}

export function getDisplayBookTitle(book = {}, fallback = '—') {
  return firstNonEmpty(
    book.tenSach,
    book.TenSach,
    book.title,
    book.Title,
    book.bookTitle,
    book.BookTitle,
    book.bookName,
    book.BookName,
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

export function translateFineReason(reason = '') {
  const text = String(reason || '').trim()
  if (!text) return 'Phạt trả quá hạn'

  const lower = text.toLowerCase()
  const overdueMatch = lower.match(/overdue return by\s+(\d+)\s+day/)
  if (overdueMatch) {
    const days = overdueMatch[1]
    return `Phạt trả quá hạn ${days} ngày`
  }

  if (lower.includes('lost book fee')) return 'Phí mất sách'
  if (lower.includes('heavydamage')) return 'Phí hư hỏng nặng'
  if (lower.includes('lightdamage')) return 'Phí hư hỏng nhẹ'
  if (lower.includes('damage')) return 'Phí hư hỏng sách'

  return text
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
