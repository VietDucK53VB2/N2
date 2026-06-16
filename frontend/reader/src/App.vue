<template>
  <v-app>
    <router-view />
  </v-app>
</template>

<script setup>
import { onMounted } from 'vue'
import { useAppStore } from '@/stores/app'
import { initAuth } from '@/utils/api'

const store = useAppStore()

onMounted(async () => {
  const ready = await initAuth()
  await store.loadUserInfo()
  if (ready) {
    store.loadAll()
  }
})
</script>
