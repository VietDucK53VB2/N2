import dayjs from 'dayjs'
import 'dayjs/locale/vi'

dayjs.locale('vi')

const timezonePattern = /(?:Z|[+-]\d{2}:?\d{2})$/i

export function parseApiDate(value) {
  if (!value) return null
  if (value instanceof Date) return value
  if (typeof value === 'number') return new Date(value)
  if (typeof value !== 'string') return new Date(value)

  const text = value.trim()
  if (!text) return null

  // Backend returns UTC timestamps without a timezone suffix in a few places.
  // Treat those as UTC so a fresh loan does not show +7 hours in Vietnam.
  return new Date(timezonePattern.test(text) ? text : `${text}Z`)
}

export function apiDateMs(value) {
  const date = parseApiDate(value)
  const ms = date?.getTime()
  return Number.isFinite(ms) ? ms : NaN
}

export function formatDate(d) {
  const date = parseApiDate(d)
  if (!date || Number.isNaN(date.getTime())) return '—'
  return dayjs(date).format('DD/MM/YYYY')
}

export function formatDateTime(d) {
  const date = parseApiDate(d)
  if (!date || Number.isNaN(date.getTime())) return '—'
  return dayjs(date).format('DD/MM/YYYY HH:mm:ss')
}

export function formatDurationMs(ms) {
  if (ms === null || ms === undefined || Number.isNaN(ms)) return '—'
  const overdue = ms < 0
  let totalSeconds = Math.abs(Math.floor(ms / 1000))
  const days = Math.floor(totalSeconds / 86400)
  totalSeconds %= 86400
  const hours = Math.floor(totalSeconds / 3600)
  totalSeconds %= 3600
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  const clock = [hours, minutes, seconds].map(v => String(v).padStart(2, '0')).join(':')
  const text = days > 0 ? `${days} ngày ${clock}` : clock
  return overdue ? `Quá hạn ${text}` : text
}

export function durationSince(start, end = Date.now()) {
  const startMs = apiDateMs(start)
  const endMs = apiDateMs(end)
  if (!Number.isFinite(startMs) || !Number.isFinite(endMs)) return '—'
  return formatDurationMs(endMs - startMs)
}

export function timeUntil(target, now = Date.now()) {
  const targetMs = apiDateMs(target)
  const nowMs = apiDateMs(now)
  if (!Number.isFinite(targetMs) || !Number.isFinite(nowMs)) return '—'
  return formatDurationMs(targetMs - nowMs)
}

export function formatMoney(v) {
  return new Intl.NumberFormat('vi-VN').format(v) + ' đ'
}

export function daysLeft(dueDate) {
  if (!dueDate) return null
  const dueMs = apiDateMs(dueDate)
  if (!Number.isFinite(dueMs)) return null
  return Math.ceil((dueMs - Date.now()) / (1000 * 60 * 60 * 24))
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
