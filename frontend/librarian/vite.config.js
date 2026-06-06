import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: { alias: { '@': path.resolve(__dirname, 'src') } },
  base: '/ui/librarian/',
  build: {
    outDir: '../../backend/wwwroot/ui/librarian',
    emptyOutDir: true
  },
  server: { port: 3001 }
})
