<template>
  <a-config-provider :theme="{ token: { colorPrimary: '#1a4fba', borderRadius: 8, fontFamily: 'Inter, sans-serif' } }">
    <router-view />
  </a-config-provider>
</template>

<script setup>
import { onMounted } from 'vue'
import { useLibrarianStore } from './stores/librarian'
const store = useLibrarianStore()

function hasToken() {
  return Boolean(localStorage.getItem('authToken') || localStorage.getItem('token'))
}

onMounted(() => {
  if (hasToken()) store.loadAll()
})
</script>
