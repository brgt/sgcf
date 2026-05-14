import{j as e,M as o}from"./index-COwdE6jM.js";import{useMDXComponents as a}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function s(r){const n={code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",ol:"ol",p:"p",pre:"pre",strong:"strong",ul:"ul",...a(),...r.components};return e.jsxs(e.Fragment,{children:[e.jsx(o,{title:"Style Guide/Dark Mode"}),`
`,e.jsx(n.h1,{id:"dark-mode",children:"Dark Mode"}),`
`,e.jsx(n.p,{children:"Guia completo para implementação de modo escuro."}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-paleta-dark-mode",children:"🎨 Paleta Dark Mode"}),`
`,e.jsx(n.h3,{id:"background-colors",children:"Background Colors"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* Light Mode */
--color-background: #ffffff
--color-surface: #f9fafb
--color-surface-hover: #f3f4f6

/* Dark Mode */
--color-background: #0f172a       /* Slate 900 */
--color-surface: #1e293b          /* Slate 800 */
--color-surface-hover: #334155    /* Slate 700 */
`})}),`
`,e.jsx(n.h3,{id:"text-colors",children:"Text Colors"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* Light Mode */
--color-text-primary: #1f2937     /* Gray 800 */
--color-text-secondary: #6b7280   /* Gray 500 */
--color-text-tertiary: #9ca3af    /* Gray 400 */

/* Dark Mode */
--color-text-primary: #f1f5f9     /* Slate 100 */
--color-text-secondary: #cbd5e1   /* Slate 300 */
--color-text-tertiary: #94a3b8    /* Slate 400 */
`})}),`
`,e.jsx(n.h3,{id:"border-colors",children:"Border Colors"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* Light Mode */
--color-border: #e5e7eb           /* Gray 200 */
--color-border-hover: #d1d5db     /* Gray 300 */

/* Dark Mode */
--color-border: #334155           /* Slate 700 */
--color-border-hover: #475569     /* Slate 600 */
`})}),`
`,e.jsx(n.h3,{id:"primary-color-mantém-se",children:"Primary Color (Mantém-se)"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* Light & Dark Mode */
--color-primary: #4ADE80          /* Verde-água tech - não inverte */
--color-primary-hover: #22c55e
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"️-o-que-não-inverter",children:"⚠️ O que NÃO inverter"}),`
`,e.jsx(n.h3,{id:"1-brand-colors",children:"1. Brand Colors"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Sempre manter"}),": Cores da marca (primary, secondary)"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* ✅ Correto - Mantém em ambos os modos */
.button-primary {
  background: var(--color-primary);
  color: var(--color-on-primary);  /* Texto escuro: 8.18:1 ✅ */
}

/* ❌ Errado - Não inverter primary */
.button-primary {
  background: var(--color-primary-inverted); /* Não existe */
}
`})}),`
`,e.jsx(n.h3,{id:"2-semantic-colors",children:"2. Semantic Colors"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Manter significado"}),": Success (verde), Error (vermelho), Warning (amarelo)"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* ✅ Correto - Mantém em ambos os modos */
--color-success: #22c55e
--color-error: #ef4444
--color-warning: #f59e0b

/* ❌ Errado - Não inverter semânticas */
Dark mode:
--color-success: #ef4444  /* NÃO! */
`})}),`
`,e.jsx(n.h3,{id:"3-imagens-e-logos",children:"3. Imagens e Logos"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Não inverter"}),": Use versões específicas para dark mode"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- ✅ Correto -->
<img :src="isDark ? '/logo-dark.svg' : '/logo-light.svg'" />

<!-- ❌ Errado -->
<img src="/logo.svg" class="dark:invert" />
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-contrast-requirements",children:"🌓 Contrast Requirements"}),`
`,e.jsx(n.h3,{id:"text-on-background",children:"Text on Background"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* Light Mode */
Text Primary on Surface:   Contrast 12:1 ✅
Text Secondary on Surface: Contrast 7:1 ✅

/* Dark Mode */
Text Primary on Surface:   Contrast 11:1 ✅
Text Secondary on Surface: Contrast 6:1 ✅
`})}),`
`,e.jsx(n.h3,{id:"interactive-elements",children:"Interactive Elements"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* Botões devem manter contraste 4.5:1 em ambos os modos */
.button-primary {
  background: var(--color-primary);
  color: var(--color-on-primary);  /* Contraste: 8.18:1 ✅ AAA */
}
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-shadows-in-dark-mode",children:"🎭 Shadows in Dark Mode"}),`
`,e.jsx(n.h3,{id:"problema-com-shadows-tradicionais",children:"Problema com Shadows Tradicionais"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* ❌ Não funciona bem em dark mode */
box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
/* Sombra preta em fundo escuro é invisível */
`})}),`
`,e.jsx(n.h3,{id:"solução-elevated-borders",children:"Solução: Elevated Borders"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* ✅ Melhor abordagem */
/* Light Mode */
.card {
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

/* Dark Mode */
.dark .card {
  box-shadow: none;
  border: 1px solid rgba(255, 255, 255, 0.1); /* Borda sutil */
  background: rgba(255, 255, 255, 0.05);      /* Levemente mais claro */
}
`})}),`
`,e.jsx(n.h3,{id:"alternativa-highlight-shadows",children:"Alternativa: Highlight Shadows"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* Dark Mode - Usar luz em vez de sombra */
.dark .card-elevated {
  box-shadow: 0 0 0 1px rgba(255, 255, 255, 0.1),
              0 2px 8px rgba(255, 255, 255, 0.05);
}
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"️-imagens-e-ilustrações",children:"🖼️ Imagens e Ilustrações"}),`
`,e.jsx(n.h3,{id:"fotos-de-produtos",children:"Fotos de Produtos"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- Fotos devem manter cores originais -->
<img src="/product.jpg" alt="Product" />
<!-- Não aplicar filtros -->
`})}),`
`,e.jsx(n.h3,{id:"ícones-e-ilustrações",children:"Ícones e Ilustrações"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- ✅ Use ícones com cor ajustável -->
<span class="i-carbon-user text-text-primary"></span>
<!-- Cor se adapta automaticamente ao modo -->

<!-- ❌ Evite ícones SVG com cores fixas -->
<img src="/icon-blue.svg" />
`})}),`
`,e.jsx(n.h3,{id:"imagens-decorativas",children:"Imagens Decorativas"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- Se necessário, reduza opacidade em dark mode -->
<img
  src="/illustration.svg"
  class="opacity-100 dark:opacity-80"
/>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-toggle-implementation",children:"🔄 Toggle Implementation"}),`
`,e.jsx(n.h3,{id:"opção-1-system-preference-recomendado",children:"Opção 1: System Preference (Recomendado)"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-typescript",children:`// Detecta preferência do sistema
const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches

// Aplica classe
if (prefersDark) {
  document.documentElement.classList.add('dark')
}
`})}),`
`,e.jsx(n.h3,{id:"opção-2-user-toggle",children:"Opção 2: User Toggle"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<script setup>
const isDark = ref(false)

const toggleDark = () => {
  isDark.value = !isDark.value
  document.documentElement.classList.toggle('dark')
  localStorage.setItem('theme', isDark.value ? 'dark' : 'light')
}

// Carregar preferência salva
onMounted(() => {
  const saved = localStorage.getItem('theme')
  if (saved) {
    isDark.value = saved === 'dark'
    if (isDark.value) {
      document.documentElement.classList.add('dark')
    }
  }
})
<\/script>

<template>
  <button @click="toggleDark" class="p-2 rounded-lg hover:bg-surface">
    <span v-if="isDark" class="i-carbon-moon"></span>
    <span v-else class="i-carbon-sun"></span>
  </button>
</template>
`})}),`
`,e.jsx(n.h3,{id:"opção-3-três-estados",children:"Opção 3: Três Estados"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-typescript",children:`type Theme = 'light' | 'dark' | 'system'

const theme = ref<Theme>('system')

const applyTheme = () => {
  if (theme.value === 'system') {
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches
    document.documentElement.classList.toggle('dark', prefersDark)
  } else {
    document.documentElement.classList.toggle('dark', theme.value === 'dark')
  }
}
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-component-adaptations",children:"🎨 Component Adaptations"}),`
`,e.jsx(n.h3,{id:"button",children:"Button"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<style>
.button-ghost {
  background: transparent;
  color: var(--color-text-primary);
}

.button-ghost:hover {
  background: rgba(0, 0, 0, 0.05);
}

.dark .button-ghost:hover {
  background: rgba(255, 255, 255, 0.1);
}
</style>
`})}),`
`,e.jsx(n.h3,{id:"card",children:"Card"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<style>
.card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
}

.card:hover {
  box-shadow: var(--shadow-md);
}

.dark .card:hover {
  box-shadow: none;
  border-color: var(--color-border-hover);
  background: var(--color-surface-hover);
}
</style>
`})}),`
`,e.jsx(n.h3,{id:"input",children:"Input"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<style>
.input {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
}

.input::placeholder {
  color: var(--color-text-tertiary);
}

.input:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(74, 222, 128, 0.1);
}

.dark .input:focus {
  box-shadow: 0 0 0 3px rgba(74, 222, 128, 0.2);
}
</style>
`})}),`
`,e.jsx(n.h3,{id:"datatable",children:"DataTable"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<style>
.table-row:hover {
  background: rgba(0, 0, 0, 0.02);
}

.dark .table-row:hover {
  background: rgba(255, 255, 255, 0.05);
}

.table-row-striped:nth-child(even) {
  background: rgba(0, 0, 0, 0.01);
}

.dark .table-row-striped:nth-child(even) {
  background: rgba(255, 255, 255, 0.02);
}
</style>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-charts--graphs",children:"📊 Charts & Graphs"}),`
`,e.jsx(n.h3,{id:"cores-de-gráficos",children:"Cores de Gráficos"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-javascript",children:`// ✅ Use cores vibrantes que funcionam em ambos os modos
const chartColors = {
  light: [
    '#3b82f6', // Blue
    '#22c55e', // Green
    '#f59e0b', // Orange
    '#8b5cf6', // Purple
    '#ec4899', // Pink
  ],
  dark: [
    '#60a5fa', // Blue (mais claro)
    '#4ade80', // Green (mais claro)
    '#fbbf24', // Orange (mais claro)
    '#a78bfa', // Purple (mais claro)
    '#f472b6', // Pink (mais claro)
  ]
}
`})}),`
`,e.jsx(n.h3,{id:"background-de-charts",children:"Background de Charts"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* Light Mode */
.chart-background {
  background: #ffffff;
}

/* Dark Mode */
.dark .chart-background {
  background: #1e293b; /* Surface color */
}
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-testing-checklist",children:"🔍 Testing Checklist"}),`
`,e.jsx(n.p,{children:"Antes de lançar dark mode, teste:"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"[ ] Todos os textos têm contraste adequado (4.5:1 mínimo)"}),`
`,e.jsx(n.li,{children:"[ ] Botões mantêm hierarquia visual"}),`
`,e.jsx(n.li,{children:"[ ] Borders são visíveis mas sutis"}),`
`,e.jsx(n.li,{children:"[ ] Shadows funcionam ou foram substituídos"}),`
`,e.jsx(n.li,{children:"[ ] Imagens e logos têm versões adequadas"}),`
`,e.jsx(n.li,{children:"[ ] Forms são legíveis e acessíveis"}),`
`,e.jsx(n.li,{children:"[ ] Tabelas mantêm zebra stripes visíveis"}),`
`,e.jsx(n.li,{children:"[ ] Charts usam cores apropriadas"}),`
`,e.jsx(n.li,{children:"[ ] Modals têm overlay suficiente"}),`
`,e.jsx(n.li,{children:"[ ] Focus states são visíveis"}),`
`,e.jsx(n.li,{children:"[ ] Loading states são claros"}),`
`,e.jsx(n.li,{children:"[ ] Toast notifications se destacam"}),`
`,e.jsx(n.li,{children:"[ ] Cores semânticas mantêm significado"}),`
`,e.jsx(n.li,{children:"[ ] Transição entre modos é suave"}),`
`,e.jsx(n.li,{children:"[ ] Preferência é persistida"}),`
`]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-unocss-dark-mode",children:"🎯 UnoCSS Dark Mode"}),`
`,e.jsx(n.h3,{id:"configuração",children:"Configuração"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-typescript",children:`// uno.config.ts
export default defineConfig({
  darkMode: 'class', // Usa .dark class
  theme: {
    colors: {
      primary: {
        DEFAULT: '#4ADE80',
        hover: '#22c55e'
      },
      background: {
        DEFAULT: '#ffffff',
        dark: '#0f172a'
      },
      surface: {
        DEFAULT: '#f9fafb',
        dark: '#1e293b'
      },
      text: {
        primary: {
          DEFAULT: '#1f2937',
          dark: '#f1f5f9'
        },
        secondary: {
          DEFAULT: '#6b7280',
          dark: '#cbd5e1'
        }
      }
    }
  }
})
`})}),`
`,e.jsx(n.h3,{id:"uso-em-componentes",children:"Uso em Componentes"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<template>
  <div class="bg-surface dark:bg-surface-dark text-text-primary dark:text-text-primary-dark">
    <h1 class="text-2xl font-bold">Hello</h1>
    <p class="text-text-secondary dark:text-text-secondary-dark">Description</p>
  </div>
</template>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-best-practices",children:"💡 Best Practices"}),`
`,e.jsxs(n.ol,{children:[`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Não inverta tudo"}),": Seja seletivo sobre o que muda"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Mantenha hierarquia"}),": Dark mode não é apenas inverter cores"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Teste contraste"}),": Use ferramentas como WebAIM Contrast Checker"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Use CSS Variables"}),": Facilita manutenção"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Reduza brilho"}),": Evite branco puro (#fff), use off-white (#f9fafb)"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Elevação sutil"}),": Use borders e backgrounds em vez de shadows"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Transição suave"}),": ",e.jsx(n.code,{children:"transition: background 0.3s, color 0.3s"})]}),`
`]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-anti-patterns",children:"🚫 Anti-Patterns"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`/* ❌ Não faça */
.dark * {
  filter: invert(1); /* Inverte TUDO */
}

/* ❌ Não use branco puro em dark mode */
.dark .text {
  color: #ffffff; /* Muito brilhante */
}

/* ❌ Não reutilize shadows de light mode */
.dark .card {
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1); /* Invisível */
}

/* ✅ Faça */
.dark .text {
  color: #f1f5f9; /* Off-white, mais suave */
}

.dark .card {
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(255, 255, 255, 0.05);
}
`})})]})}function x(r={}){const{wrapper:n}={...a(),...r.components};return n?e.jsx(n,{...r,children:e.jsx(s,{...r})}):s(r)}export{x as default};
