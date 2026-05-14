import{d,c as s,b,F as p,e as _,o as a,a as o,f as i,n as m,t as c}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as f}from"./_plugin-vue_export-helper-DlAUqK2U.js";const g={class:"nw-breadcrumb"},y={class:"nw-breadcrumb__list"},h=["href"],w={key:1,class:"nw-breadcrumb__text"},B={key:2,class:"nw-breadcrumb__separator"},u=d({__name:"Breadcrumb",props:{items:{},separator:{default:"/"}},setup(t){return(x,D)=>(a(),s("nav",g,[b("ol",y,[(a(!0),s(p,null,_(t.items,(e,l)=>(a(),s("li",{key:l,class:"nw-breadcrumb__item"},[e.to?(a(),s("a",{key:0,href:e.to,class:"nw-breadcrumb__link"},[e.icon?(a(),s("span",{key:0,class:m(["nw-breadcrumb__icon",e.icon])},null,2)):o("",!0),i(" "+c(e.label),1)],8,h)):(a(),s("span",w,[e.icon?(a(),s("span",{key:0,class:m(["nw-breadcrumb__icon",e.icon])},null,2)):o("",!0),i(" "+c(e.label),1)])),l<t.items.length-1?(a(),s("span",B,c(t.separator),1)):o("",!0)]))),128))])]))}}),k=f(u,[["__scopeId","data-v-3f73308e"]]);u.__docgenInfo={exportName:"default",displayName:"Breadcrumb",description:"",tags:{},props:[{name:"items",required:!0,type:{name:"Array",elements:[{name:"BreadcrumbItem"}]}},{name:"separator",required:!1,type:{name:"string"},defaultValue:{func:!1,value:"'/'"}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/molecules/Breadcrumb/Breadcrumb.vue"]};const N={title:"Design System/Molecules/Breadcrumb",component:k,tags:["autodocs"]},r={args:{items:[{label:"Home",to:"/"},{label:"Products",to:"/products"},{label:"Details"}]}},n={args:{items:[{label:"Home",to:"/",icon:"i-carbon-home"},{label:"Settings",to:"/settings",icon:"i-carbon-settings"},{label:"Profile",icon:"i-carbon-user"}]}};r.parameters={...r.parameters,docs:{...r.parameters?.docs,source:{originalSource:`{
  args: {
    items: [{
      label: 'Home',
      to: '/'
    }, {
      label: 'Products',
      to: '/products'
    }, {
      label: 'Details'
    }]
  }
}`,...r.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  args: {
    items: [{
      label: 'Home',
      to: '/',
      icon: 'i-carbon-home'
    }, {
      label: 'Settings',
      to: '/settings',
      icon: 'i-carbon-settings'
    }, {
      label: 'Profile',
      icon: 'i-carbon-user'
    }]
  }
}`,...n.parameters?.docs?.source}}};const v=["Default","WithIcons"];export{r as Default,n as WithIcons,v as __namedExportsOrder,N as default};
