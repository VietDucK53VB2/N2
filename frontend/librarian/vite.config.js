import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const rootDir = path.resolve(__dirname, '..', '..')

export default defineConfig({
  plugins: [
    vue(),
    {
      name: 'librarian-ping-endpoint',
      configureServer(server) {
        server.middlewares.use((req, res, next) => {
          const url = (req.url || '').split('?')[0]

          if (url === '/ping' || url === '/ui/librarian/ping') {
            res.statusCode = 200
            res.setHeader('Content-Type', 'text/plain; charset=utf-8')
            res.end('pong')
            return
          }

          next()
        })
      }
    }
  ],
  resolve: { alias: { '@': path.resolve(__dirname, 'src') } },
  base: '/ui/librarian/',
  build: {
    outDir: path.resolve(rootDir, 'backend', 'wwwroot', 'ui', 'librarian'),
    emptyOutDir: true
  },
  server: { port: 3001 }
})
