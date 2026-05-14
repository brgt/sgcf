import{j as e,M as r}from"./index-COwdE6jM.js";import{useMDXComponents as i}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function o(s){const n={code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",ol:"ol",p:"p",pre:"pre",strong:"strong",ul:"ul",...i(),...s.components};return e.jsxs(e.Fragment,{children:[e.jsx(r,{title:"Style Guide/Welcome"}),`
`,e.jsx(n.h1,{id:"nordware-design-system",children:"Nordware Design System"}),`
`,e.jsx(n.p,{children:"Bem-vindo ao Design System oficial da plataforma Nordware! Este é um sistema de design completo, modular e escalável, construído com Vue 3, TypeScript e UnoCSS."}),`
`,e.jsx(n.h2,{id:"-objetivo",children:"🎯 Objetivo"}),`
`,e.jsx(n.p,{children:"O Nordware Design System foi criado para:"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Consistência"}),": Garantir uma experiência visual e funcional uniforme em toda a plataforma"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Eficiência"}),": Acelerar o desenvolvimento com componentes reutilizáveis e bem documentados"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Qualidade"}),": Manter altos padrões de acessibilidade (WCAG 2.1 AA) e performance"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Escalabilidade"}),": Permitir fácil expansão e manutenção do sistema"]}),`
`]}),`
`,e.jsx(n.h2,{id:"️-arquitetura",children:"🏗️ Arquitetura"}),`
`,e.jsxs(n.p,{children:["Seguimos a metodologia ",e.jsx(n.strong,{children:"Atomic Design"})," de Brad Frost:"]}),`
`,e.jsx(n.h3,{id:"atoms-elementos-básicos",children:"Atoms (Elementos Básicos)"}),`
`,e.jsx(n.p,{children:"Componentes fundamentais e indivisíveis como botões, inputs, badges, etc."}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"10 componentes"}),": Button, Input, Badge, HamburgerButton, Avatar, Checkbox, Radio, Textarea, Spinner, Progress"]}),`
`,e.jsx(n.h3,{id:"molecules-combinações-simples",children:"Molecules (Combinações Simples)"}),`
`,e.jsx(n.p,{children:"Grupos de atoms trabalhando juntos como formulários, cards, menus, etc."}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"14 componentes"}),": MobileDrawer, BottomTabBar, Select, FormField, EmptyState, Skeleton, Tooltip, Dropdown, Card, DataTable, Alert, Tabs, Breadcrumb, Pagination"]}),`
`,e.jsx(n.h3,{id:"organisms-componentes-complexos",children:"Organisms (Componentes Complexos)"}),`
`,e.jsx(n.p,{children:"Seções complexas da interface que combinam molecules e atoms."}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"1 componente"}),": Modal"]}),`
`,e.jsx(n.h3,{id:"templates-layouts",children:"Templates (Layouts)"}),`
`,e.jsx(n.p,{children:"Estruturas de página que definem o layout geral."}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"3 componentes"}),": PageHeader, PageLayout, DashboardGrid"]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-quick-start",children:"🚀 Quick Start"}),`
`,e.jsx(n.h3,{id:"instalação",children:"Instalação"}),`
`,e.jsx(n.p,{children:"Os componentes já estão disponíveis no projeto. Importe-os assim:"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-typescript",children:`// Import individual (tree-shaking)
import { Button } from '@shared/ui/atoms/Button'

// Import múltiplos da mesma camada
import { Button, Input, Badge } from '@shared/ui/atoms'

// Import de todas as camadas
import { Button, Card, Modal, PageHeader } from '@shared/ui'
`})}),`
`,e.jsx(n.h3,{id:"exemplo-de-uso",children:"Exemplo de Uso"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<script setup lang="ts">
import { ref } from 'vue'
import { Button, Input, Card, Modal } from '@shared/ui'

const email = ref('')
const isModalOpen = ref(false)
<\/script>

<template>
  <Card title="Login" padding="lg">
    <Input
      v-model="email"
      label="Email"
      type="email"
      placeholder="seu@email.com"
    />

    <template #footer>
      <Button variant="primary" @click="isModalOpen = true">
        Login
      </Button>
    </template>
  </Card>

  <Modal v-model="isModalOpen" title="Bem-vindo!" size="sm">
    <p>Login realizado com sucesso!</p>
  </Modal>
</template>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-navegação",children:"📚 Navegação"}),`
`,e.jsx(n.p,{children:"Explore as seções do Style Guide:"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Design Tokens"}),": Cores, tipografia, espaçamento, sombras e mais"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Iconography"}),": Sistema de ícones Carbon e Material Design"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Grid System"}),": Sistema de grid responsivo"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Accessibility"}),": Guidelines WCAG 2.1 AA"]}),`
`]}),`
`,e.jsx(n.p,{children:"Explore os componentes organizados por categoria no menu lateral."}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-tecnologias",children:"🎨 Tecnologias"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Vue 3"}),": Composition API, script setup, TypeScript"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"UnoCSS"}),": Atomic CSS com design tokens"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"TypeScript"}),": 100% type-safe"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Storybook"}),": Documentação interativa"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"WCAG 2.1 AA"}),": Acessibilidade completa"]}),`
`]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-exportação",children:"📦 Exportação"}),`
`,e.jsxs(n.p,{children:["Este Design System pode ser exportado para outros projetos Vue 3. Consulte o arquivo ",e.jsx(n.code,{children:"DESIGN_SYSTEM_EXPORT_GUIDE.md"})," na raiz do projeto para instruções detalhadas."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-contribuindo",children:"🤝 Contribuindo"}),`
`,e.jsx(n.p,{children:"Para adicionar novos componentes ou modificar existentes:"}),`
`,e.jsxs(n.ol,{children:[`
`,e.jsx(n.li,{children:"Siga a estrutura Atomic Design"}),`
`,e.jsx(n.li,{children:"Mantenha 100% de type safety com TypeScript"}),`
`,e.jsx(n.li,{children:"Use design tokens do sistema (CSS variables)"}),`
`,e.jsx(n.li,{children:"Garanta WCAG 2.1 AA compliance"}),`
`,e.jsx(n.li,{children:"Crie stories no Storybook"}),`
`,e.jsx(n.li,{children:"Documente props, events e slots"}),`
`]}),`
`,e.jsx(n.hr,{}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Versão"}),`: 2.0
`,e.jsx(n.strong,{children:"Última atualização"}),`: Janeiro 2025
`,e.jsx(n.strong,{children:"Mantido por"}),": Nordware Team"]})]})}function h(s={}){const{wrapper:n}={...i(),...s.components};return n?e.jsx(n,{...s,children:e.jsx(o,{...s})}):o(s)}export{h as default};
