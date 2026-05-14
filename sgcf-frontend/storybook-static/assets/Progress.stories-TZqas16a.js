import{d as g,c as l,b as c,a as f,j as v,n as u,u as m,t as h,o as d}from"./vue.esm-bundler-Sd8zNG93.js";import{_}from"./_plugin-vue_export-helper-DlAUqK2U.js";const b={class:"nw-progress"},y={key:0,class:"nw-progress__label"},i=g({__name:"Progress",props:{value:{},max:{default:100},size:{default:"md"},color:{default:"bg-primary"},showLabel:{type:Boolean,default:!1}},setup(e){const t=e,n=Math.min(100,Math.max(0,t.value/t.max*100)),p={sm:"h-1",md:"h-2",lg:"h-3"};return(L,z)=>(d(),l("div",b,[c("div",{class:u(["nw-progress__track",p[e.size]])},[c("div",{class:u(["nw-progress__bar",e.color]),style:v({width:`${m(n)}%`})},null,6)],2),e.showLabel?(d(),l("span",y,h(Math.round(m(n)))+"%",1)):f("",!0)]))}}),w=_(i,[["__scopeId","data-v-c42578ca"]]);i.__docgenInfo={exportName:"default",displayName:"Progress",description:"",tags:{},props:[{name:"value",required:!0,type:{name:"number"}},{name:"max",required:!1,type:{name:"number"},defaultValue:{func:!1,value:"100"}},{name:"size",required:!1,type:{name:"union",elements:[{name:'"sm"'},{name:'"md"'},{name:'"lg"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"color",required:!1,type:{name:"string"},defaultValue:{func:!1,value:"'bg-primary'"}},{name:"showLabel",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/atoms/Progress/Progress.vue"]};const P={title:"Design System/Atoms/Progress",component:w,tags:["autodocs"]},a={args:{value:60}},s={args:{value:75,showLabel:!0}},r={args:{value:40,size:"sm"}},o={args:{value:80,size:"lg",showLabel:!0}};a.parameters={...a.parameters,docs:{...a.parameters?.docs,source:{originalSource:`{
  args: {
    value: 60
  }
}`,...a.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  args: {
    value: 75,
    showLabel: true
  }
}`,...s.parameters?.docs?.source}}};r.parameters={...r.parameters,docs:{...r.parameters?.docs,source:{originalSource:`{
  args: {
    value: 40,
    size: 'sm'
  }
}`,...r.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  args: {
    value: 80,
    size: 'lg',
    showLabel: true
  }
}`,...o.parameters?.docs?.source}}};const V=["Default","WithLabel","Small","Large"];export{a as Default,o as Large,r as Small,s as WithLabel,V as __namedExportsOrder,P as default};
