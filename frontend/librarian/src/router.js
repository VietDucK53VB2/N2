import { createRouter, createWebHistory } from 'vue-router'

export default createRouter({
  history: createWebHistory('/ui/librarian/'),
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
        { path: 'fines', name: 'fines', component: () => import('./views/Fines.vue') },
        { path: 'embed/loans', name: 'embed-loans', component: () => import('./views/Loans.vue') },
        { path: 'embed/fines', name: 'embed-fines', component: () => import('./views/Fines.vue') },
        { path: 'embed/prices', name: 'embed-prices', component: () => import('./views/FinancePrices.vue') },
        { path: 'embed/revenue', name: 'embed-revenue', component: () => import('./views/FinanceRevenue.vue') }
      ]
    }
  ]
})
