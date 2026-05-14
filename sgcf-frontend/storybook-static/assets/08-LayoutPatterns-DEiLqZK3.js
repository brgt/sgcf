import{j as n,M as i}from"./index-COwdE6jM.js";import{useMDXComponents as t}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function r(a){const e={code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",p:"p",pre:"pre",ul:"ul",...t(),...a.components};return n.jsxs(n.Fragment,{children:[n.jsx(i,{title:"Style Guide/Layout Patterns"}),`
`,n.jsx(e.h1,{id:"layout-patterns",children:"Layout Patterns"}),`
`,n.jsx(e.p,{children:"Padrões comuns de layout para consistência em toda a aplicação."}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-list-pattern",children:"📄 List Pattern"}),`
`,n.jsx(e.p,{children:"Para exibir coleções de items."}),`
`,n.jsx(e.h3,{id:"desktop-datatable",children:"Desktop: DataTable"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<DataTable :columns="columns" :data="orders" @row-click="viewOrder">
  <template #cell-status="{ value }">
    <Badge :variant="statusVariant[value]">{{ value }}</Badge>
  </template>
</DataTable>
`})}),`
`,n.jsx(e.h3,{id:"mobile-card-list",children:"Mobile: Card List"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<div class="space-y-4">
  <Card v-for="order in orders" :key="order.id" padding="md" :hoverable="true">
    <div class="flex justify-between">
      <div>
        <h4 class="font-semibold">#{{ order.id }}</h4>
        <p class="text-sm text-gray-600">{{ order.customer }}</p>
      </div>
      <Badge :variant="order.status">{{ order.status }}</Badge>
    </div>
  </Card>
</div>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-detail-pattern",children:"🔍 Detail Pattern"}),`
`,n.jsx(e.p,{children:"Para visualizar informações detalhadas de um item."}),`
`,n.jsx(e.h3,{id:"estrutura",children:"Estrutura"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<PageLayout :sticky-header="true">
  <template #header>
    <PageHeader
      title="Order #12345"
      :breadcrumbs="breadcrumbs"
      :show-back-button="true"
      @back="goBack"
    >
      <template #actions>
        <Dropdown :items="actions" />
        <Button variant="primary">Edit order</Button>
      </template>
    </PageHeader>
  </template>

  <div class="space-y-6">
    <!-- Info Card -->
    <Card title="Order Information" padding="lg">
      <dl class="grid grid-cols-2 gap-4">
        <div>
          <dt class="text-sm text-gray-600">Customer</dt>
          <dd class="font-medium">John Doe</dd>
        </div>
        <div>
          <dt class="text-sm text-gray-600">Date</dt>
          <dd class="font-medium">05/01/2025</dd>
        </div>
      </dl>
    </Card>

    <!-- Items Table -->
    <Card title="Items" padding="lg">
      <DataTable :columns="itemColumns" :data="items" />
    </Card>
  </div>
</PageLayout>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-wizard-pattern-multi-step-form",children:"🧙 Wizard Pattern (Multi-Step Form)"}),`
`,n.jsx(e.p,{children:"Para formulários complexos divididos em etapas."}),`
`,n.jsx(e.h3,{id:"com-tabs",children:"Com Tabs"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<script setup>
const steps = [
  { key: 'info', label: 'Information' },
  { key: 'shipping', label: 'Shipping' },
  { key: 'payment', label: 'Payment' }
]
const currentStep = ref('info')
<\/script>

<template>
  <Card padding="none">
    <Tabs v-model="currentStep" :items="steps" />

    <div class="p-6">
      <div v-if="currentStep === 'info'">
        <!-- Step 1 content -->
      </div>
      <div v-if="currentStep === 'shipping'">
        <!-- Step 2 content -->
      </div>
      <div v-if="currentStep === 'payment'">
        <!-- Step 3 content -->
      </div>
    </div>

    <div class="flex justify-between p-6 border-t">
      <Button variant="ghost" :disabled="currentStep === 'info'">
        Back
      </Button>
      <Button variant="primary">
        {{ currentStep === 'payment' ? 'Complete' : 'Continue' }}
      </Button>
    </div>
  </Card>
</template>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-dashboard-pattern",children:"📊 Dashboard Pattern"}),`
`,n.jsx(e.p,{children:"Para painéis com múltiplos widgets."}),`
`,n.jsx(e.h3,{id:"grid-responsivo",children:"Grid Responsivo"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<PageLayout>
  <template #header>
    <PageHeader
      title="Dashboard"
      description="Your business overview"
    >
      <template #actions>
        <Select :options="dateRanges" />
      </template>
    </PageHeader>
  </template>

  <!-- KPIs -->
  <DashboardGrid :columns="4" gap="md">
    <Card v-for="kpi in kpis" :key="kpi.id" padding="lg">
      <p class="text-sm text-gray-600">{{ kpi.label }}</p>
      <p class="text-3xl font-bold mt-2">{{ kpi.value }}</p>
      <p class="text-sm text-green-600 mt-1">{{ kpi.change }}</p>
    </Card>
  </DashboardGrid>

  <!-- Charts -->
  <DashboardGrid :columns="3" gap="md" class="mt-6">
    <Card title="Revenue" padding="lg" class="col-span-2">
      <!-- Chart component -->
    </Card>
    <Card title="Top Products" padding="lg">
      <!-- List -->
    </Card>
  </DashboardGrid>
</PageLayout>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-search-results-pattern",children:"🔍 Search Results Pattern"}),`
`,n.jsx(e.p,{children:"Para exibir resultados de busca."}),`
`,n.jsx(e.h3,{id:"com-filtros",children:"Com Filtros"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<PageLayout :has-sidebar="true">
  <template #sidebar>
    <div class="p-4 space-y-6">
      <div>
        <h3 class="font-semibold mb-2">Category</h3>
        <Checkbox label="Electronics" />
        <Checkbox label="Clothing" />
        <Checkbox label="Books" />
      </div>
      <div>
        <h3 class="font-semibold mb-2">Price Range</h3>
        <!-- Range slider -->
      </div>
    </div>
  </template>

  <div>
    <div class="flex items-center justify-between mb-4">
      <p class="text-gray-600">1,234 results</p>
      <Select :options="sortOptions" />
    </div>

    <div class="grid grid-cols-3 gap-4">
      <Card v-for="product in results" :key="product.id">
        <!-- Product card -->
      </Card>
    </div>

    <Pagination
      v-model:current-page="currentPage"
      :total-pages="totalPages"
      class="mt-6"
    />
  </div>
</PageLayout>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-form-pattern",children:"📝 Form Pattern"}),`
`,n.jsx(e.p,{children:"Para formulários extensos."}),`
`,n.jsx(e.h3,{id:"single-column-recomendado",children:"Single Column (Recomendado)"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<Card title="User Information" padding="lg" class="max-w-2xl mx-auto">
  <form @submit.prevent="handleSubmit" class="space-y-6">
    <FormField label="Full name" :required="true">
      <Input v-model="form.name" />
    </FormField>

    <FormField label="Email" :required="true">
      <Input v-model="form.email" type="email" />
    </FormField>

    <FormField label="Phone">
      <Input v-model="form.phone" />
    </FormField>

    <FormField label="Bio">
      <Textarea v-model="form.bio" :rows="4" />
    </FormField>

    <div class="flex justify-end gap-2 pt-4">
      <Button variant="ghost">Cancel</Button>
      <Button variant="primary" type="submit">
        Save
      </Button>
    </div>
  </form>
</Card>
`})}),`
`,n.jsx(e.h3,{id:"multi-column-relacionados",children:"Multi Column (Relacionados)"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<div class="grid grid-cols-2 gap-4">
  <FormField label="First name">
    <Input v-model="form.firstName" />
  </FormField>
  <FormField label="Last name">
    <Input v-model="form.lastName" />
  </FormField>
</div>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-empty-state-pattern",children:"📄 Empty State Pattern"}),`
`,n.jsx(e.p,{children:"Para quando não há conteúdo."}),`
`,n.jsx(e.h3,{id:"primeira-vez-onboarding",children:"Primeira Vez (Onboarding)"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<EmptyState
  icon="i-carbon-shopping-cart"
  title="No products yet"
  description="Start by adding your first product to your catalog"
  action-label="Add product"
  @action="openAddProduct"
/>
`})}),`
`,n.jsx(e.h3,{id:"filtro-sem-resultados",children:"Filtro Sem Resultados"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<EmptyState
  icon="i-carbon-search"
  title="No results found"
  description="Try adjusting your filters or search term"
  action-label="Clear filters"
  @action="clearFilters"
/>
`})}),`
`,n.jsx(e.h3,{id:"erro",children:"Erro"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<EmptyState
  icon="i-carbon-warning"
  title="Unable to load data"
  description="There was an error loading your orders"
  action-label="Try again"
  @action="retry"
/>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-settings-pattern",children:"🎯 Settings Pattern"}),`
`,n.jsx(e.p,{children:"Para páginas de configurações."}),`
`,n.jsx(e.h3,{id:"com-navegação-lateral",children:"Com Navegação Lateral"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<PageLayout :has-sidebar="true">
  <template #sidebar>
    <nav class="p-4 space-y-2">
      <a href="#" class="block px-4 py-2 bg-primary/10 rounded">Profile</a>
      <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded">Account</a>
      <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded">Security</a>
      <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded">Notifications</a>
    </nav>
  </template>

  <div class="space-y-6">
    <Card title="Profile Settings" padding="lg">
      <!-- Settings form -->
    </Card>
    <Card title="Privacy" padding="lg">
      <!-- Privacy settings -->
    </Card>
  </div>
</PageLayout>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-error-pages-pattern",children:"🚨 Error Pages Pattern"}),`
`,n.jsx(e.h3,{id:"404-not-found",children:"404 Not Found"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<div class="flex items-center justify-center min-h-screen">
  <div class="text-center">
    <h1 class="text-6xl font-bold text-gray-300">404</h1>
    <h2 class="text-2xl font-semibold mt-4">Page not found</h2>
    <p class="text-gray-600 mt-2">
      The page you're looking for doesn't exist
    </p>
    <Button variant="primary" class="mt-6" @click="goHome">
      Go to dashboard
    </Button>
  </div>
</div>
`})}),`
`,n.jsx(e.h3,{id:"500-server-error",children:"500 Server Error"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<div class="flex items-center justify-center min-h-screen">
  <div class="text-center max-w-md">
    <div class="i-carbon-warning text-6xl text-red-500 mx-auto"></div>
    <h2 class="text-2xl font-semibold mt-4">Something went wrong</h2>
    <p class="text-gray-600 mt-2">
      We're sorry, but something went wrong. Our team has been notified.
    </p>
    <Button variant="primary" class="mt-6" @click="retry">
      Try again
    </Button>
  </div>
</div>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-pattern-checklist",children:"✅ Pattern Checklist"}),`
`,n.jsx(e.p,{children:"Ao criar uma nova tela, considere:"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"[ ] Usa PageLayout com header apropriado?"}),`
`,n.jsx(e.li,{children:"[ ] Breadcrumbs para navegação hierárquica?"}),`
`,n.jsx(e.li,{children:"[ ] Ações principais no header?"}),`
`,n.jsx(e.li,{children:"[ ] Grid responsivo para conteúdo?"}),`
`,n.jsx(e.li,{children:"[ ] Empty states para cada possibilidade?"}),`
`,n.jsx(e.li,{children:"[ ] Loading states apropriados?"}),`
`,n.jsx(e.li,{children:"[ ] Error states com recovery?"}),`
`,n.jsx(e.li,{children:"[ ] Mobile-friendly?"}),`
`,n.jsx(e.li,{children:"[ ] Keyboard navigation?"}),`
`,n.jsx(e.li,{children:"[ ] ARIA labels apropriados?"}),`
`]})]})}function h(a={}){const{wrapper:e}={...t(),...a.components};return e?n.jsx(e,{...a,children:n.jsx(r,{...a})}):r(a)}export{h as default};
