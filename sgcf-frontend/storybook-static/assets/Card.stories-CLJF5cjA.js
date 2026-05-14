import{C as e}from"./Card-BgWWpAS0.js";import"./vue.esm-bundler-Sd8zNG93.js";import"./_plugin-vue_export-helper-DlAUqK2U.js";const b={title:"Design System/Molecules/Card",component:e,tags:["autodocs"],argTypes:{title:{control:"text",description:"Título do card"},subtitle:{control:"text",description:"Subtítulo do card"},padding:{control:"select",options:["none","sm","md","lg"],description:"Padding interno"},hoverable:{control:"boolean",description:"Habilita efeito de hover (lift)"},bordered:{control:"boolean",description:"Mostra borda"}},args:{title:"",subtitle:"",padding:"md",hoverable:!1,bordered:!0}},a={render:t=>({components:{Card:e},setup(){return{args:t}},template:`
      <Card v-bind="args">
        <p>This is the card content. You can put any content here.</p>
      </Card>
    `}),args:{title:"Card Title",subtitle:"Card subtitle",padding:"md",hoverable:!1,bordered:!0}},r={render:t=>({components:{Card:e},setup(){return{args:t}},template:`
      <Card v-bind="args">
        <p>Card without header, just content.</p>
      </Card>
    `}),args:{padding:"lg",bordered:!0}},n={render:t=>({components:{Card:e},setup(){return{args:t}},template:`
      <Card v-bind="args">
        <p>Hover over this card to see the lift effect.</p>
      </Card>
    `}),args:{title:"Hoverable Card",subtitle:"With lift animation",padding:"md",hoverable:!0,bordered:!0}},s={render:t=>({components:{Card:e},setup(){return{args:t}},template:`
      <Card v-bind="args">
        <p>This card has a footer with action buttons.</p>

        <template #footer>
          <div class="flex gap-2 justify-end">
            <button class="px-4 py-2 bg-gray-200 rounded-lg">Cancel</button>
            <button class="px-4 py-2 bg-primary text-white rounded-lg">Save</button>
          </div>
        </template>
      </Card>
    `}),args:{title:"Form Card",subtitle:"Fill in the details",padding:"lg"}},d={render:t=>({components:{Card:e},setup(){return{args:t}},template:`
      <Card v-bind="args">
        <template #header>
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 bg-primary rounded-full flex items-center justify-center text-white">
                <span class="i-carbon-user"></span>
              </div>
              <div>
                <h3 class="font-semibold">Custom Header</h3>
                <p class="text-sm text-gray-500">With avatar and actions</p>
              </div>
            </div>
            <button class="p-2 hover:bg-gray-100 rounded">
              <span class="i-carbon-overflow-menu-vertical"></span>
            </button>
          </div>
        </template>

        <p>Card with completely custom header slot.</p>
      </Card>
    `}),args:{padding:"lg"}},o={render:()=>({components:{Card:e},template:`
      <div class="space-y-4">
        <Card title="No Padding" padding="none">
          <div class="p-4 bg-blue-50">Content (manually padded)</div>
        </Card>

        <Card title="Small Padding" padding="sm">
          <p>Card with small padding (1rem)</p>
        </Card>

        <Card title="Medium Padding (Default)" padding="md">
          <p>Card with medium padding (1.5rem)</p>
        </Card>

        <Card title="Large Padding" padding="lg">
          <p>Card with large padding (2rem)</p>
        </Card>
      </div>
    `})},i={render:t=>({components:{Card:e},setup(){return{args:t}},template:`
      <Card v-bind="args">
        <p>Card without border.</p>
      </Card>
    `}),args:{title:"No Border Card",padding:"md",bordered:!1}},l={render:()=>({components:{Card:e},template:`
      <div class="grid grid-cols-3 gap-4">
        <Card title="Total Orders" padding="lg" :hoverable="true">
          <div class="text-3xl font-bold text-primary">1,234</div>
          <p class="text-sm text-gray-500 mt-2">+12% from last month</p>
        </Card>

        <Card title="Revenue" padding="lg" :hoverable="true">
          <div class="text-3xl font-bold text-green-600">$45,678</div>
          <p class="text-sm text-gray-500 mt-2">+8% from last month</p>
        </Card>

        <Card title="Active Users" padding="lg" :hoverable="true">
          <div class="text-3xl font-bold text-blue-600">890</div>
          <p class="text-sm text-gray-500 mt-2">+5% from last month</p>
        </Card>
      </div>
    `})},p={render:()=>({components:{Card:e},template:`
      <div class="max-w-sm">
        <Card padding="none" :hoverable="true">
          <img
            src="https://via.placeholder.com/400x300"
            alt="Product"
            class="w-full h-48 object-cover"
          />
          <div class="p-4">
            <h3 class="font-semibold text-lg">Product Name</h3>
            <p class="text-gray-500 mt-1">Product description goes here</p>
            <div class="flex items-center justify-between mt-4">
              <span class="text-2xl font-bold text-primary">$99.99</span>
              <button class="px-4 py-2 bg-primary text-white rounded-lg">
                Add to Cart
              </button>
            </div>
          </div>
        </Card>
      </div>
    `})},c={render:()=>({components:{Card:e},template:`
      <div>
        <div class="mb-6 p-4 bg-surface-darker rounded-lg">
          <h3 class="text-lg font-semibold mb-2">✨ Efeito Hover Neon</h3>
          <p class="text-sm text-gray-400 mb-2">
            Passe o mouse sobre os cards abaixo para ver o efeito neon interativo!
          </p>
          <ul class="text-xs text-gray-500 space-y-1">
            <li>🌟 <strong>Neon Glow:</strong> Brilho verde-água de 20px ao redor do card</li>
            <li>🚀 <strong>Enhanced Lift:</strong> Elevação de -4px para maior destaque</li>
            <li>🎨 <strong>Dynamic Border:</strong> Borda muda para cor primária no hover</li>
          </ul>
        </div>

        <div class="grid grid-cols-3 gap-6">
          <Card title="Métricas" subtitle="Performance do sistema" padding="lg" :hoverable="true">
            <div class="text-3xl font-bold text-primary">98.5%</div>
            <p class="text-sm text-gray-500 mt-2">Uptime este mês</p>
          </Card>

          <Card title="Analytics" subtitle="Dados em tempo real" padding="lg" :hoverable="true">
            <div class="text-3xl font-bold text-success">2,450</div>
            <p class="text-sm text-gray-500 mt-2">Usuários ativos agora</p>
          </Card>

          <Card title="Status" subtitle="Sistema operacional" padding="lg" :hoverable="true">
            <div class="text-3xl font-bold text-primary">100%</div>
            <p class="text-sm text-gray-500 mt-2">Todos os serviços online</p>
          </Card>
        </div>
      </div>
    `})},m={render:()=>({components:{Card:e},template:`
      <div class="max-w-md">
        <Card padding="lg">
          <template #header>
            <div class="flex items-center gap-4">
              <img
                src="https://i.pravatar.cc/80"
                alt="Avatar"
                class="w-20 h-20 rounded-full"
              />
              <div>
                <h3 class="font-semibold text-xl">John Doe</h3>
                <p class="text-gray-500">john.doe@example.com</p>
              </div>
            </div>
          </template>

          <div class="space-y-3">
            <div class="flex justify-between">
              <span class="text-gray-600">Role:</span>
              <span class="font-medium">Administrator</span>
            </div>
            <div class="flex justify-between">
              <span class="text-gray-600">Department:</span>
              <span class="font-medium">Engineering</span>
            </div>
            <div class="flex justify-between">
              <span class="text-gray-600">Joined:</span>
              <span class="font-medium">Jan 2024</span>
            </div>
          </div>

          <template #footer>
            <div class="flex gap-2">
              <button class="flex-1 px-4 py-2 bg-gray-200 rounded-lg">Message</button>
              <button class="flex-1 px-4 py-2 bg-primary text-white rounded-lg">Edit Profile</button>
            </div>
          </template>
        </Card>
      </div>
    `})};a.parameters={...a.parameters,docs:{...a.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <Card v-bind="args">
        <p>This is the card content. You can put any content here.</p>
      </Card>
    \`
  }),
  args: {
    title: 'Card Title',
    subtitle: 'Card subtitle',
    padding: 'md',
    hoverable: false,
    bordered: true
  }
}`,...a.parameters?.docs?.source}}};r.parameters={...r.parameters,docs:{...r.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <Card v-bind="args">
        <p>Card without header, just content.</p>
      </Card>
    \`
  }),
  args: {
    padding: 'lg',
    bordered: true
  }
}`,...r.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <Card v-bind="args">
        <p>Hover over this card to see the lift effect.</p>
      </Card>
    \`
  }),
  args: {
    title: 'Hoverable Card',
    subtitle: 'With lift animation',
    padding: 'md',
    hoverable: true,
    bordered: true
  }
}`,...n.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <Card v-bind="args">
        <p>This card has a footer with action buttons.</p>

        <template #footer>
          <div class="flex gap-2 justify-end">
            <button class="px-4 py-2 bg-gray-200 rounded-lg">Cancel</button>
            <button class="px-4 py-2 bg-primary text-white rounded-lg">Save</button>
          </div>
        </template>
      </Card>
    \`
  }),
  args: {
    title: 'Form Card',
    subtitle: 'Fill in the details',
    padding: 'lg'
  }
}`,...s.parameters?.docs?.source}}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <Card v-bind="args">
        <template #header>
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 bg-primary rounded-full flex items-center justify-center text-white">
                <span class="i-carbon-user"></span>
              </div>
              <div>
                <h3 class="font-semibold">Custom Header</h3>
                <p class="text-sm text-gray-500">With avatar and actions</p>
              </div>
            </div>
            <button class="p-2 hover:bg-gray-100 rounded">
              <span class="i-carbon-overflow-menu-vertical"></span>
            </button>
          </div>
        </template>

        <p>Card with completely custom header slot.</p>
      </Card>
    \`
  }),
  args: {
    padding: 'lg'
  }
}`,...d.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Card
    },
    template: \`
      <div class="space-y-4">
        <Card title="No Padding" padding="none">
          <div class="p-4 bg-blue-50">Content (manually padded)</div>
        </Card>

        <Card title="Small Padding" padding="sm">
          <p>Card with small padding (1rem)</p>
        </Card>

        <Card title="Medium Padding (Default)" padding="md">
          <p>Card with medium padding (1.5rem)</p>
        </Card>

        <Card title="Large Padding" padding="lg">
          <p>Card with large padding (2rem)</p>
        </Card>
      </div>
    \`
  })
}`,...o.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Card
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <Card v-bind="args">
        <p>Card without border.</p>
      </Card>
    \`
  }),
  args: {
    title: 'No Border Card',
    padding: 'md',
    bordered: false
  }
}`,...i.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Card
    },
    template: \`
      <div class="grid grid-cols-3 gap-4">
        <Card title="Total Orders" padding="lg" :hoverable="true">
          <div class="text-3xl font-bold text-primary">1,234</div>
          <p class="text-sm text-gray-500 mt-2">+12% from last month</p>
        </Card>

        <Card title="Revenue" padding="lg" :hoverable="true">
          <div class="text-3xl font-bold text-green-600">$45,678</div>
          <p class="text-sm text-gray-500 mt-2">+8% from last month</p>
        </Card>

        <Card title="Active Users" padding="lg" :hoverable="true">
          <div class="text-3xl font-bold text-blue-600">890</div>
          <p class="text-sm text-gray-500 mt-2">+5% from last month</p>
        </Card>
      </div>
    \`
  })
}`,...l.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Card
    },
    template: \`
      <div class="max-w-sm">
        <Card padding="none" :hoverable="true">
          <img
            src="https://via.placeholder.com/400x300"
            alt="Product"
            class="w-full h-48 object-cover"
          />
          <div class="p-4">
            <h3 class="font-semibold text-lg">Product Name</h3>
            <p class="text-gray-500 mt-1">Product description goes here</p>
            <div class="flex items-center justify-between mt-4">
              <span class="text-2xl font-bold text-primary">$99.99</span>
              <button class="px-4 py-2 bg-primary text-white rounded-lg">
                Add to Cart
              </button>
            </div>
          </div>
        </Card>
      </div>
    \`
  })
}`,...p.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Card
    },
    template: \`
      <div>
        <div class="mb-6 p-4 bg-surface-darker rounded-lg">
          <h3 class="text-lg font-semibold mb-2">✨ Efeito Hover Neon</h3>
          <p class="text-sm text-gray-400 mb-2">
            Passe o mouse sobre os cards abaixo para ver o efeito neon interativo!
          </p>
          <ul class="text-xs text-gray-500 space-y-1">
            <li>🌟 <strong>Neon Glow:</strong> Brilho verde-água de 20px ao redor do card</li>
            <li>🚀 <strong>Enhanced Lift:</strong> Elevação de -4px para maior destaque</li>
            <li>🎨 <strong>Dynamic Border:</strong> Borda muda para cor primária no hover</li>
          </ul>
        </div>

        <div class="grid grid-cols-3 gap-6">
          <Card title="Métricas" subtitle="Performance do sistema" padding="lg" :hoverable="true">
            <div class="text-3xl font-bold text-primary">98.5%</div>
            <p class="text-sm text-gray-500 mt-2">Uptime este mês</p>
          </Card>

          <Card title="Analytics" subtitle="Dados em tempo real" padding="lg" :hoverable="true">
            <div class="text-3xl font-bold text-success">2,450</div>
            <p class="text-sm text-gray-500 mt-2">Usuários ativos agora</p>
          </Card>

          <Card title="Status" subtitle="Sistema operacional" padding="lg" :hoverable="true">
            <div class="text-3xl font-bold text-primary">100%</div>
            <p class="text-sm text-gray-500 mt-2">Todos os serviços online</p>
          </Card>
        </div>
      </div>
    \`
  })
}`,...c.parameters?.docs?.source}}};m.parameters={...m.parameters,docs:{...m.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Card
    },
    template: \`
      <div class="max-w-md">
        <Card padding="lg">
          <template #header>
            <div class="flex items-center gap-4">
              <img
                src="https://i.pravatar.cc/80"
                alt="Avatar"
                class="w-20 h-20 rounded-full"
              />
              <div>
                <h3 class="font-semibold text-xl">John Doe</h3>
                <p class="text-gray-500">john.doe@example.com</p>
              </div>
            </div>
          </template>

          <div class="space-y-3">
            <div class="flex justify-between">
              <span class="text-gray-600">Role:</span>
              <span class="font-medium">Administrator</span>
            </div>
            <div class="flex justify-between">
              <span class="text-gray-600">Department:</span>
              <span class="font-medium">Engineering</span>
            </div>
            <div class="flex justify-between">
              <span class="text-gray-600">Joined:</span>
              <span class="font-medium">Jan 2024</span>
            </div>
          </div>

          <template #footer>
            <div class="flex gap-2">
              <button class="flex-1 px-4 py-2 bg-gray-200 rounded-lg">Message</button>
              <button class="flex-1 px-4 py-2 bg-primary text-white rounded-lg">Edit Profile</button>
            </div>
          </template>
        </Card>
      </div>
    \`
  })
}`,...m.parameters?.docs?.source}}};const x=["Default","WithoutHeader","Hoverable","WithFooter","CustomHeader","PaddingVariants","NoBorder","Dashboard","ProductCard","NeonHoverEffect","UserProfile"];export{d as CustomHeader,l as Dashboard,a as Default,n as Hoverable,c as NeonHoverEffect,i as NoBorder,o as PaddingVariants,p as ProductCard,m as UserProfile,s as WithFooter,r as WithoutHeader,x as __namedExportsOrder,b as default};
