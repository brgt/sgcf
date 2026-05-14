import{d as p,c as s,b as v,F as _,e as y,o as a,n as l,a as T,f as k,t as g,g as m}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as f}from"./_plugin-vue_export-helper-DlAUqK2U.js";const h={class:"nw-tabs"},w={class:"nw-tabs__list"},V=["onClick"],b=p({__name:"Tabs",props:{items:{},modelValue:{}},emits:["update:modelValue"],setup(t,{emit:r}){const d=r,u=i=>{i.disabled||d("update:modelValue",i.key)};return(i,S)=>(a(),s("div",h,[v("div",w,[(a(!0),s(_,null,y(t.items,e=>(a(),s("button",{key:e.key,class:l(["nw-tabs__item",{"nw-tabs__item--active":t.modelValue===e.key,"nw-tabs__item--disabled":e.disabled}]),onClick:x=>u(e)},[e.icon?(a(),s("span",{key:0,class:l(["nw-tabs__icon",e.icon])},null,2)):T("",!0),k(" "+g(e.label),1)],10,V))),128))])]))}}),c=f(b,[["__scopeId","data-v-916620f0"]]);b.__docgenInfo={exportName:"default",displayName:"Tabs",description:"",tags:{},props:[{name:"items",required:!0,type:{name:"Array",elements:[{name:"TabItem"}]}},{name:"modelValue",required:!0,type:{name:"string"}}],events:[{name:"update:modelValue",type:{names:["string"]}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/molecules/Tabs/Tabs.vue"]};const N={title:"Design System/Molecules/Tabs",component:c,tags:["autodocs"]},o={render:()=>({components:{Tabs:c},setup(){return{activeTab:m("tab1"),items:[{key:"tab1",label:"Overview"},{key:"tab2",label:"Settings"},{key:"tab3",label:"Activity"}]}},template:'<Tabs v-model="activeTab" :items="items" />'})},n={render:()=>({components:{Tabs:c},setup(){return{activeTab:m("home"),items:[{key:"home",label:"Home",icon:"i-carbon-home"},{key:"users",label:"Users",icon:"i-carbon-user"},{key:"settings",label:"Settings",icon:"i-carbon-settings"}]}},template:'<Tabs v-model="activeTab" :items="items" />'})};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Tabs
    },
    setup() {
      const activeTab = ref('tab1');
      const items = [{
        key: 'tab1',
        label: 'Overview'
      }, {
        key: 'tab2',
        label: 'Settings'
      }, {
        key: 'tab3',
        label: 'Activity'
      }];
      return {
        activeTab,
        items
      };
    },
    template: \`<Tabs v-model="activeTab" :items="items" />\`
  })
}`,...o.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Tabs
    },
    setup() {
      const activeTab = ref('home');
      const items = [{
        key: 'home',
        label: 'Home',
        icon: 'i-carbon-home'
      }, {
        key: 'users',
        label: 'Users',
        icon: 'i-carbon-user'
      }, {
        key: 'settings',
        label: 'Settings',
        icon: 'i-carbon-settings'
      }];
      return {
        activeTab,
        items
      };
    },
    template: \`<Tabs v-model="activeTab" :items="items" />\`
  })
}`,...n.parameters?.docs?.source}}};const D=["Default","WithIcons"];export{o as Default,n as WithIcons,D as __namedExportsOrder,N as default};
