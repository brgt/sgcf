import{P as t}from"./PageHeader-mAywb_6M.js";import"./vue.esm-bundler-Sd8zNG93.js";import"./_plugin-vue_export-helper-DlAUqK2U.js";const m={title:"Design System/Templates/PageHeader",component:t,tags:["autodocs"],argTypes:{title:{control:"text",description:"Título da página"},description:{control:"text",description:"Descrição da página"},breadcrumbs:{control:"object",description:"Array de breadcrumbs"},showBackButton:{control:"boolean",description:"Mostrar botão de voltar"}},args:{title:"Page Title",description:"Page description goes here",showBackButton:!1}},i=[{label:"Home",to:"/",icon:"i-carbon-home"},{label:"Dashboard",to:"/dashboard"},{label:"Settings"}],r={args:{title:"Settings",description:"Manage your account settings and preferences",breadcrumbs:i}},a={args:{title:"Dashboard",description:"Welcome back! Here's your overview"}},o={render:e=>({components:{PageHeader:t},setup(){return{args:e,handleBack:()=>{alert("Back button clicked!")}}},template:`
      <PageHeader v-bind="args" @back="handleBack" />
    `}),args:{title:"Order Details",description:"View and edit order information",showBackButton:!0,breadcrumbs:[{label:"Orders",to:"/orders"},{label:"#12345"}]}},s={render:e=>({components:{PageHeader:t},setup(){return{args:e}},template:`
      <PageHeader v-bind="args">
        <template #actions>
          <button class="px-4 py-2 bg-gray-200 rounded-lg">Cancel</button>
          <button class="px-4 py-2 bg-primary text-white rounded-lg flex items-center gap-2">
            <span class="i-carbon-save"></span>
            Save Changes
          </button>
        </template>
      </PageHeader>
    `}),args:{title:"Edit Profile",description:"Update your personal information",breadcrumbs:[{label:"Settings",to:"/settings"},{label:"Profile"}]}},n={render:e=>({components:{PageHeader:t},setup(){return{args:e}},template:`
      <PageHeader v-bind="args">
        <template #actions>
          <button class="p-2 hover:bg-gray-100 rounded-lg" title="Share">
            <span class="i-carbon-share text-xl"></span>
          </button>
          <button class="p-2 hover:bg-gray-100 rounded-lg" title="Export">
            <span class="i-carbon-download text-xl"></span>
          </button>
          <button class="p-2 hover:bg-gray-100 rounded-lg" title="Print">
            <span class="i-carbon-printer text-xl"></span>
          </button>
          <button class="px-4 py-2 bg-primary text-white rounded-lg flex items-center gap-2">
            <span class="i-carbon-add"></span>
            New Item
          </button>
        </template>
      </PageHeader>
    `}),args:{title:"Products",description:"1,234 products in your catalog",breadcrumbs:[{label:"Home",to:"/"},{label:"Catalog",to:"/catalog"},{label:"Products"}]}},l={render:e=>({components:{PageHeader:t},setup(){return{args:e}},template:`
      <PageHeader v-bind="args">
        <template #tabs>
          <div class="flex gap-1 border-b border-gray-200">
            <button class="px-4 py-2 border-b-2 border-primary text-primary font-medium">
              Overview
            </button>
            <button class="px-4 py-2 border-b-2 border-transparent text-gray-600 hover:text-gray-900">
              Activity
            </button>
            <button class="px-4 py-2 border-b-2 border-transparent text-gray-600 hover:text-gray-900">
              Settings
            </button>
          </div>
        </template>

        <template #actions>
          <button class="px-4 py-2 bg-primary text-white rounded-lg">
            Manage
          </button>
        </template>
      </PageHeader>
    `}),args:{title:"Project Alpha",description:"Web application development project",breadcrumbs:[{label:"Projects",to:"/projects"},{label:"Alpha"}]}},c={args:{title:"Component Details",breadcrumbs:[{label:"Home",to:"/",icon:"i-carbon-home"},{label:"Workspace",to:"/workspace"},{label:"Projects",to:"/projects"},{label:"Design System",to:"/design-system"},{label:"Components",to:"/components"},{label:"Button"}]}},p={render:e=>({components:{PageHeader:t},setup(){return{args:e}},template:`
      <PageHeader v-bind="args">
        <template #actions>
          <button class="px-4 py-2 bg-primary text-white rounded-lg">
            Create New
          </button>
        </template>
      </PageHeader>
    `}),args:{title:"Dashboard",description:"Your personalized workspace overview"}};r.parameters={...r.parameters,docs:{...r.parameters?.docs,source:{originalSource:`{
  args: {
    title: 'Settings',
    description: 'Manage your account settings and preferences',
    breadcrumbs
  }
}`,...r.parameters?.docs?.source}}};a.parameters={...a.parameters,docs:{...a.parameters?.docs,source:{originalSource:`{
  args: {
    title: 'Dashboard',
    description: 'Welcome back! Here\\'s your overview'
  }
}`,...a.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageHeader
    },
    setup() {
      const handleBack = () => {
        alert('Back button clicked!');
      };
      return {
        args,
        handleBack
      };
    },
    template: \`
      <PageHeader v-bind="args" @back="handleBack" />
    \`
  }),
  args: {
    title: 'Order Details',
    description: 'View and edit order information',
    showBackButton: true,
    breadcrumbs: [{
      label: 'Orders',
      to: '/orders'
    }, {
      label: '#12345'
    }]
  }
}`,...o.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageHeader v-bind="args">
        <template #actions>
          <button class="px-4 py-2 bg-gray-200 rounded-lg">Cancel</button>
          <button class="px-4 py-2 bg-primary text-white rounded-lg flex items-center gap-2">
            <span class="i-carbon-save"></span>
            Save Changes
          </button>
        </template>
      </PageHeader>
    \`
  }),
  args: {
    title: 'Edit Profile',
    description: 'Update your personal information',
    breadcrumbs: [{
      label: 'Settings',
      to: '/settings'
    }, {
      label: 'Profile'
    }]
  }
}`,...s.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageHeader v-bind="args">
        <template #actions>
          <button class="p-2 hover:bg-gray-100 rounded-lg" title="Share">
            <span class="i-carbon-share text-xl"></span>
          </button>
          <button class="p-2 hover:bg-gray-100 rounded-lg" title="Export">
            <span class="i-carbon-download text-xl"></span>
          </button>
          <button class="p-2 hover:bg-gray-100 rounded-lg" title="Print">
            <span class="i-carbon-printer text-xl"></span>
          </button>
          <button class="px-4 py-2 bg-primary text-white rounded-lg flex items-center gap-2">
            <span class="i-carbon-add"></span>
            New Item
          </button>
        </template>
      </PageHeader>
    \`
  }),
  args: {
    title: 'Products',
    description: '1,234 products in your catalog',
    breadcrumbs: [{
      label: 'Home',
      to: '/'
    }, {
      label: 'Catalog',
      to: '/catalog'
    }, {
      label: 'Products'
    }]
  }
}`,...n.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageHeader v-bind="args">
        <template #tabs>
          <div class="flex gap-1 border-b border-gray-200">
            <button class="px-4 py-2 border-b-2 border-primary text-primary font-medium">
              Overview
            </button>
            <button class="px-4 py-2 border-b-2 border-transparent text-gray-600 hover:text-gray-900">
              Activity
            </button>
            <button class="px-4 py-2 border-b-2 border-transparent text-gray-600 hover:text-gray-900">
              Settings
            </button>
          </div>
        </template>

        <template #actions>
          <button class="px-4 py-2 bg-primary text-white rounded-lg">
            Manage
          </button>
        </template>
      </PageHeader>
    \`
  }),
  args: {
    title: 'Project Alpha',
    description: 'Web application development project',
    breadcrumbs: [{
      label: 'Projects',
      to: '/projects'
    }, {
      label: 'Alpha'
    }]
  }
}`,...l.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  args: {
    title: 'Component Details',
    breadcrumbs: [{
      label: 'Home',
      to: '/',
      icon: 'i-carbon-home'
    }, {
      label: 'Workspace',
      to: '/workspace'
    }, {
      label: 'Projects',
      to: '/projects'
    }, {
      label: 'Design System',
      to: '/design-system'
    }, {
      label: 'Components',
      to: '/components'
    }, {
      label: 'Button'
    }]
  }
}`,...c.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      PageHeader
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <PageHeader v-bind="args">
        <template #actions>
          <button class="px-4 py-2 bg-primary text-white rounded-lg">
            Create New
          </button>
        </template>
      </PageHeader>
    \`
  }),
  args: {
    title: 'Dashboard',
    description: 'Your personalized workspace overview'
  }
}`,...p.parameters?.docs?.source}}};const y=["Default","Simple","WithBackButton","WithActions","WithMultipleActions","WithTabs","WithLongBreadcrumbs","NoBreadcrumbs"];export{r as Default,p as NoBreadcrumbs,a as Simple,s as WithActions,o as WithBackButton,c as WithLongBreadcrumbs,n as WithMultipleActions,l as WithTabs,y as __namedExportsOrder,m as default};
