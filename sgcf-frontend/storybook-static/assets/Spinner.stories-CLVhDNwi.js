import{d,c as g,b as x,j as u,n as S,o as f}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as z}from"./_plugin-vue_export-helper-DlAUqK2U.js";const m=d({__name:"Spinner",props:{size:{default:"md"},color:{}},setup(l){return(y,p)=>(f(),g("div",{class:S(["nw-spinner",`nw-spinner--${l.size}`]),style:u({borderTopColor:l.color}),role:"status","aria-label":"Loading"},[...p[0]||(p[0]=[x("span",{class:"sr-only"},"Loading...",-1)])],6))}}),i=z(m,[["__scopeId","data-v-5e1ffaf3"]]);m.__docgenInfo={exportName:"default",displayName:"Spinner",description:"",tags:{},props:[{name:"size",required:!1,type:{name:"union",elements:[{name:'"sm"'},{name:'"md"'},{name:'"lg"'},{name:'"xl"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"color",required:!1,type:{name:"string"}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/atoms/Spinner/Spinner.vue"]};const C={title:"Design System/Atoms/Spinner",component:i,tags:["autodocs"],argTypes:{size:{control:"select",options:["sm","md","lg","xl"],description:"Tamanho do spinner"},color:{control:"text",description:"Cor do spinner (classe UnoCSS ou cor CSS)"}},args:{size:"md",color:"text-primary"}},e={args:{size:"md",color:"text-primary"}},r={args:{size:"sm",color:"text-primary"}},s={args:{size:"lg",color:"text-primary"}},n={args:{size:"xl",color:"text-primary"}},t={render:()=>({components:{Spinner:i},template:`
      <div class="flex items-center gap-8">
        <div class="text-center">
          <Spinner size="sm" />
          <p class="text-xs mt-2">Small</p>
        </div>
        <div class="text-center">
          <Spinner size="md" />
          <p class="text-xs mt-2">Medium</p>
        </div>
        <div class="text-center">
          <Spinner size="lg" />
          <p class="text-xs mt-2">Large</p>
        </div>
        <div class="text-center">
          <Spinner size="xl" />
          <p class="text-xs mt-2">Extra Large</p>
        </div>
      </div>
    `})},a={args:{size:"lg",color:"text-green-500"}},o={render:()=>({components:{Spinner:i},template:`
      <button class="px-6 py-3 bg-primary text-white rounded-lg flex items-center gap-2" disabled>
        <Spinner size="sm" color="text-white" />
        Carregando...
      </button>
    `})},c={render:()=>({components:{Spinner:i},template:`
      <div class="p-8 bg-surface rounded-lg border border-gray-200 flex flex-col items-center justify-center">
        <Spinner size="lg" />
        <p class="mt-4 text-gray-600">Carregando dados...</p>
      </div>
    `})};e.parameters={...e.parameters,docs:{...e.parameters?.docs,source:{originalSource:`{
  args: {
    size: 'md',
    color: 'text-primary'
  }
}`,...e.parameters?.docs?.source}}};r.parameters={...r.parameters,docs:{...r.parameters?.docs,source:{originalSource:`{
  args: {
    size: 'sm',
    color: 'text-primary'
  }
}`,...r.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  args: {
    size: 'lg',
    color: 'text-primary'
  }
}`,...s.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  args: {
    size: 'xl',
    color: 'text-primary'
  }
}`,...n.parameters?.docs?.source}}};t.parameters={...t.parameters,docs:{...t.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Spinner
    },
    template: \`
      <div class="flex items-center gap-8">
        <div class="text-center">
          <Spinner size="sm" />
          <p class="text-xs mt-2">Small</p>
        </div>
        <div class="text-center">
          <Spinner size="md" />
          <p class="text-xs mt-2">Medium</p>
        </div>
        <div class="text-center">
          <Spinner size="lg" />
          <p class="text-xs mt-2">Large</p>
        </div>
        <div class="text-center">
          <Spinner size="xl" />
          <p class="text-xs mt-2">Extra Large</p>
        </div>
      </div>
    \`
  })
}`,...t.parameters?.docs?.source}}};a.parameters={...a.parameters,docs:{...a.parameters?.docs,source:{originalSource:`{
  args: {
    size: 'lg',
    color: 'text-green-500'
  }
}`,...a.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Spinner
    },
    template: \`
      <button class="px-6 py-3 bg-primary text-white rounded-lg flex items-center gap-2" disabled>
        <Spinner size="sm" color="text-white" />
        Carregando...
      </button>
    \`
  })
}`,...o.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Spinner
    },
    template: \`
      <div class="p-8 bg-surface rounded-lg border border-gray-200 flex flex-col items-center justify-center">
        <Spinner size="lg" />
        <p class="mt-4 text-gray-600">Carregando dados...</p>
      </div>
    \`
  })
}`,...c.parameters?.docs?.source}}};const _=["Default","Small","Large","ExtraLarge","AllSizes","CustomColor","InButton","InCard"];export{t as AllSizes,a as CustomColor,e as Default,n as ExtraLarge,o as InButton,c as InCard,s as Large,r as Small,_ as __namedExportsOrder,C as default};
