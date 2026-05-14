import{d as D,h as S,k as V,l as C,n as v,m as q,o as r,c as y,a as h,b as I,r as R}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as E}from"./_plugin-vue_export-helper-DlAUqK2U.js";const N={key:1,class:"nw-button__spinner"},A={class:"nw-button__content"},k=D({__name:"Button",props:{variant:{default:"primary"},size:{default:"md"},type:{default:"button"},disabled:{type:Boolean,default:!1},loading:{type:Boolean,default:!1},fullWidth:{type:Boolean,default:!1},iconLeft:{},iconRight:{},href:{},target:{}},emits:["click"],setup(e,{emit:w}){const n=e,L=w,x=S(()=>{const a=["nw-button"];return a.push(`nw-button--${n.variant}`),a.push(`nw-button--${n.size}`),(n.disabled||n.loading)&&a.push("nw-button--disabled"),n.loading&&a.push("nw-button--loading"),n.fullWidth&&a.push("nw-button--full-width"),a}),W=a=>{!n.disabled&&!n.loading&&L("click",a)},z=S(()=>n.href?"a":"button");return(a,G)=>(r(),V(q(z.value),{class:v(x.value),type:e.href?void 0:e.type,disabled:e.disabled||e.loading,href:e.href,target:e.target,"aria-disabled":e.disabled||e.loading,"aria-busy":e.loading,onClick:W},{default:C(()=>[e.iconLeft&&!e.loading?(r(),y("span",{key:0,class:v(["nw-button__icon",e.iconLeft])},null,2)):h("",!0),e.loading?(r(),y("span",N)):h("",!0),I("span",A,[R(a.$slots,"default",{},void 0,!0)]),e.iconRight&&!e.loading?(r(),y("span",{key:2,class:v(["nw-button__icon",e.iconRight])},null,2)):h("",!0)]),_:3},8,["class","type","disabled","href","target","aria-disabled","aria-busy"]))}}),t=E(k,[["__scopeId","data-v-a2d8ac72"]]);k.__docgenInfo={exportName:"default",displayName:"Button",description:"",tags:{},props:[{name:"variant",description:"Variante visual do botão",required:!1,type:{name:"union",elements:[{name:'"primary"'},{name:'"secondary"'},{name:'"ghost"'},{name:'"danger"'},{name:'"success"'}]},defaultValue:{func:!1,value:"'primary'"}},{name:"size",description:"Tamanho do botão",required:!1,type:{name:"union",elements:[{name:'"sm"'},{name:'"md"'},{name:'"lg"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"type",description:"Tipo do botão HTML",required:!1,type:{name:"union",elements:[{name:'"button"'},{name:'"submit"'},{name:'"reset"'}]},defaultValue:{func:!1,value:"'button'"}},{name:"disabled",description:"Estado desabilitado",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"loading",description:"Estado de loading",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"fullWidth",description:"Botão em largura total",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"iconLeft",description:"Ícone à esquerda (classe UnoCSS)",required:!1,type:{name:"string"}},{name:"iconRight",description:"Ícone à direita (classe UnoCSS)",required:!1,type:{name:"string"}},{name:"href",description:"Link (comporta-se como <a>)",required:!1,type:{name:"string"}},{name:"target",description:"Target para links",required:!1,type:{name:"union",elements:[{name:'"_blank"'},{name:'"_self"'},{name:'"_parent"'},{name:'"_top"'}]}}],events:[{name:"click",type:{names:["MouseEvent"]}}],slots:[{name:"default"}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/atoms/Button/Button.vue"]};const M={title:"Design System/Atoms/Button",component:t,tags:["autodocs"],argTypes:{variant:{control:"select",options:["primary","secondary","ghost","danger","success"],description:"Variante visual do botão"},size:{control:"select",options:["sm","md","lg"],description:"Tamanho do botão"},disabled:{control:"boolean",description:"Estado desabilitado"},loading:{control:"boolean",description:"Estado de loading"},fullWidth:{control:"boolean",description:"Botão em largura total"},onClick:{action:"clicked",description:"Evento de click"}},args:{variant:"primary",size:"md",disabled:!1,loading:!1,fullWidth:!1}},s={args:{default:"Button"},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})},o={args:{variant:"secondary",default:"Secondary Button"},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})},u={args:{variant:"ghost",default:"Ghost Button"},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})},d={args:{variant:"danger",default:"Delete"},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})},i={args:{variant:"success",default:"Confirm"},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})},l={args:{iconLeft:"i-carbon-add",default:"Add Item"},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})},c={args:{iconRight:"i-carbon-arrow-right",default:"Next"},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})},m={args:{loading:!0,default:"Loading..."},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})},p={args:{disabled:!0,default:"Disabled Button"},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})},g={args:{fullWidth:!0,default:"Full Width Button"},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})},f={render:()=>({components:{Button:t},template:`
      <div style="display: flex; gap: 1rem; align-items: center;">
        <Button size="sm">Small</Button>
        <Button size="md">Medium</Button>
        <Button size="lg">Large</Button>
      </div>
    `})},B={render:()=>({components:{Button:t},template:`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <div style="display: flex; gap: 1rem;">
          <Button variant="primary">Primary</Button>
          <Button variant="secondary">Secondary</Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="danger">Danger</Button>
          <Button variant="success">Success</Button>
        </div>
        <div style="display: flex; gap: 1rem;">
          <Button variant="primary" :loading="true">Loading</Button>
          <Button variant="secondary" :disabled="true">Disabled</Button>
          <Button variant="primary" icon-left="i-carbon-add">With Icon</Button>
        </div>
      </div>
    `})},b={args:{href:"https://nordware.io",target:"_blank",iconRight:"i-carbon-launch",default:"Visit Nordware"},render:e=>({components:{Button:t},setup(){return{args:e}},template:'<Button v-bind="args">{{ args.default }}</Button>'})};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  args: {
    default: 'Button'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...s.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'secondary',
    default: 'Secondary Button'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...o.parameters?.docs?.source}}};u.parameters={...u.parameters,docs:{...u.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'ghost',
    default: 'Ghost Button'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...u.parameters?.docs?.source}}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'danger',
    default: 'Delete'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...d.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'success',
    default: 'Confirm'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...i.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  args: {
    iconLeft: 'i-carbon-add',
    default: 'Add Item'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...l.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  args: {
    iconRight: 'i-carbon-arrow-right',
    default: 'Next'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...c.parameters?.docs?.source}}};m.parameters={...m.parameters,docs:{...m.parameters?.docs,source:{originalSource:`{
  args: {
    loading: true,
    default: 'Loading...'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...m.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  args: {
    disabled: true,
    default: 'Disabled Button'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...p.parameters?.docs?.source}}};g.parameters={...g.parameters,docs:{...g.parameters?.docs,source:{originalSource:`{
  args: {
    fullWidth: true,
    default: 'Full Width Button'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...g.parameters?.docs?.source}}};f.parameters={...f.parameters,docs:{...f.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Button
    },
    template: \`
      <div style="display: flex; gap: 1rem; align-items: center;">
        <Button size="sm">Small</Button>
        <Button size="md">Medium</Button>
        <Button size="lg">Large</Button>
      </div>
    \`
  })
}`,...f.parameters?.docs?.source}}};B.parameters={...B.parameters,docs:{...B.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Button
    },
    template: \`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <div style="display: flex; gap: 1rem;">
          <Button variant="primary">Primary</Button>
          <Button variant="secondary">Secondary</Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="danger">Danger</Button>
          <Button variant="success">Success</Button>
        </div>
        <div style="display: flex; gap: 1rem;">
          <Button variant="primary" :loading="true">Loading</Button>
          <Button variant="secondary" :disabled="true">Disabled</Button>
          <Button variant="primary" icon-left="i-carbon-add">With Icon</Button>
        </div>
      </div>
    \`
  })
}`,...B.parameters?.docs?.source}}};b.parameters={...b.parameters,docs:{...b.parameters?.docs,source:{originalSource:`{
  args: {
    href: 'https://nordware.io',
    target: '_blank',
    iconRight: 'i-carbon-launch',
    default: 'Visit Nordware'
  },
  render: args => ({
    components: {
      Button
    },
    setup() {
      return {
        args
      };
    },
    template: '<Button v-bind="args">{{ args.default }}</Button>'
  })
}`,...b.parameters?.docs?.source}}};const P=["Primary","Secondary","Ghost","Danger","Success","WithIconLeft","WithIconRight","Loading","Disabled","FullWidth","Sizes","AllVariants","AsLink"];export{B as AllVariants,b as AsLink,d as Danger,p as Disabled,g as FullWidth,u as Ghost,m as Loading,s as Primary,o as Secondary,f as Sizes,i as Success,l as WithIconLeft,c as WithIconRight,P as __namedExportsOrder,M as default};
