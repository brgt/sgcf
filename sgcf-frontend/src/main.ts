import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { VueQueryPlugin } from '@tanstack/vue-query'
import { createI18n } from 'vue-i18n'
import 'virtual:uno.css'
import '@nordware/design-system/globals'
import '@nordware/design-system/styles'
import App from './app/App.vue'
import { router } from './app/router'
import ptBR from './shared/i18n/locales/pt-BR.json'

const pinia = createPinia()

const i18n = createI18n({
  locale: 'pt-BR',
  fallbackLocale: 'pt-BR',
  messages: { 'pt-BR': ptBR },
  legacy: false,
})

const app = createApp(App)
app.use(pinia)
app.use(router)
app.use(VueQueryPlugin)
app.use(i18n)
app.mount('#app')
