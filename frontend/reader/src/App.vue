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
  await initAuth()
  await store.loadUserInfo()
  store.loadAll()
})
</script>
