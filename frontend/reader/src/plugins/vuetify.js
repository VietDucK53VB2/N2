import 'vuetify/styles'
import '@mdi/font/css/materialdesignicons.css'
import { createVuetify } from 'vuetify'

export default createVuetify({
  aliases: {},
  theme: {
    defaultTheme: 'libraryTheme',
    themes: {
      libraryTheme: {
        dark: false,
        colors: {
          primary: '#e8855a',
          secondary: '#7c5cbf',
          accent: '#f5c842',
          error: '#ef4444',
          warning: '#f59e0b',
          info: '#3b82f6',
          success: '#22c55e',
          background: '#f5f0e8',
          surface: '#ffffff',
          'on-surface': '#1a1a1a'
        }
      }
    }
  },
  defaults: {
    global: {
      class: 'font-inter'
    },
    VCard: { rounded: 'xl', elevation: 1 },
    VBtn: { rounded: 'lg', style: 'text-transform: none; letter-spacing: 0;' },
    VChip: { rounded: 'lg' },
    VTextField: { variant: 'outlined', density: 'comfortable' }
  }
})
