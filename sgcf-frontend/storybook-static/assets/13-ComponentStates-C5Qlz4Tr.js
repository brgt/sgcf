import{j as n,M as o}from"./index-COwdE6jM.js";import{useMDXComponents as t}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function a(r){const e={code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",p:"p",pre:"pre",ul:"ul",...t(),...r.components};return n.jsxs(n.Fragment,{children:[n.jsx(o,{title:"Style Guide/Component States"}),`
`,n.jsx(e.h1,{id:"component-states",children:"Component States"}),`
`,n.jsx(e.p,{children:"Documentação visual de todos os estados dos componentes."}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-estados-universais",children:"🎯 Estados Universais"}),`
`,n.jsx(e.p,{children:"Todos os componentes interativos devem suportar estes estados:"}),`
`,n.jsx("div",{style:{width:"100%",overflowX:"auto",marginTop:"1.5rem",marginBottom:"1.5rem"},children:n.jsxs("table",{style:{width:"100%",borderCollapse:"collapse",fontSize:"0.9375rem"},children:[n.jsx("thead",{children:n.jsxs("tr",{style:{backgroundColor:"var(--color-surface)",borderBottom:"2px solid var(--color-border)"},children:[n.jsx("th",{style:{padding:"0.875rem 1rem",textAlign:"left",fontWeight:600,color:"#FFFFFF"},children:"Estado"}),n.jsx("th",{style:{padding:"0.875rem 1rem",textAlign:"left",fontWeight:600,color:"#FFFFFF"},children:"Quando"}),n.jsx("th",{style:{padding:"0.875rem 1rem",textAlign:"left",fontWeight:600,color:"#FFFFFF"},children:"Propriedades Visuais"})]})}),n.jsxs("tbody",{children:[n.jsxs("tr",{style:{borderBottom:"1px solid var(--color-border)"},children:[n.jsx("td",{style:{padding:"0.875rem 1rem"},children:n.jsx("strong",{children:"Default"})}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Estado inicial"}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Cores padrão, sem interação"})]}),n.jsxs("tr",{style:{borderBottom:"1px solid var(--color-border)"},children:[n.jsx("td",{style:{padding:"0.875rem 1rem"},children:n.jsx("strong",{children:"Hover"})}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Mouse sobre o elemento"}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Background mais escuro, cursor pointer"})]}),n.jsxs("tr",{style:{borderBottom:"1px solid var(--color-border)"},children:[n.jsx("td",{style:{padding:"0.875rem 1rem"},children:n.jsx("strong",{children:"Focus"})}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Elemento focado (Tab)"}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Ring azul, outline visível"})]}),n.jsxs("tr",{style:{borderBottom:"1px solid var(--color-border)"},children:[n.jsx("td",{style:{padding:"0.875rem 1rem"},children:n.jsx("strong",{children:"Active"})}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Elemento sendo clicado"}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Background ainda mais escuro"})]}),n.jsxs("tr",{style:{borderBottom:"1px solid var(--color-border)"},children:[n.jsx("td",{style:{padding:"0.875rem 1rem"},children:n.jsx("strong",{children:"Disabled"})}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Elemento não disponível"}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Opacidade 50%, cursor not-allowed"})]}),n.jsxs("tr",{style:{borderBottom:"1px solid var(--color-border)"},children:[n.jsx("td",{style:{padding:"0.875rem 1rem"},children:n.jsx("strong",{children:"Loading"})}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Processando ação"}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Spinner, disabled temporário"})]}),n.jsxs("tr",{style:{borderBottom:"1px solid var(--color-border)"},children:[n.jsx("td",{style:{padding:"0.875rem 1rem"},children:n.jsx("strong",{children:"Error"})}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Validação falhou"}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Borda vermelha, texto de erro"})]}),n.jsxs("tr",{children:[n.jsx("td",{style:{padding:"0.875rem 1rem"},children:n.jsx("strong",{children:"Success"})}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Ação bem-sucedida"}),n.jsx("td",{style:{padding:"0.875rem 1rem",color:"#CBD5E1"},children:"Borda/ícone verde"})]})]})]})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-button-states",children:"🔘 Button States"}),`
`,n.jsx(e.h3,{id:"primary-button",children:"Primary Button"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Default -->
<Button variant="primary">Save changes</Button>
<!-- Background: primary, Text: white -->

<!-- Hover -->
<Button variant="primary" class="hover">Save changes</Button>
<!-- Background: primary-hover, Transform: translateY(-1px) -->

<!-- Focus -->
<Button variant="primary" class="focus">Save changes</Button>
<!-- Ring: ring-2 ring-primary ring-offset-2 -->

<!-- Active -->
<Button variant="primary" class="active">Save changes</Button>
<!-- Transform: translateY(0), Shadow: none -->

<!-- Disabled -->
<Button variant="primary" :disabled="true">Save changes</Button>
<!-- Opacity: 0.5, Cursor: not-allowed -->

<!-- Loading -->
<Button variant="primary" :loading="true">Saving...</Button>
<!-- Spinner animado, Disabled: true -->
`})}),`
`,n.jsx(e.h3,{id:"secondary-button",children:"Secondary Button"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Default -->
<Button variant="secondary">Cancel</Button>
<!-- Background: gray-200, Text: gray-800 -->

<!-- Hover -->
<Button variant="secondary" class="hover">Cancel</Button>
<!-- Background: gray-300 -->

<!-- Focus -->
<Button variant="secondary" class="focus">Cancel</Button>
<!-- Ring: ring-2 ring-gray-400 ring-offset-2 -->
`})}),`
`,n.jsx(e.h3,{id:"ghost-button",children:"Ghost Button"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Default -->
<Button variant="ghost">Learn more</Button>
<!-- Background: transparent, Text: gray-700 -->

<!-- Hover -->
<Button variant="ghost" class="hover">Learn more</Button>
<!-- Background: gray-100 -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-input-states",children:"📝 Input States"}),`
`,n.jsx(e.h3,{id:"text-input",children:"Text Input"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Default -->
<Input label="Email" placeholder="you@example.com" />
<!-- Border: gray-300, Background: white -->

<!-- Focus -->
<Input label="Email" class="focus" />
<!-- Border: primary, Ring: ring-2 ring-primary/20 -->

<!-- Error -->
<Input label="Email" :error="'Email is required'" />
<!-- Border: red-500, Ring: ring-2 ring-red-500/20 -->
<!-- Texto de erro abaixo em vermelho -->

<!-- Success -->
<Input label="Email" success />
<!-- Border: green-500, Ícone checkmark verde -->

<!-- Disabled -->
<Input label="Email" :disabled="true" />
<!-- Background: gray-100, Cursor: not-allowed, Opacity: 0.6 -->

<!-- With Icon -->
<Input label="Search" icon="i-carbon-search" />
<!-- Ícone à esquerda, padding ajustado -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"️-checkbox-states",children:"☑️ Checkbox States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Unchecked -->
<Checkbox label="Accept terms" />
<!-- Border: gray-300, Background: white -->

<!-- Unchecked Hover -->
<Checkbox label="Accept terms" class="hover" />
<!-- Border: gray-400 -->

<!-- Checked -->
<Checkbox label="Accept terms" :model-value="true" />
<!-- Background: primary, Border: primary, Checkmark branco -->

<!-- Checked Hover -->
<Checkbox label="Accept terms" :model-value="true" class="hover" />
<!-- Background: primary-hover -->

<!-- Indeterminate -->
<Checkbox label="Select all" :indeterminate="true" />
<!-- Background: primary, Dash branco em vez de checkmark -->

<!-- Disabled Unchecked -->
<Checkbox label="Accept terms" :disabled="true" />
<!-- Border: gray-200, Background: gray-50, Opacity: 0.5 -->

<!-- Disabled Checked -->
<Checkbox label="Accept terms" :model-value="true" :disabled="true" />
<!-- Background: gray-300, Opacity: 0.5 -->

<!-- Focus -->
<Checkbox label="Accept terms" class="focus" />
<!-- Ring: ring-2 ring-primary ring-offset-2 -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-radio-states",children:"🔘 Radio States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Unselected -->
<Radio label="Option 1" value="1" />
<!-- Border: gray-300, Background: white, Circle vazio -->

<!-- Unselected Hover -->
<Radio label="Option 1" value="1" class="hover" />
<!-- Border: gray-400 -->

<!-- Selected -->
<Radio label="Option 1" value="1" :model-value="'1'" />
<!-- Border: primary, Dot interno primary -->

<!-- Selected Hover -->
<Radio label="Option 1" value="1" :model-value="'1'" class="hover" />
<!-- Border: primary-hover, Dot: primary-hover -->

<!-- Disabled Unselected -->
<Radio label="Option 1" value="1" :disabled="true" />
<!-- Border: gray-200, Opacity: 0.5 -->

<!-- Disabled Selected -->
<Radio label="Option 1" value="1" :model-value="'1'" :disabled="true" />
<!-- Border: gray-300, Dot: gray-300, Opacity: 0.5 -->

<!-- Focus -->
<Radio label="Option 1" value="1" class="focus" />
<!-- Ring: ring-2 ring-primary ring-offset-2 -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-select-states",children:"📋 Select States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Default -->
<Select label="Country" :options="countries" />
<!-- Border: gray-300, Chevron icon cinza -->

<!-- Hover -->
<Select label="Country" :options="countries" class="hover" />
<!-- Border: gray-400 -->

<!-- Focus -->
<Select label="Country" :options="countries" class="focus" />
<!-- Border: primary, Ring: ring-2 ring-primary/20 -->

<!-- Open -->
<Select label="Country" :options="countries" :open="true" />
<!-- Border: primary, Dropdown menu visível abaixo -->

<!-- Error -->
<Select label="Country" :options="countries" :error="'Country is required'" />
<!-- Border: red-500, Ring: ring-2 ring-red-500/20 -->

<!-- Disabled -->
<Select label="Country" :options="countries" :disabled="true" />
<!-- Background: gray-100, Opacity: 0.6, Cursor: not-allowed -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"️-badge-states",children:"🏷️ Badge States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Primary (Default) -->
<Badge variant="primary">Active</Badge>
<!-- Background: blue-100, Text: blue-800 -->

<!-- Success -->
<Badge variant="success">Completed</Badge>
<!-- Background: green-100, Text: green-800 -->

<!-- Warning -->
<Badge variant="warning">Pending</Badge>
<!-- Background: yellow-100, Text: yellow-800 -->

<!-- Error -->
<Badge variant="error">Failed</Badge>
<!-- Background: red-100, Text: red-800 -->

<!-- Neutral -->
<Badge variant="neutral">Draft</Badge>
<!-- Background: gray-100, Text: gray-800 -->

<!-- With Dot -->
<Badge variant="success" :dot="true">Online</Badge>
<!-- Dot verde pulsando à esquerda -->

<!-- Removable -->
<Badge variant="primary" :removable="true">Tag</Badge>
<!-- Ícone X à direita, hover mostra background mais escuro no X -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-card-states",children:"🃏 Card States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Default -->
<Card padding="lg">
  <p>Card content</p>
</Card>
<!-- Background: surface, Border: gray-200 -->

<!-- Hoverable -->
<Card padding="lg" :hoverable="true">
  <p>Card content</p>
</Card>
<!-- Hover: Transform translateY(-2px), Shadow: lg -->

<!-- Selected -->
<Card padding="lg" :selected="true">
  <p>Card content</p>
</Card>
<!-- Border: primary, Ring: ring-2 ring-primary/20 -->

<!-- Disabled -->
<Card padding="lg" :disabled="true">
  <p>Card content</p>
</Card>
<!-- Opacity: 0.6, Cursor: not-allowed -->

<!-- Loading -->
<Card padding="lg" :loading="true">
  <Skeleton height="4rem" />
  <Skeleton height="2rem" class="mt-2" />
</Card>
<!-- Skeleton loader dentro -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-alert-states",children:"🔔 Alert States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Info (Default) -->
<Alert variant="info">
  This is an informational message
</Alert>
<!-- Background: blue-50, Border: blue-200, Text: blue-800 -->

<!-- Success -->
<Alert variant="success">
  Changes saved successfully
</Alert>
<!-- Background: green-50, Border: green-200, Icon: checkmark -->

<!-- Warning -->
<Alert variant="warning">
  Your trial expires in 3 days
</Alert>
<!-- Background: yellow-50, Border: yellow-200, Icon: warning -->

<!-- Error -->
<Alert variant="error">
  Unable to save changes
</Alert>
<!-- Background: red-50, Border: red-200, Icon: error -->

<!-- Dismissible -->
<Alert variant="info" :dismissible="true">
  Message
</Alert>
<!-- Botão X à direita, hover mostra background cinza no X -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-datatable-states",children:"📊 DataTable States"}),`
`,n.jsx(e.h3,{id:"row-states",children:"Row States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Default Row -->
<tr>
  <td>John Doe</td>
  <td>john@example.com</td>
</tr>
<!-- Background: white -->

<!-- Hover Row -->
<tr class="hover">
  <td>John Doe</td>
  <td>john@example.com</td>
</tr>
<!-- Background: gray-50 -->

<!-- Selected Row -->
<tr class="selected">
  <td>John Doe</td>
  <td>john@example.com</td>
</tr>
<!-- Background: primary/10, Border-left: 4px primary -->

<!-- Striped Row (Odd) -->
<tr class="odd">
  <td>John Doe</td>
  <td>john@example.com</td>
</tr>
<!-- Background: gray-25 (muito sutil) -->
`})}),`
`,n.jsx(e.h3,{id:"loading-state",children:"Loading State"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<DataTable :columns="columns" :loading="true">
  <!-- 5 linhas de skeleton -->
</DataTable>
`})}),`
`,n.jsx(e.h3,{id:"empty-state",children:"Empty State"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<DataTable :columns="columns" :data="[]">
  <!-- EmptyState component -->
</DataTable>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"️-tabs-states",children:"🗂️ Tabs States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Unselected Tab -->
<button class="tab">
  Profile
</button>
<!-- Border-bottom: transparent, Text: gray-600 -->

<!-- Unselected Tab Hover -->
<button class="tab hover">
  Profile
</button>
<!-- Border-bottom: gray-300, Text: gray-800 -->

<!-- Selected Tab -->
<button class="tab active">
  Profile
</button>
<!-- Border-bottom: 2px primary, Text: primary, Font-weight: 600 -->

<!-- Disabled Tab -->
<button class="tab" disabled>
  Profile
</button>
<!-- Text: gray-400, Cursor: not-allowed -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"️-switch-states",children:"🎚️ Switch States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Off (Unchecked) -->
<Switch :model-value="false" />
<!-- Background: gray-300, Knob à esquerda, Knob: white -->

<!-- Off Hover -->
<Switch :model-value="false" class="hover" />
<!-- Background: gray-400 -->

<!-- On (Checked) -->
<Switch :model-value="true" />
<!-- Background: primary, Knob à direita, Knob: white -->

<!-- On Hover -->
<Switch :model-value="true" class="hover" />
<!-- Background: primary-hover -->

<!-- Disabled Off -->
<Switch :model-value="false" :disabled="true" />
<!-- Background: gray-200, Opacity: 0.5 -->

<!-- Disabled On -->
<Switch :model-value="true" :disabled="true" />
<!-- Background: primary/50, Opacity: 0.5 -->

<!-- Focus -->
<Switch :model-value="false" class="focus" />
<!-- Ring: ring-2 ring-primary ring-offset-2 -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-modal-states",children:"🎭 Modal States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Closed (Hidden) -->
<Modal :model-value="false">
  Content
</Modal>
<!-- Display: none, Não renderizado -->

<!-- Opening Animation -->
<Modal :model-value="true" class="entering">
  Content
</Modal>
<!-- Overlay: opacity 0 → 1 (300ms) -->
<!-- Modal: scale 0.95 → 1, opacity 0 → 1 (300ms) -->

<!-- Open -->
<Modal :model-value="true">
  Content
</Modal>
<!-- Overlay: opacity 1, Background: rgba(0,0,0,0.5) -->
<!-- Modal: scale 1, opacity 1 -->

<!-- Closing Animation -->
<Modal :model-value="false" class="leaving">
  Content
</Modal>
<!-- Overlay: opacity 1 → 0 (300ms) -->
<!-- Modal: scale 1 → 0.95, opacity 1 → 0 (300ms) -->

<!-- Loading Content -->
<Modal :model-value="true" :loading="true">
  <Spinner />
</Modal>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-dropdown-states",children:"🔽 Dropdown States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Closed -->
<Dropdown :items="items" :open="false">
  <Button>Actions</Button>
</Dropdown>
<!-- Menu não visível -->

<!-- Open -->
<Dropdown :items="items" :open="true">
  <Button>Actions</Button>
</Dropdown>
<!-- Menu visível abaixo do trigger -->
<!-- Animation: opacity 0 → 1, translateY(-10px) → 0 -->

<!-- Item Hover -->
<div class="dropdown-item hover">
  Edit
</div>
<!-- Background: gray-100 -->

<!-- Item Active (Clicking) -->
<div class="dropdown-item active">
  Edit
</div>
<!-- Background: gray-200 -->

<!-- Item Disabled -->
<div class="dropdown-item disabled">
  Delete
</div>
<!-- Text: gray-400, Cursor: not-allowed -->

<!-- Item with Divider -->
<div class="dropdown-item">
  Settings
</div>
<hr class="dropdown-divider" />
<!-- Border-top: 1px gray-200, Margin: 0.5rem 0 -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-tooltip-states",children:"💡 Tooltip States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Hidden (Default) -->
<Tooltip content="Help text">
  <button>Hover me</button>
</Tooltip>
<!-- Display: none -->

<!-- Visible (On Hover) -->
<Tooltip content="Help text" :visible="true">
  <button>Hover me</button>
</Tooltip>
<!-- Display: block, Animation: opacity 0 → 1 (150ms) -->
<!-- Background: gray-900, Text: white, Font-size: sm -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-spinner-states",children:"🔄 Spinner States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Small -->
<Spinner size="sm" />
<!-- Width: 1rem, Height: 1rem -->

<!-- Medium (Default) -->
<Spinner size="md" />
<!-- Width: 2rem, Height: 2rem -->

<!-- Large -->
<Spinner size="lg" />
<!-- Width: 3rem, Height: 3rem -->

<!-- Custom Color -->
<Spinner color="text-primary" />
<!-- Border-color: primary com animação spin -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-emptystate-states",children:"📦 EmptyState States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- Default (No Action) -->
<EmptyState
  icon="i-carbon-folder"
  title="No items"
  description="There are no items to display"
/>
<!-- Apenas texto e ícone -->

<!-- With Action -->
<EmptyState
  icon="i-carbon-add"
  title="No products"
  description="Start by adding your first product"
  action-label="Add product"
  @action="handleAdd"
/>
<!-- Botão primary abaixo da descrição -->

<!-- Loading (Skeleton) -->
<Skeleton height="200px" />
<!-- Enquanto carrega dados -->

<!-- Error State -->
<EmptyState
  icon="i-carbon-warning"
  title="Unable to load"
  description="There was an error loading data"
  action-label="Try again"
  @action="retry"
/>
<!-- Ícone de erro, ação de retry -->
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-state-transition-rules",children:"✅ State Transition Rules"}),`
`,n.jsx(e.h3,{id:"timing",children:"Timing"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-css",children:`/* Fast transitions (micro-interactions) */
transition: all 150ms ease;
/* Hover, focus, active states */

/* Base transitions (standard) */
transition: all 300ms ease;
/* Modal open/close, dropdown, etc */

/* Slow transitions (complex) */
transition: all 500ms ease;
/* Page transitions, complex animations */
`})}),`
`,n.jsx(e.h3,{id:"properties-to-animate",children:"Properties to Animate"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-css",children:`/* ✅ Performant (GPU accelerated) */
transition: transform 300ms, opacity 300ms;

/* ⚠️ OK for small elements */
transition: color 150ms, background 150ms, border-color 150ms;

/* ❌ Avoid (causes reflow) */
transition: width 300ms, height 300ms, margin 300ms;
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-accessibility-requirements",children:"🎯 Accessibility Requirements"}),`
`,n.jsx(e.h3,{id:"focus-states",children:"Focus States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-css",children:`/* SEMPRE visível */
:focus {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
}

/* Ou use ring */
:focus {
  box-shadow: 0 0 0 3px rgba(74, 222, 128, 0.3);
}
`})}),`
`,n.jsx(e.h3,{id:"disabled-states",children:"Disabled States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-html",children:`<!-- Use disabled attribute -->
<button disabled>Save</button>

<!-- Com aria-disabled para custom elements -->
<div role="button" aria-disabled="true">Save</div>
`})}),`
`,n.jsx(e.h3,{id:"loading-states",children:"Loading States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-html",children:`<!-- Comunique ao screen reader -->
<button aria-busy="true">
  <span class="sr-only">Loading...</span>
  <Spinner />
</button>
`})}),`
`,n.jsx(e.h3,{id:"error-states",children:"Error States"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-html",children:`<!-- Associe mensagem de erro -->
<input
  id="email"
  aria-invalid="true"
  aria-describedby="email-error"
/>
<span id="email-error" role="alert">
  Email is required
</span>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-state-testing-checklist",children:"📋 State Testing Checklist"}),`
`,n.jsx(e.p,{children:"Para cada componente interativo, teste:"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"[ ] Default state renderiza corretamente"}),`
`,n.jsx(e.li,{children:"[ ] Hover state tem feedback visual"}),`
`,n.jsx(e.li,{children:"[ ] Focus state tem ring visível (acessibilidade)"}),`
`,n.jsx(e.li,{children:'[ ] Active state mostra "press" feedback'}),`
`,n.jsx(e.li,{children:"[ ] Disabled state previne interação"}),`
`,n.jsx(e.li,{children:"[ ] Loading state mostra spinner/skeleton"}),`
`,n.jsx(e.li,{children:"[ ] Error state mostra mensagem clara"}),`
`,n.jsx(e.li,{children:"[ ] Success state tem feedback positivo"}),`
`,n.jsx(e.li,{children:"[ ] Transições são suaves (300ms padrão)"}),`
`,n.jsx(e.li,{children:"[ ] Estados funcionam no mobile (sem hover)"}),`
`,n.jsx(e.li,{children:"[ ] Screen readers anunciam mudanças de estado"}),`
`,n.jsx(e.li,{children:"[ ] Keyboard navigation funciona"}),`
`,n.jsx(e.li,{children:"[ ] Dark mode mantém contraste adequado"}),`
`]})]})}function h(r={}){const{wrapper:e}={...t(),...r.components};return e?n.jsx(e,{...r,children:n.jsx(a,{...r})}):a(r)}export{h as default};
