import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const rootDir = path.resolve(__dirname, '..', '..')

export default defineConfig({
  plugins: [vue()],
  resolve: { alias: { '@': path.resolve(__dirname, 'src') } },
  base: '/ui/librarian/',
  build: {
    outDir: path.resolve(rootDir, 'backend', 'wwwroot', 'ui', 'librarian'),
    emptyOutDir: true
  },
  server: { port: 3001 }
})
