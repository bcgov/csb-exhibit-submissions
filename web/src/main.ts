import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import 'vuetify/styles'
// import { createVuetify } from 'vuetify'
import { registerPlugins } from './plugins'

import "./assets/styles/main.scss"

const app = createApp(App)

app.use(router)

registerPlugins(app)

app.mount('#app')
