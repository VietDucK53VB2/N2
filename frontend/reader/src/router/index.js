import { createRouter, createWebHashHistory } from 'vue-router'

const routes = [
  // Reader routes
    {
      path: '/',
      component: () => import('@/views/Layout.vue'),
      children: [
        { path: '', name: 'dashboard', component: () => import('@/views/Dashboard.vue') },
        { path: 'mybooks', name: 'mybooks', component: () => import('@/views/MyBooks.vue') },
        { path: 'favorites', name: 'favorites', component: () => import('@/views/Favorites.vue') },
        { path: 'cart', name: 'cart', component: () => import('@/views/Cart.vue') },
        { path: 'categories/:category?', name: 'categories', component: () => import('@/views/Categories.vue') },
        { path: 'history', name: 'history', component: () => import('@/views/History.vue') },
        { path: 'profile', name: 'profile', component: () => import('@/views/Profile.vue') }
      ]
    },
  // Librarian routes
  {
    path: '/librarian',
    component: () => import('@/views/librarian/LibrarianLayout.vue'),
    children: [
      { path: '', name: 'lib-overview', component: () => import('@/views/librarian/Overview.vue') },
      { path: 'loans', name: 'lib-loans', component: () => import('@/views/librarian/Loans.vue') },
      { path: 'search', name: 'lib-search', component: () => import('@/views/librarian/Search.vue') },
      { path: 'return', name: 'lib-return', component: () => import('@/views/librarian/Return.vue') },
      { path: 'fines', name: 'lib-fines', component: () => import('@/views/librarian/Fines.vue') }
    ]
  }
]

const router = createRouter({
  history: createWebHashHistory(),
  routes
})

export default router
