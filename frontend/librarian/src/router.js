import { createRouter, createWebHashHistory } from 'vue-router'

export default createRouter({
  history: createWebHashHistory('/ui/librarian/'),
  routes: [
    {
      path: '/',
      component: () => import('./views/Layout.vue'),
      children: [
        { path: '', name: 'overview', component: () => import('./views/Overview.vue') },
        { path: 'loans', name: 'loans', component: () => import('./views/Loans.vue') },
        { path: 'search', name: 'search', component: () => import('./views/Search.vue') },
        { path: 'return', name: 'return', component: () => import('./views/Return.vue') },
        { path: 'fines', name: 'fines', component: () => import('./views/Fines.vue') }
      ]
    }
  ]
})
