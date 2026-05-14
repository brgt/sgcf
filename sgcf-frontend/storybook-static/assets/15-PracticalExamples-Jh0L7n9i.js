import{j as n,M as i}from"./index-COwdE6jM.js";import{useMDXComponents as s}from"./index-owtiYoj_.js";import"./iframe-CsQkYykc.js";import"./index-BfiH1CDa.js";import"./_commonjsHelpers-Cpj98o6Y.js";import"./index-CeQnsxAM.js";import"./index-DrFu-skq.js";function t(r){const e={a:"a",code:"code",h1:"h1",h2:"h2",h3:"h3",hr:"hr",li:"li",p:"p",pre:"pre",strong:"strong",ul:"ul",...s(),...r.components};return n.jsxs(n.Fragment,{children:[n.jsx(i,{title:"Style Guide/Practical Examples"}),`
`,n.jsx(e.h1,{id:"practical-examples",children:"Practical Examples"}),`
`,n.jsx(e.p,{children:"Exemplos práticos de implementações reais do Nordware Platform."}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-1-transport-dashboard---flywheel-visualization",children:"🚛 1. Transport Dashboard - Flywheel Visualization"}),`
`,n.jsx(e.h3,{id:"real-implementation",children:"Real Implementation"}),`
`,n.jsx(e.p,{children:"Implementação real do módulo de Transporte com FlywheelPhase components."}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
    <FlywheelPhase
      v-for="phase in flywheelPhases"
      :key="phase.id"
      :title="phase.name"
      :description="phase.description"
      :icon="phase.icon"
      :health="phase.health"
      :variant="phase.color"
      :metrics="phase.metrics"
    />
  </div>
</template>

<script setup>
import { FlywheelPhase } from '@shared/ui/molecules'

const flywheelPhases = [
  {
    id: 1,
    name: 'Atrair',
    description: 'Cotação Inteligente',
    icon: 'i-carbon-chart-line-smooth',
    health: 92,
    color: 'primary',
    metrics: {
      quotes: '1.240',
      conversionRate: '68.5%',
      avgResponseTime: '1.2s'
    }
  },
  // ... more phases
]
<\/script>
`})}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Key Features:"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"✅ Hover effect com lift animation"}),`
`,n.jsx(e.li,{children:"✅ Health circle com conic-gradient"}),`
`,n.jsx(e.li,{children:"✅ Metrics grid responsivo"}),`
`,n.jsx(e.li,{children:"✅ 4 color variants"}),`
`]}),`
`,n.jsxs(e.p,{children:[n.jsx(e.strong,{children:"Files:"})," ",n.jsx(e.code,{children:"src/modules/transport/views/TransportDashboard.vue:371-404"})]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-2-form-patterns---multi-step-checkout",children:"📋 2. Form Patterns - Multi-Step Checkout"}),`
`,n.jsx(e.h3,{id:"live-example",children:"Live Example"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <Card padding="none" class="max-w-2xl mx-auto">
    <!-- Progress Steps -->
    <div class="p-6 border-b border-border">
      <div class="flex items-center justify-between mb-4">
        <div
          v-for="(step, i) in steps"
          :key="step.key"
          class="flex items-center"
          :class="i < steps.length - 1 ? 'flex-1' : ''"
        >
          <div
            class="flex items-center justify-center w-10 h-10 rounded-full"
            :class="
              currentStep >= i + 1
                ? 'bg-primary text-white'
                : 'bg-surface border border-border'
            "
          >
            {{ i + 1 }}
          </div>
          <div v-if="i < steps.length - 1" class="flex-1 h-1 mx-4 bg-border" />
        </div>
      </div>
    </div>

    <!-- Form Content -->
    <div class="p-6">
      <div v-show="currentStep === 1" class="space-y-4">
        <Input label="Full Name" required />
        <Input label="Email" type="email" required />
      </div>

      <div v-show="currentStep === 2" class="space-y-4">
        <Input label="Street Address" required />
        <div class="grid grid-cols-3 gap-4">
          <Input label="City" />
          <Select label="State" :options="states" />
          <Input label="ZIP" />
        </div>
      </div>

      <div v-show="currentStep === 3" class="space-y-4">
        <Input label="Card Number" />
        <div class="grid grid-cols-2 gap-4">
          <Input label="Expiry" placeholder="MM/YY" />
          <Input label="CVV" />
        </div>
      </div>
    </div>

    <!-- Actions -->
    <div class="flex justify-between p-6 border-t border-border">
      <Button variant="ghost" :disabled="currentStep === 1" @click="prevStep">
        Back
      </Button>
      <Button variant="primary" @click="nextStep">
        {{ currentStep === 3 ? 'Complete Order' : 'Continue' }}
      </Button>
    </div>
  </Card>
</template>
`})}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Best Practices Applied:"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"✅ Visual progress indicator"}),`
`,n.jsx(e.li,{children:"✅ Single column layout"}),`
`,n.jsx(e.li,{children:"✅ Grouped related fields"}),`
`,n.jsx(e.li,{children:"✅ Clear action hierarchy"}),`
`,n.jsx(e.li,{children:"✅ Disabled state with reasoning"}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-3-datatable---orders-list",children:"📊 3. DataTable - Orders List"}),`
`,n.jsx(e.h3,{id:"implementation-with-real-data",children:"Implementation with Real Data"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <DataTable
    :columns="columns"
    :data="orders"
    :loading="loading"
    @row-click="viewOrder"
  >
    <!-- Custom Status Cell -->
    <template #cell-status="{ value }">
      <Badge :variant="statusVariant[value]">
        {{ value }}
      </Badge>
    </template>

    <!-- Custom Actions Cell -->
    <template #cell-actions="{ row }">
      <Dropdown :items="getActions(row)" />
    </template>
  </DataTable>
</template>

<script setup>
import { DataTable, Badge, Dropdown } from '@shared/ui/molecules'

const columns = [
  { key: 'id', label: 'Order ID', sortable: true },
  { key: 'customer', label: 'Customer', sortable: true },
  { key: 'date', label: 'Date', sortable: true },
  { key: 'total', label: 'Total' },
  { key: 'status', label: 'Status' },
  { key: 'actions', label: 'Actions', align: 'right' }
]

const statusVariant = {
  'Delivered': 'success',
  'In Transit': 'info',
  'Pending': 'warning',
  'Cancelled': 'error'
}

const getActions = (row) => [
  { label: 'View Details', value: 'view', icon: 'i-carbon-view' },
  { label: 'Edit', value: 'edit', icon: 'i-carbon-edit' },
  { label: 'Cancel', value: 'cancel', icon: 'i-carbon-close', divider: true }
]
<\/script>
`})}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Features:"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"✅ Sortable columns"}),`
`,n.jsx(e.li,{children:"✅ Custom cell renderers"}),`
`,n.jsx(e.li,{children:"✅ Badge status indicators"}),`
`,n.jsx(e.li,{children:"✅ Actions dropdown per row"}),`
`,n.jsx(e.li,{children:"✅ Row click handler"}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-4-dashboard-kpi-cards",children:"📈 4. Dashboard KPI Cards"}),`
`,n.jsx(e.h3,{id:"grid-layout",children:"Grid Layout"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
    <Card
      v-for="kpi in kpis"
      :key="kpi.id"
      padding="lg"
      :hoverable="true"
    >
      <div class="flex items-start justify-between">
        <div>
          <p class="text-sm text-text-secondary">{{ kpi.label }}</p>
          <p class="text-3xl font-bold mt-2">{{ kpi.value }}</p>
          <div class="flex items-center gap-2 mt-2">
            <span
              :class="kpi.trend > 0 ? 'text-success' : 'text-error'"
              class="text-sm font-semibold"
            >
              {{ kpi.trend > 0 ? '↑' : '↓' }} {{ Math.abs(kpi.trend) }}%
            </span>
            <span class="text-xs text-text-tertiary">vs last month</span>
          </div>
        </div>
        <div
          class="p-3 rounded-lg"
          :style="{ background: kpi.color + '20', color: kpi.color }"
        >
          <span :class="kpi.icon" class="text-2xl" />
        </div>
      </div>
    </Card>
  </div>
</template>

<script setup>
const kpis = [
  {
    id: 1,
    label: 'Total Revenue',
    value: 'R$ 124.5K',
    trend: 12.5,
    icon: 'i-carbon-currency-dollar',
    color: '#00E5A0'
  },
  {
    id: 2,
    label: 'Active Orders',
    value: '1,834',
    trend: 8.2,
    icon: 'i-carbon-shopping-cart',
    color: '#00f9b8'
  },
  {
    id: 3,
    label: 'Avg. Delivery Time',
    value: '2.4 days',
    trend: -5.3,
    icon: 'i-carbon-delivery-truck',
    color: '#4FB3FF'
  },
  {
    id: 4,
    label: 'Customer Satisfaction',
    value: '96.8%',
    trend: 2.1,
    icon: 'i-carbon-favorite',
    color: '#FFB020'
  }
]
<\/script>
`})}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Design Patterns:"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"✅ Icon with background color"}),`
`,n.jsx(e.li,{children:"✅ Trend indicator with arrow"}),`
`,n.jsx(e.li,{children:"✅ Hover effect on cards"}),`
`,n.jsx(e.li,{children:"✅ Responsive grid (1/2/4 columns)"}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-5-button-hierarchy-example",children:"🔘 5. Button Hierarchy Example"}),`
`,n.jsx(e.h3,{id:"-do-clear-visual-hierarchy",children:"✅ DO: Clear Visual Hierarchy"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <div class="flex items-center gap-3">
    <!-- Tertiary action -->
    <Button variant="ghost">
      Cancel
    </Button>

    <!-- Secondary action -->
    <Button variant="secondary">
      Save Draft
    </Button>

    <!-- Primary action -->
    <Button variant="primary">
      Publish Post
    </Button>
  </div>
</template>
`})}),`
`,n.jsxs(e.p,{children:[n.jsx(e.strong,{children:"Result:"}),' User instantly knows "Publish Post" is the main action.']}),`
`,n.jsx(e.h3,{id:"-dont-multiple-primary-buttons",children:"❌ DON'T: Multiple Primary Buttons"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <div class="flex items-center gap-3">
    <Button variant="primary">Save</Button>
    <Button variant="primary">Submit</Button>
    <Button variant="primary">Publish</Button>
  </div>
</template>
`})}),`
`,n.jsxs(e.p,{children:[n.jsx(e.strong,{children:"Problem:"})," User doesn't know which action is most important."]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-6-alert-variants-in-context",children:"🔔 6. Alert Variants in Context"}),`
`,n.jsx(e.h3,{id:"page-level-alerts",children:"Page-Level Alerts"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <div class="space-y-4">
    <!-- Info: Feature announcement -->
    <Alert variant="info" :dismissible="true">
      <strong>New Feature:</strong> AI-powered route optimization is now available!
    </Alert>

    <!-- Success: Action confirmation -->
    <Alert variant="success">
      Order #12345 was created successfully and is being processed.
    </Alert>

    <!-- Warning: Action needed -->
    <Alert variant="warning">
      Your trial expires in 3 days. Upgrade now to continue using premium features.
    </Alert>

    <!-- Error: Critical issue -->
    <Alert variant="error">
      Unable to save changes. Check your connection and try again.
    </Alert>
  </div>
</template>
`})}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Usage Guidelines:"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Info"}),": Neutral information, tips, announcements"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Success"}),": Successful actions, confirmations"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Warning"}),": Cautionary information, expiring trials"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Error"}),": Failed actions, validation errors"]}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-7-loading-states",children:"💤 7. Loading States"}),`
`,n.jsx(e.h3,{id:"skeleton-vs-spinner",children:"Skeleton vs Spinner"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <!-- ✅ Use Skeleton for predictable layouts -->
  <Card v-if="loading" padding="lg">
    <div class="space-y-4">
      <Skeleton height="2rem" width="60%" />
      <Skeleton height="1rem" />
      <Skeleton height="1rem" width="80%" />
      <div class="grid grid-cols-3 gap-4 mt-6">
        <Skeleton height="4rem" />
        <Skeleton height="4rem" />
        <Skeleton height="4rem" />
      </div>
    </div>
  </Card>

  <!-- ✅ Use Spinner for quick actions -->
  <Button variant="primary" :loading="submitting">
    {{ submitting ? 'Saving...' : 'Save Changes' }}
  </Button>
</template>
`})}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Rule of Thumb:"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Skeleton"}),": Long loading (> 3s), shows page structure"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.strong,{children:"Spinner"}),": Quick actions (< 3s), button states"]}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-8-empty-states-with-actions",children:"🚫 8. Empty States with Actions"}),`
`,n.jsx(e.h3,{id:"first-time-user-onboarding",children:"First-Time User (Onboarding)"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <EmptyState
    icon="i-carbon-shopping-cart"
    title="No products yet"
    description="Start by adding your first product to your catalog"
    action-label="Add Product"
    @action="openProductModal"
  />
</template>
`})}),`
`,n.jsx(e.h3,{id:"no-search-results",children:"No Search Results"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <EmptyState
    icon="i-carbon-search"
    title="No results found"
    description="Try adjusting your filters or search term"
    action-label="Clear Filters"
    @action="clearFilters"
  />
</template>
`})}),`
`,n.jsx(e.h3,{id:"error-state",children:"Error State"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <EmptyState
    icon="i-carbon-warning"
    title="Unable to load data"
    description="There was an error loading your orders"
    action-label="Try Again"
    @action="retry"
  />
</template>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-9-modal-patterns",children:"💬 9. Modal Patterns"}),`
`,n.jsx(e.h3,{id:"confirmation-modal-sm",children:"Confirmation Modal (sm)"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <Modal v-model="showDelete" title="Delete Product?" size="sm">
    <p class="text-text-secondary">
      This action cannot be undone. The product will be permanently deleted.
    </p>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button variant="ghost" @click="showDelete = false">
          Cancel
        </Button>
        <Button variant="danger" @click="handleDelete">
          Delete Product
        </Button>
      </div>
    </template>
  </Modal>
</template>
`})}),`
`,n.jsx(e.h3,{id:"form-modal-md",children:"Form Modal (md)"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <Modal v-model="showForm" title="Add New Product" size="md">
    <form class="space-y-4">
      <Input label="Product Name" required />
      <Textarea label="Description" :rows="3" />
      <Input label="Price" type="number" />
    </form>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button variant="ghost">Cancel</Button>
        <Button variant="primary" type="submit">Add Product</Button>
      </div>
    </template>
  </Modal>
</template>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-10-form-validation---progressive-disclosure",children:"📝 10. Form Validation - Progressive Disclosure"}),`
`,n.jsx(e.h3,{id:"only-show-errors-after-blur",children:"Only Show Errors After Blur"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <form @submit.prevent="handleSubmit" class="space-y-4">
    <Input
      v-model="email"
      label="Email"
      type="email"
      :error="touched.email ? emailError : ''"
      @blur="touched.email = true"
    />

    <Input
      v-model="password"
      label="Password"
      type="password"
      :error="touched.password ? passwordError : ''"
      @blur="touched.password = true"
    />

    <Button variant="primary" type="submit">
      Sign In
    </Button>
  </form>
</template>

<script setup>
const touched = ref({ email: false, password: false })

const emailError = computed(() => {
  if (!email.value) return 'Email is required'
  if (!isValidEmail(email.value)) return 'Invalid email format'
  return ''
})
<\/script>
`})}),`
`,n.jsxs(e.p,{children:[n.jsx(e.strong,{children:"Why:"})," Don't show errors until user has tried to fill the field."]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"️-11-badge-usage-in-datatable",children:"🏷️ 11. Badge Usage in DataTable"}),`
`,n.jsx(e.h3,{id:"status-indicators",children:"Status Indicators"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <DataTable :columns="columns" :data="orders">
    <template #cell-status="{ value }">
      <Badge :variant="getStatusVariant(value)">
        {{ value }}
      </Badge>
    </template>

    <template #cell-priority="{ value }">
      <Badge
        :variant="getPriorityVariant(value)"
        :dot="true"
      >
        {{ value }}
      </Badge>
    </template>
  </DataTable>
</template>

<script setup>
const getStatusVariant = (status) => {
  const map = {
    'Completed': 'success',
    'In Progress': 'info',
    'Pending': 'warning',
    'Cancelled': 'error'
  }
  return map[status] || 'neutral'
}

const getPriorityVariant = (priority) => {
  const map = {
    'High': 'error',
    'Medium': 'warning',
    'Low': 'neutral'
  }
  return map[priority] || 'neutral'
}
<\/script>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-12-card-layouts",children:"🎨 12. Card Layouts"}),`
`,n.jsx(e.h3,{id:"product-grid",children:"Product Grid"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
    <Card
      v-for="product in products"
      :key="product.id"
      padding="none"
      :hoverable="true"
    >
      <img
        :src="product.image"
        :alt="product.name"
        class="w-full h-48 object-cover"
      />
      <div class="p-4">
        <h3 class="font-semibold text-lg mb-2">{{ product.name }}</h3>
        <p class="text-sm text-text-secondary mb-4">{{ product.description }}</p>
        <div class="flex items-center justify-between">
          <span class="text-2xl font-bold text-primary">
            {{ product.price }}
          </span>
          <Button variant="primary" size="sm">
            Add to Cart
          </Button>
        </div>
      </div>
    </Card>
  </div>
</template>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-13-search-with-filters",children:"🔍 13. Search with Filters"}),`
`,n.jsx(e.h3,{id:"sidebar-filters--results",children:"Sidebar Filters + Results"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <div class="flex gap-6">
    <!-- Filters Sidebar -->
    <Card padding="lg" class="w-64 h-fit">
      <h3 class="font-semibold mb-4">Filters</h3>

      <div class="space-y-6">
        <div>
          <label class="text-sm font-medium mb-2 block">Category</label>
          <div class="space-y-2">
            <Checkbox label="Electronics" />
            <Checkbox label="Clothing" />
            <Checkbox label="Books" />
          </div>
        </div>

        <div>
          <label class="text-sm font-medium mb-2 block">Price Range</label>
          <div class="space-y-2">
            <Radio value="0-50" label="$0 - $50" />
            <Radio value="50-100" label="$50 - $100" />
            <Radio value="100+" label="$100+" />
          </div>
        </div>
      </div>

      <Button variant="ghost" class="w-full mt-6">
        Clear Filters
      </Button>
    </Card>

    <!-- Results -->
    <div class="flex-1">
      <div class="flex items-center justify-between mb-6">
        <p class="text-text-secondary">1,234 results</p>
        <Select :options="sortOptions" />
      </div>

      <div class="grid grid-cols-3 gap-6">
        <!-- Product cards -->
      </div>
    </div>
  </div>
</template>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-14-health-monitoring-dashboard",children:"📊 14. Health Monitoring Dashboard"}),`
`,n.jsx(e.h3,{id:"using-healthcircle-component",children:"Using HealthCircle Component"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <div class="grid grid-cols-2 md:grid-cols-4 gap-6">
    <Card
      v-for="metric in healthMetrics"
      :key="metric.id"
      padding="lg"
      class="text-center"
    >
      <div class="flex justify-center mb-4">
        <HealthCircle
          :value="metric.health"
          size="lg"
          :color="getHealthColor(metric.health)"
        />
      </div>
      <h4 class="font-semibold mb-1">{{ metric.name }}</h4>
      <p class="text-sm text-text-secondary">{{ metric.description }}</p>
    </Card>
  </div>
</template>

<script setup>
import { HealthCircle } from '@shared/ui/atoms'

const healthMetrics = [
  { id: 1, name: 'API Health', health: 98, description: 'All systems operational' },
  { id: 2, name: 'Database', health: 95, description: 'Optimal performance' },
  { id: 3, name: 'Cache', health: 87, description: 'Good hit rate' },
  { id: 4, name: 'Queue', health: 72, description: 'Moderate load' }
]

const getHealthColor = (health) => {
  if (health >= 90) return 'var(--color-success)'
  if (health >= 70) return 'var(--color-primary)'
  if (health >= 50) return 'var(--color-warning)'
  return 'var(--color-error)'
}
<\/script>
`})}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-15-beforeafter-form-improvements",children:"✅ 15. Before/After: Form Improvements"}),`
`,n.jsx(e.h3,{id:"-before-poor-ux",children:"❌ Before (Poor UX)"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <form>
    <input placeholder="Email" />
    <input placeholder="Password" type="password" />
    <button>OK</button>
  </form>
</template>
`})}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Problems:"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"❌ No labels (accessibility issue)"}),`
`,n.jsx(e.li,{children:"❌ Placeholder as label (disappears on type)"}),`
`,n.jsx(e.li,{children:'❌ Vague button text ("OK" for what?)'}),`
`,n.jsx(e.li,{children:"❌ No visual hierarchy"}),`
`,n.jsx(e.li,{children:"❌ No error handling"}),`
`,n.jsx(e.li,{children:"❌ No loading state"}),`
`]}),`
`,n.jsx(e.h3,{id:"-after-best-practices",children:"✅ After (Best Practices)"}),`
`,n.jsx(e.pre,{children:n.jsx(e.code,{className:"language-vue",children:`<template>
  <Card padding="lg" class="max-w-md mx-auto">
    <h2 class="text-2xl font-bold mb-6">Sign In</h2>

    <form @submit.prevent="handleSubmit" class="space-y-4">
      <Alert v-if="error" variant="error" class="mb-4">
        {{ error }}
      </Alert>

      <Input
        v-model="email"
        label="Email address"
        type="email"
        required
        :error="touched.email ? emailError : ''"
        @blur="touched.email = true"
      />

      <Input
        v-model="password"
        label="Password"
        type="password"
        required
        :error="touched.password ? passwordError : ''"
        @blur="touched.password = true"
      />

      <div class="flex items-center justify-between">
        <Checkbox label="Remember me" />
        <a href="#" class="text-sm text-primary hover:underline">
          Forgot password?
        </a>
      </div>

      <Button
        variant="primary"
        type="submit"
        :loading="loading"
        :disabled="!isValid"
        class="w-full"
      >
        {{ loading ? 'Signing in...' : 'Sign In' }}
      </Button>
    </form>
  </Card>
</template>
`})}),`
`,n.jsx(e.p,{children:n.jsx(e.strong,{children:"Improvements:"})}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"✅ Clear labels for accessibility"}),`
`,n.jsx(e.li,{children:"✅ Descriptive button text"}),`
`,n.jsx(e.li,{children:"✅ Visual hierarchy (title, form, actions)"}),`
`,n.jsx(e.li,{children:"✅ Error handling (alert + inline)"}),`
`,n.jsx(e.li,{children:"✅ Progressive disclosure (errors after blur)"}),`
`,n.jsx(e.li,{children:"✅ Loading state"}),`
`,n.jsx(e.li,{children:"✅ Disabled state when invalid"}),`
`,n.jsx(e.li,{children:"✅ Remember me + forgot password links"}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-implementation-checklist",children:"🎯 Implementation Checklist"}),`
`,n.jsx(e.p,{children:"Ao criar novas telas, use estes exemplos como referência:"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsx(e.li,{children:"[ ] Consultar exemplo correspondente (Dashboard, Form, List, etc)"}),`
`,n.jsx(e.li,{children:"[ ] Aplicar hierarquia visual correta (Button Hierarchy)"}),`
`,n.jsx(e.li,{children:"[ ] Adicionar estados de loading (Skeleton/Spinner)"}),`
`,n.jsx(e.li,{children:"[ ] Implementar empty states apropriados"}),`
`,n.jsx(e.li,{children:"[ ] Usar badges para status indicators"}),`
`,n.jsx(e.li,{children:"[ ] Adicionar error handling com Alert"}),`
`,n.jsx(e.li,{children:"[ ] Validar forms com progressive disclosure"}),`
`,n.jsx(e.li,{children:"[ ] Testar responsividade (grid layouts)"}),`
`,n.jsx(e.li,{children:"[ ] Garantir acessibilidade (labels, ARIA)"}),`
`,n.jsx(e.li,{children:"[ ] Adicionar hover effects em elementos interativos"}),`
`]}),`
`,n.jsx(e.hr,{}),`
`,n.jsx(e.h2,{id:"-related-documentation",children:"📚 Related Documentation"}),`
`,n.jsxs(e.ul,{children:[`
`,n.jsxs(e.li,{children:[n.jsx(e.a,{href:"/docs/style-guide-component-usage--docs",children:"Component Usage"})," - Quando usar cada componente"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.a,{href:"/docs/style-guide-layout-patterns--docs",children:"Layout Patterns"})," - Padrões comuns de layout"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.a,{href:"/docs/style-guide-form-patterns--docs",children:"Form Patterns"})," - Best practices de formulários"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.a,{href:"/docs/style-guide-error-handling--docs",children:"Error Handling"})," - Tratamento de erros"]}),`
`,n.jsxs(e.li,{children:[n.jsx(e.a,{href:"/docs/style-guide-dos-and-donts--docs",children:"Do's and Don'ts"})," - Exemplos corretos vs incorretos"]}),`
`]})]})}function u(r={}){const{wrapper:e}={...s(),...r.components};return e?n.jsx(e,{...r,children:n.jsx(t,{...r})}):t(r)}export{u as default};
