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
        { path: 'reviews', name: 'reviews', component: () => import('./views/Reviews.vue') },
        { path: 'finance/prices', name: 'finance-prices', component: () => import('./views/FinancePrices.vue') },
        { path: 'finance/revenue', name: 'finance-revenue', component: () => import('./views/FinanceRevenue.vue') },
        { path: 'fines', name: 'fines', component: () => import('./views/Fines.vue') }
      ]
    }
  ]
})
