import{j as n,M as r}from"./index-COwdE6jM.js";import{useMDXComponents as a}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function s(i){const e={code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",p:"p",pre:"pre",strong:"strong",ul:"ul",...a(),...i.components};return n.jsxs(n.Fragment,{children:[n.jsx(r,{title:"Style Guide/Motion & Animation"}),`
`,n.jsx(e.h1,{id:"motion--animation",children:"Motion & Animation"}),`
`,n.jsx(e.p,{children:"Guia completo de animações e transições."}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"️-durations",children:"⏱️ Durations"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-css",children:`--duration-fast: 150ms   /* Micro-interactions */
--duration-base: 300ms   /* Standard transitions */
--duration-slow: 500ms   /* Complex animations */
`})}),`
`,n.jsx(e.h3,{id:"quando-usar",children:"Quando Usar"}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Fast (150ms)"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"Hover states"}),`
`,n.jsx(e.li,{children:"Button press"}),`
`,n.jsx(e.li,{children:"Checkbox/Radio toggle"}),`
`,n.jsx(e.li,{children:"Tooltip appear"}),`
`]}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Base (300ms)"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"Modal open/close"}),`
`,n.jsx(e.li,{children:"Dropdown menu"}),`
`,n.jsx(e.li,{children:"Tab switching"}),`
`,n.jsx(e.li,{children:"Drawer slide"}),`
`]}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Slow (500ms)"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"Page transitions"}),`
`,n.jsx(e.li,{children:"Skeleton to content"}),`
`,n.jsx(e.li,{children:"Large element movements"}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-easing-functions",children:"🎭 Easing Functions"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-css",children:`--motion-smooth: cubic-bezier(0.4, 0, 0.2, 1)  /* Default */
--motion-bounce: cubic-bezier(0.68, -0.55, 0.265, 1.55)  /* Playful */
--motion-ease-in: cubic-bezier(0.4, 0, 1, 1)  /* Decelerate */
--motion-ease-out: cubic-bezier(0, 0, 0.2, 1)  /* Accelerate */
`})}),`
`,n.jsx(e.h3,{id:"quando-usar-1",children:"Quando Usar"}),`
`,n.jsxs(e.p,{children:[n.jsx(e.strong,{children:"Smooth"})," (Padrão)"]}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"Transições gerais"}),`
`,n.jsx(e.li,{children:"Hover effects"}),`
`,n.jsx(e.li,{children:"Color changes"}),`
`]}),`
`,n.jsxs(e.p,{children:[n.jsx(e.strong,{children:"Bounce"})," (Cuidado!)"]}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"Success confirmations"}),`
`,n.jsx(e.li,{children:"Checkmark appear"}),`
`,n.jsx(e.li,{children:"Playful interactions"}),`
`]}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Ease-In"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"Elements leaving screen"}),`
`,n.jsx(e.li,{children:"Closing modals"}),`
`,n.jsx(e.li,{children:"Dismissing toasts"}),`
`]}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Ease-Out"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"Elements entering screen"}),`
`,n.jsx(e.li,{children:"Opening modals"}),`
`,n.jsx(e.li,{children:"Loading states"}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-animation-types",children:"🎬 Animation Types"}),`
`,n.jsx(e.h3,{id:"1-fade",children:"1. Fade"}),`
`,n.jsx(e.p,{children:"Para aparecer/desaparecer elementos"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<style>
.fade-enter-active,
.fade-leave-active {
  transition: opacity var(--duration-base);
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>

<Transition name="fade">
  <div v-if="show">Content</div>
</Transition>
`})}),`
`,n.jsx(e.h3,{id:"2-slide",children:"2. Slide"}),`
`,n.jsx(e.p,{children:"Para elementos que entram/saem lateralmente"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<style>
.slide-enter-active,
.slide-leave-active {
  transition: transform var(--duration-base) var(--motion-smooth);
}

.slide-enter-from {
  transform: translateX(-100%);
}

.slide-leave-to {
  transform: translateX(100%);
}
</style>
`})}),`
`,n.jsx(e.h3,{id:"3-scale",children:"3. Scale"}),`
`,n.jsx(e.p,{children:"Para modals e pop-ups"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<style>
.scale-enter-active,
.scale-leave-active {
  transition: all var(--duration-base) var(--motion-smooth);
}

.scale-enter-from,
.scale-leave-to {
  opacity: 0;
  transform: scale(0.95);
}
</style>
`})}),`
`,n.jsx(e.h3,{id:"4-expandcollapse",children:"4. Expand/Collapse"}),`
`,n.jsx(e.p,{children:"Para accordions e dropdowns"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<style>
.expand-enter-active,
.expand-leave-active {
  transition: all var(--duration-base);
  overflow: hidden;
}

.expand-enter-from,
.expand-leave-to {
  max-height: 0;
  opacity: 0;
}

.expand-enter-to,
.expand-leave-from {
  max-height: 500px;
  opacity: 1;
}
</style>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-por-componente",children:"🎯 Por Componente"}),`
`,n.jsx(e.h3,{id:"button",children:"Button"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<style>
.button {
  transition: all var(--duration-fast) var(--motion-smooth);
}

.button:hover {
  transform: translateY(-1px);
  box-shadow: var(--shadow-md);
}

.button:active {
  transform: translateY(0);
}
</style>
`})}),`
`,n.jsx(e.h3,{id:"card",children:"Card"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<style>
.card {
  transition: all var(--duration-base) var(--motion-smooth);
}

.card:hover {
  transform: translateY(-4px);
  box-shadow: var(--shadow-lg);
}
</style>
`})}),`
`,n.jsx(e.h3,{id:"modal",children:"Modal"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Overlay fade + Modal scale -->
<Transition name="modal">
  <div v-if="show" class="modal-overlay">
    <div class="modal">Content</div>
  </div>
</Transition>

<style>
.modal-enter-active,
.modal-leave-active {
  transition: opacity var(--duration-base);
}

.modal-enter-active .modal,
.modal-leave-active .modal {
  transition: transform var(--duration-base) var(--motion-smooth);
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-from .modal,
.modal-leave-to .modal {
  transform: scale(0.95);
}
</style>
`})}),`
`,n.jsx(e.h3,{id:"toast",children:"Toast"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Slide in from right -->
<style>
.toast-enter-active,
.toast-leave-active {
  transition: all var(--duration-base) var(--motion-smooth);
}

.toast-enter-from,
.toast-leave-to {
  transform: translateX(100%);
  opacity: 0;
}
</style>
`})}),`
`,n.jsx(e.h3,{id:"skeleton--content",children:"Skeleton → Content"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<Transition name="fade" mode="out-in">
  <Skeleton v-if="loading" />
  <div v-else>Actual content</div>
</Transition>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-choreography",children:"🎪 Choreography"}),`
`,n.jsx(e.p,{children:"Quando animar múltiplos elementos, use sequência"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<TransitionGroup name="list" tag="div">
  <Card v-for="(item, i) in items" :key="item.id" :style="{ transitionDelay: \`\${i * 50}ms\` }">
    {{ item.name }}
  </Card>
</TransitionGroup>

<style>
.list-enter-active {
  transition: all 0.3s ease;
}

.list-enter-from {
  opacity: 0;
  transform: translateY(20px);
}
</style>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-reduced-motion",children:"♿ Reduced Motion"}),`
`,n.jsxs(e.p,{children:["SEMPRE respeite ",n.jsx(e.code,{children:"prefers-reduced-motion"})]}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-css",children:`@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
`})}),`
`,n.jsx(e.h3,{id:"implementação-em-componentes",children:"Implementação em Componentes"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<style>
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
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-performance",children:"🚀 Performance"}),`
`,n.jsx(e.h3,{id:"use-gpu-acceleration",children:"Use GPU Acceleration"}),`
`,n.jsx(e.p,{children:"Anime apenas propriedades que usam GPU:"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.code,{children:"transform"})," ✅"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.code,{children:"opacity"})," ✅"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.code,{children:"filter"})," ✅"]}),`
`]}),`
`,n.jsx(e.p,{children:"Evite animar:"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.code,{children:"width"}),"/",n.jsx(e.code,{children:"height"})," ❌"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.code,{children:"margin"}),"/",n.jsx(e.code,{children:"padding"})," ❌"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.code,{children:"top"}),"/",n.jsx(e.code,{children:"left"})," ❌"]}),`
`]}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-css",children:`/* ✅ Bom - GPU accelerated */
.element {
  transition: transform 0.3s, opacity 0.3s;
}

.element:hover {
  transform: translateY(-4px);
  opacity: 0.9;
}

/* ❌ Ruim - Causes reflow */
.element {
  transition: margin-top 0.3s;
}

.element:hover {
  margin-top: -4px;
}
`})}),`
`,n.jsx(e.h3,{id:"will-change",children:"Will-Change"}),`
`,n.jsx(e.p,{children:"Para animações complexas frequentes"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-css",children:`.frequently-animated {
  will-change: transform, opacity;
}
`})}),`
`,n.jsxs(e.p,{children:["⚠️ ",n.jsx(e.strong,{children:"Cuidado"}),": Use apenas quando necessário, remove após animação."]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-animation-checklist",children:"✅ Animation Checklist"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"[ ] Duração apropriada (150-500ms)"}),`
`,n.jsx(e.li,{children:"[ ] Easing suave (cubic-bezier)"}),`
`,n.jsx(e.li,{children:"[ ] Apenas transform/opacity quando possível"}),`
`,n.jsx(e.li,{children:"[ ] Reduced motion implementado"}),`
`,n.jsx(e.li,{children:"[ ] Não animar largura/altura"}),`
`,n.jsx(e.li,{children:"[ ] Sequência lógica (choreography)"}),`
`,n.jsx(e.li,{children:"[ ] 60fps garantido"}),`
`,n.jsx(e.li,{children:"[ ] Will-change removido após uso"}),`
`,n.jsx(e.li,{children:"[ ] Não animar durante scroll"}),`
`,n.jsx(e.li,{children:"[ ] Loading states animados"}),`
`]})]})}function x(i={}){const{wrapper:e}={...a(),...i.components};return e?n.jsx(e,{...i,children:n.jsx(s,{...i})}):s(i)}export{x as default};
