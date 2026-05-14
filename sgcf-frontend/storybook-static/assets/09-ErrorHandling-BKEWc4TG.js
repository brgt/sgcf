import{j as e,M as o}from"./index-COwdE6jM.js";import{useMDXComponents as s}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function i(r){const n={code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",p:"p",pre:"pre",strong:"strong",ul:"ul",...s(),...r.components};return e.jsxs(e.Fragment,{children:[e.jsx(o,{title:"Style Guide/Error Handling"}),`
`,e.jsx(n.h1,{id:"error-handling",children:"Error Handling"}),`
`,e.jsx(n.p,{children:"Como lidar com erros de forma empática e útil."}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-hierarquia-de-erros",children:"🎯 Hierarquia de Erros"}),`
`,e.jsx(n.h3,{id:"1-field-level-inline",children:"1. Field-Level (Inline)"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Quando"}),": Erro em campo específico de formulário"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Input
  v-model="email"
  label="Email"
  :error="emailError"
/>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Mensagens"}),":"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{children:`✅ "Email is required"
✅ "Invalid email format"
✅ "This email is already registered"
`})}),`
`,e.jsx(n.h3,{id:"2-form-level-topo-do-form",children:"2. Form-Level (Topo do form)"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Quando"}),": Erro geral do formulário"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Alert variant="error" class="mb-4">
  Please fix the errors below before submitting
</Alert>
`})}),`
`,e.jsx(n.h3,{id:"3-page-level-banner",children:"3. Page-Level (Banner)"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Quando"}),": Erro afeta página inteira"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Alert variant="error" :dismissible="true">
  Unable to load data. Check your connection and refresh the page.
</Alert>
`})}),`
`,e.jsx(n.h3,{id:"4-global-toastmodal",children:"4. Global (Toast/Modal)"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Quando"}),": Erro crítico do sistema"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Modal v-model="showError" title="Connection Error" size="sm">
  <p>Lost connection to server. Reconnecting...</p>
</Modal>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-tipos-de-erro",children:"📝 Tipos de Erro"}),`
`,e.jsx(n.h3,{id:"validação",children:"Validação"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Estrutura"}),": O quê + Por quê"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{children:`❌ "Invalid"
✅ "Email is required"
✅ "Password must be at least 8 characters"
✅ "Phone number must be in format (XX) XXXXX-XXXX"
`})}),`
`,e.jsx(n.h3,{id:"network",children:"Network"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Estrutura"}),": O quê aconteceu + O que fazer"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{children:`✅ "Unable to save. Check your connection and try again."
✅ "Connection timeout. Please try again."
✅ "Server not responding. Try again in a few minutes."
`})}),`
`,e.jsx(n.h3,{id:"autenticação",children:"Autenticação"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Estrutura"}),": Problema + Solução"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{children:`✅ "Incorrect password. Try again or reset your password."
✅ "Session expired. Please log in again."
✅ "Account locked. Contact support to unlock."
`})}),`
`,e.jsx(n.h3,{id:"permissão",children:"Permissão"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Estrutura"}),": O quê não pode + Por quê"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{children:`✅ "You don't have permission to delete this order"
✅ "This feature is only available on Premium plan"
✅ "Contact your admin to access this page"
`})}),`
`,e.jsx(n.h3,{id:"negócio-business-logic",children:"Negócio (Business Logic)"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Estrutura"}),": O quê falhou + Como resolver"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{children:`✅ "Cannot delete product. Remove it from active orders first."
✅ "Insufficient stock. Only 5 units available."
✅ "Order cannot be cancelled after shipment."
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-recovery-strategies",children:"🔄 Recovery Strategies"}),`
`,e.jsx(n.h3,{id:"retry-automático",children:"Retry Automático"}),`
`,e.jsx(n.p,{children:"Para erros transitórios (network, timeout)"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div v-if="error">
  <Alert variant="error">
    Failed to load. Retrying automatically...
  </Alert>
  <Progress :value="retryProgress" />
</div>
`})}),`
`,e.jsx(n.h3,{id:"retry-manual",children:"Retry Manual"}),`
`,e.jsx(n.p,{children:"Para erros que precisam intervenção"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<EmptyState
  icon="i-carbon-warning"
  title="Unable to load orders"
  description="There was an error loading your data"
  action-label="Try again"
  @action="retry"
/>
`})}),`
`,e.jsx(n.h3,{id:"fallback",children:"Fallback"}),`
`,e.jsx(n.p,{children:"Quando dados parciais estão disponíveis"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Alert variant="warning">
  Some data may be outdated. Last synced 2 hours ago.
</Alert>
<!-- Mostra dados em cache -->
`})}),`
`,e.jsx(n.h3,{id:"degradação",children:"Degradação"}),`
`,e.jsx(n.p,{children:"Sistema funciona com funcionalidade reduzida"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Alert variant="info">
  Search is temporarily unavailable. Showing all results.
</Alert>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-error-pages",children:"📄 Error Pages"}),`
`,e.jsx(n.h3,{id:"404---not-found",children:"404 - Not Found"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="flex items-center justify-center min-h-screen p-4">
  <div class="text-center max-w-md">
    <h1 class="text-6xl font-bold text-gray-300">404</h1>
    <h2 class="text-2xl font-semibold mt-4">Page not found</h2>
    <p class="text-gray-600 mt-2">
      The page you're looking for doesn't exist or has been moved.
    </p>
    <div class="flex gap-2 justify-center mt-6">
      <Button variant="ghost" @click="goBack">Go back</Button>
      <Button variant="primary" @click="goHome">Go to dashboard</Button>
    </div>
  </div>
</div>
`})}),`
`,e.jsx(n.h3,{id:"500---server-error",children:"500 - Server Error"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="text-center max-w-md mx-auto">
  <div class="i-carbon-warning text-6xl text-red-500"></div>
  <h2 class="text-2xl font-semibold mt-4">Something went wrong</h2>
  <p class="text-gray-600 mt-2">
    We're experiencing technical difficulties. Our team has been notified and is working on it.
  </p>
  <Button variant="primary" class="mt-6" @click="retry">
    Try again
  </Button>
  <p class="text-sm text-gray-500 mt-4">
    Error ID: {{ errorId }}
  </p>
</div>
`})}),`
`,e.jsx(n.h3,{id:"503---maintenance",children:"503 - Maintenance"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="text-center max-w-md mx-auto">
  <div class="i-carbon-tools text-6xl text-blue-500"></div>
  <h2 class="text-2xl font-semibold mt-4">Under Maintenance</h2>
  <p class="text-gray-600 mt-2">
    We're performing scheduled maintenance. We'll be back soon!
  </p>
  <p class="text-sm text-gray-500 mt-4">
    Expected return: {{ estimatedTime }}
  </p>
</div>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-exemplos-por-componente",children:"🎨 Exemplos por Componente"}),`
`,e.jsx(n.h3,{id:"datatable-error",children:"DataTable Error"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<DataTable :columns="columns" :data="orders" :loading="loading" />

<div v-if="error" class="text-center py-12">
  <div class="i-carbon-warning text-4xl text-red-500 mb-4"></div>
  <p class="font-medium">Unable to load orders</p>
  <Button variant="ghost" class="mt-4" @click="retry">
    Try again
  </Button>
</div>
`})}),`
`,e.jsx(n.h3,{id:"form-submission-error",children:"Form Submission Error"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<form @submit.prevent="handleSubmit">
  <Alert v-if="submitError" variant="error" class="mb-4">
    {{ submitError }}
  </Alert>

  <!-- Form fields -->

  <Button type="submit" :loading="submitting">
    Submit
  </Button>
</form>
`})}),`
`,e.jsx(n.h3,{id:"file-upload-error",children:"File Upload Error"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div>
  <input type="file" @change="handleFile" />

  <Alert v-if="fileError" variant="error" class="mt-2">
    {{ fileError }}
  </Alert>
</div>

<script>
const handleFile = (e) => {
  const file = e.target.files[0]

  if (file.size > 5 * 1024 * 1024) {
    fileError.value = "File too large. Maximum size is 5MB."
    return
  }

  if (!['image/jpeg', 'image/png'].includes(file.type)) {
    fileError.value = "Invalid file type. Only JPG and PNG allowed."
    return
  }

  // Process file
}
<\/script>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-checklist-de-erro-bem-tratado",children:"✅ Checklist de Erro Bem Tratado"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsxs(n.li,{children:["[ ] ",e.jsx(n.strong,{children:"Clara"}),": Usuário entende o que aconteceu?"]}),`
`,e.jsxs(n.li,{children:["[ ] ",e.jsx(n.strong,{children:"Acionável"}),": Usuário sabe como resolver?"]}),`
`,e.jsxs(n.li,{children:["[ ] ",e.jsx(n.strong,{children:"Empática"}),": Tom não culpa o usuário?"]}),`
`,e.jsxs(n.li,{children:["[ ] ",e.jsx(n.strong,{children:"Específica"}),": Detalha o problema real?"]}),`
`,e.jsxs(n.li,{children:["[ ] ",e.jsx(n.strong,{children:"Visível"}),": Erro é óbvio para o usuário?"]}),`
`,e.jsxs(n.li,{children:["[ ] ",e.jsx(n.strong,{children:"Recuperável"}),": Oferece caminho de recovery?"]}),`
`,e.jsxs(n.li,{children:["[ ] ",e.jsx(n.strong,{children:"Logada"}),": Erro foi registrado para debugging?"]}),`
`,e.jsxs(n.li,{children:["[ ] ",e.jsx(n.strong,{children:"Acessível"}),": Screen readers podem ler?"]}),`
`]}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-o-que-não-fazer",children:"🚫 O que NÃO fazer"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{children:`❌ "Error"
❌ "Something went wrong"
❌ "Invalid input"
❌ "Error code: 0x8007042B"
❌ "null is not defined"
❌ "You made a mistake"
❌ "Failed"
`})}),`
`,e.jsx(n.h2,{id:"-o-que-fazer",children:"✅ O que fazer"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{children:`✅ "Email is required"
✅ "Unable to save. Check your connection."
✅ "Password must be at least 8 characters"
✅ "Order #12345 not found"
✅ "Cannot delete product while in use"
`})})]})}function x(r={}){const{wrapper:n}={...s(),...r.components};return n?e.jsx(n,{...r,children:e.jsx(i,{...r})}):i(r)}export{x as default};
