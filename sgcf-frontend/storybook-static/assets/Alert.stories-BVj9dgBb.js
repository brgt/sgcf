import{d as b,c,b as n,a as u,n as p,t as w,r as _,o as m}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as y}from"./_plugin-vue_export-helper-DlAUqK2U.js";const h={class:"nw-alert__icon"},S={class:"nw-alert__content"},D={key:0,class:"nw-alert__title"},k={class:"nw-alert__message"},g=b({__name:"Alert",props:{variant:{default:"info"},title:{},dismissible:{type:Boolean,default:!1},icon:{}},emits:["dismiss"],setup(e,{emit:d}){const f=d,v={info:"i-carbon-information",success:"i-carbon-checkmark-filled",warning:"i-carbon-warning-alt",error:"i-carbon-error-filled"};return(A,r)=>(m(),c("div",{class:p(["nw-alert",`nw-alert--${e.variant}`]),role:"alert"},[n("div",h,[n("span",{class:p(e.icon||v[e.variant])},null,2)]),n("div",S,[e.title?(m(),c("h4",D,w(e.title),1)):u("",!0),n("div",k,[_(A.$slots,"default",{},void 0,!0)])]),e.dismissible?(m(),c("button",{key:0,onClick:r[0]||(r[0]=E=>f("dismiss")),class:"nw-alert__close"},[...r[1]||(r[1]=[n("span",{class:"i-carbon-close"},null,-1)])])):u("",!0)],2))}}),s=y(g,[["__scopeId","data-v-fcc795b6"]]);g.__docgenInfo={exportName:"default",displayName:"Alert",description:"",tags:{},props:[{name:"variant",required:!1,type:{name:"union",elements:[{name:'"info"'},{name:'"success"'},{name:'"warning"'},{name:'"error"'}]},defaultValue:{func:!1,value:"'info'"}},{name:"title",required:!1,type:{name:"string"}},{name:"dismissible",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"icon",required:!1,type:{name:"string"}}],events:[{name:"dismiss"}],slots:[{name:"default"}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/molecules/Alert/Alert.vue"]};const x={title:"Design System/Molecules/Alert",component:s,tags:["autodocs"]},t={args:{variant:"info",title:"Information",dismissible:!1},render:e=>({components:{Alert:s},setup(){return{args:e}},template:'<Alert v-bind="args">This is an informational message.</Alert>'})},a={args:{variant:"success",title:"Success!"},render:e=>({components:{Alert:s},setup(){return{args:e}},template:'<Alert v-bind="args">Operation completed successfully.</Alert>'})},i={args:{variant:"warning",title:"Warning"},render:e=>({components:{Alert:s},setup(){return{args:e}},template:'<Alert v-bind="args">Please review your settings.</Alert>'})},o={args:{variant:"error",title:"Error"},render:e=>({components:{Alert:s},setup(){return{args:e}},template:'<Alert v-bind="args">Something went wrong.</Alert>'})},l={args:{variant:"info",dismissible:!0},render:e=>({components:{Alert:s},setup(){return{args:e,handleDismiss:()=>alert("Dismissed!")}},template:'<Alert v-bind="args" @dismiss="handleDismiss">This alert can be dismissed.</Alert>'})};t.parameters={...t.parameters,docs:{...t.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'info',
    title: 'Information',
    dismissible: false
  },
  render: args => ({
    components: {
      Alert
    },
    setup() {
      return {
        args
      };
    },
    template: \`<Alert v-bind="args">This is an informational message.</Alert>\`
  })
}`,...t.parameters?.docs?.source}}};a.parameters={...a.parameters,docs:{...a.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'success',
    title: 'Success!'
  },
  render: args => ({
    components: {
      Alert
    },
    setup() {
      return {
        args
      };
    },
    template: \`<Alert v-bind="args">Operation completed successfully.</Alert>\`
  })
}`,...a.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'warning',
    title: 'Warning'
  },
  render: args => ({
    components: {
      Alert
    },
    setup() {
      return {
        args
      };
    },
    template: \`<Alert v-bind="args">Please review your settings.</Alert>\`
  })
}`,...i.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'error',
    title: 'Error'
  },
  render: args => ({
    components: {
      Alert
    },
    setup() {
      return {
        args
      };
    },
    template: \`<Alert v-bind="args">Something went wrong.</Alert>\`
  })
}`,...o.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  args: {
    variant: 'info',
    dismissible: true
  },
  render: args => ({
    components: {
      Alert
    },
    setup() {
      const handleDismiss = () => alert('Dismissed!');
      return {
        args,
        handleDismiss
      };
    },
    template: \`<Alert v-bind="args" @dismiss="handleDismiss">This alert can be dismissed.</Alert>\`
  })
}`,...l.parameters?.docs?.source}}};const B=["Info","Success","Warning","Error","Dismissible"];export{l as Dismissible,o as Error,t as Info,a as Success,i as Warning,B as __namedExportsOrder,x as default};
