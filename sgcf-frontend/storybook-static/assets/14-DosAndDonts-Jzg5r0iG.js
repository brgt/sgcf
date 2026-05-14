import{j as e,M as i}from"./index-COwdE6jM.js";import{useMDXComponents as a}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function r(s){const n={code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",p:"p",pre:"pre",strong:"strong",ul:"ul",...a(),...s.components};return e.jsxs(e.Fragment,{children:[e.jsx(i,{title:"Style Guide/Do's and Don'ts"}),`
`,e.jsx(n.h1,{id:"dos-and-donts",children:"Do's and Don'ts"}),`
`,e.jsx(n.p,{children:"Exemplos visuais de uso correto e incorreto dos componentes."}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-buttons",children:"🔘 Buttons"}),`
`,e.jsx(n.h3,{id:"-visual-comparison-button-hierarchy",children:"🎨 Visual Comparison: Button Hierarchy"}),`
`,e.jsxs("div",{style:{display:"grid",gridTemplateColumns:"1fr 1fr",gap:"2rem",marginBottom:"3rem"},children:[e.jsxs("div",{className:"contrast-demo-success",style:{padding:"2rem",borderRadius:"8px"},children:[e.jsxs("h4",{style:{marginBottom:"1rem",display:"flex",alignItems:"center",gap:"0.5rem"},children:[e.jsx("span",{style:{fontSize:"1.5rem"},children:"✅"}),e.jsx("span",{children:"DO: Hierarquia Clara"})]}),e.jsxs("div",{style:{display:"flex",gap:"0.5rem",marginBottom:"1rem"},children:[e.jsx("button",{style:{padding:"0.5rem 1rem",background:"transparent",color:"#FFFFFF",border:"none",borderRadius:"6px",fontSize:"0.875rem",cursor:"pointer"},children:e.jsx(n.p,{children:"Cancel"})}),e.jsx("button",{style:{padding:"0.5rem 1rem",background:"#00f9b8",color:"#0c141a",border:"none",borderRadius:"6px",fontSize:"0.875rem",fontWeight:600,cursor:"pointer"},children:e.jsx(n.p,{children:"Save changes"})})]}),e.jsx("p",{style:{fontSize:"0.8125rem",margin:0},children:e.jsx(n.p,{children:"✅ Apenas 1 ação primária, ações secundárias menos destacadas."})})]}),e.jsxs("div",{className:"contrast-demo-error",style:{padding:"2rem",borderRadius:"8px"},children:[e.jsxs("h4",{style:{marginBottom:"1rem",display:"flex",alignItems:"center",gap:"0.5rem"},children:[e.jsx("span",{style:{fontSize:"1.5rem"},children:"❌"}),e.jsx("span",{children:"DON'T: Múltiplos Primary"})]}),e.jsxs("div",{style:{display:"flex",gap:"0.5rem",marginBottom:"1rem"},children:[e.jsx("button",{style:{padding:"0.5rem 1rem",background:"#00f9b8",color:"#0c141a",border:"none",borderRadius:"6px",fontSize:"0.875rem",fontWeight:600,cursor:"pointer"},children:e.jsx(n.p,{children:"Save"})}),e.jsx("button",{style:{padding:"0.5rem 1rem",background:"#00f9b8",color:"#0c141a",border:"none",borderRadius:"6px",fontSize:"0.875rem",fontWeight:600,cursor:"pointer"},children:e.jsx(n.p,{children:"Submit"})}),e.jsx("button",{style:{padding:"0.5rem 1rem",background:"#00f9b8",color:"#0c141a",border:"none",borderRadius:"6px",fontSize:"0.875rem",fontWeight:600,cursor:"pointer"},children:e.jsx(n.p,{children:"Continue"})})]}),e.jsx("p",{style:{fontSize:"0.8125rem",margin:0},children:e.jsx(n.p,{children:"❌ Confunde o usuário sobre qual é a ação principal."})})]})]}),`
`,e.jsx(n.h3,{id:"-do-use-hierarquia-clara",children:"✅ DO: Use hierarquia clara"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="flex gap-2">
  <Button variant="ghost">Cancel</Button>
  <Button variant="primary">Save changes</Button>
</div>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Apenas 1 ação primária, ações secundárias menos destacadas."]}),`
`,e.jsx(n.h3,{id:"-dont-múltiplos-primary-buttons",children:"❌ DON'T: Múltiplos primary buttons"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="flex gap-2">
  <Button variant="primary">Save</Button>
  <Button variant="primary">Submit</Button>
  <Button variant="primary">Continue</Button>
</div>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Confunde o usuário sobre qual é a ação principal."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-texto-claro-e-acionável",children:"✅ DO: Texto claro e acionável"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Button variant="primary">Save changes</Button>
<Button variant="danger">Delete order</Button>
<Button variant="secondary">Cancel</Button>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Usuário sabe exatamente o que vai acontecer."]}),`
`,e.jsx(n.h3,{id:"-dont-texto-vago",children:"❌ DON'T: Texto vago"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Button variant="primary">OK</Button>
<Button variant="secondary">Close</Button>
<Button variant="danger">Yes</Button>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),': "OK" para quê? "Yes" para fazer o quê?']}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-use-loading-state",children:"✅ DO: Use loading state"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Button variant="primary" :loading="submitting">
  {{ submitting ? 'Saving...' : 'Save changes' }}
</Button>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Usuário sabe que ação está processando."]}),`
`,e.jsx(n.h3,{id:"-dont-button-sem-feedback",children:"❌ DON'T: Button sem feedback"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Button variant="primary" @click="save">
  Save changes
</Button>
<!-- Sem loading, sem disabled -->
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Usuário pode clicar múltiplas vezes."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-forms",children:"📝 Forms"}),`
`,e.jsx(n.h3,{id:"-do-label-em-todos-os-inputs",children:"✅ DO: Label em todos os inputs"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<FormField label="Email address" :required="true">
  <Input v-model="email" type="email" />
</FormField>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Acessibilidade e clareza."]}),`
`,e.jsx(n.h3,{id:"-dont-placeholder-como-label",children:"❌ DON'T: Placeholder como label"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Input placeholder="Enter your email" />
<!-- Sem label -->
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Placeholder desaparece ao digitar, confunde usuário."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-mensagens-de-erro-específicas",children:"✅ DO: Mensagens de erro específicas"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Input
  label="Password"
  :error="'Password must be at least 8 characters'"
/>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Usuário sabe como corrigir o erro."]}),`
`,e.jsx(n.h3,{id:"-dont-mensagens-genéricas",children:"❌ DON'T: Mensagens genéricas"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Input
  label="Password"
  :error="'Invalid input'"
/>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Usuário não sabe o que fazer."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-single-column-layout",children:"✅ DO: Single column layout"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<form class="max-w-2xl space-y-6">
  <Input label="Full name" />
  <Input label="Email" />
  <Input label="Phone" />
</form>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Mais fácil de ler, mobile-friendly, menor taxa de erro."]}),`
`,e.jsx(n.h3,{id:"-dont-múltiplas-colunas-sem-motivo",children:"❌ DON'T: Múltiplas colunas sem motivo"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="grid grid-cols-3 gap-4">
  <Input label="Email" />
  <Input label="Phone" />
  <Input label="Address" />
</div>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Difícil de escanear, não funciona bem no mobile."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-modals",children:"💬 Modals"}),`
`,e.jsx(n.h3,{id:"-do-título-descritivo-e-ações-claras",children:"✅ DO: Título descritivo e ações claras"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Modal title="Delete order #12345?" size="sm">
  <p>This action cannot be undone. The order will be permanently deleted.</p>
  <template #footer>
    <Button variant="ghost">Cancel</Button>
    <Button variant="danger">Delete order</Button>
  </template>
</Modal>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Usuário sabe exatamente o que vai acontecer."]}),`
`,e.jsx(n.h3,{id:"-dont-título-vago",children:"❌ DON'T: Título vago"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Modal title="Are you sure?" size="sm">
  <p>Do you want to continue?</p>
  <template #footer>
    <Button variant="ghost">No</Button>
    <Button variant="primary">Yes</Button>
  </template>
</Modal>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Usuário não sabe o contexto da ação."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-use-tamanho-apropriado",children:"✅ DO: Use tamanho apropriado"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- Confirmação simples -->
<Modal size="sm">Delete item?</Modal>

<!-- Formulário -->
<Modal size="md">User form</Modal>

<!-- Conteúdo rico -->
<Modal size="lg">Product details</Modal>
`})}),`
`,e.jsx(n.h3,{id:"-dont-modal-gigante-para-confirmação",children:"❌ DON'T: Modal gigante para confirmação"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Modal size="xl">
  <p>Delete item?</p>
</Modal>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Desperdiça espaço, má UX."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-datatable",children:"📊 DataTable"}),`
`,e.jsx(n.h3,{id:"-do-use-para-muitos-dados-tabulares",children:"✅ DO: Use para muitos dados tabulares"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<DataTable
  :columns="[
    { key: 'id', label: 'Order ID' },
    { key: 'customer', label: 'Customer' },
    { key: 'total', label: 'Total' },
    { key: 'status', label: 'Status' }
  ]"
  :data="orders"
/>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Ideal para comparar dados estruturados."]}),`
`,e.jsx(n.h3,{id:"-dont-use-para-poucos-items-ou-conteúdo-rico",children:"❌ DON'T: Use para poucos items ou conteúdo rico"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<DataTable
  :columns="columns"
  :data="[item1, item2, item3]"
/>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Cards são melhores para poucos items."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-ações-em-coluna-dedicada",children:"✅ DO: Ações em coluna dedicada"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<DataTable :columns="[
  { key: 'name', label: 'Name' },
  { key: 'email', label: 'Email' },
  { key: 'actions', label: 'Actions' }
]">
  <template #cell-actions="{ row }">
    <Dropdown :items="actions" />
  </template>
</DataTable>
`})}),`
`,e.jsx(n.h3,{id:"-dont-ações-inline-sem-controle",children:"❌ DON'T: Ações inline sem controle"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<DataTable>
  <template #cell-name="{ row }">
    {{ row.name }}
    <button>Edit</button>
    <button>Delete</button>
  </template>
</DataTable>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Polui a célula, dificulta leitura."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"️-badges",children:"🏷️ Badges"}),`
`,e.jsx(n.h3,{id:"-do-use-cores-semânticas",children:"✅ DO: Use cores semânticas"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Badge variant="success">Completed</Badge>
<Badge variant="warning">Pending</Badge>
<Badge variant="error">Failed</Badge>
<Badge variant="neutral">Draft</Badge>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Cores reforçam significado."]}),`
`,e.jsx(n.h3,{id:"-dont-cores-arbitrárias",children:"❌ DON'T: Cores arbitrárias"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Badge variant="success">Failed</Badge>
<Badge variant="error">Completed</Badge>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Confunde o usuário."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-texto-curto-e-objetivo",children:"✅ DO: Texto curto e objetivo"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Badge variant="success">Paid</Badge>
<Badge variant="warning">Pending payment</Badge>
`})}),`
`,e.jsx(n.h3,{id:"-dont-frases-longas",children:"❌ DON'T: Frases longas"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Badge variant="success">
  This order has been successfully completed and paid
</Badge>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Badge não é para parágrafos."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-alerts",children:"🔔 Alerts"}),`
`,e.jsx(n.h3,{id:"-do-use-variant-correto",children:"✅ DO: Use variant correto"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Alert variant="success">
  Order created successfully
</Alert>

<Alert variant="warning">
  Your trial expires in 3 days
</Alert>

<Alert variant="error">
  Unable to save changes. Try again.
</Alert>

<Alert variant="info">
  New features are available
</Alert>
`})}),`
`,e.jsx(n.h3,{id:"-dont-variant-errado",children:"❌ DON'T: Variant errado"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Alert variant="success">
  Error: Failed to save
</Alert>

<Alert variant="error">
  Success! Order created
</Alert>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Cores conflitam com mensagem."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-mensagem-acionável",children:"✅ DO: Mensagem acionável"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Alert variant="error">
  Unable to save. Check your connection and try again.
</Alert>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Usuário sabe como resolver."]}),`
`,e.jsx(n.h3,{id:"-dont-mensagem-vaga",children:"❌ DON'T: Mensagem vaga"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Alert variant="error">
  Something went wrong
</Alert>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Usuário não sabe o que fazer."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-cards",children:"🃏 Cards"}),`
`,e.jsx(n.h3,{id:"-do-use-para-conteúdo-independente",children:"✅ DO: Use para conteúdo independente"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="grid grid-cols-3 gap-4">
  <Card v-for="product in products" :hoverable="true">
    <img :src="product.image" />
    <h3>{{ product.name }}</h3>
    <p>{{ product.price }}</p>
  </Card>
</div>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Cada card é uma unidade independente."]}),`
`,e.jsx(n.h3,{id:"-dont-cards-desnecessários",children:"❌ DON'T: Cards desnecessários"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Card>
  <Card>
    <Card>
      <p>Texto simples</p>
    </Card>
  </Card>
</Card>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Over-engineering, não adiciona valor."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-padding-consistente",children:"✅ DO: Padding consistente"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Card padding="lg">
  <h2>Title</h2>
  <p>Content with breathing room</p>
</Card>
`})}),`
`,e.jsx(n.h3,{id:"-dont-sem-padding-ou-muito-padding",children:"❌ DON'T: Sem padding ou muito padding"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Card padding="none">
  <h2>Title</h2><!-- Colado na borda -->
</Card>

<Card padding="xl">
  <h2>Title</h2><!-- Muito espaço desperdiçado -->
</Card>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-typography",children:"🎨 Typography"}),`
`,e.jsx(n.h3,{id:"-do-hierarquia-clara",children:"✅ DO: Hierarquia clara"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<h1 class="text-4xl font-bold">Page Title</h1>
<h2 class="text-2xl font-semibold mt-8">Section</h2>
<p class="text-base text-gray-700">Body text</p>
`})}),`
`,e.jsx(n.h3,{id:"-dont-tamanhos-aleatórios",children:"❌ DON'T: Tamanhos aleatórios"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<h1 class="text-sm">Page Title</h1>
<p class="text-5xl">Body text</p>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Quebra hierarquia visual."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-line-height-adequado",children:"✅ DO: Line-height adequado"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`.text-body {
  line-height: 1.6; /* 160% */
}

.text-heading {
  line-height: 1.2; /* 120% */
}
`})}),`
`,e.jsx(n.h3,{id:"-dont-line-height-muito-apertado",children:"❌ DON'T: Line-height muito apertado"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`.text {
  line-height: 1; /* 100% - ilegível */
}
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-spacing",children:"🎨 Spacing"}),`
`,e.jsx(n.h3,{id:"-do-use-escala-consistente-8px",children:"✅ DO: Use escala consistente (8px)"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="space-y-2">  <!-- 8px -->
  <div class="space-y-4">  <!-- 16px -->
    <div class="space-y-6">  <!-- 24px -->
`})}),`
`,e.jsx(n.h3,{id:"-dont-valores-arbitrários",children:"❌ DON'T: Valores arbitrários"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="space-y-[13px]">
  <div class="space-y-[27px]">
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Quebra sistema de design."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-icons",children:"🎭 Icons"}),`
`,e.jsx(n.h3,{id:"-do-tamanho-consistente-com-texto",children:"✅ DO: Tamanho consistente com texto"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<button class="flex items-center gap-2">
  <span class="i-carbon-add text-base"></span>
  <span class="text-base">Add item</span>
</button>
`})}),`
`,e.jsx(n.h3,{id:"-dont-ícone-desproporcional",children:"❌ DON'T: Ícone desproporcional"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<button class="flex items-center gap-2">
  <span class="i-carbon-add text-6xl"></span>
  <span class="text-sm">Add</span>
</button>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-use-ícones-com-significado-claro",children:"✅ DO: Use ícones com significado claro"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<button>
  <span class="i-carbon-trash-can"></span>
  Delete
</button>

<button>
  <span class="i-carbon-edit"></span>
  Edit
</button>
`})}),`
`,e.jsx(n.h3,{id:"-dont-ícones-confusos",children:"❌ DON'T: Ícones confusos"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<button>
  <span class="i-carbon-sun"></span>
  Delete
</button>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Ícone não condiz com ação."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-empty-states",children:"🔍 Empty States"}),`
`,e.jsx(n.h3,{id:"-do-informativo-e-acionável",children:"✅ DO: Informativo e acionável"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<EmptyState
  icon="i-carbon-shopping-cart"
  title="No orders yet"
  description="Start by creating your first order"
  action-label="Create order"
  @action="createOrder"
/>
`})}),`
`,e.jsx(n.h3,{id:"-dont-vago-e-sem-ação",children:"❌ DON'T: Vago e sem ação"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<EmptyState
  title="Empty"
  description="No data"
/>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"️-loading-states",children:"⏱️ Loading States"}),`
`,e.jsx(n.h3,{id:"-do-use-skeleton-para-conteúdo-previsível",children:"✅ DO: Use skeleton para conteúdo previsível"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Card v-if="loading">
  <Skeleton height="2rem" />
  <Skeleton height="1rem" class="mt-2" width="60%" />
</Card>
<Card v-else>
  <h2>{{ title }}</h2>
  <p>{{ description }}</p>
</Card>
`})}),`
`,e.jsx(n.h3,{id:"-dont-apenas-texto-loading",children:`❌ DON'T: Apenas texto "Loading..."`}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div v-if="loading">Loading...</div>
<Card v-else>...</Card>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Skeleton mostra estrutura, reduz layout shift."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-colors",children:"🌈 Colors"}),`
`,e.jsx(n.h3,{id:"-do-use-tokens-do-design-system",children:"✅ DO: Use tokens do design system"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="bg-primary text-white">
  <p class="text-text-secondary">Description</p>
</div>
`})}),`
`,e.jsx(n.h3,{id:"-dont-cores-hardcoded",children:"❌ DON'T: Cores hardcoded"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div style="background: #4ADE80; color: #ffffff">
  <p style="color: #6b7280">Description</p>
</div>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Dificulta manutenção e temas."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-accessibility",children:"♿ Accessibility"}),`
`,e.jsx(n.h3,{id:"-do-labels-em-todos-os-inputs",children:"✅ DO: Labels em todos os inputs"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<FormField label="Email">
  <Input id="email" v-model="email" />
</FormField>
`})}),`
`,e.jsx(n.h3,{id:"-dont-input-sem-label",children:"❌ DON'T: Input sem label"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Input v-model="email" placeholder="Email" />
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Screen readers não conseguem identificar."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-focus-visível",children:"✅ DO: Focus visível"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`button:focus {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
}
`})}),`
`,e.jsx(n.h3,{id:"-dont-remover-outline",children:"❌ DON'T: Remover outline"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`button:focus {
  outline: none; /* Nunca faça isso */
}
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Por quê"}),": Usuários de teclado não sabem onde estão."]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-contraste-adequado",children:"✅ DO: Contraste adequado"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- Text primary on surface: 12:1 ✅ -->
<div class="bg-surface text-text-primary">
  High contrast text
</div>
`})}),`
`,e.jsx(n.h3,{id:"-dont-baixo-contraste",children:"❌ DON'T: Baixo contraste"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- Gray text on gray background: 2:1 ❌ -->
<div class="bg-gray-200 text-gray-300">
  Unreadable text
</div>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-responsive-design",children:"📱 Responsive Design"}),`
`,e.jsx(n.h3,{id:"-do-mobile-first-com-breakpoints",children:"✅ DO: Mobile-first com breakpoints"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
  <Card v-for="item in items">...</Card>
</div>
`})}),`
`,e.jsx(n.h3,{id:"-dont-desktop-only",children:"❌ DON'T: Desktop-only"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="grid grid-cols-3 gap-4">
  <!-- Quebra no mobile -->
</div>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-animations",children:"🎬 Animations"}),`
`,e.jsx(n.h3,{id:"-do-transições-suaves",children:"✅ DO: Transições suaves"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`.button {
  transition: all 150ms ease;
}

.button:hover {
  transform: translateY(-2px);
}
`})}),`
`,e.jsx(n.h3,{id:"-dont-sem-transição",children:"❌ DON'T: Sem transição"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`.button:hover {
  transform: translateY(-2px);
  /* Pulo brusco */
}
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h3,{id:"-do-respeite-reduced-motion",children:"✅ DO: Respeite reduced motion"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`@media (prefers-reduced-motion: reduce) {
  * {
    animation-duration: 0.01ms !important;
    transition-duration: 0.01ms !important;
  }
}
`})}),`
`,e.jsx(n.h3,{id:"-dont-forçar-animações",children:"❌ DON'T: Forçar animações"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-css",children:`.element {
  animation: spin 1s infinite; /* Sem respeitar preferência */
}
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-quick-reference",children:"📋 Quick Reference"}),`
`,e.jsx(n.h3,{id:"-sempre-faça",children:"✅ SEMPRE faça"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"Use hierarquia visual clara"}),`
`,e.jsx(n.li,{children:"Textos descritivos e acionáveis"}),`
`,e.jsx(n.li,{children:"Labels em todos os inputs"}),`
`,e.jsx(n.li,{children:"Mensagens de erro específicas"}),`
`,e.jsx(n.li,{children:"Loading states visíveis"}),`
`,e.jsx(n.li,{children:"Focus states acessíveis"}),`
`,e.jsx(n.li,{children:"Cores do design system"}),`
`,e.jsx(n.li,{children:"Mobile-first responsive"}),`
`,e.jsx(n.li,{children:"Transições suaves"}),`
`,e.jsx(n.li,{children:"Contraste adequado (4.5:1+)"}),`
`]}),`
`,e.jsx(n.h3,{id:"-nunca-faça",children:"❌ NUNCA faça"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"Múltiplos primary buttons"}),`
`,e.jsx(n.li,{children:"Placeholder como label"}),`
`,e.jsx(n.li,{children:"Remover focus outline"}),`
`,e.jsx(n.li,{children:"Cores arbitrárias"}),`
`,e.jsx(n.li,{children:'Mensagens genéricas ("Error", "Invalid")'}),`
`,e.jsx(n.li,{children:"Baixo contraste"}),`
`,e.jsx(n.li,{children:"Layout só desktop"}),`
`,e.jsx(n.li,{children:"Over-engineering (Cards dentro de Cards)"}),`
`,e.jsx(n.li,{children:"Textos longos em Badges"}),`
`,e.jsx(n.li,{children:"Variants errados (sucesso em vermelho)"}),`
`]})]})}function x(s={}){const{wrapper:n}={...a(),...s.components};return n?e.jsx(n,{...s,children:e.jsx(r,{...s})}):r(s)}export{x as default};
