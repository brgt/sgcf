import{d as I,h as w,k as W,l as T,n as x,m as E,o as k,c as S,a as h,b as q,r as A}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as M}from"./_plugin-vue_export-helper-DlAUqK2U.js";const O={key:0,class:"nw-badge__dot","aria-hidden":"true"},N={class:"nw-badge__content"},C=I({__name:"Badge",props:{variant:{default:"default"},size:{default:"md"},pill:{type:Boolean,default:!1},outline:{type:Boolean,default:!1},dot:{type:Boolean,default:!1},icon:{},clickable:{type:Boolean,default:!1}},emits:["click"],setup(e,{emit:P}){const r=e,z=P,D=w(()=>{const n=["nw-badge"];return n.push(`nw-badge--${r.variant}`),n.push(`nw-badge--${r.size}`),r.pill&&n.push("nw-badge--pill"),r.outline&&n.push("nw-badge--outline"),r.dot&&n.push("nw-badge--dot"),r.clickable&&n.push("nw-badge--clickable"),n}),V=n=>{r.clickable&&z("click",n)},_=w(()=>r.clickable?"button":"span");return(n,L)=>(k(),W(E(_.value),{class:x(D.value),type:e.clickable?"button":void 0,onClick:V},{default:T(()=>[e.dot?(k(),S("span",O)):h("",!0),e.icon?(k(),S("span",{key:1,class:x(["nw-badge__icon",e.icon]),"aria-hidden":"true"},null,2)):h("",!0),q("span",N,[A(n.$slots,"default",{},void 0,!0)])]),_:3},8,["class","type"]))}}),a=M(C,[["__scopeId","data-v-282ecf39"]]);C.__docgenInfo={exportName:"default",displayName:"Badge",description:"",tags:{},props:[{name:"variant",description:"Variante visual do badge",required:!1,type:{name:"union",elements:[{name:'"default"'},{name:'"primary"'},{name:'"success"'},{name:'"warning"'},{name:'"danger"'},{name:'"info"'}]},defaultValue:{func:!1,value:"'default'"}},{name:"size",description:"Tamanho do badge",required:!1,type:{name:"union",elements:[{name:'"sm"'},{name:'"md"'},{name:'"lg"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"pill",description:"Badge arredondado (pill)",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"outline",description:"Badge com outline",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"dot",description:"Badge com dot (indicador)",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"icon",description:"Ícone (classe UnoCSS)",required:!1,type:{name:"string"}},{name:"clickable",description:"Clickable badge",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}}],events:[{name:"click",type:{names:["MouseEvent"]}}],slots:[{name:"default"}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/atoms/Badge/Badge.vue"]};const H={title:"Design System/Atoms/Badge",component:a,tags:["autodocs"],argTypes:{variant:{control:"select",options:["default","primary","success","warning","danger","info"],description:"Variante visual do badge"},size:{control:"select",options:["sm","md","lg"],description:"Tamanho do badge"},pill:{control:"boolean",description:"Badge arredondado (pill)"},outline:{control:"boolean",description:"Badge com outline"},dot:{control:"boolean",description:"Badge com dot (indicador)"},clickable:{control:"boolean",description:"Badge clickável"},onClick:{action:"clicked",description:"Evento de click"}},args:{variant:"default",size:"md",pill:!1,outline:!1,dot:!1,clickable:!1}},t={args:{default:"Badge"},render:e=>({components:{Badge:a},setup(){return{args:e}},template:'<Badge v-bind="args">{{ args.default }}</Badge>'})},i={args:{variant:"primary",default:"Primary"},render:e=>({components:{Badge:a},setup(){return{args:e}},template:'<Badge v-bind="args">{{ args.default }}</Badge>'})},d={args:{variant:"success",default:"Success"},render:e=>({components:{Badge:a},setup(){return{args:e}},template:'<Badge v-bind="args">{{ args.default }}</Badge>'})},s={args:{variant:"warning",default:"Warning"},render:e=>({components:{Badge:a},setup(){return{args:e}},template:'<Badge v-bind="args">{{ args.default }}</Badge>'})},l={args:{variant:"danger",default:"Danger"},render:e=>({components:{Badge:a},setup(){return{args:e}},template:'<Badge v-bind="args">{{ args.default }}</Badge>'})},o={args:{variant:"info",default:"Info"},render:e=>({components:{Badge:a},setup(){return{args:e}},template:'<Badge v-bind="args">{{ args.default }}</Badge>'})},c={render:()=>({components:{Badge:a},template:`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="default">Default</Badge>
        <Badge variant="primary">Primary</Badge>
        <Badge variant="success">Success</Badge>
        <Badge variant="warning">Warning</Badge>
        <Badge variant="danger">Danger</Badge>
        <Badge variant="info">Info</Badge>
      </div>
    `})},g={render:()=>({components:{Badge:a},template:`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="default" :outline="true">Default</Badge>
        <Badge variant="primary" :outline="true">Primary</Badge>
        <Badge variant="success" :outline="true">Success</Badge>
        <Badge variant="warning" :outline="true">Warning</Badge>
        <Badge variant="danger" :outline="true">Danger</Badge>
        <Badge variant="info" :outline="true">Info</Badge>
      </div>
    `})},p={render:()=>({components:{Badge:a},template:`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="primary" :pill="true">Primary Pill</Badge>
        <Badge variant="success" :pill="true">Success Pill</Badge>
        <Badge variant="warning" :pill="true">Warning Pill</Badge>
        <Badge variant="danger" :pill="true">Danger Pill</Badge>
      </div>
    `})},u={render:()=>({components:{Badge:a},template:`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="success" :dot="true">Online</Badge>
        <Badge variant="warning" :dot="true">Busy</Badge>
        <Badge variant="danger" :dot="true">Offline</Badge>
        <Badge variant="default" :dot="true">Away</Badge>
      </div>
    `})},m={render:()=>({components:{Badge:a},template:`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="primary" icon="i-carbon-checkmark-filled">Verified</Badge>
        <Badge variant="success" icon="i-carbon-checkmark">Completed</Badge>
        <Badge variant="warning" icon="i-carbon-warning">Warning</Badge>
        <Badge variant="danger" icon="i-carbon-close">Error</Badge>
        <Badge variant="info" icon="i-carbon-information">Info</Badge>
      </div>
    `})},B={render:()=>({components:{Badge:a},template:`
      <div style="display: flex; align-items: center; gap: 1rem;">
        <Badge variant="primary" size="sm">Small</Badge>
        <Badge variant="primary" size="md">Medium</Badge>
        <Badge variant="primary" size="lg">Large</Badge>
      </div>
    `})},v={render:()=>({components:{Badge:a},setup(){return{handleClick:()=>{alert("Badge clicked!")}}},template:`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="primary" :clickable="true" @click="handleClick">Click me</Badge>
        <Badge variant="success" :clickable="true" @click="handleClick">Clickable</Badge>
        <Badge variant="default" :clickable="true" :outline="true" @click="handleClick">Remove</Badge>
      </div>
    `})},f={render:()=>({components:{Badge:a},template:`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Pedido:</span>
          <Badge variant="success" :dot="true" :pill="true">Entregue</Badge>
        </div>
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Status:</span>
          <Badge variant="warning" :dot="true" :pill="true">Processando</Badge>
        </div>
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Integração:</span>
          <Badge variant="success" icon="i-carbon-checkmark-filled" :pill="true">VTEX</Badge>
        </div>
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Ambiente:</span>
          <Badge variant="info" :pill="true">Production</Badge>
        </div>
      </div>
    `})},y={render:()=>({components:{Badge:a},template:`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">React</Badge>
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">Vue</Badge>
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">TypeScript</Badge>
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">JavaScript</Badge>
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">CSS</Badge>
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">HTML</Badge>
      </div>
    `})},b={render:()=>({components:{Badge:a},template:`
      <div style="display: flex; align-items: center; gap: 1.5rem;">
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Notificações</span>
          <Badge variant="danger" size="sm" :pill="true">12</Badge>
        </div>
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Mensagens</span>
          <Badge variant="primary" size="sm" :pill="true">5</Badge>
        </div>
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Tarefas</span>
          <Badge variant="warning" size="sm" :pill="true">99+</Badge>
        </div>
      </div>
    `})};t.parameters={...t.parameters,docs:{...t.parameters?.docs,source:{originalSource:`{
  args: {
    default: 'Badge'
  },
  render: args => ({
    components: {
      Badge
    },
    setup() {
      return {
        args
      };
    },
    template: '<Badge v-bind="args">{{ args.default }}</Badge>'
  })
}`,...t.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'primary',
    default: 'Primary'
  },
  render: args => ({
    components: {
      Badge
    },
    setup() {
      return {
        args
      };
    },
    template: '<Badge v-bind="args">{{ args.default }}</Badge>'
  })
}`,...i.parameters?.docs?.source}}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'success',
    default: 'Success'
  },
  render: args => ({
    components: {
      Badge
    },
    setup() {
      return {
        args
      };
    },
    template: '<Badge v-bind="args">{{ args.default }}</Badge>'
  })
}`,...d.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'warning',
    default: 'Warning'
  },
  render: args => ({
    components: {
      Badge
    },
    setup() {
      return {
        args
      };
    },
    template: '<Badge v-bind="args">{{ args.default }}</Badge>'
  })
}`,...s.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'danger',
    default: 'Danger'
  },
  render: args => ({
    components: {
      Badge
    },
    setup() {
      return {
        args
      };
    },
    template: '<Badge v-bind="args">{{ args.default }}</Badge>'
  })
}`,...l.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'info',
    default: 'Info'
  },
  render: args => ({
    components: {
      Badge
    },
    setup() {
      return {
        args
      };
    },
    template: '<Badge v-bind="args">{{ args.default }}</Badge>'
  })
}`,...o.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Badge
    },
    template: \`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="default">Default</Badge>
        <Badge variant="primary">Primary</Badge>
        <Badge variant="success">Success</Badge>
        <Badge variant="warning">Warning</Badge>
        <Badge variant="danger">Danger</Badge>
        <Badge variant="info">Info</Badge>
      </div>
    \`
  })
}`,...c.parameters?.docs?.source}}};g.parameters={...g.parameters,docs:{...g.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Badge
    },
    template: \`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="default" :outline="true">Default</Badge>
        <Badge variant="primary" :outline="true">Primary</Badge>
        <Badge variant="success" :outline="true">Success</Badge>
        <Badge variant="warning" :outline="true">Warning</Badge>
        <Badge variant="danger" :outline="true">Danger</Badge>
        <Badge variant="info" :outline="true">Info</Badge>
      </div>
    \`
  })
}`,...g.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Badge
    },
    template: \`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="primary" :pill="true">Primary Pill</Badge>
        <Badge variant="success" :pill="true">Success Pill</Badge>
        <Badge variant="warning" :pill="true">Warning Pill</Badge>
        <Badge variant="danger" :pill="true">Danger Pill</Badge>
      </div>
    \`
  })
}`,...p.parameters?.docs?.source}}};u.parameters={...u.parameters,docs:{...u.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Badge
    },
    template: \`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="success" :dot="true">Online</Badge>
        <Badge variant="warning" :dot="true">Busy</Badge>
        <Badge variant="danger" :dot="true">Offline</Badge>
        <Badge variant="default" :dot="true">Away</Badge>
      </div>
    \`
  })
}`,...u.parameters?.docs?.source}}};m.parameters={...m.parameters,docs:{...m.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Badge
    },
    template: \`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="primary" icon="i-carbon-checkmark-filled">Verified</Badge>
        <Badge variant="success" icon="i-carbon-checkmark">Completed</Badge>
        <Badge variant="warning" icon="i-carbon-warning">Warning</Badge>
        <Badge variant="danger" icon="i-carbon-close">Error</Badge>
        <Badge variant="info" icon="i-carbon-information">Info</Badge>
      </div>
    \`
  })
}`,...m.parameters?.docs?.source}}};B.parameters={...B.parameters,docs:{...B.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Badge
    },
    template: \`
      <div style="display: flex; align-items: center; gap: 1rem;">
        <Badge variant="primary" size="sm">Small</Badge>
        <Badge variant="primary" size="md">Medium</Badge>
        <Badge variant="primary" size="lg">Large</Badge>
      </div>
    \`
  })
}`,...B.parameters?.docs?.source}}};v.parameters={...v.parameters,docs:{...v.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Badge
    },
    setup() {
      const handleClick = () => {
        alert('Badge clicked!');
      };
      return {
        handleClick
      };
    },
    template: \`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="primary" :clickable="true" @click="handleClick">Click me</Badge>
        <Badge variant="success" :clickable="true" @click="handleClick">Clickable</Badge>
        <Badge variant="default" :clickable="true" :outline="true" @click="handleClick">Remove</Badge>
      </div>
    \`
  })
}`,...v.parameters?.docs?.source}}};f.parameters={...f.parameters,docs:{...f.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Badge
    },
    template: \`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Pedido:</span>
          <Badge variant="success" :dot="true" :pill="true">Entregue</Badge>
        </div>
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Status:</span>
          <Badge variant="warning" :dot="true" :pill="true">Processando</Badge>
        </div>
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Integração:</span>
          <Badge variant="success" icon="i-carbon-checkmark-filled" :pill="true">VTEX</Badge>
        </div>
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Ambiente:</span>
          <Badge variant="info" :pill="true">Production</Badge>
        </div>
      </div>
    \`
  })
}`,...f.parameters?.docs?.source}}};y.parameters={...y.parameters,docs:{...y.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Badge
    },
    template: \`
      <div style="display: flex; flex-wrap: wrap; gap: 0.5rem;">
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">React</Badge>
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">Vue</Badge>
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">TypeScript</Badge>
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">JavaScript</Badge>
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">CSS</Badge>
        <Badge variant="default" :outline="true" :pill="true" :clickable="true">HTML</Badge>
      </div>
    \`
  })
}`,...y.parameters?.docs?.source}}};b.parameters={...b.parameters,docs:{...b.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Badge
    },
    template: \`
      <div style="display: flex; align-items: center; gap: 1.5rem;">
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Notificações</span>
          <Badge variant="danger" size="sm" :pill="true">12</Badge>
        </div>
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Mensagens</span>
          <Badge variant="primary" size="sm" :pill="true">5</Badge>
        </div>
        <div style="display: flex; align-items: center; gap: 0.5rem;">
          <span>Tarefas</span>
          <Badge variant="warning" size="sm" :pill="true">99+</Badge>
        </div>
      </div>
    \`
  })
}`,...b.parameters?.docs?.source}}};const J=["Default","Primary","Success","Warning","Danger","Info","AllVariants","Outline","Pill","WithDot","WithIcon","Sizes","Clickable","StatusBadges","TagsExample","CountBadges"];export{c as AllVariants,v as Clickable,b as CountBadges,l as Danger,t as Default,o as Info,g as Outline,p as Pill,i as Primary,B as Sizes,f as StatusBadges,d as Success,y as TagsExample,s as Warning,u as WithDot,m as WithIcon,J as __namedExportsOrder,H as default};
