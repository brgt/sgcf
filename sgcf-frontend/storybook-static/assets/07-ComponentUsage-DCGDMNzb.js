import{j as n,M as o}from"./index-COwdE6jM.js";import{useMDXComponents as i}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function r(s){const e={code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",p:"p",pre:"pre",strong:"strong",ul:"ul",...i(),...s.components};return n.jsxs(n.Fragment,{children:[n.jsx(o,{title:"Style Guide/Component Usage"}),`
`,n.jsx(e.h1,{id:"component-usage-guidelines",children:"Component Usage Guidelines"}),`
`,n.jsx(e.p,{children:"Quando usar cada componente e por quê."}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-button-vs-link",children:"🔘 Button vs Link"}),`
`,n.jsx(e.h3,{id:"use-button-quando",children:"Use Button quando:"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"Ação modifica dados (save, delete, submit)"}),`
`,n.jsx(e.li,{children:"Abre modal ou inicia processo"}),`
`,n.jsx(e.li,{children:"Ação primária ou secundária da página"}),`
`]}),`
`,n.jsx(e.h3,{id:"use-link-quando",children:"Use Link quando:"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"Navega para outra página"}),`
`,n.jsx(e.li,{children:"Download de arquivo"}),`
`,n.jsx(e.li,{children:"Link externo"}),`
`]}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- ✅ Correto -->
<Button @click="saveChanges">Save</Button>
<a href="/products">View products</a>

<!-- ❌ Errado -->
<Button @click="navigate">Go to products</Button>
<a @click="saveChanges">Save</a>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-button-variants",children:"🎨 Button Variants"}),`
`,n.jsx(e.h3,{id:"primary",children:"Primary"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Ação principal da tela (1 por página)"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),': "Save changes", "Create order", "Continue"']}),`
`]}),`
`,n.jsx(e.h3,{id:"secondary",children:"Secondary"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Ações alternativas importantes"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),': "Cancel", "Back", "Save draft"']}),`
`]}),`
`,n.jsx(e.h3,{id:"ghost",children:"Ghost"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Ações terciárias, menos destaque"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),': "Skip", "Learn more", "View details"']}),`
`]}),`
`,n.jsx(e.h3,{id:"danger",children:"Danger"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Ações destrutivas irreversíveis"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),': "Delete", "Remove", "Deactivate"']}),`
`]}),`
`,n.jsx(e.h3,{id:"-live-demo-button-hierarchy",children:"🎨 Live Demo: Button Hierarchy"}),`
`,n.jsxs("div",{className:"contrast-demo-dark",style:{padding:"2rem",borderRadius:"8px",marginBottom:"2rem"},children:[n.jsx("h4",{style:{marginBottom:"1rem"},children:n.jsx(e.p,{children:"✅ Correct: Clear Visual Hierarchy"})}),n.jsxs("div",{style:{display:"flex",gap:"0.75rem",marginBottom:"2rem"},children:[n.jsx("button",{style:{padding:"0.75rem 1.5rem",background:"transparent",border:"1px solid transparent",borderRadius:"8px",cursor:"pointer",transition:"all 0.2s"},children:n.jsx(e.p,{children:"Cancel"})}),n.jsx("button",{style:{padding:"0.75rem 1.5rem",background:"#121a2a",color:"#FFFFFF",border:"1px solid #7A8DB8",borderRadius:"8px",cursor:"pointer",transition:"all 0.2s"},children:n.jsx(e.p,{children:"Save Draft"})}),n.jsx("button",{style:{padding:"0.75rem 1.5rem",background:"#00f9b8",color:"#0c141a",border:"none",borderRadius:"8px",fontWeight:600,cursor:"pointer",transition:"all 0.2s"},children:n.jsx(e.p,{children:"Save Changes"})})]}),n.jsx("p",{style:{fontSize:"0.875rem",margin:0},children:n.jsx(e.p,{children:'User instantly knows "Save Changes" is the primary action.'})}),n.jsx("hr",{style:{margin:"2rem 0",border:"none",borderTop:"1px solid #7A8DB8"}}),n.jsx("h4",{style:{marginBottom:"1rem"},children:n.jsx(e.p,{children:"❌ Incorrect: Multiple Primary Buttons"})}),n.jsxs("div",{style:{display:"flex",gap:"0.75rem",marginBottom:"2rem"},children:[n.jsx("button",{style:{padding:"0.75rem 1.5rem",background:"#00f9b8",color:"#0c141a",border:"none",borderRadius:"8px",fontWeight:600,cursor:"pointer"},children:n.jsx(e.p,{children:"Save"})}),n.jsx("button",{style:{padding:"0.75rem 1.5rem",background:"#00f9b8",color:"#0c141a",border:"none",borderRadius:"8px",fontWeight:600,cursor:"pointer"},children:n.jsx(e.p,{children:"Submit"})}),n.jsx("button",{style:{padding:"0.75rem 1.5rem",background:"#00f9b8",color:"#0c141a",border:"none",borderRadius:"8px",fontWeight:600,cursor:"pointer"},children:n.jsx(e.p,{children:"Publish"})})]}),n.jsx("p",{style:{fontSize:"0.875rem",color:"#FF5757",margin:0},children:n.jsx(e.p,{children:"User doesn't know which action is most important. Confusing UX."})})]}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- ✅ Hierarquia correta -->
<div class="flex gap-2">
  <Button variant="ghost">Cancel</Button>
  <Button variant="primary">Save changes</Button>
</div>

<!-- ❌ Múltiplos primary -->
<div class="flex gap-2">
  <Button variant="primary">Save</Button>
  <Button variant="primary">Submit</Button>
  <Button variant="primary">Continue</Button>
</div>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-modal-vs-drawer-vs-dropdown",children:"💬 Modal vs Drawer vs Dropdown"}),`
`,n.jsx(e.h3,{id:"modal",children:"Modal"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Decisão importante que bloqueia fluxo"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),": Confirmações, formulários críticos"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Tamanho"}),": sm (confirmações), md (formulários), lg (conteúdo rico)"]}),`
`]}),`
`,n.jsx(e.h3,{id:"drawer-mobiledrawer",children:"Drawer (MobileDrawer)"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Navegação ou filtros em mobile"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),": Menu lateral, filtros avançados"]}),`
`]}),`
`,n.jsx(e.h3,{id:"dropdown",children:"Dropdown"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Menu de ações rápidas"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),": Ações de linha de tabela, menu de usuário"]}),`
`]}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- ✅ Modal para confirmação -->
<Modal v-model="showDelete" title="Delete product?" size="sm">
  <p>This action cannot be undone.</p>
  <template #footer>
    <Button variant="ghost">Cancel</Button>
    <Button variant="danger">Delete</Button>
  </template>
</Modal>

<!-- ✅ Dropdown para ações -->
<Dropdown :items="actions">
  <template #trigger>
    <Button variant="ghost">Actions</Button>
  </template>
</Dropdown>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-alert-vs-toast",children:"🔔 Alert vs Toast"}),`
`,n.jsx(e.h3,{id:"alert",children:"Alert"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Informação contextual na página"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Persiste"}),": Sim, até ser fechada"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Posição"}),": Inline com conteúdo"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Uso"}),": Avisos de página, informações importantes"]}),`
`]}),`
`,n.jsx(e.h3,{id:"toast-futuro",children:"Toast (futuro)"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Feedback de ação rápida"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Persiste"}),": Não, auto-dismiss"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Posição"}),": Fixed (canto da tela)"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Uso"}),': "Salvo com sucesso", "Email enviado"']}),`
`]}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- ✅ Alert para aviso de página -->
<Alert variant="warning" :dismissible="true">
  Your trial ends in 3 days. Upgrade to continue.
</Alert>

<!-- ✅ Toast para feedback -->
<Toast variant="success">
  Order created successfully
</Toast>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-datatable-vs-card-list",children:"📊 DataTable vs Card List"}),`
`,n.jsx(e.h3,{id:"datatable",children:"DataTable"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Muitos items (20+), comparação de dados"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Desktop"}),": Excelente"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Mobile"}),": Problemático (scroll horizontal)"]}),`
`]}),`
`,n.jsx(e.h3,{id:"card-list",children:"Card List"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Poucos items (< 20), conteúdo rico"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Desktop"}),": Bom"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Mobile"}),": Excelente"]}),`
`]}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- ✅ DataTable para muitos items -->
<DataTable :columns="columns" :data="orders" />

<!-- ✅ Cards para poucos items -->
<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
  <Card v-for="product in products" :key="product.id">
    <!-- Conteúdo rico -->
  </Card>
</div>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-input-types",children:"📝 Input Types"}),`
`,n.jsx(e.h3,{id:"input",children:"Input"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Texto curto (nome, email, telefone)"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Max"}),": 1 linha"]}),`
`]}),`
`,n.jsx(e.h3,{id:"textarea",children:"Textarea"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Texto longo (descrição, comentário)"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Max"}),": Ilimitado"]}),`
`]}),`
`,n.jsx(e.h3,{id:"select",children:"Select"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": 5-15 opções predefinidas"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),": País, estado, categoria"]}),`
`]}),`
`,n.jsx(e.h3,{id:"radio-group",children:"Radio Group"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": 2-5 opções mutuamente exclusivas visíveis"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),": Tamanho (P/M/G), Tipo de envio"]}),`
`]}),`
`,n.jsx(e.h3,{id:"checkbox",children:"Checkbox"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Opção booleana ou múltipla escolha"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),": Aceito termos, Selecionar recursos"]}),`
`]}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- ✅ Select para muitas opções -->
<Select :options="countries" label="Country" />

<!-- ✅ Radio para poucas opções -->
<div>
  <Radio value="standard" label="Standard shipping" />
  <Radio value="express" label="Express shipping" />
</div>

<!-- ✅ Checkbox para sim/não -->
<Checkbox label="Send me email notifications" />
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-card-vs-panel",children:"🎴 Card vs Panel"}),`
`,n.jsx(e.h3,{id:"card",children:"Card"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Conteúdo independente, clicável"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Hover"}),": Sim, pode ter lift effect"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),": Product card, blog post card"]}),`
`]}),`
`,n.jsx(e.h3,{id:"panel-use-card-sem-hover",children:"Panel (use Card sem hover)"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Seção de conteúdo estático"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Hover"}),": Não"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Exemplo"}),": Dashboard widgets, formulário sections"]}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-spinner-vs-skeleton",children:"🔄 Spinner vs Skeleton"}),`
`,n.jsx(e.h3,{id:"spinner",children:"Spinner"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Loading rápido (< 3 segundos)"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Onde"}),": Centro da área de conteúdo"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Tamanho"}),": Proporcional ao espaço"]}),`
`]}),`
`,n.jsx(e.h3,{id:"skeleton",children:"Skeleton"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Loading lento (> 3 segundos)"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Onde"}),": No lugar exato do conteúdo"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Vantagem"}),": Mostra estrutura da página"]}),`
`]}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<!-- ✅ Spinner para ação rápida -->
<Button :loading="true">Saving...</Button>

<!-- ✅ Skeleton para lista -->
<Skeleton v-for="i in 5" :key="i" height="4rem" />
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-tooltip-vs-popover",children:"📍 Tooltip vs Popover"}),`
`,n.jsx(e.h3,{id:"tooltip",children:"Tooltip"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Informação extra curta (1-2 linhas)"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Trigger"}),": Hover"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Conteúdo"}),": Apenas texto"]}),`
`]}),`
`,n.jsx(e.h3,{id:"popover-use-dropdown",children:"Popover (use Dropdown)"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": Conteúdo rico (links, botões, imagens)"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Trigger"}),": Click"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Conteúdo"}),": HTML complexo"]}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-tabs-vs-segmented-control",children:"🧭 Tabs vs Segmented Control"}),`
`,n.jsx(e.h3,{id:"tabs",children:"Tabs"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": 2-7 seções de conteúdo"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Layout"}),": Horizontal"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Uso"}),": Settings páginas, detail views"]}),`
`]}),`
`,n.jsx(e.h3,{id:"segmented-control-use-tabs",children:"Segmented Control (use Tabs)"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Quando"}),": 2-4 toggles visuais"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Layout"}),": Compacto"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Uso"}),": View switchers (list/grid)"]}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-quick-decision-tree",children:"✅ Quick Decision Tree"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{children:`Preciso mostrar dados?
├─ Poucos items (< 20)? → Cards
└─ Muitos items (20+)? → DataTable

Preciso de input?
├─ Texto curto? → Input
├─ Texto longo? → Textarea
├─ Escolha única (5-15 opções)? → Select
├─ Escolha única (2-5 opções)? → Radio
└─ Múltipla escolha? → Checkbox

Preciso de feedback?
├─ Ação rápida? → Toast
├─ Informação contextual? → Alert
└─ Confirmação? → Modal

Preciso carregar?
├─ Rápido (< 3s)? → Spinner
└─ Lento (> 3s)? → Skeleton
`})})]})}function j(s={}){const{wrapper:e}={...i(),...s.components};return e?n.jsx(e,{...s,children:n.jsx(r,{...s})}):r(s)}export{j as default};
