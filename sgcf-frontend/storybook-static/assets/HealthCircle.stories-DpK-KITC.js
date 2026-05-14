import{d as z,c as u,a as h,t as f,j as C,n as b,o as g}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as H}from"./_plugin-vue_export-helper-DlAUqK2U.js";const S={key:0,class:"health-value"},y=z({__name:"HealthCircle",props:{value:{},size:{default:"md"},color:{default:"var(--color-primary)"},showLabel:{type:Boolean,default:!0},label:{}},setup(e){const x={sm:"health-circle-sm",md:"health-circle-md",lg:"health-circle-lg",xl:"health-circle-xl"};return(L,k)=>(g(),u("div",{class:b(["health-circle",x[e.size]]),style:C({"--health":e.value,"--health-color":e.color})},[e.showLabel?(g(),u("span",S,f(e.label||`${e.value}%`),1)):h("",!0)],6))}}),v=H(y,[["__scopeId","data-v-6ad2b1dc"]]);y.__docgenInfo={exportName:"default",displayName:"HealthCircle",description:"",tags:{},props:[{name:"value",required:!0,type:{name:"number"}},{name:"size",required:!1,type:{name:"union",elements:[{name:'"sm"'},{name:'"md"'},{name:'"lg"'},{name:'"xl"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"color",required:!1,type:{name:"string"},defaultValue:{func:!1,value:"'var(--color-primary)'"}},{name:"showLabel",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"true"}},{name:"label",required:!1,type:{name:"string"}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/atoms/HealthCircle/HealthCircle.vue"]};const _={title:"Design System/Atoms/HealthCircle",component:v,tags:["autodocs"],argTypes:{value:{control:{type:"range",min:0,max:100,step:1}},size:{control:"select",options:["sm","md","lg","xl"]},showLabel:{control:"boolean"}}},r={args:{value:75,size:"md",showLabel:!0}},a={args:{value:85,size:"sm"}},o={args:{value:92,size:"lg"}},t={args:{value:95,size:"xl"}},l={args:{value:95,size:"lg",color:"var(--color-success)"}},s={args:{value:67,size:"lg",color:"var(--color-warning)"}},n={args:{value:42,size:"lg",color:"var(--color-error)"}},c={args:{value:88,size:"md",showLabel:!1}},i={args:{value:85,size:"lg",label:"A+"}},m={render:()=>({components:{HealthCircle:v},template:`
      <div style="
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(100px, 1fr));
        gap: 2rem;
        padding: 2rem;
        background: var(--color-background);
        border-radius: 8px;
        justify-items: center;
      ">
        <div style="text-align: center;">
          <HealthCircle :value="0" size="lg" color="var(--color-error)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Critical</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="25" size="lg" color="var(--color-error)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Poor</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="50" size="lg" color="var(--color-warning)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Fair</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="75" size="lg" color="var(--color-info)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Good</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="92" size="lg" color="var(--color-primary)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Excellent</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="100" size="lg" color="var(--color-success)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Perfect</p>
        </div>
      </div>
    `})},d={render:()=>({components:{HealthCircle:v},template:`
      <div style="
        display: flex;
        align-items: center;
        gap: 2rem;
        padding: 2rem;
        background: var(--color-background);
        border-radius: 8px;
      ">
        <div style="text-align: center;">
          <HealthCircle :value="85" size="sm" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.75rem;">Small</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="85" size="md" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.75rem;">Medium</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="85" size="lg" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.75rem;">Large</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="85" size="xl" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.75rem;">Extra Large</p>
        </div>
      </div>
    `})},p={render:()=>({components:{HealthCircle:v},template:`
      <div style="padding: 2rem; background: var(--color-background); border-radius: 8px;">
        <h3 style="margin-bottom: 1rem; color: var(--color-text-primary);">
          🎨 Interactive Health Circle
        </h3>
        <p style="margin-bottom: 2rem; color: var(--color-text-secondary); font-size: 0.875rem;">
          Pass o mouse para ver a animação de hover (rotate + scale)
        </p>
        <div style="display: flex; justify-content: center;">
          <HealthCircle :value="87" size="xl" />
        </div>
      </div>
    `})};r.parameters={...r.parameters,docs:{...r.parameters?.docs,source:{originalSource:`{
  args: {
    value: 75,
    size: 'md',
    showLabel: true
  }
}`,...r.parameters?.docs?.source}}};a.parameters={...a.parameters,docs:{...a.parameters?.docs,source:{originalSource:`{
  args: {
    value: 85,
    size: 'sm'
  }
}`,...a.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  args: {
    value: 92,
    size: 'lg'
  }
}`,...o.parameters?.docs?.source}}};t.parameters={...t.parameters,docs:{...t.parameters?.docs,source:{originalSource:`{
  args: {
    value: 95,
    size: 'xl'
  }
}`,...t.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  args: {
    value: 95,
    size: 'lg',
    color: 'var(--color-success)'
  }
}`,...l.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  args: {
    value: 67,
    size: 'lg',
    color: 'var(--color-warning)'
  }
}`,...s.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  args: {
    value: 42,
    size: 'lg',
    color: 'var(--color-error)'
  }
}`,...n.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  args: {
    value: 88,
    size: 'md',
    showLabel: false
  }
}`,...c.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  args: {
    value: 85,
    size: 'lg',
    label: 'A+'
  }
}`,...i.parameters?.docs?.source}}};m.parameters={...m.parameters,docs:{...m.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      HealthCircle
    },
    template: \`
      <div style="
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(100px, 1fr));
        gap: 2rem;
        padding: 2rem;
        background: var(--color-background);
        border-radius: 8px;
        justify-items: center;
      ">
        <div style="text-align: center;">
          <HealthCircle :value="0" size="lg" color="var(--color-error)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Critical</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="25" size="lg" color="var(--color-error)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Poor</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="50" size="lg" color="var(--color-warning)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Fair</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="75" size="lg" color="var(--color-info)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Good</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="92" size="lg" color="var(--color-primary)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Excellent</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="100" size="lg" color="var(--color-success)" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.875rem;">Perfect</p>
        </div>
      </div>
    \`
  })
}`,...m.parameters?.docs?.source}}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      HealthCircle
    },
    template: \`
      <div style="
        display: flex;
        align-items: center;
        gap: 2rem;
        padding: 2rem;
        background: var(--color-background);
        border-radius: 8px;
      ">
        <div style="text-align: center;">
          <HealthCircle :value="85" size="sm" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.75rem;">Small</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="85" size="md" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.75rem;">Medium</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="85" size="lg" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.75rem;">Large</p>
        </div>
        <div style="text-align: center;">
          <HealthCircle :value="85" size="xl" />
          <p style="margin-top: 0.5rem; color: var(--color-text-secondary); font-size: 0.75rem;">Extra Large</p>
        </div>
      </div>
    \`
  })
}`,...d.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      HealthCircle
    },
    template: \`
      <div style="padding: 2rem; background: var(--color-background); border-radius: 8px;">
        <h3 style="margin-bottom: 1rem; color: var(--color-text-primary);">
          🎨 Interactive Health Circle
        </h3>
        <p style="margin-bottom: 2rem; color: var(--color-text-secondary); font-size: 0.875rem;">
          Pass o mouse para ver a animação de hover (rotate + scale)
        </p>
        <div style="display: flex; justify-content: center;">
          <HealthCircle :value="87" size="xl" />
        </div>
      </div>
    \`
  })
}`,...p.parameters?.docs?.source}}};const j=["Default","Small","Large","ExtraLarge","SuccessColor","WarningColor","ErrorColor","WithoutLabel","CustomLabel","HealthStates","AllSizes","InteractiveDemo"];export{d as AllSizes,i as CustomLabel,r as Default,n as ErrorColor,t as ExtraLarge,m as HealthStates,p as InteractiveDemo,o as Large,a as Small,l as SuccessColor,s as WarningColor,c as WithoutLabel,j as __namedExportsOrder,_ as default};
