import{d as g,c,e as u,F as v,o as h,j as k,n as S}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as w}from"./_plugin-vue_export-helper-DlAUqK2U.js";const p=g({__name:"Skeleton",props:{width:{default:"100%"},height:{default:"1rem"},circle:{type:Boolean,default:!1},count:{default:1}},setup(t){return(f,x)=>(h(!0),c(v,null,u(t.count,m=>(h(),c("div",{key:m,class:S(["nw-skeleton",{"nw-skeleton--circle":t.circle}]),style:k({width:t.width,height:t.height})},null,6))),128))}}),e=w(p,[["__scopeId","data-v-d4578c39"]]);p.__docgenInfo={exportName:"default",displayName:"Skeleton",description:"",tags:{},props:[{name:"width",required:!1,type:{name:"string"},defaultValue:{func:!1,value:"'100%'"}},{name:"height",required:!1,type:{name:"string"},defaultValue:{func:!1,value:"'1rem'"}},{name:"circle",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"count",required:!1,type:{name:"number"},defaultValue:{func:!1,value:"1"}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/molecules/Skeleton/Skeleton.vue"]};const _={title:"Design System/Molecules/Skeleton",component:e,tags:["autodocs"],argTypes:{width:{control:"text",description:"Largura do skeleton"},height:{control:"text",description:"Altura do skeleton"},circle:{control:"boolean",description:"Modo circular (para avatars)"},count:{control:"number",description:"Número de skeletons a repetir"}},args:{width:"100%",height:"1rem",circle:!1,count:1}},r={args:{width:"200px",height:"1rem"}},n={args:{width:"48px",height:"48px",circle:!0}},s={render:()=>({components:{Skeleton:e},template:`
      <div class="space-y-2">
        <Skeleton width="100%" height="1rem" />
        <Skeleton width="90%" height="1rem" />
        <Skeleton width="80%" height="1rem" />
      </div>
    `})},a={args:{count:5}},i={render:()=>({components:{Skeleton:e},template:`
      <div class="p-4 bg-surface rounded-lg border border-gray-200 space-y-4">
        <div class="flex items-center gap-3">
          <Skeleton width="48px" height="48px" :circle="true" />
          <div class="flex-1 space-y-2">
            <Skeleton width="150px" height="1rem" />
            <Skeleton width="100px" height="0.875rem" />
          </div>
        </div>
        <Skeleton width="100%" height="4rem" />
        <div class="flex gap-2">
          <Skeleton width="80px" height="2rem" />
          <Skeleton width="80px" height="2rem" />
        </div>
      </div>
    `})},o={render:()=>({components:{Skeleton:e},template:`
      <div class="space-y-4">
        <div v-for="i in 3" :key="i" class="flex items-center gap-3">
          <Skeleton width="40px" height="40px" :circle="true" />
          <div class="flex-1 space-y-2">
            <Skeleton width="120px" height="1rem" />
            <Skeleton width="180px" height="0.75rem" />
          </div>
        </div>
      </div>
    `})},d={render:()=>({components:{Skeleton:e},template:`
      <div class="space-y-3">
        <!-- Header -->
        <div class="flex gap-4">
          <Skeleton width="25%" height="1rem" />
          <Skeleton width="25%" height="1rem" />
          <Skeleton width="25%" height="1rem" />
          <Skeleton width="25%" height="1rem" />
        </div>
        <!-- Rows -->
        <div v-for="i in 5" :key="i" class="flex gap-4">
          <Skeleton width="25%" height="2.5rem" />
          <Skeleton width="25%" height="2.5rem" />
          <Skeleton width="25%" height="2.5rem" />
          <Skeleton width="25%" height="2.5rem" />
        </div>
      </div>
    `})},l={render:()=>({components:{Skeleton:e},template:`
      <div class="space-y-6">
        <!-- Stats Cards -->
        <div class="grid grid-cols-4 gap-4">
          <div v-for="i in 4" :key="i" class="p-4 bg-surface rounded-lg border border-gray-200">
            <Skeleton width="100px" height="0.875rem" />
            <Skeleton width="80px" height="2rem" class="mt-2" />
          </div>
        </div>

        <!-- Chart -->
        <div class="p-6 bg-surface rounded-lg border border-gray-200">
          <Skeleton width="150px" height="1.25rem" />
          <Skeleton width="100%" height="16rem" class="mt-4" />
        </div>
      </div>
    `})};r.parameters={...r.parameters,docs:{...r.parameters?.docs,source:{originalSource:`{
  args: {
    width: '200px',
    height: '1rem'
  }
}`,...r.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  args: {
    width: '48px',
    height: '48px',
    circle: true
  }
}`,...n.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Skeleton
    },
    template: \`
      <div class="space-y-2">
        <Skeleton width="100%" height="1rem" />
        <Skeleton width="90%" height="1rem" />
        <Skeleton width="80%" height="1rem" />
      </div>
    \`
  })
}`,...s.parameters?.docs?.source}}};a.parameters={...a.parameters,docs:{...a.parameters?.docs,source:{originalSource:`{
  args: {
    count: 5
  }
}`,...a.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Skeleton
    },
    template: \`
      <div class="p-4 bg-surface rounded-lg border border-gray-200 space-y-4">
        <div class="flex items-center gap-3">
          <Skeleton width="48px" height="48px" :circle="true" />
          <div class="flex-1 space-y-2">
            <Skeleton width="150px" height="1rem" />
            <Skeleton width="100px" height="0.875rem" />
          </div>
        </div>
        <Skeleton width="100%" height="4rem" />
        <div class="flex gap-2">
          <Skeleton width="80px" height="2rem" />
          <Skeleton width="80px" height="2rem" />
        </div>
      </div>
    \`
  })
}`,...i.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Skeleton
    },
    template: \`
      <div class="space-y-4">
        <div v-for="i in 3" :key="i" class="flex items-center gap-3">
          <Skeleton width="40px" height="40px" :circle="true" />
          <div class="flex-1 space-y-2">
            <Skeleton width="120px" height="1rem" />
            <Skeleton width="180px" height="0.75rem" />
          </div>
        </div>
      </div>
    \`
  })
}`,...o.parameters?.docs?.source}}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Skeleton
    },
    template: \`
      <div class="space-y-3">
        <!-- Header -->
        <div class="flex gap-4">
          <Skeleton width="25%" height="1rem" />
          <Skeleton width="25%" height="1rem" />
          <Skeleton width="25%" height="1rem" />
          <Skeleton width="25%" height="1rem" />
        </div>
        <!-- Rows -->
        <div v-for="i in 5" :key="i" class="flex gap-4">
          <Skeleton width="25%" height="2.5rem" />
          <Skeleton width="25%" height="2.5rem" />
          <Skeleton width="25%" height="2.5rem" />
          <Skeleton width="25%" height="2.5rem" />
        </div>
      </div>
    \`
  })
}`,...d.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Skeleton
    },
    template: \`
      <div class="space-y-6">
        <!-- Stats Cards -->
        <div class="grid grid-cols-4 gap-4">
          <div v-for="i in 4" :key="i" class="p-4 bg-surface rounded-lg border border-gray-200">
            <Skeleton width="100px" height="0.875rem" />
            <Skeleton width="80px" height="2rem" class="mt-2" />
          </div>
        </div>

        <!-- Chart -->
        <div class="p-6 bg-surface rounded-lg border border-gray-200">
          <Skeleton width="150px" height="1.25rem" />
          <Skeleton width="100%" height="16rem" class="mt-4" />
        </div>
      </div>
    \`
  })
}`,...l.parameters?.docs?.source}}};const C=["Default","Avatar","Text","Paragraph","Card","UserList","Table","Dashboard"];export{n as Avatar,i as Card,l as Dashboard,r as Default,a as Paragraph,d as Table,s as Text,o as UserList,C as __namedExportsOrder,_ as default};
