import{d as w,h,c as a,a as r,b as q,f as V,t as n,j as C,n as T,o as t,g as p}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as B}from"./_plugin-vue_export-helper-DlAUqK2U.js";const k={class:"nw-textarea-wrapper"},D=["for"],z={key:0,class:"nw-textarea-required"},S=["id","value","placeholder","disabled","readonly","required","rows","maxlength"],E={key:1,class:"nw-textarea-footer"},I={key:0,class:"nw-textarea-helper"},N={key:1,class:"nw-textarea-error"},W={key:2,class:"nw-textarea-count"},f=w({__name:"Textarea",props:{modelValue:{},label:{},placeholder:{},helper:{},error:{},disabled:{type:Boolean},readonly:{type:Boolean},required:{type:Boolean},rows:{default:4},maxlength:{},showCount:{type:Boolean,default:!1},resize:{default:"vertical"}},emits:["update:modelValue","input","change"],setup(e,{emit:g}){const x=e,m=g,i=h(()=>`textarea-${Math.random().toString(36).substr(2,9)}`),y=h(()=>x.modelValue?.length||0),v=d=>{const l=d.target;m("update:modelValue",l.value),m("input",d)};return(d,l)=>(t(),a("div",k,[e.label?(t(),a("label",{key:0,for:i.value,class:"nw-textarea-label"},[V(n(e.label)+" ",1),e.required?(t(),a("span",z,"*")):r("",!0)],8,D)):r("",!0),q("textarea",{id:i.value,class:T(["nw-textarea",{"nw-textarea--error":e.error}]),value:e.modelValue,placeholder:e.placeholder,disabled:e.disabled,readonly:e.readonly,required:e.required,rows:e.rows,maxlength:e.maxlength,style:C({resize:e.resize}),onInput:v,onChange:l[0]||(l[0]=b=>m("change",b))},null,46,S),e.helper||e.error||e.showCount&&e.maxlength?(t(),a("div",E,[!e.error&&e.helper?(t(),a("span",I,n(e.helper),1)):r("",!0),e.error?(t(),a("span",N,n(e.error),1)):r("",!0),e.showCount&&e.maxlength?(t(),a("span",W,n(y.value)+"/"+n(e.maxlength),1)):r("",!0)])):r("",!0)]))}}),c=B(f,[["__scopeId","data-v-850fa365"]]);f.__docgenInfo={exportName:"default",displayName:"Textarea",description:"",tags:{},props:[{name:"modelValue",required:!1,type:{name:"string"}},{name:"label",required:!1,type:{name:"string"}},{name:"placeholder",required:!1,type:{name:"string"}},{name:"helper",required:!1,type:{name:"string"}},{name:"error",required:!1,type:{name:"string"}},{name:"disabled",required:!1,type:{name:"boolean"}},{name:"readonly",required:!1,type:{name:"boolean"}},{name:"required",required:!1,type:{name:"boolean"}},{name:"rows",required:!1,type:{name:"number"},defaultValue:{func:!1,value:"4"}},{name:"maxlength",required:!1,type:{name:"number"}},{name:"showCount",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"resize",required:!1,type:{name:"union",elements:[{name:'"none"'},{name:'"vertical"'},{name:'"horizontal"'},{name:'"both"'}]},defaultValue:{func:!1,value:"'vertical'"}}],events:[{name:"update:modelValue",type:{names:["string"]}},{name:"input",type:{names:["Event"]}},{name:"change",type:{names:["Event"]}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/atoms/Textarea/Textarea.vue"]};const A={title:"Design System/Atoms/Textarea",component:c,tags:["autodocs"]},o={render:()=>({components:{Textarea:c},setup(){return{value:p("")}},template:'<Textarea v-model="value" label="Mensagem" placeholder="Digite sua mensagem..." />'})},s={render:()=>({components:{Textarea:c},setup(){return{value:p("")}},template:'<Textarea v-model="value" label="Bio" :maxlength="200" :show-count="true" />'})},u={args:{label:"Descrição",error:"Campo obrigatório",modelValue:""}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Textarea
    },
    setup() {
      const value = ref('');
      return {
        value
      };
    },
    template: '<Textarea v-model="value" label="Mensagem" placeholder="Digite sua mensagem..." />'
  })
}`,...o.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Textarea
    },
    setup() {
      const value = ref('');
      return {
        value
      };
    },
    template: '<Textarea v-model="value" label="Bio" :maxlength="200" :show-count="true" />'
  })
}`,...s.parameters?.docs?.source}}};u.parameters={...u.parameters,docs:{...u.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Descrição',
    error: 'Campo obrigatório',
    modelValue: ''
  }
}`,...u.parameters?.docs?.source}}};const F=["Default","WithCounter","WithError"];export{o as Default,s as WithCounter,u as WithError,F as __namedExportsOrder,A as default};
