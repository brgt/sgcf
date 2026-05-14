import{j as e,M as l}from"./index-COwdE6jM.js";import{useMDXComponents as i}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function a(s){const n={code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",p:"p",pre:"pre",strong:"strong",ul:"ul",...i(),...s.components};return e.jsxs(e.Fragment,{children:[e.jsx(l,{title:"Style Guide/Form Patterns"}),`
`,e.jsx(n.h1,{id:"form-patterns",children:"Form Patterns"}),`
`,e.jsx(n.p,{children:"Best practices para criar formulários eficientes e acessíveis."}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-layout",children:"📐 Layout"}),`
`,e.jsx(n.h3,{id:"single-column-recomendado",children:"Single Column (Recomendado)"}),`
`,e.jsx(n.p,{children:`✅ Mais fácil de ler e preencher
✅ Mobile-friendly
✅ Menor taxa de erro`}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<form class="max-w-2xl space-y-6">
  <FormField label="Full name" :required="true">
    <Input v-model="form.name" />
  </FormField>

  <FormField label="Email" :required="true">
    <Input v-model="form.email" type="email" />
  </FormField>

  <FormField label="Phone">
    <Input v-model="form.phone" />
  </FormField>
</form>
`})}),`
`,e.jsx(n.h3,{id:"multi-column-campos-relacionados",children:"Multi Column (Campos Relacionados)"}),`
`,e.jsx(n.p,{children:"Use apenas para campos intimamente relacionados"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="grid grid-cols-2 gap-4">
  <FormField label="First name">
    <Input v-model="form.firstName" />
  </FormField>
  <FormField label="Last name">
    <Input v-model="form.lastName" />
  </FormField>
</div>

<div class="grid grid-cols-3 gap-4">
  <FormField label="City">
    <Input v-model="form.city" />
  </FormField>
  <FormField label="State">
    <Select :options="states" />
  </FormField>
  <FormField label="ZIP">
    <Input v-model="form.zip" />
  </FormField>
</div>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-required-fields",children:"✨ Required Fields"}),`
`,e.jsx(n.h3,{id:"indicação-visual",children:"Indicação Visual"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<FormField label="Email" :required="true">
  <Input v-model="email" />
</FormField>

<!-- Renderiza -->
<label>
  Email <abbr title="required" aria-label="required">*</abbr>
</label>
`})}),`
`,e.jsx(n.h3,{id:"alternativa-muitos-required",children:"Alternativa (Muitos Required)"}),`
`,e.jsx(n.p,{children:"Se maioria dos campos é required, marque apenas os opcionais"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<FormField label="Phone (optional)">
  <Input v-model="phone" />
</FormField>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-validação",children:"✅ Validação"}),`
`,e.jsx(n.h3,{id:"inline-real-time",children:"Inline (Real-time)"}),`
`,e.jsx(n.p,{children:"Para feedback imediato em campos complexos"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Input
  v-model="password"
  type="password"
  label="Password"
  :error="passwordError"
  @blur="validatePassword"
/>

<div v-if="password" class="mt-2 space-y-1 text-sm">
  <div :class="hasLength ? 'text-green-600' : 'text-gray-500'">
    <span class="i-carbon-checkmark"></span> At least 8 characters
  </div>
  <div :class="hasNumber ? 'text-green-600' : 'text-gray-500'">
    <span class="i-carbon-checkmark"></span> Contains a number
  </div>
</div>
`})}),`
`,e.jsx(n.h3,{id:"on-submit",children:"On Submit"}),`
`,e.jsx(n.p,{children:"Para validação geral"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<form @submit.prevent="handleSubmit">
  <Alert v-if="errors.length" variant="error" class="mb-4">
    Please fix the following errors:
    <ul class="mt-2 list-disc list-inside">
      <li v-for="error in errors" :key="error">{{ error }}</li>
    </ul>
  </Alert>

  <!-- Form fields -->

  <Button type="submit">Submit</Button>
</form>
`})}),`
`,e.jsx(n.h3,{id:"progressive-disclosure",children:"Progressive Disclosure"}),`
`,e.jsx(n.p,{children:"Valide apenas campos já visitados"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Input
  v-model="email"
  :error="touched.email ? emailError : ''"
  @blur="touched.email = true"
/>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-helper-text",children:"💬 Helper Text"}),`
`,e.jsx(n.h3,{id:"quando-usar",children:"Quando Usar"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"Formato esperado não é óbvio"}),`
`,e.jsx(n.li,{children:"Há restrições específicas"}),`
`,e.jsx(n.li,{children:"Usuário pode precisar de contexto adicional"}),`
`]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<FormField
  label="Username"
  helper-text="3-20 characters, letters and numbers only"
>
  <Input v-model="username" />
</FormField>

<FormField
  label="API Key"
  helper-text="Find this in your account settings"
>
  <Input v-model="apiKey" type="password" />
</FormField>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-input-types",children:"🔘 Input Types"}),`
`,e.jsx(n.h3,{id:"text-inputs",children:"Text Inputs"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<!-- Nome -->
<Input type="text" autocomplete="name" />

<!-- Email -->
<Input type="email" autocomplete="email" />

<!-- Telefone -->
<Input type="tel" autocomplete="tel" placeholder="(XX) XXXXX-XXXX" />

<!-- URL -->
<Input type="url" placeholder="https://" />

<!-- Número -->
<Input type="number" :min="0" :step="1" />

<!-- Senha -->
<Input type="password" autocomplete="current-password" />
`})}),`
`,e.jsx(n.h3,{id:"select-vs-radio-vs-checkbox",children:"Select vs Radio vs Checkbox"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Select"}),": 5+ opções"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Select :options="countries" label="Country" />
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Radio"}),": 2-5 opções, visíveis"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="space-y-2">
  <Radio v-model="shipping" value="standard" label="Standard (5-7 days)" />
  <Radio v-model="shipping" value="express" label="Express (2-3 days)" />
  <Radio v-model="shipping" value="overnight" label="Overnight" />
</div>
`})}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Checkbox"}),": Múltipla escolha ou booleano"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Checkbox v-model="newsletter" label="Subscribe to newsletter" />

<div class="space-y-2">
  <Checkbox v-model="features" value="sms" label="SMS notifications" />
  <Checkbox v-model="features" value="email" label="Email notifications" />
  <Checkbox v-model="features" value="push" label="Push notifications" />
</div>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-grupos-lógicos",children:"🎯 Grupos Lógicos"}),`
`,e.jsx(n.p,{children:"Agrupe campos relacionados"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Card title="Personal Information" padding="lg">
  <div class="space-y-4">
    <Input label="Full name" />
    <Input label="Email" />
    <Input label="Phone" />
  </div>
</Card>

<Card title="Shipping Address" padding="lg" class="mt-6">
  <div class="space-y-4">
    <Input label="Street address" />
    <div class="grid grid-cols-3 gap-4">
      <Input label="City" />
      <Select label="State" />
      <Input label="ZIP" />
    </div>
  </div>
</Card>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-actions",children:"📤 Actions"}),`
`,e.jsx(n.h3,{id:"alignment",children:"Alignment"}),`
`,e.jsxs(n.p,{children:[e.jsx(n.strong,{children:"Primary à direita"}),", Secondary à esquerda"]}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div class="flex justify-between mt-6">
  <Button variant="ghost">Cancel</Button>
  <div class="flex gap-2">
    <Button variant="secondary">Save draft</Button>
    <Button variant="primary" type="submit">
      Submit
    </Button>
  </div>
</div>
`})}),`
`,e.jsx(n.h3,{id:"loading-state",children:"Loading State"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Button variant="primary" :loading="submitting" type="submit">
  {{ submitting ? 'Saving...' : 'Save changes' }}
</Button>
`})}),`
`,e.jsx(n.h3,{id:"disabled-state",children:"Disabled State"}),`
`,e.jsx(n.p,{children:"Explique por que está disabled"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Button variant="primary" :disabled="!isValid">
  Continue
</Button>
<p v-if="!isValid" class="text-sm text-gray-600 mt-2">
  Fill in all required fields to continue
</p>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-success-feedback",children:"✅ Success Feedback"}),`
`,e.jsx(n.h3,{id:"inline-pequenas-mudanças",children:"Inline (Pequenas mudanças)"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<div v-if="saved" class="flex items-center gap-2 text-green-600">
  <span class="i-carbon-checkmark"></span>
  Saved
</div>
`})}),`
`,e.jsx(n.h3,{id:"toast-ações-rápidas",children:"Toast (Ações rápidas)"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`// Após submit bem-sucedido
toast.success('Profile updated successfully')
`})}),`
`,e.jsx(n.h3,{id:"page-redirect-criação-de-recursos",children:"Page Redirect (Criação de recursos)"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`// Após criar order
router.push({ name: 'order-detail', params: { id: newOrder.id } })
toast.success('Order created successfully')
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-multi-step-forms",children:"🔄 Multi-Step Forms"}),`
`,e.jsx(n.h3,{id:"com-tabs",children:"Com Tabs"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Tabs v-model="currentStep" :items="steps" />

<div v-show="currentStep === 1">
  <!-- Step 1 fields -->
</div>

<div class="flex justify-between mt-6">
  <Button :disabled="currentStep === 1" @click="prevStep">
    Back
  </Button>
  <Button @click="nextStep">
    {{ currentStep === totalSteps ? 'Submit' : 'Continue' }}
  </Button>
</div>
`})}),`
`,e.jsx(n.h3,{id:"com-progress",children:"Com Progress"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<Progress
  :value="(currentStep / totalSteps) * 100"
  :show-label="true"
  class="mb-6"
/>

<p class="text-sm text-gray-600 mb-4">
  Step {{ currentStep }} of {{ totalSteps }}
</p>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-auto-save",children:"🎨 Auto-save"}),`
`,e.jsx(n.p,{children:"Para formulários longos"}),`
`,e.jsx(n.pre,{children:e.jsx(n.code,{className:"language-vue",children:`<script setup>
const form = ref({})
const saveStatus = ref('saved') // 'saved' | 'saving' | 'unsaved'

watch(form, () => {
  saveStatus.value = 'unsaved'
  debouncedSave()
}, { deep: true })

const debouncedSave = useDebounceFn(async () => {
  saveStatus.value = 'saving'
  await saveDraft(form.value)
  saveStatus.value = 'saved'
}, 2000)
<\/script>

<template>
  <div class="flex items-center gap-2 text-sm text-gray-600">
    <Spinner v-if="saveStatus === 'saving'" size="sm" />
    <span v-else-if="saveStatus === 'saved'" class="i-carbon-checkmark text-green-600"></span>
    {{ saveStatus === 'saved' ? 'All changes saved' : 'Saving...' }}
  </div>
</template>
`})}),`
`,e.jsx(n.hr,{}),`
`,e.jsx(n.h2,{id:"-accessibility-checklist",children:"✅ Accessibility Checklist"}),`
`,e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"[ ] Labels em todos os inputs"}),`
`,e.jsx(n.li,{children:"[ ] Required fields marcados"}),`
`,e.jsx(n.li,{children:"[ ] Mensagens de erro associadas (aria-describedby)"}),`
`,e.jsx(n.li,{children:"[ ] Focus visível"}),`
`,e.jsx(n.li,{children:"[ ] Tab order lógico"}),`
`,e.jsx(n.li,{children:"[ ] Autocomplete attributes"}),`
`,e.jsx(n.li,{children:"[ ] Placeholder não substitui label"}),`
`,e.jsxs(n.li,{children:["[ ] Botão submit dentro do ",e.jsx(n.code,{children:"<form>"})]}),`
`,e.jsx(n.li,{children:'[ ] Error messages com role="alert"'}),`
`,e.jsx(n.li,{children:"[ ] Disabled state explicado"}),`
`]})]})}function p(s={}){const{wrapper:n}={...i(),...s.components};return n?e.jsx(n,{...s,children:e.jsx(a,{...s})}):a(s)}export{p as default};
