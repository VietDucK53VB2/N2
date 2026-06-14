const GENERIC_CATEGORY_ICON = 'mdi-tag-outline'

const knownCategoryIcons = [
  { keywords: ['van hoc', 'truyen', 'tieu thuyet', 'tho'], icon: 'mdi-pen' },
  { keywords: ['khoa hoc', 'science', 'vat ly', 'hoa hoc', 'sinh hoc'], icon: 'mdi-microscope' },
  { keywords: ['lich su', 'history'], icon: 'mdi-bank' },
  { keywords: ['kinh te', 'business', 'tai chinh', 'money'], icon: 'mdi-briefcase' },
  { keywords: ['tam ly', 'psychology', 'ky nang'], icon: 'mdi-brain' }
]

const knownCategoryEmoji = [
  { keywords: ['van hoc', 'truyen', 'tieu thuyet', 'tho'], emoji: '✒️' },
  { keywords: ['khoa hoc', 'science', 'vat ly', 'hoa hoc', 'sinh hoc'], emoji: '🔬' },
  { keywords: ['lich su', 'history'], emoji: '🏛️' },
  { keywords: ['kinh te', 'business', 'tai chinh', 'money'], emoji: '💼' },
  { keywords: ['tam ly', 'psychology', 'ky nang'], emoji: '🧠' }
]

function stripDiacritics(value = '') {
  return String(value)
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
}

export function normalizeCategoryText(value) {
  if (value == null) return ''
  if (Array.isArray(value)) return value.map(normalizeCategoryText).filter(Boolean).join(', ')
  if (typeof value === 'object') {
    return normalizeCategoryText(
      value.tenTheLoai ?? value.TenTheLoai ??
      value.categoryName ?? value.CategoryName ??
      value.name ?? value.Name ??
      value.title ?? value.Title ??
      value.label ?? value.Label ??
      value.value ?? value.Value
    )
  }
  return String(value).trim()
}

export function slugCategory(value) {
  const text = stripDiacritics(normalizeCategoryText(value)).toLowerCase()
  return text.replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'unknown'
}

function splitCategoryText(value) {
  const text = normalizeCategoryText(value)
  if (!text) return []
  return text
    .split(/[;,/|]+/)
    .map(item => item.trim())
    .filter(Boolean)
}

function readCategoryCandidates(book = {}) {
  return [
    book.theLoai,
    book.TheLoai,
    book.tenTheLoai,
    book.TenTheLoai,
    book.category,
    book.Category,
    book.categoryName,
    book.CategoryName,
    book.genre,
    book.Genre,
    book.loaiSach,
    book.LoaiSach,
    book.nhomSach,
    book.NhomSach,
    book.catalogCategory,
    book.CatalogCategory,
    book.categories,
    book.Categories,
    book.genres,
    book.Genres,
    book.theLoais,
    book.TheLoais
  ]
}

export function categoryNamesOfBook(book = {}) {
  const names = []
  for (const candidate of readCategoryCandidates(book)) {
    if (Array.isArray(candidate)) {
      candidate.forEach(item => names.push(...splitCategoryText(item)))
    } else {
      names.push(...splitCategoryText(candidate))
    }
  }

  const unique = new Map()
  names.forEach(name => {
    const slug = slugCategory(name)
    if (!unique.has(slug)) unique.set(slug, name)
  })
  return [...unique.values()]
}

function findKnownMeta(label, source) {
  const key = stripDiacritics(label).toLowerCase()
  return source.find(item => item.keywords.some(keyword => key.includes(keyword)))
}

export function categoryIcon(label = '') {
  return findKnownMeta(label, knownCategoryIcons)?.icon || GENERIC_CATEGORY_ICON
}

export function categoryEmoji(label = '') {
  return findKnownMeta(label, knownCategoryEmoji)?.emoji || '🏷️'
}

export function categoriesFromBooks(books = []) {
  const map = new Map()
  books.forEach(book => {
    categoryNamesOfBook(book).forEach(label => {
      const value = slugCategory(label)
      if (!map.has(value)) {
        map.set(value, {
          label,
          value,
          icon: categoryIcon(label),
          count: 0
        })
      }
      map.get(value).count += 1
    })
  })

  return [...map.values()].sort((a, b) => a.label.localeCompare(b.label, 'vi'))
}

export function categoriesFromCatalog(categories = []) {
  const map = new Map()
  categories.forEach(category => {
    const label = normalizeCategoryText(category)
    if (!label) return
    const value = slugCategory(label)
    map.set(value, {
      label,
      value,
      icon: categoryIcon(label),
      count: Number(category.count ?? category.Count ?? category.bookCount ?? category.BookCount ?? 0)
    })
  })
  return [...map.values()].sort((a, b) => a.label.localeCompare(b.label, 'vi'))
}

export function mergeCategories(catalogCategories = [], bookCategories = []) {
  const map = new Map()
  catalogCategories.forEach(item => map.set(item.value, { ...item }))
  bookCategories.forEach(item => {
    if (map.has(item.value)) {
      const current = map.get(item.value)
      map.set(item.value, {
        ...current,
        count: Math.max(Number(current.count || 0), Number(item.count || 0))
      })
      return
    }
    map.set(item.value, { ...item })
  })
  return [...map.values()].sort((a, b) => a.label.localeCompare(b.label, 'vi'))
}

export function categoryLabel(value, books = []) {
  if (!value || value === 'all') return 'Tất cả thể loại'
  const found = categoriesFromBooks(books).find(item => item.value === value)
  return found?.label || normalizeCategoryText(value)
}

export function matchesCategory(book = {}, category = '') {
  if (!category || category === 'all' || category === 'new' || category === 'popular') return true
  return categoryNamesOfBook(book).some(label => slugCategory(label) === category)
}

export function dashboardCategoriesFromBooks(books = []) {
  return [
    { label: '📚 Tất cả', value: 'all' },
    { label: '✨ Mới nhất', value: 'new' },
    { label: '🔥 Phổ biến', value: 'popular' },
    ...categoriesFromBooks(books).map(item => ({
      ...item,
      label: `${categoryEmoji(item.label)} ${item.label}`
    }))
  ]
}
