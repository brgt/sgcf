import{d as x,h as P,c as h,b as e,n as g,j as F,t as a,F as R,e as S,o as u}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as k}from"./_plugin-vue_export-helper-DlAUqK2U.js";const E={class:"phase-header"},C={class:"phase-icon"},D={class:"phase-health"},_={class:"health-percentage"},T={class:"phase-content"},I={class:"phase-metrics"},A={class:"metric-label"},q={class:"metric-value"},y=x({__name:"FlywheelPhase",props:{title:{},description:{},icon:{},health:{},metrics:{},variant:{default:"primary"}},setup(r){const f=r,b=P(()=>`phase-${f.variant}`);return(z,H)=>(u(),h("div",{class:g(["flywheel-phase",b.value])},[e("div",E,[e("div",C,[e("span",{class:g(r.icon),"aria-hidden":"true"},null,2)]),e("div",D,[e("div",{class:"health-circle",style:F({"--health":r.health})},[e("span",_,a(r.health)+"%",1)],4)])]),e("div",T,[e("h3",null,a(r.title),1),e("p",null,a(r.description),1)]),e("div",I,[(u(!0),h(R,null,S(r.metrics,(w,v)=>(u(),h("div",{key:v,class:"phase-metric"},[e("span",A,a(v),1),e("span",q,a(w),1)]))),128))])],2))}}),p=k(y,[["__scopeId","data-v-c167cd05"]]);y.__docgenInfo={exportName:"default",displayName:"FlywheelPhase",description:"",tags:{},props:[{name:"title",required:!0,type:{name:"string"}},{name:"description",required:!0,type:{name:"string"}},{name:"icon",required:!0,type:{name:"string"}},{name:"health",required:!0,type:{name:"number"}},{name:"metrics",required:!0,type:{name:"Record",elements:[{name:"string"},{name:"union",elements:[{name:"string"},{name:"number"}]}]}},{name:"variant",required:!1,type:{name:"union",elements:[{name:'"primary"'},{name:'"success"'},{name:'"info"'},{name:'"warning"'}]},defaultValue:{func:!1,value:"'primary'"}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/molecules/FlywheelPhase/FlywheelPhase.vue"]};const M={title:"Design System/Molecules/FlywheelPhase",component:p,tags:["autodocs"],argTypes:{variant:{control:"select",options:["primary","success","info","warning"]},health:{control:{type:"range",min:0,max:100,step:1}}}},t={args:{title:"Atrair",description:"Cotação Inteligente",icon:"i-carbon-chart-line-smooth",health:92,variant:"primary",metrics:{quotes:"1.240",conversionRate:"68.5%",avgResponseTime:"1.2s"}}},n={args:{title:"Converter",description:"Fechamento Estratégico",icon:"i-carbon-checkmark-filled",health:95,variant:"success",metrics:{closedDeals:"847",winRate:"72.3%",avgDealSize:"R$ 8.2K"}}},i={args:{title:"Engajar",description:"Relacionamento Contínuo",icon:"i-carbon-user-multiple",health:88,variant:"info",metrics:{activeClients:"3.421",engagement:"84.5%",avgInteractions:"12.3"}}},s={args:{title:"Expandir",description:"Cross-sell & Upsell",icon:"i-carbon-arrow-up-right",health:67,variant:"warning",metrics:{opportunities:"234",crossSellRate:"45.2%",upsellRevenue:"R$ 124K"}}},o={args:{title:"Reter",description:"Prevenção de Churn",icon:"i-carbon-warning-alt",health:42,variant:"warning",metrics:{atRisk:"89",churnRate:"8.7%",recoveryRate:"52.3%"}}},c={args:{title:"Encantar",description:"Advocacy & Indicações",icon:"i-carbon-favorite-filled",health:100,variant:"success",metrics:{nps:"87",referrals:"412",satisfaction:"96.8%"}}},l={args:{title:"Hover Me!",description:"Pass o mouse sobre este card",icon:"i-carbon-cursor-1",health:75,variant:"primary",metrics:{lift:"-8px",glow:"40px",gradient:"revealed"}},render:r=>({components:{FlywheelPhase:p},setup(){return{args:r}},template:`
      <div style="padding: 2rem; background: var(--color-background); border-radius: 8px;">
        <h3 style="margin-bottom: 1rem; color: var(--color-text-primary);">
          🎨 Hover Effect Demo
        </h3>
        <p style="margin-bottom: 2rem; color: var(--color-text-secondary); font-size: 0.875rem;">
          Ao passar o mouse, observe os efeitos:
        </p>
        <ul style="margin-bottom: 2rem; color: var(--color-text-secondary); font-size: 0.875rem; padding-left: 1.5rem;">
          <li>✨ Card sobe 8px (translateY)</li>
          <li>🎨 Gradiente overlay aparece suavemente</li>
          <li>💚 Borda acende com cor primary</li>
          <li>🌟 Shadow glow aparece</li>
          <li>🔄 Ícone e health circle ganham animação</li>
        </ul>
        <FlywheelPhase v-bind="args" />
      </div>
    `})},m={render:()=>({components:{FlywheelPhase:p},template:`
      <div style="
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
        gap: 1.5rem;
        padding: 2rem;
        background: var(--color-background);
        border-radius: 8px;
      ">
        <FlywheelPhase
          title="Primary"
          description="Default variant"
          icon="i-carbon-chart-line-smooth"
          :health="92"
          variant="primary"
          :metrics="{ metric1: '1.2K', metric2: '68%' }"
        />
        <FlywheelPhase
          title="Success"
          description="Success variant"
          icon="i-carbon-checkmark-filled"
          :health="95"
          variant="success"
          :metrics="{ metric1: '847', metric2: '72%' }"
        />
        <FlywheelPhase
          title="Info"
          description="Info variant"
          icon="i-carbon-information-filled"
          :health="88"
          variant="info"
          :metrics="{ metric1: '3.4K', metric2: '84%' }"
        />
        <FlywheelPhase
          title="Warning"
          description="Warning variant"
          icon="i-carbon-warning-alt"
          :health="67"
          variant="warning"
          :metrics="{ metric1: '234', metric2: '45%' }"
        />
      </div>
    `})},d={render:()=>({components:{FlywheelPhase:p},template:`
      <div style="padding: 2rem; background: var(--color-background); border-radius: 8px;">
        <h2 style="margin-bottom: 1rem; color: var(--color-text-primary); font-family: var(--font-family-display);">
          Transport Management System - Flywheel
        </h2>
        <p style="margin-bottom: 2rem; color: var(--color-text-secondary);">
          Exemplo real do módulo de Transporte do Nordware Platform
        </p>
        <div style="
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
          gap: 1.5rem;
        ">
          <FlywheelPhase
            title="Atrair"
            description="Cotação Inteligente"
            icon="i-carbon-chart-line-smooth"
            :health="92"
            variant="primary"
            :metrics="{
              quotes: '1.240',
              conversionRate: '68.5%',
              avgResponseTime: '1.2s'
            }"
          />
          <FlywheelPhase
            title="Converter"
            description="Fechamento Estratégico"
            icon="i-carbon-checkmark-filled"
            :health="87"
            variant="success"
            :metrics="{
              closedDeals: '847',
              winRate: '72.3%',
              avgDealSize: 'R$ 8.2K'
            }"
          />
          <FlywheelPhase
            title="Entregar"
            description="Execução Perfeita"
            icon="i-carbon-delivery-truck"
            :health="94"
            variant="info"
            :metrics="{
              onTimeDelivery: '96.2%',
              avgTransitTime: '2.4 days',
              activeShipments: '1.834'
            }"
          />
          <FlywheelPhase
            title="Encantar"
            description="Advocacy & Recorrência"
            icon="i-carbon-favorite-filled"
            :health="89"
            variant="success"
            :metrics="{
              nps: '87',
              repeatRate: '78.3%',
              referrals: '412'
            }"
          />
        </div>
      </div>
    `})};t.parameters={...t.parameters,docs:{...t.parameters?.docs,source:{originalSource:`{
  args: {
    title: 'Atrair',
    description: 'Cotação Inteligente',
    icon: 'i-carbon-chart-line-smooth',
    health: 92,
    variant: 'primary',
    metrics: {
      quotes: '1.240',
      conversionRate: '68.5%',
      avgResponseTime: '1.2s'
    }
  }
}`,...t.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  args: {
    title: 'Converter',
    description: 'Fechamento Estratégico',
    icon: 'i-carbon-checkmark-filled',
    health: 95,
    variant: 'success',
    metrics: {
      closedDeals: '847',
      winRate: '72.3%',
      avgDealSize: 'R$ 8.2K'
    }
  }
}`,...n.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  args: {
    title: 'Engajar',
    description: 'Relacionamento Contínuo',
    icon: 'i-carbon-user-multiple',
    health: 88,
    variant: 'info',
    metrics: {
      activeClients: '3.421',
      engagement: '84.5%',
      avgInteractions: '12.3'
    }
  }
}`,...i.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  args: {
    title: 'Expandir',
    description: 'Cross-sell & Upsell',
    icon: 'i-carbon-arrow-up-right',
    health: 67,
    variant: 'warning',
    metrics: {
      opportunities: '234',
      crossSellRate: '45.2%',
      upsellRevenue: 'R$ 124K'
    }
  }
}`,...s.parameters?.docs?.source}}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  args: {
    title: 'Reter',
    description: 'Prevenção de Churn',
    icon: 'i-carbon-warning-alt',
    health: 42,
    variant: 'warning',
    metrics: {
      atRisk: '89',
      churnRate: '8.7%',
      recoveryRate: '52.3%'
    }
  }
}`,...o.parameters?.docs?.source}}};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  args: {
    title: 'Encantar',
    description: 'Advocacy & Indicações',
    icon: 'i-carbon-favorite-filled',
    health: 100,
    variant: 'success',
    metrics: {
      nps: '87',
      referrals: '412',
      satisfaction: '96.8%'
    }
  }
}`,...c.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  args: {
    title: 'Hover Me!',
    description: 'Pass o mouse sobre este card',
    icon: 'i-carbon-cursor-1',
    health: 75,
    variant: 'primary',
    metrics: {
      lift: '-8px',
      glow: '40px',
      gradient: 'revealed'
    }
  },
  render: args => ({
    components: {
      FlywheelPhase
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <div style="padding: 2rem; background: var(--color-background); border-radius: 8px;">
        <h3 style="margin-bottom: 1rem; color: var(--color-text-primary);">
          🎨 Hover Effect Demo
        </h3>
        <p style="margin-bottom: 2rem; color: var(--color-text-secondary); font-size: 0.875rem;">
          Ao passar o mouse, observe os efeitos:
        </p>
        <ul style="margin-bottom: 2rem; color: var(--color-text-secondary); font-size: 0.875rem; padding-left: 1.5rem;">
          <li>✨ Card sobe 8px (translateY)</li>
          <li>🎨 Gradiente overlay aparece suavemente</li>
          <li>💚 Borda acende com cor primary</li>
          <li>🌟 Shadow glow aparece</li>
          <li>🔄 Ícone e health circle ganham animação</li>
        </ul>
        <FlywheelPhase v-bind="args" />
      </div>
    \`
  })
}`,...l.parameters?.docs?.source}}};m.parameters={...m.parameters,docs:{...m.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      FlywheelPhase
    },
    template: \`
      <div style="
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
        gap: 1.5rem;
        padding: 2rem;
        background: var(--color-background);
        border-radius: 8px;
      ">
        <FlywheelPhase
          title="Primary"
          description="Default variant"
          icon="i-carbon-chart-line-smooth"
          :health="92"
          variant="primary"
          :metrics="{ metric1: '1.2K', metric2: '68%' }"
        />
        <FlywheelPhase
          title="Success"
          description="Success variant"
          icon="i-carbon-checkmark-filled"
          :health="95"
          variant="success"
          :metrics="{ metric1: '847', metric2: '72%' }"
        />
        <FlywheelPhase
          title="Info"
          description="Info variant"
          icon="i-carbon-information-filled"
          :health="88"
          variant="info"
          :metrics="{ metric1: '3.4K', metric2: '84%' }"
        />
        <FlywheelPhase
          title="Warning"
          description="Warning variant"
          icon="i-carbon-warning-alt"
          :health="67"
          variant="warning"
          :metrics="{ metric1: '234', metric2: '45%' }"
        />
      </div>
    \`
  })
}`,...m.parameters?.docs?.source}}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      FlywheelPhase
    },
    template: \`
      <div style="padding: 2rem; background: var(--color-background); border-radius: 8px;">
        <h2 style="margin-bottom: 1rem; color: var(--color-text-primary); font-family: var(--font-family-display);">
          Transport Management System - Flywheel
        </h2>
        <p style="margin-bottom: 2rem; color: var(--color-text-secondary);">
          Exemplo real do módulo de Transporte do Nordware Platform
        </p>
        <div style="
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
          gap: 1.5rem;
        ">
          <FlywheelPhase
            title="Atrair"
            description="Cotação Inteligente"
            icon="i-carbon-chart-line-smooth"
            :health="92"
            variant="primary"
            :metrics="{
              quotes: '1.240',
              conversionRate: '68.5%',
              avgResponseTime: '1.2s'
            }"
          />
          <FlywheelPhase
            title="Converter"
            description="Fechamento Estratégico"
            icon="i-carbon-checkmark-filled"
            :health="87"
            variant="success"
            :metrics="{
              closedDeals: '847',
              winRate: '72.3%',
              avgDealSize: 'R$ 8.2K'
            }"
          />
          <FlywheelPhase
            title="Entregar"
            description="Execução Perfeita"
            icon="i-carbon-delivery-truck"
            :health="94"
            variant="info"
            :metrics="{
              onTimeDelivery: '96.2%',
              avgTransitTime: '2.4 days',
              activeShipments: '1.834'
            }"
          />
          <FlywheelPhase
            title="Encantar"
            description="Advocacy & Recorrência"
            icon="i-carbon-favorite-filled"
            :health="89"
            variant="success"
            :metrics="{
              nps: '87',
              repeatRate: '78.3%',
              referrals: '412'
            }"
          />
        </div>
      </div>
    \`
  })
}`,...d.parameters?.docs?.source}}};const $=["Default","SuccessVariant","InfoVariant","WarningVariant","LowHealth","PerfectHealth","HoverDemo","AllVariants","TMSFlywheel"];export{m as AllVariants,t as Default,l as HoverDemo,i as InfoVariant,o as LowHealth,c as PerfectHealth,n as SuccessVariant,d as TMSFlywheel,s as WarningVariant,$ as __namedExportsOrder,M as default};
