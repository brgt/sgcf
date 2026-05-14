import{d as h,c as r,o,a as g,b,n as v,r as l,j as P}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as k}from"./_plugin-vue_export-helper-DlAUqK2U.js";import{P as s}from"./PageHeader-mAywb_6M.js";const w={class:"nw-page-layout"},L={class:"nw-page-layout__container"},S={class:"nw-page-layout__main"},H={key:1,class:"nw-page-layout__footer"},y=h({__name:"PageLayout",props:{maxWidth:{default:"default"},padding:{default:"lg"},hasSidebar:{type:Boolean,default:!1},sidebarWidth:{default:"280px"},stickyHeader:{type:Boolean,default:!1}},setup(e){const f={narrow:"max-w-4xl",default:"max-w-7xl",wide:"max-w-screen-2xl",full:"max-w-full"},x={none:"p-0",sm:"p-4",md:"p-6",lg:"p-8"};return(t,W)=>(o(),r("div",w,[t.$slots.header?(o(),r("div",{key:0,class:v(["nw-page-layout__header",{"nw-page-layout__header--sticky":e.stickyHeader}])},[l(t.$slots,"header",{},void 0,!0)],2)):g("",!0),b("div",L,[e.hasSidebar&&t.$slots.sidebar?(o(),r("aside",{key:0,class:"nw-page-layout__sidebar",style:P({width:e.sidebarWidth})},[l(t.$slots,"sidebar",{},void 0,!0)],4)):g("",!0),b("main",S,[b("div",{class:v(["nw-page-layout__content",f[e.maxWidth],x[e.padding]])},[l(t.$slots,"default",{},void 0,!0)],2)])]),t.$slots.footer?(o(),r("footer",H,[l(t.$slots,"footer",{},void 0,!0)])):g("",!0)]))}}),a=k(y,[["__scopeId","data-v-b9cbcc2a"]]);y.__docgenInfo={exportName:"default",displayName:"PageLayout",description:"",tags:{},props:[{name:"maxWidth",required:!1,type:{name:"union",elements:[{name:'"narrow"'},{name:'"default"'},{name:'"wide"'},{name:'"full"'}]},defaultValue:{func:!1,value:"'default'"}},{name:"padding",required:!1,type:{name:"union",elements:[{name:'"none"'},{name:'"sm"'},{name:'"md"'},{name:'"lg"'}]},defaultValue:{func:!1,value:"'lg'"}},{name:"hasSidebar",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"sidebarWidth",required:!1,type:{name:"string"},defaultValue:{func:!1,value:"'280px'"}},{name:"stickyHeader",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}}],slots:[{name:"header"},{name:"sidebar"},{name:"default"},{name:"footer"}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/templates/PageLayout/PageLayout.vue"]};const C={title:"Design System/Templates/PageLayout",component:a,tags:["autodocs"],argTypes:{maxWidth:{control:"select",options:["narrow","default","wide","full"],description:"Largura máxima do conteúdo"},padding:{control:"select",options:["none","sm","md","lg"],description:"Padding do conteúdo"},hasSidebar:{control:"boolean",description:"Mostrar sidebar"},sidebarWidth:{control:"text",description:"Largura da sidebar"},stickyHeader:{control:"boolean",description:"Header sticky"}},args:{maxWidth:"default",padding:"lg",hasSidebar:!1,sidebarWidth:"280px",stickyHeader:!1}},d={render:e=>({components:{PageLayout:a,PageHeader:s},setup(){return{args:e}},template:`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Dashboard"
            description="Your personalized overview"
          />
        </template>

        <div class="space-y-6">
          <div class="grid grid-cols-3 gap-4">
            <div class="p-6 bg-surface rounded-lg border">
              <h3 class="font-semibold mb-2">Total Sales</h3>
              <p class="text-3xl font-bold text-primary">$45,231</p>
            </div>
            <div class="p-6 bg-surface rounded-lg border">
              <h3 class="font-semibold mb-2">Orders</h3>
              <p class="text-3xl font-bold text-primary">1,234</p>
            </div>
            <div class="p-6 bg-surface rounded-lg border">
              <h3 class="font-semibold mb-2">Customers</h3>
              <p class="text-3xl font-bold text-primary">5,678</p>
            </div>
          </div>

          <div class="p-6 bg-surface rounded-lg border">
            <h3 class="font-semibold mb-4">Recent Activity</h3>
            <p class="text-gray-600">Activity content goes here...</p>
          </div>
        </div>
      </PageLayout>
    `})},n={render:e=>({components:{PageLayout:a,PageHeader:s},setup(){return{args:e}},template:`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Settings"
            description="Manage your preferences"
            :breadcrumbs="[
              { label: 'Home', to: '/' },
              { label: 'Settings' }
            ]"
          />
        </template>

        <template #sidebar>
          <nav class="p-4 space-y-2">
            <a href="#" class="block px-4 py-2 bg-primary/10 text-primary rounded-lg font-medium">
              Profile
            </a>
            <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded-lg">
              Account
            </a>
            <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded-lg">
              Security
            </a>
            <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded-lg">
              Notifications
            </a>
            <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded-lg">
              Billing
            </a>
          </nav>
        </template>

        <div class="space-y-6">
          <div class="p-6 bg-surface rounded-lg border">
            <h3 class="font-semibold mb-4">Profile Information</h3>
            <div class="space-y-4">
              <div>
                <label class="block text-sm font-medium mb-1">Name</label>
                <input type="text" class="w-full px-3 py-2 border rounded-lg" value="John Doe" />
              </div>
              <div>
                <label class="block text-sm font-medium mb-1">Email</label>
                <input type="email" class="w-full px-3 py-2 border rounded-lg" value="john@example.com" />
              </div>
            </div>
          </div>
        </div>
      </PageLayout>
    `}),args:{hasSidebar:!0,sidebarWidth:"240px"}},i={render:e=>({components:{PageLayout:a,PageHeader:s},setup(){return{args:e}},template:`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Products"
            description="Manage your product catalog"
          />
        </template>

        <div class="space-y-4">
          <div v-for="i in 10" :key="i" class="p-4 bg-surface rounded-lg border flex items-center justify-between">
            <div>
              <h4 class="font-medium">Product {{ i }}</h4>
              <p class="text-sm text-gray-600">SKU: PROD-{{ i.toString().padStart(4, '0') }}</p>
            </div>
            <span class="text-lg font-semibold">$99.99</span>
          </div>
        </div>

        <template #footer>
          <div class="max-w-7xl mx-auto flex items-center justify-between">
            <p class="text-sm text-gray-600">© 2025 Nordware. All rights reserved.</p>
            <div class="flex gap-4">
              <a href="#" class="text-sm text-gray-600 hover:text-primary">Privacy</a>
              <a href="#" class="text-sm text-gray-600 hover:text-primary">Terms</a>
              <a href="#" class="text-sm text-gray-600 hover:text-primary">Contact</a>
            </div>
          </div>
        </template>
      </PageLayout>
    `})},c={render:e=>({components:{PageLayout:a,PageHeader:s},setup(){return{args:e}},template:`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Long Content Page"
            description="Scroll to see sticky header in action"
          />
        </template>

        <div class="space-y-4">
          <div v-for="i in 30" :key="i" class="p-6 bg-surface rounded-lg border">
            <h3 class="font-semibold mb-2">Section {{ i }}</h3>
            <p class="text-gray-600">
              Lorem ipsum dolor sit amet, consectetur adipiscing elit.
              Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.
            </p>
          </div>
        </div>
      </PageLayout>
    `}),args:{stickyHeader:!0}},p={render:e=>({components:{PageLayout:a,PageHeader:s},setup(){return{args:e}},template:`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Article"
            description="A narrow content area for better readability"
          />
        </template>

        <article class="prose max-w-none">
          <h2>Introduction</h2>
          <p>
            Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod
            tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam,
            quis nostrud exercitation ullamco laboris.
          </p>
          <h2>Main Content</h2>
          <p>
            Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore
            eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident.
          </p>
        </article>
      </PageLayout>
    `}),args:{maxWidth:"narrow"}},m={render:e=>({components:{PageLayout:a,PageHeader:s},setup(){return{args:e}},template:`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Analytics"
            description="Full-width layout for dashboards"
          />
        </template>

        <div class="space-y-6">
          <div class="grid grid-cols-4 gap-4">
            <div v-for="i in 4" :key="i" class="p-4 bg-surface rounded-lg border">
              <h4 class="text-sm text-gray-600 mb-2">Metric {{ i }}</h4>
              <p class="text-2xl font-bold">{{ (i * 1234).toLocaleString() }}</p>
            </div>
          </div>

          <div class="p-6 bg-surface rounded-lg border" style="height: 400px;">
            <h3 class="font-semibold mb-4">Chart Area</h3>
            <div class="h-full flex items-center justify-center text-gray-400">
              Chart visualization goes here
            </div>
          </div>
        </div>
      </PageLayout>
    `}),args:{maxWidth:"full",padding:"md"}},u={render:e=>({components:{PageLayout:a,PageHeader:s},setup(){return{args:e}},template:`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Project Management"
            description="Comprehensive project overview"
            :breadcrumbs="[
              { label: 'Home', to: '/', icon: 'i-carbon-home' },
              { label: 'Projects', to: '/projects' },
              { label: 'Alpha' }
            ]"
          >
            <template #actions>
              <button class="px-4 py-2 bg-gray-200 rounded-lg">Share</button>
              <button class="px-4 py-2 bg-primary text-white rounded-lg">Edit</button>
            </template>
          </PageHeader>
        </template>

        <template #sidebar>
          <div class="p-4 space-y-6">
            <div>
              <h4 class="text-sm font-semibold mb-2 text-gray-600">Navigation</h4>
              <nav class="space-y-1">
                <a href="#" class="block px-3 py-2 bg-primary/10 text-primary rounded-lg text-sm">Overview</a>
                <a href="#" class="block px-3 py-2 hover:bg-gray-100 rounded-lg text-sm">Tasks</a>
                <a href="#" class="block px-3 py-2 hover:bg-gray-100 rounded-lg text-sm">Team</a>
                <a href="#" class="block px-3 py-2 hover:bg-gray-100 rounded-lg text-sm">Files</a>
              </nav>
            </div>

            <div>
              <h4 class="text-sm font-semibold mb-2 text-gray-600">Quick Stats</h4>
              <div class="space-y-2 text-sm">
                <div class="flex justify-between">
                  <span class="text-gray-600">Tasks</span>
                  <span class="font-medium">24/30</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-gray-600">Team</span>
                  <span class="font-medium">8 members</span>
                </div>
              </div>
            </div>
          </div>
        </template>

        <div class="space-y-6">
          <div class="grid grid-cols-3 gap-4">
            <div class="p-4 bg-surface rounded-lg border">
              <h4 class="text-sm text-gray-600 mb-1">Progress</h4>
              <p class="text-2xl font-bold">80%</p>
            </div>
            <div class="p-4 bg-surface rounded-lg border">
              <h4 class="text-sm text-gray-600 mb-1">Due Date</h4>
              <p class="text-lg font-semibold">Jan 30, 2025</p>
            </div>
            <div class="p-4 bg-surface rounded-lg border">
              <h4 class="text-sm text-gray-600 mb-1">Status</h4>
              <p class="text-lg font-semibold text-green-600">On Track</p>
            </div>
          </div>

          <div class="p-6 bg-surface rounded-lg border">
            <h3 class="font-semibold mb-4">Recent Activity</h3>
            <div class="space-y-3">
              <div v-for="i in 5" :key="i" class="flex items-start gap-3 pb-3 border-b last:border-0">
                <div class="w-8 h-8 bg-primary/10 rounded-full flex items-center justify-center flex-shrink-0">
                  <span class="i-carbon-checkmark text-primary"></span>
                </div>
                <div class="flex-1">
                  <p class="text-sm font-medium">Task completed</p>
                  <p class="text-xs text-gray-600">2 hours ago</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <template #footer>
          <div class="max-w-7xl mx-auto text-center">
            <p class="text-sm text-gray-600">© 2025 Nordware Platform. All rights reserved.</p>
          </div>
        </template>
      </PageLayout>
    `}),args:{hasSidebar:!0,stickyHeader:!0,sidebarWidth:"260px"}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageLayout,
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Dashboard"
            description="Your personalized overview"
          />
        </template>

        <div class="space-y-6">
          <div class="grid grid-cols-3 gap-4">
            <div class="p-6 bg-surface rounded-lg border">
              <h3 class="font-semibold mb-2">Total Sales</h3>
              <p class="text-3xl font-bold text-primary">$45,231</p>
            </div>
            <div class="p-6 bg-surface rounded-lg border">
              <h3 class="font-semibold mb-2">Orders</h3>
              <p class="text-3xl font-bold text-primary">1,234</p>
            </div>
            <div class="p-6 bg-surface rounded-lg border">
              <h3 class="font-semibold mb-2">Customers</h3>
              <p class="text-3xl font-bold text-primary">5,678</p>
            </div>
          </div>

          <div class="p-6 bg-surface rounded-lg border">
            <h3 class="font-semibold mb-4">Recent Activity</h3>
            <p class="text-gray-600">Activity content goes here...</p>
          </div>
        </div>
      </PageLayout>
    \`
  })
}`,...d.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageLayout,
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Settings"
            description="Manage your preferences"
            :breadcrumbs="[
              { label: 'Home', to: '/' },
              { label: 'Settings' }
            ]"
          />
        </template>

        <template #sidebar>
          <nav class="p-4 space-y-2">
            <a href="#" class="block px-4 py-2 bg-primary/10 text-primary rounded-lg font-medium">
              Profile
            </a>
            <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded-lg">
              Account
            </a>
            <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded-lg">
              Security
            </a>
            <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded-lg">
              Notifications
            </a>
            <a href="#" class="block px-4 py-2 hover:bg-gray-100 rounded-lg">
              Billing
            </a>
          </nav>
        </template>

        <div class="space-y-6">
          <div class="p-6 bg-surface rounded-lg border">
            <h3 class="font-semibold mb-4">Profile Information</h3>
            <div class="space-y-4">
              <div>
                <label class="block text-sm font-medium mb-1">Name</label>
                <input type="text" class="w-full px-3 py-2 border rounded-lg" value="John Doe" />
              </div>
              <div>
                <label class="block text-sm font-medium mb-1">Email</label>
                <input type="email" class="w-full px-3 py-2 border rounded-lg" value="john@example.com" />
              </div>
            </div>
          </div>
        </div>
      </PageLayout>
    \`
  }),
  args: {
    hasSidebar: true,
    sidebarWidth: '240px'
  }
}`,...n.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageLayout,
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Products"
            description="Manage your product catalog"
          />
        </template>

        <div class="space-y-4">
          <div v-for="i in 10" :key="i" class="p-4 bg-surface rounded-lg border flex items-center justify-between">
            <div>
              <h4 class="font-medium">Product {{ i }}</h4>
              <p class="text-sm text-gray-600">SKU: PROD-{{ i.toString().padStart(4, '0') }}</p>
            </div>
            <span class="text-lg font-semibold">$99.99</span>
          </div>
        </div>

        <template #footer>
          <div class="max-w-7xl mx-auto flex items-center justify-between">
            <p class="text-sm text-gray-600">© 2025 Nordware. All rights reserved.</p>
            <div class="flex gap-4">
              <a href="#" class="text-sm text-gray-600 hover:text-primary">Privacy</a>
              <a href="#" class="text-sm text-gray-600 hover:text-primary">Terms</a>
              <a href="#" class="text-sm text-gray-600 hover:text-primary">Contact</a>
            </div>
          </div>
        </template>
      </PageLayout>
    \`
  })
}`,...i.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageLayout,
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Long Content Page"
            description="Scroll to see sticky header in action"
          />
        </template>

        <div class="space-y-4">
          <div v-for="i in 30" :key="i" class="p-6 bg-surface rounded-lg border">
            <h3 class="font-semibold mb-2">Section {{ i }}</h3>
            <p class="text-gray-600">
              Lorem ipsum dolor sit amet, consectetur adipiscing elit.
              Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.
            </p>
          </div>
        </div>
      </PageLayout>
    \`
  }),
  args: {
    stickyHeader: true
  }
}`,...c.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageLayout,
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Article"
            description="A narrow content area for better readability"
          />
        </template>

        <article class="prose max-w-none">
          <h2>Introduction</h2>
          <p>
            Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod
            tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam,
            quis nostrud exercitation ullamco laboris.
          </p>
          <h2>Main Content</h2>
          <p>
            Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore
            eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident.
          </p>
        </article>
      </PageLayout>
    \`
  }),
  args: {
    maxWidth: 'narrow'
  }
}`,...p.parameters?.docs?.source}}};m.parameters={...m.parameters,docs:{...m.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageLayout,
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Analytics"
            description="Full-width layout for dashboards"
          />
        </template>

        <div class="space-y-6">
          <div class="grid grid-cols-4 gap-4">
            <div v-for="i in 4" :key="i" class="p-4 bg-surface rounded-lg border">
              <h4 class="text-sm text-gray-600 mb-2">Metric {{ i }}</h4>
              <p class="text-2xl font-bold">{{ (i * 1234).toLocaleString() }}</p>
            </div>
          </div>

          <div class="p-6 bg-surface rounded-lg border" style="height: 400px;">
            <h3 class="font-semibold mb-4">Chart Area</h3>
            <div class="h-full flex items-center justify-center text-gray-400">
              Chart visualization goes here
            </div>
          </div>
        </div>
      </PageLayout>
    \`
  }),
  args: {
    maxWidth: 'full',
    padding: 'md'
  }
}`,...m.parameters?.docs?.source}}};u.parameters={...u.parameters,docs:{...u.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageLayout,
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageLayout v-bind="args">
        <template #header>
          <PageHeader
            title="Project Management"
            description="Comprehensive project overview"
            :breadcrumbs="[
              { label: 'Home', to: '/', icon: 'i-carbon-home' },
              { label: 'Projects', to: '/projects' },
              { label: 'Alpha' }
            ]"
          >
            <template #actions>
              <button class="px-4 py-2 bg-gray-200 rounded-lg">Share</button>
              <button class="px-4 py-2 bg-primary text-white rounded-lg">Edit</button>
            </template>
          </PageHeader>
        </template>

        <template #sidebar>
          <div class="p-4 space-y-6">
            <div>
              <h4 class="text-sm font-semibold mb-2 text-gray-600">Navigation</h4>
              <nav class="space-y-1">
                <a href="#" class="block px-3 py-2 bg-primary/10 text-primary rounded-lg text-sm">Overview</a>
                <a href="#" class="block px-3 py-2 hover:bg-gray-100 rounded-lg text-sm">Tasks</a>
                <a href="#" class="block px-3 py-2 hover:bg-gray-100 rounded-lg text-sm">Team</a>
                <a href="#" class="block px-3 py-2 hover:bg-gray-100 rounded-lg text-sm">Files</a>
              </nav>
            </div>

            <div>
              <h4 class="text-sm font-semibold mb-2 text-gray-600">Quick Stats</h4>
              <div class="space-y-2 text-sm">
                <div class="flex justify-between">
                  <span class="text-gray-600">Tasks</span>
                  <span class="font-medium">24/30</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-gray-600">Team</span>
                  <span class="font-medium">8 members</span>
                </div>
              </div>
            </div>
          </div>
        </template>

        <div class="space-y-6">
          <div class="grid grid-cols-3 gap-4">
            <div class="p-4 bg-surface rounded-lg border">
              <h4 class="text-sm text-gray-600 mb-1">Progress</h4>
              <p class="text-2xl font-bold">80%</p>
            </div>
            <div class="p-4 bg-surface rounded-lg border">
              <h4 class="text-sm text-gray-600 mb-1">Due Date</h4>
              <p class="text-lg font-semibold">Jan 30, 2025</p>
            </div>
            <div class="p-4 bg-surface rounded-lg border">
              <h4 class="text-sm text-gray-600 mb-1">Status</h4>
              <p class="text-lg font-semibold text-green-600">On Track</p>
            </div>
          </div>

          <div class="p-6 bg-surface rounded-lg border">
            <h3 class="font-semibold mb-4">Recent Activity</h3>
            <div class="space-y-3">
              <div v-for="i in 5" :key="i" class="flex items-start gap-3 pb-3 border-b last:border-0">
                <div class="w-8 h-8 bg-primary/10 rounded-full flex items-center justify-center flex-shrink-0">
                  <span class="i-carbon-checkmark text-primary"></span>
                </div>
                <div class="flex-1">
                  <p class="text-sm font-medium">Task completed</p>
                  <p class="text-xs text-gray-600">2 hours ago</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <template #footer>
          <div class="max-w-7xl mx-auto text-center">
            <p class="text-sm text-gray-600">© 2025 Nordware Platform. All rights reserved.</p>
          </div>
        </template>
      </PageLayout>
    \`
  }),
  args: {
    hasSidebar: true,
    stickyHeader: true,
    sidebarWidth: '260px'
  }
}`,...u.parameters?.docs?.source}}};const T=["Default","WithSidebar","WithFooter","StickyHeader","NarrowWidth","FullWidth","ComplexLayout"];export{u as ComplexLayout,d as Default,m as FullWidth,p as NarrowWidth,c as StickyHeader,i as WithFooter,n as WithSidebar,T as __namedExportsOrder,C as default};
