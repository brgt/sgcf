import{d as f,c as x,r as C,n as y,u as D,o as G}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as w}from"./_plugin-vue_export-helper-DlAUqK2U.js";import{C as e}from"./Card-BgWWpAS0.js";const g=f({__name:"DashboardGrid",props:{columns:{default:3},gap:{default:"md"},responsive:{type:Boolean,default:!0}},setup(a){const m=a,u={none:"gap-0",sm:"gap-3",md:"gap-4",lg:"gap-6"},b={1:"grid-cols-1",2:"grid-cols-2",3:"grid-cols-3",4:"grid-cols-4",6:"grid-cols-6",12:"grid-cols-12"},v=m.responsive?"nw-dashboard-grid--responsive":"";return(h,k)=>(G(),x("div",{class:y(["nw-dashboard-grid",b[a.columns],u[a.gap],D(v)])},[C(h.$slots,"default",{},void 0,!0)],2))}}),s=w(g,[["__scopeId","data-v-b949b667"]]);g.__docgenInfo={exportName:"default",displayName:"DashboardGrid",description:"",tags:{},props:[{name:"columns",required:!1,type:{name:"union",elements:[{name:"1"},{name:"2"},{name:"3"},{name:"4"},{name:"6"},{name:"12"}]},defaultValue:{func:!1,value:"3"}},{name:"gap",required:!1,type:{name:"union",elements:[{name:'"none"'},{name:'"sm"'},{name:'"md"'},{name:'"lg"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"responsive",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"true"}}],slots:[{name:"default"}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/templates/DashboardGrid/DashboardGrid.vue"]};const M={title:"Design System/Templates/DashboardGrid",component:s,tags:["autodocs"],argTypes:{columns:{control:"select",options:[1,2,3,4,6,12],description:"Número de colunas"},gap:{control:"select",options:["none","sm","md","lg"],description:"Espaçamento entre items"},responsive:{control:"boolean",description:"Habilitar comportamento responsivo"}},args:{columns:3,gap:"md",responsive:!0}},r={render:a=>({components:{DashboardGrid:s,Card:e},setup(){return{args:a}},template:`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 6" :key="i" padding="lg">
          <h3 class="font-semibold mb-2">Card {{ i }}</h3>
          <p class="text-gray-600">Content for card {{ i }}</p>
        </Card>
      </DashboardGrid>
    `})},n={render:a=>({components:{DashboardGrid:s,Card:e},setup(){return{args:a}},template:`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 4" :key="i" padding="lg">
          <h3 class="font-semibold mb-2">Card {{ i }}</h3>
          <p class="text-gray-600">Two column layout</p>
        </Card>
      </DashboardGrid>
    `}),args:{columns:2}},t={render:a=>({components:{DashboardGrid:s,Card:e},setup(){return{args:a}},template:`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 8" :key="i" padding="md">
          <h4 class="font-semibold mb-1 text-sm">Metric {{ i }}</h4>
          <p class="text-2xl font-bold text-primary">{{ (i * 123).toLocaleString() }}</p>
        </Card>
      </DashboardGrid>
    `}),args:{columns:4}},d={render:a=>({components:{DashboardGrid:s,Card:e},setup(){return{args:a,stats:[{title:"Total Revenue",value:"$45,231",change:"+12.5%",positive:!0},{title:"Orders",value:"1,234",change:"+8.2%",positive:!0},{title:"Customers",value:"5,678",change:"-2.3%",positive:!1},{title:"Avg. Order",value:"$36.65",change:"+4.1%",positive:!0}]}},template:`
      <DashboardGrid v-bind="args">
        <Card
          v-for="(stat, i) in stats"
          :key="i"
          padding="lg"
          :hoverable="true"
        >
          <p class="text-sm text-gray-600 mb-1">{{ stat.title }}</p>
          <p class="text-3xl font-bold text-primary mb-2">{{ stat.value }}</p>
          <p :class="['text-sm font-medium', stat.positive ? 'text-green-600' : 'text-red-600']">
            {{ stat.change }}
          </p>
        </Card>
      </DashboardGrid>
    `}),args:{columns:4,gap:"md"}},o={render:a=>({components:{DashboardGrid:s,Card:e},setup(){return{args:a}},template:`
      <DashboardGrid v-bind="args">
        <!-- Wide card -->
        <Card padding="lg" class="col-span-2">
          <h3 class="font-semibold mb-4">Revenue Chart</h3>
          <div class="h-48 bg-gray-100 rounded flex items-center justify-center">
            Chart Area
          </div>
        </Card>

        <!-- Regular cards -->
        <Card padding="md">
          <p class="text-sm text-gray-600 mb-1">Orders</p>
          <p class="text-2xl font-bold">1,234</p>
        </Card>

        <Card padding="md">
          <p class="text-sm text-gray-600 mb-1">Revenue</p>
          <p class="text-2xl font-bold">$45K</p>
        </Card>

        <Card padding="md">
          <p class="text-sm text-gray-600 mb-1">Customers</p>
          <p class="text-2xl font-bold">567</p>
        </Card>

        <!-- Tall card -->
        <Card padding="lg" class="row-span-2">
          <h3 class="font-semibold mb-4">Recent Activity</h3>
          <div class="space-y-3">
            <div v-for="i in 8" :key="i" class="flex items-center gap-2 text-sm">
              <div class="w-2 h-2 bg-primary rounded-full"></div>
              <span class="text-gray-600">Activity {{ i }}</span>
            </div>
          </div>
        </Card>

        <!-- Full width card -->
        <Card padding="lg" class="col-span-2">
          <h3 class="font-semibold mb-4">Performance Metrics</h3>
          <div class="grid grid-cols-3 gap-4">
            <div v-for="i in 3" :key="i">
              <p class="text-xs text-gray-600 mb-1">Metric {{ i }}</p>
              <p class="text-xl font-bold">{{ (i * 89).toFixed(1) }}%</p>
            </div>
          </div>
        </Card>
      </DashboardGrid>
    `}),args:{columns:3,gap:"md"}},i={render:a=>({components:{DashboardGrid:s,Card:e},setup(){return{args:a}},template:`
      <div class="space-y-6">
        <!-- Top metrics -->
        <DashboardGrid columns="4" gap="md">
          <Card padding="lg" v-for="i in 4" :key="i">
            <div class="flex items-center justify-between mb-2">
              <p class="text-sm text-gray-600">Metric {{ i }}</p>
              <span :class="['i-carbon-arrow-up text-green-500']"></span>
            </div>
            <p class="text-2xl font-bold">{{ (i * 1234).toLocaleString() }}</p>
            <p class="text-xs text-green-600 mt-1">+12.5% vs last month</p>
          </Card>
        </DashboardGrid>

        <!-- Main charts -->
        <DashboardGrid v-bind="args">
          <Card padding="lg" class="col-span-2">
            <h3 class="font-semibold mb-4">Revenue Overview</h3>
            <div class="h-64 bg-gradient-to-t from-primary/10 to-transparent rounded flex items-end justify-center">
              <p class="text-gray-400 mb-8">Chart visualization</p>
            </div>
          </Card>

          <Card padding="lg">
            <h3 class="font-semibold mb-4">Top Products</h3>
            <div class="space-y-3">
              <div v-for="i in 5" :key="i" class="flex justify-between items-center">
                <span class="text-sm">Product {{ i }}</span>
                <span class="text-sm font-semibold">$ {{ (i * 123) }}</span>
              </div>
            </div>
          </Card>
        </DashboardGrid>

        <!-- Bottom stats -->
        <DashboardGrid columns="3" gap="md">
          <Card padding="md" v-for="i in 3" :key="i">
            <p class="text-xs text-gray-600 mb-2">Category {{ i }}</p>
            <p class="text-xl font-bold mb-1">{{ (i * 45).toFixed(1) }}%</p>
            <div class="w-full bg-gray-200 rounded-full h-2">
              <div class="bg-primary rounded-full h-2" :style="{ width: (i * 45) + '%' }"></div>
            </div>
          </Card>
        </DashboardGrid>
      </div>
    `}),args:{columns:3,gap:"lg"}},l={render:a=>({components:{DashboardGrid:s,Card:e},setup(){return{args:a}},template:`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 6" :key="i" padding="md">
          <p class="font-semibold">Card {{ i }}</p>
        </Card>
      </DashboardGrid>
    `}),args:{gap:"sm"}},p={render:a=>({components:{DashboardGrid:s,Card:e},setup(){return{args:a}},template:`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 6" :key="i" padding="lg">
          <h3 class="font-semibold mb-2">Card {{ i }}</h3>
          <p class="text-gray-600">Large spacing between cards</p>
        </Card>
      </DashboardGrid>
    `}),args:{gap:"lg"}},c={render:a=>({components:{DashboardGrid:s,Card:e},setup(){return{args:a}},template:`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 6" :key="i" padding="md">
          <p class="font-semibold">Card {{ i }}</p>
          <p class="text-sm text-gray-600">Fixed grid (not responsive)</p>
        </Card>
      </DashboardGrid>
    `}),args:{responsive:!1}};r.parameters={...r.parameters,docs:{...r.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      DashboardGrid,
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 6" :key="i" padding="lg">
          <h3 class="font-semibold mb-2">Card {{ i }}</h3>
          <p class="text-gray-600">Content for card {{ i }}</p>
        </Card>
      </DashboardGrid>
    \`
  })
}`,...r.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      DashboardGrid,
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 4" :key="i" padding="lg">
          <h3 class="font-semibold mb-2">Card {{ i }}</h3>
          <p class="text-gray-600">Two column layout</p>
        </Card>
      </DashboardGrid>
    \`
  }),
  args: {
    columns: 2
  }
}`,...n.parameters?.docs?.source}}};t.parameters={...t.parameters,docs:{...t.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      DashboardGrid,
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 8" :key="i" padding="md">
          <h4 class="font-semibold mb-1 text-sm">Metric {{ i }}</h4>
          <p class="text-2xl font-bold text-primary">{{ (i * 123).toLocaleString() }}</p>
        </Card>
      </DashboardGrid>
    \`
  }),
  args: {
    columns: 4
  }
}`,...t.parameters?.docs?.source}}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      DashboardGrid,
      Card
    },
    setup() {
      const stats = [{
        title: 'Total Revenue',
        value: '$45,231',
        change: '+12.5%',
        positive: true
      }, {
        title: 'Orders',
        value: '1,234',
        change: '+8.2%',
        positive: true
      }, {
        title: 'Customers',
        value: '5,678',
        change: '-2.3%',
        positive: false
      }, {
        title: 'Avg. Order',
        value: '$36.65',
        change: '+4.1%',
        positive: true
      }];
      return {
        args,
        stats
      };
    },
    template: \`
      <DashboardGrid v-bind="args">
        <Card
          v-for="(stat, i) in stats"
          :key="i"
          padding="lg"
          :hoverable="true"
        >
          <p class="text-sm text-gray-600 mb-1">{{ stat.title }}</p>
          <p class="text-3xl font-bold text-primary mb-2">{{ stat.value }}</p>
          <p :class="['text-sm font-medium', stat.positive ? 'text-green-600' : 'text-red-600']">
            {{ stat.change }}
          </p>
        </Card>
      </DashboardGrid>
    \`
  }),
  args: {
    columns: 4,
    gap: 'md'
  }
}`,...d.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      DashboardGrid,
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <DashboardGrid v-bind="args">
        <!-- Wide card -->
        <Card padding="lg" class="col-span-2">
          <h3 class="font-semibold mb-4">Revenue Chart</h3>
          <div class="h-48 bg-gray-100 rounded flex items-center justify-center">
            Chart Area
          </div>
        </Card>

        <!-- Regular cards -->
        <Card padding="md">
          <p class="text-sm text-gray-600 mb-1">Orders</p>
          <p class="text-2xl font-bold">1,234</p>
        </Card>

        <Card padding="md">
          <p class="text-sm text-gray-600 mb-1">Revenue</p>
          <p class="text-2xl font-bold">$45K</p>
        </Card>

        <Card padding="md">
          <p class="text-sm text-gray-600 mb-1">Customers</p>
          <p class="text-2xl font-bold">567</p>
        </Card>

        <!-- Tall card -->
        <Card padding="lg" class="row-span-2">
          <h3 class="font-semibold mb-4">Recent Activity</h3>
          <div class="space-y-3">
            <div v-for="i in 8" :key="i" class="flex items-center gap-2 text-sm">
              <div class="w-2 h-2 bg-primary rounded-full"></div>
              <span class="text-gray-600">Activity {{ i }}</span>
            </div>
          </div>
        </Card>

        <!-- Full width card -->
        <Card padding="lg" class="col-span-2">
          <h3 class="font-semibold mb-4">Performance Metrics</h3>
          <div class="grid grid-cols-3 gap-4">
            <div v-for="i in 3" :key="i">
              <p class="text-xs text-gray-600 mb-1">Metric {{ i }}</p>
              <p class="text-xl font-bold">{{ (i * 89).toFixed(1) }}%</p>
            </div>
          </div>
        </Card>
      </DashboardGrid>
    \`
  }),
  args: {
    columns: 3,
    gap: 'md'
  }
}`,...o.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      DashboardGrid,
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <div class="space-y-6">
        <!-- Top metrics -->
        <DashboardGrid columns="4" gap="md">
          <Card padding="lg" v-for="i in 4" :key="i">
            <div class="flex items-center justify-between mb-2">
              <p class="text-sm text-gray-600">Metric {{ i }}</p>
              <span :class="['i-carbon-arrow-up text-green-500']"></span>
            </div>
            <p class="text-2xl font-bold">{{ (i * 1234).toLocaleString() }}</p>
            <p class="text-xs text-green-600 mt-1">+12.5% vs last month</p>
          </Card>
        </DashboardGrid>

        <!-- Main charts -->
        <DashboardGrid v-bind="args">
          <Card padding="lg" class="col-span-2">
            <h3 class="font-semibold mb-4">Revenue Overview</h3>
            <div class="h-64 bg-gradient-to-t from-primary/10 to-transparent rounded flex items-end justify-center">
              <p class="text-gray-400 mb-8">Chart visualization</p>
            </div>
          </Card>

          <Card padding="lg">
            <h3 class="font-semibold mb-4">Top Products</h3>
            <div class="space-y-3">
              <div v-for="i in 5" :key="i" class="flex justify-between items-center">
                <span class="text-sm">Product {{ i }}</span>
                <span class="text-sm font-semibold">$ {{ (i * 123) }}</span>
              </div>
            </div>
          </Card>
        </DashboardGrid>

        <!-- Bottom stats -->
        <DashboardGrid columns="3" gap="md">
          <Card padding="md" v-for="i in 3" :key="i">
            <p class="text-xs text-gray-600 mb-2">Category {{ i }}</p>
            <p class="text-xl font-bold mb-1">{{ (i * 45).toFixed(1) }}%</p>
            <div class="w-full bg-gray-200 rounded-full h-2">
              <div class="bg-primary rounded-full h-2" :style="{ width: (i * 45) + '%' }"></div>
            </div>
          </Card>
        </DashboardGrid>
      </div>
    \`
  }),
  args: {
    columns: 3,
    gap: 'lg'
  }
}`,...i.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      DashboardGrid,
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 6" :key="i" padding="md">
          <p class="font-semibold">Card {{ i }}</p>
        </Card>
      </DashboardGrid>
    \`
  }),
  args: {
    gap: 'sm'
  }
}`,...l.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      DashboardGrid,
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 6" :key="i" padding="lg">
          <h3 class="font-semibold mb-2">Card {{ i }}</h3>
          <p class="text-gray-600">Large spacing between cards</p>
        </Card>
      </DashboardGrid>
    \`
  }),
  args: {
    gap: 'lg'
  }
}`,...p.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      DashboardGrid,
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <DashboardGrid v-bind="args">
        <Card v-for="i in 6" :key="i" padding="md">
          <p class="font-semibold">Card {{ i }}</p>
          <p class="text-sm text-gray-600">Fixed grid (not responsive)</p>
        </Card>
      </DashboardGrid>
    \`
  }),
  args: {
    responsive: false
  }
}`,...c.parameters?.docs?.source}}};const F=["Default","TwoColumns","FourColumns","StatsDashboard","MixedSizes","AnalyticsDashboard","SmallGap","LargeGap","NonResponsive"];export{i as AnalyticsDashboard,r as Default,t as FourColumns,p as LargeGap,o as MixedSizes,c as NonResponsive,l as SmallGap,d as StatsDashboard,n as TwoColumns,F as __namedExportsOrder,M as default};
