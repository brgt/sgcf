import{d as k,c as a,b as r,a as d,t as u,n as f,F as x,o as t,g}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as y}from"./_plugin-vue_export-helper-DlAUqK2U.js";const C=["checked","disabled","indeterminate"],v={class:"nw-checkbox__box"},_={key:0,class:"i-carbon-subtract-alt nw-checkbox__icon"},w={key:1,class:"i-carbon-checkmark nw-checkbox__icon"},z={key:0,class:"nw-checkbox__label"},V={key:0,class:"nw-checkbox__error"},b=k({__name:"Checkbox",props:{modelValue:{type:Boolean},label:{},disabled:{type:Boolean,default:!1},indeterminate:{type:Boolean,default:!1},size:{default:"md"},error:{}},emits:["update:modelValue","change"],setup(e,{emit:h}){const m=h,p=i=>{const c=i.target;m("update:modelValue",c.checked),m("change",c.checked)};return(i,c)=>(t(),a(x,null,[r("label",{class:f(["nw-checkbox",`nw-checkbox--${e.size}`,{"nw-checkbox--disabled":e.disabled,"nw-checkbox--error":e.error}])},[r("input",{type:"checkbox",checked:e.modelValue,disabled:e.disabled,indeterminate:e.indeterminate,class:"nw-checkbox__input",onChange:p},null,40,C),r("span",v,[e.indeterminate?(t(),a("span",_)):e.modelValue?(t(),a("span",w)):d("",!0)]),e.label?(t(),a("span",z,u(e.label),1)):d("",!0)],2),e.error?(t(),a("span",V,u(e.error),1)):d("",!0)],64))}}),s=y(b,[["__scopeId","data-v-972b7e1c"]]);b.__docgenInfo={exportName:"default",displayName:"Checkbox",description:"",tags:{},props:[{name:"modelValue",required:!1,type:{name:"boolean"}},{name:"label",required:!1,type:{name:"string"}},{name:"disabled",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"indeterminate",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"size",required:!1,type:{name:"union",elements:[{name:'"sm"'},{name:'"md"'},{name:'"lg"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"error",required:!1,type:{name:"string"}}],events:[{name:"update:modelValue",type:{names:["boolean"]}},{name:"change",type:{names:["boolean"]}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/atoms/Checkbox/Checkbox.vue"]};const B={title:"Design System/Atoms/Checkbox",component:s,tags:["autodocs"]},n={render:()=>({components:{Checkbox:s},setup(){return{checked:g(!1)}},template:'<Checkbox v-model="checked" label="Accept terms" />'})},o={render:()=>({components:{Checkbox:s},template:`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <Checkbox :model-value="true" label="Small" size="sm" />
        <Checkbox :model-value="true" label="Medium" size="md" />
        <Checkbox :model-value="true" label="Large" size="lg" />
      </div>
    `})},l={render:()=>({components:{Checkbox:s},template:'<Checkbox :model-value="false" :indeterminate="true" label="Indeterminate" />'})};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Checkbox
    },
    setup() {
      const checked = ref(false);
      return {
        checked
      };
    },
    template: '<Checkbox v-model="checked" label="Accept terms" />'
  })
}`,...n.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Checkbox
    },
    template: \`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <Checkbox :model-value="true" label="Small" size="sm" />
        <Checkbox :model-value="true" label="Medium" size="md" />
        <Checkbox :model-value="true" label="Large" size="lg" />
      </div>
    \`
  })
}`,...o.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Checkbox
    },
    template: '<Checkbox :model-value="false" :indeterminate="true" label="Indeterminate" />'
  })
}`,...l.parameters?.docs?.source}}};const I=["Default","Sizes","Indeterminate"];export{n as Default,l as Indeterminate,o as Sizes,I as __namedExportsOrder,B as default};
