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
