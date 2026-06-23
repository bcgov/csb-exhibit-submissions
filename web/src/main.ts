import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import 'vuetify/styles'
// import { createVuetify } from 'vuetify'
import { registerPlugins } from './plugins'

import "@bcgov/design-tokens/css/variables.css"
import "./styles/main.scss"

const pinia = createPinia()
const app = createApp(App)

app.use(router)

registerPlugins(app)

app.use(pinia)
app.mount('#app')
