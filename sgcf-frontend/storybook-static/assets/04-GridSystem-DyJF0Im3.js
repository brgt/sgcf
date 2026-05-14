import{j as e,M as l}from"./index-COwdE6jM.js";import{useMDXComponents as d}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function s(n){const r={code:"code",div:"div",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",ol:"ol",p:"p",pre:"pre",strong:"strong",ul:"ul",...d(),...n.components};return e.jsxs(e.Fragment,{children:[e.jsx(l,{title:"Style Guide/Grid System"}),`
`,e.jsx(r.h1,{id:"grid-system",children:"Grid System"}),`
`,e.jsx(r.p,{children:"Sistema de grid flexível e responsivo para layouts consistentes."}),`
`,e.jsx(r.hr,{}),`
`,e.jsx(r.h2,{id:"-container-system",children:"📐 Container System"}),`
`,e.jsx(r.h3,{id:"max-width-containers",children:"Max Width Containers"}),`
`,e.jsx(r.pre,{children:e.jsx(r.code,{className:"language-css",children:`container-narrow:  max-width: 768px  (ideal para texto)
container-default: max-width: 1024px (padrão)
container-wide:    max-width: 1280px (layouts amplos)
container-full:    max-width: 100%   (full width)
`})}),`
`,e.jsx(r.h3,{id:"exemplo",children:"Exemplo"}),`
`,e.jsx(r.pre,{children:e.jsx(r.code,{className:"language-vue",children:`<template>
  <div class="container-default mx-auto px-4">
    <h1>Conteúdo Centralizado</h1>
  </div>
</template>
`})}),`
`,e.jsx(r.hr,{}),`
`,e.jsx(r.h2,{id:"-grid-columns",children:"🔢 Grid Columns"}),`
`,e.jsx(r.p,{children:"Sistema de 12 colunas flexível:"}),`
`,e.jsx(r.pre,{children:e.jsx(r.code,{className:"language-css",children:`grid-cols-1   /* 1 coluna */
grid-cols-2   /* 2 colunas */
grid-cols-3   /* 3 colunas */
grid-cols-4   /* 4 colunas */
grid-cols-6   /* 6 colunas */
grid-cols-12  /* 12 colunas */
`})}),`
`,e.jsx(r.h3,{id:"grid-básico",children:"Grid Básico"}),`
`,e.jsx(r.pre,{children:e.jsx(r.code,{className:"language-vue",children:`<template>
  <div class="grid grid-cols-3 gap-4">
    <div>Item 1</div>
    <div>Item 2</div>
    <div>Item 3</div>
  </div>
</template>
`})}),`
`,e.jsx(r.hr,{}),`
`,e.jsx(r.h2,{id:"-responsive-grid",children:"📱 Responsive Grid"}),`
`,e.jsx(r.p,{children:"Mobile-first approach com breakpoints:"}),`
`,e.jsx(r.pre,{children:e.jsx(r.code,{className:"language-vue",children:`<template>
  <!-- 1 col em mobile, 2 em tablet, 4 em desktop -->
  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
    <div v-for="i in 8" :key="i">Card {{ i }}</div>
  </div>
</template>
`})}),`
`,e.jsx(r.h3,{id:"breakpoints",children:"Breakpoints"}),`
`,e.jsxs(r.p,{children:[`| Breakpoint | Min Width | Típico Use Case |
|------------|-----------|-----------------|
| `,e.jsx(r.code,{children:"sm:"}),`      | 640px     | Mobile landscape |
| `,e.jsx(r.code,{children:"md:"}),`      | 768px     | Tablet portrait |
| `,e.jsx(r.code,{children:"lg:"}),`      | 1024px    | Tablet landscape / Desktop |
| `,e.jsx(r.code,{children:"xl:"}),`      | 1280px    | Desktop |
| `,e.jsx(r.code,{children:"2xl:"}),"     | 1536px    | Large desktop |"]}),`
`,e.jsx(r.hr,{}),`
`,e.jsx(r.h2,{id:"-gap-utilities",children:"🎨 Gap Utilities"}),`
`,e.jsx(r.p,{children:"Espaçamento entre grid items:"}),`
`,e.jsx(r.pre,{children:e.jsx(r.code,{className:"language-css",children:`gap-2   /* 0.5rem (8px) */
gap-4   /* 1rem (16px) */
gap-6   /* 1.5rem (24px) */
gap-8   /* 2rem (32px) */
`})}),`
`,e.jsx(r.hr,{}),`
`,e.jsx(r.h2,{id:"-dashboardgrid-component",children:"🔧 DashboardGrid Component"}),`
`,e.jsxs(r.p,{children:["Para dashboards complexos, use o componente ",e.jsx(r.code,{children:"DashboardGrid"}),":"]}),`
`,e.jsx(r.pre,{children:e.jsx(r.code,{className:"language-vue",children:`<script setup>
import { DashboardGrid, Card } from '@shared/ui'
<\/script>

<template>
  <DashboardGrid :columns="3" gap="md" :responsive="true">
    <Card v-for="i in 6" :key="i" padding="lg">
      Card {{ i }}
    </Card>
  </DashboardGrid>
</template>
`})}),`
`,e.jsx(r.h3,{id:"props",children:"Props"}),`
`,e.jsxs(r.ul,{children:[`
`,e.jsxs(r.li,{children:[e.jsx(r.code,{children:"columns"}),": 1 | 2 | 3 | 4 | 6 | 12"]}),`
`,e.jsxs(r.li,{children:[e.jsx(r.code,{children:"gap"}),": 'none' | 'sm' | 'md' | 'lg'"]}),`
`,e.jsxs(r.li,{children:[e.jsx(r.code,{children:"responsive"}),": boolean (adapta automaticamente)"]}),`
`]}),`
`,e.jsx(r.hr,{}),`
`,e.jsx(r.h2,{id:"-grid-examples",children:"📊 Grid Examples"}),`
`,e.jsx(r.h3,{id:"dashboard-layout-4-colunas",children:"Dashboard Layout (4 colunas)"}),`
`,e.jsx("div",{style:{display:"grid",gridTemplateColumns:"repeat(4, 1fr)",gap:"1rem",marginBottom:"2rem"},children:[1,2,3,4].map(i=>e.jsxs(r.div,{style:{padding:"2rem",background:"var(--color-surface)",border:"1px solid var(--color-border)",borderRadius:"8px",textAlign:"center"},children:["Metric ",i]},i))}),`
`,e.jsx(r.pre,{children:e.jsx(r.code,{className:"language-vue",children:`<div class="grid grid-cols-4 gap-4">
  <Card v-for="i in 4" :key="i">Metric {{ i }}</Card>
</div>
`})}),`
`,e.jsx(r.h3,{id:"mixed-sizes-layout",children:"Mixed Sizes Layout"}),`
`,e.jsxs("div",{style:{display:"grid",gridTemplateColumns:"repeat(3, 1fr)",gap:"1rem",marginBottom:"2rem"},children:[e.jsx("div",{style:{gridColumn:"span 2",padding:"2rem",background:"var(--color-surface)",border:"1px solid var(--color-border)",borderRadius:"8px",textAlign:"center"},children:e.jsx(r.p,{children:"Wide Card (2 cols)"})}),e.jsx("div",{style:{padding:"2rem",background:"var(--color-surface)",border:"1px solid var(--color-border)",borderRadius:"8px",textAlign:"center"},children:e.jsx(r.p,{children:"Regular"})}),e.jsx("div",{style:{padding:"2rem",background:"var(--color-surface)",border:"1px solid var(--color-border)",borderRadius:"8px",textAlign:"center"},children:e.jsx(r.p,{children:"Regular"})}),e.jsx("div",{style:{gridColumn:"span 2",padding:"2rem",background:"var(--color-surface)",border:"1px solid var(--color-border)",borderRadius:"8px",textAlign:"center"},children:e.jsx(r.p,{children:"Wide Card (2 cols)"})})]}),`
`,e.jsx(r.pre,{children:e.jsx(r.code,{className:"language-vue",children:`<div class="grid grid-cols-3 gap-4">
  <div class="col-span-2">Wide Card</div>
  <div>Regular</div>
  <div>Regular</div>
  <div class="col-span-2">Wide Card</div>
</div>
`})}),`
`,e.jsx(r.hr,{}),`
`,e.jsx(r.h2,{id:"-best-practices",children:"✅ Best Practices"}),`
`,e.jsxs(r.ol,{children:[`
`,e.jsxs(r.li,{children:[e.jsx(r.strong,{children:"Mobile First"}),": Sempre comece com grid simples para mobile"]}),`
`,e.jsxs(r.li,{children:[e.jsx(r.strong,{children:"Consistência"}),": Use gaps padronizados (4, 6, 8)"]}),`
`,e.jsxs(r.li,{children:[e.jsx(r.strong,{children:"Responsividade"}),": Teste em todos os breakpoints"]}),`
`,e.jsxs(r.li,{children:[e.jsx(r.strong,{children:"Performance"}),": Evite grids muito complexos (use DashboardGrid)"]}),`
`,e.jsxs(r.li,{children:[e.jsx(r.strong,{children:"Acessibilidade"}),": Ordem visual deve seguir ordem do DOM"]}),`
`]})]})}function m(n={}){const{wrapper:r}={...d(),...n.components};return r?e.jsx(r,{...n,children:e.jsx(s,{...n})}):s(n)}export{m as default};
