import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vuetify from 'vite-plugin-vuetify'
import path from 'path'

export default defineConfig({
  plugins: [
    vue(),
    vuetify({ autoImport: true })
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src')
    }
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://163.223.210.87:5000',
        changeOrigin: true
      }
    }
  },
  base: '/ui/reader/',
  build: {
    outDir: '../../backend/wwwroot/ui/reader',
    emptyOutDir: true
  }
})
