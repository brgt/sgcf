import{d as H,g as l,h as M,c as o,a as s,b as R,f as k,t as B,n as m,o as n}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as J}from"./_plugin-vue_export-helper-DlAUqK2U.js";const O=["for"],Q={key:0,class:"nw-input-required","aria-label":"required"},G={class:"nw-input-container"},K=["id","type","value","placeholder","disabled","readonly","required","autocomplete","aria-invalid","aria-describedby"],X=["id"],Y=["id"],F=H({__name:"Input",props:{modelValue:{},type:{default:"text"},label:{},placeholder:{},helper:{},error:{},disabled:{type:Boolean,default:!1},readonly:{type:Boolean,default:!1},required:{type:Boolean,default:!1},size:{default:"md"},iconLeft:{},iconRight:{},fullWidth:{type:Boolean,default:!1},autocomplete:{},id:{}},emits:["update:modelValue","focus","blur","input","change"],setup(e,{expose:i,emit:d}){const r=e,u=d,C=l(null),t=M(()=>r.id||`input-${Math.random().toString(36).substr(2,9)}`),U=M(()=>{const a=["nw-input"];return a.push(`nw-input--${r.size}`),r.error&&a.push("nw-input--error"),r.disabled&&a.push("nw-input--disabled"),r.readonly&&a.push("nw-input--readonly"),r.iconLeft&&a.push("nw-input--has-icon-left"),r.iconRight&&a.push("nw-input--has-icon-right"),a}),A=a=>{const p=a.target;u("update:modelValue",p.value),u("input",a)},$=a=>{u("change",a)},P=a=>{u("focus",a)},j=a=>{u("blur",a)};return i({focus:()=>C.value?.focus(),blur:()=>C.value?.blur()}),(a,p)=>(n(),o("div",{class:m(["nw-input-wrapper",{"nw-input-wrapper--full-width":e.fullWidth}])},[e.label?(n(),o("label",{key:0,for:t.value,class:"nw-input-label"},[k(B(e.label)+" ",1),e.required?(n(),o("span",Q,"*")):s("",!0)],8,O)):s("",!0),R("div",G,[e.iconLeft?(n(),o("span",{key:0,class:m(["nw-input-icon nw-input-icon--left",e.iconLeft]),"aria-hidden":"true"},null,2)):s("",!0),R("input",{id:t.value,ref_key:"inputRef",ref:C,class:m(U.value),type:e.type,value:e.modelValue,placeholder:e.placeholder,disabled:e.disabled,readonly:e.readonly,required:e.required,autocomplete:e.autocomplete,"aria-invalid":!!e.error,"aria-describedby":e.error?`${t.value}-error`:e.helper?`${t.value}-helper`:void 0,onInput:A,onChange:$,onFocus:P,onBlur:j},null,42,K),e.iconRight?(n(),o("span",{key:1,class:m(["nw-input-icon nw-input-icon--right",e.iconRight]),"aria-hidden":"true"},null,2)):s("",!0)]),e.helper&&!e.error?(n(),o("span",{key:1,id:`${t.value}-helper`,class:"nw-input-helper"},B(e.helper),9,X)):s("",!0),e.error?(n(),o("span",{key:2,id:`${t.value}-error`,class:"nw-input-error",role:"alert"},[p[0]||(p[0]=R("span",{class:"i-carbon-warning-alt nw-input-error-icon"},null,-1)),k(" "+B(e.error),1)],8,Y)):s("",!0)],2))}}),c=J(F,[["__scopeId","data-v-bedb472c"]]);F.__docgenInfo={exportName:"default",displayName:"Input",description:"",tags:{},expose:[{name:"focus"},{name:"blur"}],props:[{name:"modelValue",description:"Valor do input (v-model)",required:!1,type:{name:"union",elements:[{name:"string"},{name:"number"}]}},{name:"type",description:"Tipo do input HTML",required:!1,type:{name:"union",elements:[{name:'"text"'},{name:'"email"'},{name:'"password"'},{name:'"number"'},{name:'"tel"'},{name:'"url"'},{name:'"search"'}]},defaultValue:{func:!1,value:"'text'"}},{name:"label",description:"Label do input",required:!1,type:{name:"string"}},{name:"placeholder",description:"Placeholder",required:!1,type:{name:"string"}},{name:"helper",description:"Texto de ajuda",required:!1,type:{name:"string"}},{name:"error",description:"Mensagem de erro",required:!1,type:{name:"string"}},{name:"disabled",description:"Input desabilitado",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"readonly",description:"Input readonly",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"required",description:"Input required",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"size",description:"Tamanho do input",required:!1,type:{name:"union",elements:[{name:'"sm"'},{name:'"md"'},{name:'"lg"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"iconLeft",description:"Ícone à esquerda (classe UnoCSS)",required:!1,type:{name:"string"}},{name:"iconRight",description:"Ícone à direita (classe UnoCSS)",required:!1,type:{name:"string"}},{name:"fullWidth",description:"Largura total",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"autocomplete",description:"Autocomplete",required:!1,type:{name:"string"}},{name:"id",description:"Input ID",required:!1,type:{name:"string"}}],events:[{name:"update:modelValue",type:{names:["union"],elements:[{name:"string"},{name:"number"}]}},{name:"focus",type:{names:["FocusEvent"]}},{name:"blur",type:{names:["FocusEvent"]}},{name:"input",type:{names:["Event"]}},{name:"change",type:{names:["Event"]}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/atoms/Input/Input.vue"]};const ee={title:"Design System/Atoms/Input",component:c,tags:["autodocs"],argTypes:{type:{control:"select",options:["text","email","password","number","tel","url","search"],description:"Tipo do input HTML"},size:{control:"select",options:["sm","md","lg"],description:"Tamanho do input"},disabled:{control:"boolean",description:"Estado desabilitado"},readonly:{control:"boolean",description:"Estado readonly"},required:{control:"boolean",description:"Campo obrigatório"},fullWidth:{control:"boolean",description:"Largura total"}},args:{type:"text",size:"md",disabled:!1,readonly:!1,required:!1,fullWidth:!1}},h={args:{label:"Nome",placeholder:"Digite seu nome"}},f={render:()=>({components:{Input:c},setup(){return{value:l("John Doe")}},template:'<Input v-model="value" label="Nome" />'})},b={args:{label:"E-mail",placeholder:"voce@exemplo.com",helper:"Usaremos este e-mail para entrar em contato",type:"email"}},g={args:{label:"E-mail",placeholder:"voce@exemplo.com",type:"email",error:"E-mail inválido",modelValue:"invalido"}},y={args:{label:"Nome completo",placeholder:"Digite seu nome completo",required:!0}},v={args:{label:"Buscar",placeholder:"Buscar produtos...",iconLeft:"i-carbon-search",type:"search"}},I={args:{label:"Website",placeholder:"https://exemplo.com",iconRight:"i-carbon-launch",type:"url"}},S={args:{label:"Senha",placeholder:"••••••••",type:"password"}},V={args:{label:"E-mail",placeholder:"voce@exemplo.com",type:"email",iconLeft:"i-carbon-email"}},x={args:{label:"Telefone",placeholder:"(11) 99999-9999",type:"tel",iconLeft:"i-carbon-phone"}},w={args:{label:"Quantidade",placeholder:"0",type:"number",modelValue:1}},q={args:{label:"Campo desabilitado",placeholder:"Não é editável",disabled:!0,modelValue:"Valor fixo"}},E={args:{label:"Campo somente leitura",readonly:!0,modelValue:"Este valor não pode ser editado"}},L={args:{label:"Input pequeno",placeholder:"Tamanho pequeno",size:"sm"}},D={args:{label:"Input grande",placeholder:"Tamanho grande",size:"lg"}},z={args:{label:"Input full width",placeholder:"Ocupa 100% da largura",fullWidth:!0}},W={render:()=>({components:{Input:c},template:`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <Input label="Small" placeholder="Small input" size="sm" />
        <Input label="Medium (default)" placeholder="Medium input" size="md" />
        <Input label="Large" placeholder="Large input" size="lg" />
      </div>
    `})},N={render:()=>({components:{Input:c},setup(){const e=l(""),i=l("invalido"),d=l("Desabilitado"),r=l("Somente leitura");return{defaultValue:e,errorValue:i,disabledValue:d,readonlyValue:r}},template:`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <Input
          v-model="defaultValue"
          label="Normal"
          placeholder="Digite algo..."
          helper="Texto de ajuda"
        />
        <Input
          v-model="errorValue"
          label="Com erro"
          placeholder="Digite algo..."
          error="Campo obrigatório"
        />
        <Input
          v-model="disabledValue"
          label="Desabilitado"
          :disabled="true"
        />
        <Input
          v-model="readonlyValue"
          label="Somente leitura"
          :readonly="true"
        />
      </div>
    `})},T={render:()=>({components:{Input:c},setup(){const e=l(""),i=l(""),d=l(""),r=l("");return{name:e,email:i,phone:d,password:r}},template:`
      <form style="display: flex; flex-direction: column; gap: 1rem; max-width: 400px;">
        <Input
          v-model="name"
          label="Nome completo"
          placeholder="John Doe"
          required
        />
        <Input
          v-model="email"
          label="E-mail"
          type="email"
          placeholder="voce@exemplo.com"
          icon-left="i-carbon-email"
          helper="Usaremos para login"
          required
        />
        <Input
          v-model="phone"
          label="Telefone"
          type="tel"
          placeholder="(11) 99999-9999"
          icon-left="i-carbon-phone"
        />
        <Input
          v-model="password"
          label="Senha"
          type="password"
          placeholder="••••••••"
          helper="Mínimo 8 caracteres"
          required
        />
      </form>
    `})};h.parameters={...h.parameters,docs:{...h.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Nome',
    placeholder: 'Digite seu nome'
  }
}`,...h.parameters?.docs?.source}}};f.parameters={...f.parameters,docs:{...f.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Input
    },
    setup() {
      const value = ref('John Doe');
      return {
        value
      };
    },
    template: '<Input v-model="value" label="Nome" />'
  })
}`,...f.parameters?.docs?.source}}};b.parameters={...b.parameters,docs:{...b.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'E-mail',
    placeholder: 'voce@exemplo.com',
    helper: 'Usaremos este e-mail para entrar em contato',
    type: 'email'
  }
}`,...b.parameters?.docs?.source}}};g.parameters={...g.parameters,docs:{...g.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'E-mail',
    placeholder: 'voce@exemplo.com',
    type: 'email',
    error: 'E-mail inválido',
    modelValue: 'invalido'
  }
}`,...g.parameters?.docs?.source}}};y.parameters={...y.parameters,docs:{...y.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Nome completo',
    placeholder: 'Digite seu nome completo',
    required: true
  }
}`,...y.parameters?.docs?.source}}};v.parameters={...v.parameters,docs:{...v.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Buscar',
    placeholder: 'Buscar produtos...',
    iconLeft: 'i-carbon-search',
    type: 'search'
  }
}`,...v.parameters?.docs?.source}}};I.parameters={...I.parameters,docs:{...I.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Website',
    placeholder: 'https://exemplo.com',
    iconRight: 'i-carbon-launch',
    type: 'url'
  }
}`,...I.parameters?.docs?.source}}};S.parameters={...S.parameters,docs:{...S.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Senha',
    placeholder: '••••••••',
    type: 'password'
  }
}`,...S.parameters?.docs?.source}}};V.parameters={...V.parameters,docs:{...V.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'E-mail',
    placeholder: 'voce@exemplo.com',
    type: 'email',
    iconLeft: 'i-carbon-email'
  }
}`,...V.parameters?.docs?.source}}};x.parameters={...x.parameters,docs:{...x.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Telefone',
    placeholder: '(11) 99999-9999',
    type: 'tel',
    iconLeft: 'i-carbon-phone'
  }
}`,...x.parameters?.docs?.source}}};w.parameters={...w.parameters,docs:{...w.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Quantidade',
    placeholder: '0',
    type: 'number',
    modelValue: 1
  }
}`,...w.parameters?.docs?.source}}};q.parameters={...q.parameters,docs:{...q.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Campo desabilitado',
    placeholder: 'Não é editável',
    disabled: true,
    modelValue: 'Valor fixo'
  }
}`,...q.parameters?.docs?.source}}};E.parameters={...E.parameters,docs:{...E.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Campo somente leitura',
    readonly: true,
    modelValue: 'Este valor não pode ser editado'
  }
}`,...E.parameters?.docs?.source}}};L.parameters={...L.parameters,docs:{...L.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Input pequeno',
    placeholder: 'Tamanho pequeno',
    size: 'sm'
  }
}`,...L.parameters?.docs?.source}}};D.parameters={...D.parameters,docs:{...D.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Input grande',
    placeholder: 'Tamanho grande',
    size: 'lg'
  }
}`,...D.parameters?.docs?.source}}};z.parameters={...z.parameters,docs:{...z.parameters?.docs,source:{originalSource:`{
  args: {
    label: 'Input full width',
    placeholder: 'Ocupa 100% da largura',
    fullWidth: true
  }
}`,...z.parameters?.docs?.source}}};W.parameters={...W.parameters,docs:{...W.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Input
    },
    template: \`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <Input label="Small" placeholder="Small input" size="sm" />
        <Input label="Medium (default)" placeholder="Medium input" size="md" />
        <Input label="Large" placeholder="Large input" size="lg" />
      </div>
    \`
  })
}`,...W.parameters?.docs?.source}}};N.parameters={...N.parameters,docs:{...N.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Input
    },
    setup() {
      const defaultValue = ref('');
      const errorValue = ref('invalido');
      const disabledValue = ref('Desabilitado');
      const readonlyValue = ref('Somente leitura');
      return {
        defaultValue,
        errorValue,
        disabledValue,
        readonlyValue
      };
    },
    template: \`
      <div style="display: flex; flex-direction: column; gap: 1rem;">
        <Input
          v-model="defaultValue"
          label="Normal"
          placeholder="Digite algo..."
          helper="Texto de ajuda"
        />
        <Input
          v-model="errorValue"
          label="Com erro"
          placeholder="Digite algo..."
          error="Campo obrigatório"
        />
        <Input
          v-model="disabledValue"
          label="Desabilitado"
          :disabled="true"
        />
        <Input
          v-model="readonlyValue"
          label="Somente leitura"
          :readonly="true"
        />
      </div>
    \`
  })
}`,...N.parameters?.docs?.source}}};T.parameters={...T.parameters,docs:{...T.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Input
    },
    setup() {
      const name = ref('');
      const email = ref('');
      const phone = ref('');
      const password = ref('');
      return {
        name,
        email,
        phone,
        password
      };
    },
    template: \`
      <form style="display: flex; flex-direction: column; gap: 1rem; max-width: 400px;">
        <Input
          v-model="name"
          label="Nome completo"
          placeholder="John Doe"
          required
        />
        <Input
          v-model="email"
          label="E-mail"
          type="email"
          placeholder="voce@exemplo.com"
          icon-left="i-carbon-email"
          helper="Usaremos para login"
          required
        />
        <Input
          v-model="phone"
          label="Telefone"
          type="tel"
          placeholder="(11) 99999-9999"
          icon-left="i-carbon-phone"
        />
        <Input
          v-model="password"
          label="Senha"
          type="password"
          placeholder="••••••••"
          helper="Mínimo 8 caracteres"
          required
        />
      </form>
    \`
  })
}`,...T.parameters?.docs?.source}}};const ae=["Default","WithValue","WithHelper","WithError","Required","WithIconLeft","WithIconRight","Password","Email","Phone","Number","Disabled","Readonly","SmallSize","LargeSize","FullWidth","AllSizes","AllStates","FormExample"];export{W as AllSizes,N as AllStates,h as Default,q as Disabled,V as Email,T as FormExample,z as FullWidth,D as LargeSize,w as Number,S as Password,x as Phone,E as Readonly,y as Required,L as SmallSize,g as WithError,b as WithHelper,v as WithIconLeft,I as WithIconRight,f as WithValue,ae as __namedExportsOrder,ee as default};
