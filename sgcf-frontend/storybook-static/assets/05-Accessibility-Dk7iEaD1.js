import{j as e,M as o}from"./index-COwdE6jM.js";import{useMDXComponents as i}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function s(r){const n={a:"a",code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",ol:"ol",p:"p",pre:"pre",strong:"strong",ul:"ul",...i(),...r.components};return e.jsxs(e.Fragment,{children:[e.jsx(o,{title:"Style Guide/Accessibility"}),`
`,e.jsx(n.h1,{id:"accessibility-guidelines",children:"Accessibility Guidelines"}),`
`,e.jsxs(n.p,{children:["O Nordware Design System segue ",e.jsx(n.strong,{children:"WCAG 2.1 Level AA"})," para garantir acessibilidade universal."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-princípios-wcag",children:"🎯 Princípios WCAG"}),`
`,e.jsx(n.h3,{id:"1-perceptible-perceptível",children:"1. Perceptible (Perceptível)"}),`
`,e.jsx(n.p,{children:"Informação e componentes da UI devem ser apresentados de forma perceptível aos usuários."}),`
`,e.jsx(n.h3,{id:"2-operable-operável",children:"2. Operable (Operável)"}),`
`,e.jsx(n.p,{children:"Componentes da UI e navegação devem ser operáveis."}),`
`,e.jsx(n.h3,{id:"3-understandable-compreensível",children:"3. Understandable (Compreensível)"}),`
`,e.jsx(n.p,{children:"Informação e operação da UI devem ser compreensíveis."}),`
`,e.jsx(n.h3,{id:"4-robust-robusto",children:"4. Robust (Robusto)"}),`
`,e.jsx(n.p,{children:"Conteúdo deve ser robusto o suficiente para ser interpretado por diversos user agents, incluindo tecnologias assistivas."}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-color-contrast",children:"🎨 Color Contrast"}),`
`,e.jsx(n.h3,{id:"ratios-mínimos-wcag-aa",children:"Ratios Mínimos (WCAG AA)"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Texto Normal"}),": 4.5:1"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Texto Grande"})," (18pt+ ou 14pt+ bold): 3:1"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Componentes UI"}),": 3:1"]}),`
`]}),`
`,e.jsx(n.h3,{id:"nossos-contrastes",children:"Nossos Contrastes"}),`
`,e.jsxs(n.p,{children:["✅ ",e.jsx(n.strong,{children:"Primary Text on Background"}),`: 16:1 (Excelente)
✅ `,e.jsx(n.strong,{children:"Primary Button"}),`: 8.2:1 (Excelente)
✅ `,e.jsx(n.strong,{children:"Secondary Text"}),": 7:1 (Excelente)"]}),`
`,e.jsx(n.h3,{id:"ferramentas",children:"Ferramentas"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:e.jsx(n.a,{href:"https://webaim.org/resources/contrastchecker/",rel:"nofollow",children:"WebAIM Contrast Checker"})}),`
`,e.jsx(n.li,{children:"Chrome DevTools (Lighthouse)"}),`
`]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"️-keyboard-navigation",children:"⌨️ Keyboard Navigation"}),`
`,e.jsx(n.p,{children:"Todos os componentes são navegáveis por teclado:"}),`
`,e.jsxs(n.p,{children:[`| Ação | Tecla |
|------|-------|
| Navegar entre elementos | `,e.jsx(n.code,{children:"Tab"})," / ",e.jsx(n.code,{children:"Shift + Tab"}),` |
| Ativar botão/link | `,e.jsx(n.code,{children:"Enter"})," ou ",e.jsx(n.code,{children:"Space"}),` |
| Fechar modal/dropdown | `,e.jsx(n.code,{children:"Esc"}),` |
| Radio/Checkbox | `,e.jsx(n.code,{children:"Space"}),` |
| Tabs | `,e.jsx(n.code,{children:"Arrow Keys"}),` |
| Select/Dropdown | `,e.jsx(n.code,{children:"Arrow Up/Down"})," |"]}),`
`,e.jsx(n.h3,{id:"exemplo-de-implementação",children:"Exemplo de Implementação"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<button
  @click="handleClick"
  @keydown.enter="handleClick"
  @keydown.space.prevent="handleClick"
  aria-label="Descriptive label"
>
  Action
</button>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-focus-states",children:"🔍 Focus States"}),`
`,e.jsx(n.p,{children:"Todos os elementos interativos possuem estado de foco visível:"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`*:focus-visible {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
}
`})}),`
`,e.jsx(n.h3,{id:"personalizado-por-componente",children:"Personalizado por Componente"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`button:focus-visible {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
  box-shadow: 0 0 0 4px rgba(74, 222, 128, 0.1);
}
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"️-aria-labels",children:"🏷️ ARIA Labels"}),`
`,e.jsx(n.h3,{id:"quando-usar",children:"Quando Usar"}),`
`,e.jsxs(n.ol,{children:[`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Ícones sem texto"}),": Sempre adicione ",e.jsx(n.code,{children:"aria-label"})]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Botões de ação"}),": Descreva a ação claramente"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Form inputs"}),": Use ",e.jsx(n.code,{children:"aria-describedby"})," para helper text"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Modais"}),": Use ",e.jsx(n.code,{children:'role="dialog"'})," e ",e.jsx(n.code,{children:'aria-modal="true"'})]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Loading states"}),": Use ",e.jsx(n.code,{children:'aria-busy="true"'})]}),`
`]}),`
`,e.jsx(n.h3,{id:"exemplos",children:"Exemplos"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- Botão com ícone -->
<button aria-label="Delete item">
  <span class="i-carbon-trash-can"></span>
</button>

<!-- Input com erro -->
<input
  v-model="email"
  aria-describedby="email-error"
  aria-invalid="true"
/>
<span id="email-error">Email inválido</span>

<!-- Modal -->
<div
  role="dialog"
  aria-modal="true"
  aria-labelledby="modal-title"
>
  <h2 id="modal-title">Confirm Action</h2>
</div>

<!-- Loading -->
<div aria-busy="true" aria-live="polite">
  <Spinner />
  Loading...
</div>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-screen-readers",children:"📱 Screen Readers"}),`
`,e.jsx(n.h3,{id:"texto-alternativo",children:"Texto Alternativo"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- Imagens -->
<img
  src="/avatar.jpg"
  alt="Profile picture of John Doe"
/>

<!-- Ícones decorativos -->
<span class="i-carbon-user" aria-hidden="true"></span>

<!-- Conteúdo sr-only -->
<span class="sr-only">
  Current page: Dashboard
</span>
`})}),`
`,e.jsx(n.h3,{id:"landmarks",children:"Landmarks"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<header role="banner">...</header>
<nav role="navigation">...</nav>
<main role="main">...</main>
<aside role="complementary">...</aside>
<footer role="contentinfo">...</footer>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-motion--animation",children:"🎬 Motion & Animation"}),`
`,e.jsx(n.h3,{id:"reduzir-movimento",children:"Reduzir Movimento"}),`
`,e.jsxs(n.p,{children:["Respeitamos a preferência ",e.jsx(n.code,{children:"prefers-reduced-motion"}),":"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`@media (prefers-reduced-motion: reduce) {
  * {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
`})}),`
`,e.jsx(n.h3,{id:"implementação",children:"Implementação"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<style>
.button {
  transition: all var(--duration-fast);
}

@media (prefers-reduced-motion: reduce) {
  .button {
    transition: none;
  }
}
</style>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-form-accessibility",children:"📋 Form Accessibility"}),`
`,e.jsx(n.h3,{id:"labels-obrigatórios",children:"Labels Obrigatórios"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- ✅ Correto -->
<label for="email">Email</label>
<input id="email" type="email" required />

<!-- ❌ Errado -->
<input type="email" placeholder="Email" />
`})}),`
`,e.jsx(n.h3,{id:"indicadores-de-required",children:"Indicadores de Required"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<label for="name">
  Name
  <abbr title="required" aria-label="required">*</abbr>
</label>
<input id="name" required />
`})}),`
`,e.jsx(n.h3,{id:"mensagens-de-erro",children:"Mensagens de Erro"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<input
  v-model="email"
  :aria-invalid="hasError"
  :aria-describedby="hasError ? 'email-error' : undefined"
/>
<span
  v-if="hasError"
  id="email-error"
  role="alert"
>
  Please enter a valid email
</span>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-testing-tools",children:"🧪 Testing Tools"}),`
`,e.jsx(n.h3,{id:"automatizados",children:"Automatizados"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Storybook Accessibility Addon"})," (incluído no projeto)"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Lighthouse"})," (Chrome DevTools)"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"axe DevTools"})," (extensão browser)"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"WAVE"})," (extensão browser)"]}),`
`]}),`
`,e.jsx(n.h3,{id:"manuais",children:"Manuais"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Keyboard Navigation"}),": Navegue apenas com teclado"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Screen Reader"}),": NVDA (Windows), VoiceOver (Mac), TalkBack (Android)"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Zoom"}),": Teste com zoom 200%+"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.strong,{children:"Color Blindness"}),": Simuladores de daltonismo"]}),`
`]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-checklist-de-acessibilidade",children:"✅ Checklist de Acessibilidade"}),`
`,e.jsx(n.p,{children:"Antes de fazer deploy:"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"[ ] Contraste de cores AA compliant"}),`
`,e.jsx(n.li,{children:"[ ] Navegação por teclado funcional"}),`
`,e.jsx(n.li,{children:"[ ] Focus states visíveis"}),`
`,e.jsx(n.li,{children:"[ ] ARIA labels apropriados"}),`
`,e.jsx(n.li,{children:"[ ] Alt text em imagens"}),`
`,e.jsx(n.li,{children:"[ ] Formulários com labels"}),`
`,e.jsx(n.li,{children:"[ ] Mensagens de erro claras"}),`
`,e.jsxs(n.li,{children:["[ ] Sem elementos com ",e.jsx(n.code,{children:"tabindex"})," positivo"]}),`
`,e.jsxs(n.li,{children:["[ ] ",e.jsx(n.code,{children:"prefers-reduced-motion"})," respeitado"]}),`
`,e.jsx(n.li,{children:"[ ] Testado com screen reader"}),`
`,e.jsx(n.li,{children:"[ ] Lighthouse score 90+"}),`
`]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-recursos",children:"📚 Recursos"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:e.jsx(n.a,{href:"https://www.w3.org/WAI/WCAG21/quickref/",rel:"nofollow",children:"WCAG 2.1 Guidelines"})}),`
`,e.jsx(n.li,{children:e.jsx(n.a,{href:"https://developer.mozilla.org/en-US/docs/Web/Accessibility",rel:"nofollow",children:"MDN Accessibility"})}),`
`,e.jsx(n.li,{children:e.jsx(n.a,{href:"https://webaim.org/",rel:"nofollow",children:"WebAIM"})}),`
`,e.jsx(n.li,{children:e.jsx(n.a,{href:"https://www.a11yproject.com/",rel:"nofollow",children:"A11y Project"})}),`
`]})]})}function j(r={}){const{wrapper:n}={...i(),...r.components};return n?e.jsx(n,{...r,children:e.jsx(s,{...r})}):s(r)}export{j as default};
