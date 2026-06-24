<template>
  <v-app>
    <router-view />
  </v-app>
</template>

<script setup>
import { onBeforeUnmount, onMounted } from 'vue'
import { useAppStore } from '@/stores/app'
import { initAuth } from '@/utils/api'

const store = useAppStore()
let refreshTimer = null

onMounted(async () => {
  const ready = await initAuth()
  await store.loadUserInfo()
  if (ready) {
    await store.loadAll()
  }

  refreshTimer = window.setInterval(async () => {
    try {
      await store.loadBooks()
    } catch {
      // Keep the current book list when the refresh endpoint is temporarily unavailable.
    }
  }, 5 * 60 * 1000)
})

onBeforeUnmount(() => {
  if (refreshTimer) {
    window.clearInterval(refreshTimer)
    refreshTimer = null
  }
})
</script>
