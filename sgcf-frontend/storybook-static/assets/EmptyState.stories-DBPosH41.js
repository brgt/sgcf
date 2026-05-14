import{d as L,c as o,a as m,b as f,r as N,n as S,t as p,o as a}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as v}from"./_plugin-vue_export-helper-DlAUqK2U.js";const E={class:"nw-empty-state"},x={class:"nw-empty-state__title"},A={key:1,class:"nw-empty-state__description"},b=L({__name:"EmptyState",props:{icon:{},title:{},description:{},actionLabel:{}},emits:["action"],setup(e,{emit:l}){const y=l;return(h,u)=>(a(),o("div",E,[e.icon?(a(),o("div",{key:0,class:S(["nw-empty-state__icon",e.icon])},null,2)):m("",!0),f("h3",x,p(e.title),1),e.description?(a(),o("p",A,p(e.description),1)):m("",!0),N(h.$slots,"action",{},()=>[e.actionLabel?(a(),o("button",{key:0,class:"btn-primary",onClick:u[0]||(u[0]=_=>y("action"))},p(e.actionLabel),1)):m("",!0)],!0)]))}}),g=v(b,[["__scopeId","data-v-4c213428"]]);b.__docgenInfo={exportName:"default",displayName:"EmptyState",description:"",tags:{},props:[{name:"icon",required:!1,type:{name:"string"}},{name:"title",required:!0,type:{name:"string"}},{name:"description",required:!1,type:{name:"string"}},{name:"actionLabel",required:!1,type:{name:"string"}}],events:[{name:"action"}],slots:[{name:"action"}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/molecules/EmptyState/EmptyState.vue"]};const w={title:"Design System/Molecules/EmptyState",component:g,tags:["autodocs"],argTypes:{icon:{control:"text",description:"Classe UnoCSS do ícone"},title:{control:"text",description:"Título do empty state"},description:{control:"text",description:"Descrição do empty state"},actionLabel:{control:"text",description:"Label do botão de ação"}},args:{icon:"i-carbon-inbox",title:"Nenhum item encontrado",description:"Não há itens para exibir no momento",actionLabel:"Adicionar Item"}},t={args:{icon:"i-carbon-inbox",title:"Nenhum item encontrado",description:"Não há itens para exibir no momento",actionLabel:"Adicionar Item"}},n={args:{icon:"i-carbon-shopping-cart",title:"Nenhum pedido encontrado",description:"Você ainda não possui pedidos cadastrados",actionLabel:"Criar Primeiro Pedido"}},r={args:{icon:"i-carbon-search",title:"Nenhum resultado encontrado",description:"Tente ajustar os filtros ou buscar por outro termo",actionLabel:"Limpar Filtros"}},i={args:{icon:"i-carbon-data-1",title:"Sem dados disponíveis",description:"Os dados ainda não foram carregados",actionLabel:"Recarregar"}},s={args:{icon:"i-carbon-information",title:"Informação",description:"Esta seção ainda não possui conteúdo",actionLabel:void 0}},c={args:{icon:"i-carbon-warning",title:"Erro ao carregar",description:"Não foi possível carregar os dados. Tente novamente.",actionLabel:"Tentar Novamente"}},d={render:e=>({components:{EmptyState:g},setup(){return{args:e,handleAction:()=>{alert("Ação clicada!")}}},template:`
      <EmptyState
        v-bind="args"
        @action="handleAction"
      />
    `}),args:{icon:"i-carbon-add",title:"Lista vazia",description:"Clique no botão abaixo para adicionar o primeiro item",actionLabel:"Adicionar Item"}};t.parameters={...t.parameters,docs:{...t.parameters?.docs,source:{originalSource:`{
  args: {
    icon: 'i-carbon-inbox',
    title: 'Nenhum item encontrado',
    description: 'Não há itens para exibir no momento',
    actionLabel: 'Adicionar Item'
  }
}`,...t.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  args: {
    icon: 'i-carbon-shopping-cart',
    title: 'Nenhum pedido encontrado',
    description: 'Você ainda não possui pedidos cadastrados',
    actionLabel: 'Criar Primeiro Pedido'
  }
}`,...n.parameters?.docs?.source}}};r.parameters={...r.parameters,docs:{...r.parameters?.docs,source:{originalSource:`{
  args: {
    icon: 'i-carbon-search',
    title: 'Nenhum resultado encontrado',
    description: 'Tente ajustar os filtros ou buscar por outro termo',
    actionLabel: 'Limpar Filtros'
  }
}`,...r.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  args: {
    icon: 'i-carbon-data-1',
    title: 'Sem dados disponíveis',
    description: 'Os dados ainda não foram carregados',
    actionLabel: 'Recarregar'
  }
}`,...i.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  args: {
    icon: 'i-carbon-information',
    title: 'Informação',
    description: 'Esta seção ainda não possui conteúdo',
    actionLabel: undefined
  }
}`,...s.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  args: {
    icon: 'i-carbon-warning',
    title: 'Erro ao carregar',
    description: 'Não foi possível carregar os dados. Tente novamente.',
    actionLabel: 'Tentar Novamente'
  }
}`,...c.parameters?.docs?.source}}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      EmptyState
    },
    setup() {
      const handleAction = () => {
        alert('Ação clicada!');
      };
      return {
        args,
        handleAction
      };
    },
    template: \`
      <EmptyState
        v-bind="args"
        @action="handleAction"
      />
    \`
  }),
  args: {
    icon: 'i-carbon-add',
    title: 'Lista vazia',
    description: 'Clique no botão abaixo para adicionar o primeiro item',
    actionLabel: 'Adicionar Item'
  }
}`,...d.parameters?.docs?.source}}};const T=["Default","NoOrders","NoResults","NoData","WithoutAction","Error","Interactive"];export{t as Default,c as Error,d as Interactive,i as NoData,n as NoOrders,r as NoResults,s as WithoutAction,T as __namedExportsOrder,w as default};
