<template>
  <a-config-provider :theme="{ token: { colorPrimary: '#1a4fba', borderRadius: 8, fontFamily: 'Inter, sans-serif' } }">
    <router-view />
  </a-config-provider>
</template>

<script setup>
import { onMounted } from 'vue'
import { initAuth, useLibrarianStore } from './stores/librarian'
const store = useLibrarianStore()

onMounted(async () => {
  const ready = await initAuth()
  if (ready || Boolean(localStorage.getItem('authToken') || localStorage.getItem('token'))) {
    await store.loadAll()
  }
})
</script>
