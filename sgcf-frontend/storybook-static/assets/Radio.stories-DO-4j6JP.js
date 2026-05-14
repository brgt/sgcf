import{d as v,c as a,b as t,a as s,t as i,n as b,F as f,o as n,g}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as y}from"./_plugin-vue_export-helper-DlAUqK2U.js";const R=["name","value","checked","disabled"],h={class:"nw-radio__circle"},x={key:0,class:"nw-radio__dot"},z={key:0,class:"nw-radio__label"},w={key:0,class:"nw-radio__error"},u=v({__name:"Radio",props:{modelValue:{type:[String,Number,Boolean]},value:{type:[String,Number,Boolean]},label:{},name:{},disabled:{type:Boolean,default:!1},size:{default:"md"},error:{}},emits:["update:modelValue","change"],setup(e,{emit:c}){const m=e,r=c,p=()=>{r("update:modelValue",m.value),r("change",m.value)};return(V,S)=>(n(),a(f,null,[t("label",{class:b(["nw-radio",`nw-radio--${e.size}`,{"nw-radio--disabled":e.disabled,"nw-radio--error":e.error}])},[t("input",{type:"radio",name:e.name,value:e.value,checked:e.modelValue===e.value,disabled:e.disabled,class:"nw-radio__input",onChange:p},null,40,R),t("span",h,[e.modelValue===e.value?(n(),a("span",x)):s("",!0)]),e.label?(n(),a("span",z,i(e.label),1)):s("",!0)],2),e.error?(n(),a("span",w,i(e.error),1)):s("",!0)],64))}}),d=y(u,[["__scopeId","data-v-56ec2884"]]);u.__docgenInfo={exportName:"default",displayName:"Radio",description:"",tags:{},props:[{name:"modelValue",required:!1,type:{name:"union",elements:[{name:"string"},{name:"number"},{name:"boolean"}]}},{name:"value",required:!0,type:{name:"union",elements:[{name:"string"},{name:"number"},{name:"boolean"}]}},{name:"label",required:!1,type:{name:"string"}},{name:"name",required:!1,type:{name:"string"}},{name:"disabled",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"size",required:!1,type:{name:"union",elements:[{name:'"sm"'},{name:'"md"'},{name:'"lg"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"error",required:!1,type:{name:"string"}}],events:[{name:"update:modelValue",type:{names:["union"],elements:[{name:"string"},{name:"number"},{name:"boolean"}]}},{name:"change",type:{names:["union"],elements:[{name:"string"},{name:"number"},{name:"boolean"}]}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/atoms/Radio/Radio.vue"]};const O={title:"Design System/Atoms/Radio",component:d,tags:["autodocs"]},l={render:()=>({components:{Radio:d},setup(){return{selected:g("option1")}},template:`
      <div style="display: flex; flex-direction: column; gap: 0.5rem;">
        <Radio v-model="selected" value="option1" label="Option 1" name="demo" />
        <Radio v-model="selected" value="option2" label="Option 2" name="demo" />
        <Radio v-model="selected" value="option3" label="Option 3" name="demo" />
      </div>
    `})},o={render:()=>({components:{Radio:d},template:`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <Radio :model-value="true" :value="true" label="Small" size="sm" />
        <Radio :model-value="true" :value="true" label="Medium" size="md" />
        <Radio :model-value="true" :value="true" label="Large" size="lg" />
      </div>
    `})};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Radio
    },
    setup() {
      const selected = ref('option1');
      return {
        selected
      };
    },
    template: \`
      <div style="display: flex; flex-direction: column; gap: 0.5rem;">
        <Radio v-model="selected" value="option1" label="Option 1" name="demo" />
        <Radio v-model="selected" value="option2" label="Option 2" name="demo" />
        <Radio v-model="selected" value="option3" label="Option 3" name="demo" />
      </div>
    \`
  })
}`,...l.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Radio
    },
    template: \`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <Radio :model-value="true" :value="true" label="Small" size="sm" />
        <Radio :model-value="true" :value="true" label="Medium" size="md" />
        <Radio :model-value="true" :value="true" label="Large" size="lg" />
      </div>
    \`
  })
}`,...o.parameters?.docs?.source}}};const _=["Default","Sizes"];export{l as Default,o as Sizes,_ as __namedExportsOrder,O as default};
