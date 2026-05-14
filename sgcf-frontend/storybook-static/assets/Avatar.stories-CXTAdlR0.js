import{d as B,g as b,h as t,c as r,a as L,n as y,t as N,j as V,o as n}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as $}from"./_plugin-vue_export-helper-DlAUqK2U.js";const M=["src","alt"],j={key:2,class:"nw-avatar__initials"},F={key:3,class:"nw-avatar__icon i-carbon-user","aria-hidden":"true"},G=["aria-label"],z=B({__name:"Avatar",props:{src:{},alt:{},name:{},size:{default:"md"},shape:{default:"circle"},status:{},showStatus:{type:Boolean,default:!1},icon:{},bgColor:{}},emits:["click","error"],setup(s,{emit:_}){const e=s,w=_,D=b(!1),C=b(!1),J=t(()=>{if(!e.name)return"";const a=e.name.trim().split(" ");return a.length===1?a[0].charAt(0).toUpperCase():(a[0].charAt(0)+a[a.length-1].charAt(0)).toUpperCase()}),g=t(()=>e.src&&!D.value),S=t(()=>e.icon&&!g.value),x=t(()=>!g.value&&!S.value&&J.value),k=t(()=>{const a=["nw-avatar"];return a.push(`nw-avatar--${e.size}`),a.push(`nw-avatar--${e.shape}`),e.showStatus&&e.status&&a.push("nw-avatar--with-status"),a}),q=t(()=>e.status?["nw-avatar__status",`nw-avatar__status--${e.status}`]:[]),I=a=>{D.value=!0,w("error",a)},U=()=>{C.value=!0},W=a=>{w("click",a)},E=t(()=>{if(e.bgColor)return e.bgColor;if(!e.name)return"var(--color-surface-elevated)";let a=0;for(let A=0;A<e.name.length;A++)a=e.name.charCodeAt(A)+((a<<5)-a);const f=["var(--color-error)","var(--color-warning)","#10b981","var(--color-info)","#8b5cf6","#ec4899","#06b6d4","#84cc16"];return f[Math.abs(a)%f.length]});return(a,f)=>(n(),r("div",{class:y(k.value),style:V({backgroundColor:x.value?E.value:void 0}),onClick:W},[g.value?(n(),r("img",{key:0,src:s.src,alt:s.alt||s.name||"Avatar",class:"nw-avatar__image",onError:I,onLoad:U},null,40,M)):S.value?(n(),r("span",{key:1,class:y(["nw-avatar__icon",s.icon]),"aria-hidden":"true"},null,2)):x.value?(n(),r("span",j,N(J.value),1)):(n(),r("span",F)),s.showStatus&&s.status?(n(),r("span",{key:4,class:y(q.value),"aria-label":`Status: ${s.status}`},null,10,G)):L("",!0)],6))}}),o=$(z,[["__scopeId","data-v-33c6ba39"]]);z.__docgenInfo={exportName:"default",displayName:"Avatar",description:"",tags:{},props:[{name:"src",description:"URL da imagem",required:!1,type:{name:"string"}},{name:"alt",description:"Alt text da imagem",required:!1,type:{name:"string"}},{name:"name",description:"Nome para gerar iniciais",required:!1,type:{name:"string"}},{name:"size",description:"Tamanho do avatar",required:!1,type:{name:"union",elements:[{name:'"xs"'},{name:'"sm"'},{name:'"md"'},{name:'"lg"'},{name:'"xl"'},{name:'"2xl"'}]},defaultValue:{func:!1,value:"'md'"}},{name:"shape",description:"Forma do avatar",required:!1,type:{name:"union",elements:[{name:'"circle"'},{name:'"square"'},{name:'"rounded"'}]},defaultValue:{func:!1,value:"'circle'"}},{name:"status",description:"Status indicator",required:!1,type:{name:"union",elements:[{name:'"online"'},{name:'"offline"'},{name:'"busy"'},{name:'"away"'}]}},{name:"showStatus",description:"Mostrar indicador de status",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"icon",description:"Custom icon (classe UnoCSS)",required:!1,type:{name:"string"}},{name:"bgColor",description:"Custom background color (para iniciais)",required:!1,type:{name:"string"}}],events:[{name:"click",type:{names:["MouseEvent"]}},{name:"error",type:{names:["Event"]}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/atoms/Avatar/Avatar.vue"]};const R={title:"Design System/Atoms/Avatar",component:o,tags:["autodocs"],argTypes:{size:{control:"select",options:["xs","sm","md","lg","xl","2xl"]},shape:{control:"select",options:["circle","square","rounded"]},status:{control:"select",options:["online","offline","busy","away"]},showStatus:{control:"boolean"}},args:{size:"md",shape:"circle",showStatus:!1}},c={args:{name:"John Doe"}},i={args:{src:"https://i.pravatar.cc/150?img=12",name:"John Doe",alt:"John Doe avatar"}},l={args:{name:"Welysson Soares"}},m={args:{icon:"i-carbon-user-avatar"}},u={args:{name:"John Doe",showStatus:!0,status:"online"}},p={render:()=>({components:{Avatar:o},template:`
      <div style="display: flex; align-items: center; gap: 1rem;">
        <Avatar name="JD" size="xs" />
        <Avatar name="JD" size="sm" />
        <Avatar name="JD" size="md" />
        <Avatar name="JD" size="lg" />
        <Avatar name="JD" size="xl" />
        <Avatar name="JD" size="2xl" />
      </div>
    `})},d={render:()=>({components:{Avatar:o},template:`
      <div style="display: flex; gap: 1rem;">
        <Avatar name="JD" shape="circle" />
        <Avatar name="JD" shape="rounded" />
        <Avatar name="JD" shape="square" />
      </div>
    `})},v={render:()=>({components:{Avatar:o},template:`
      <div style="display: flex; gap: 1rem;">
        <Avatar name="JD" :show-status="true" status="online" />
        <Avatar name="JD" :show-status="true" status="offline" />
        <Avatar name="JD" :show-status="true" status="busy" />
        <Avatar name="JD" :show-status="true" status="away" />
      </div>
    `})},h={render:()=>({components:{Avatar:o},template:`
      <div class="nw-avatar-group">
        <Avatar src="https://i.pravatar.cc/150?img=1" name="User 1" />
        <Avatar src="https://i.pravatar.cc/150?img=2" name="User 2" />
        <Avatar src="https://i.pravatar.cc/150?img=3" name="User 3" />
        <Avatar name="+5" />
      </div>
    `})};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  args: {
    name: 'John Doe'
  }
}`,...c.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  args: {
    src: 'https://i.pravatar.cc/150?img=12',
    name: 'John Doe',
    alt: 'John Doe avatar'
  }
}`,...i.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  args: {
    name: 'Welysson Soares'
  }
}`,...l.parameters?.docs?.source}}};m.parameters={...m.parameters,docs:{...m.parameters?.docs,source:{originalSource:`{
  args: {
    icon: 'i-carbon-user-avatar'
  }
}`,...m.parameters?.docs?.source}}};u.parameters={...u.parameters,docs:{...u.parameters?.docs,source:{originalSource:`{
  args: {
    name: 'John Doe',
    showStatus: true,
    status: 'online'
  }
}`,...u.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Avatar
    },
    template: \`
      <div style="display: flex; align-items: center; gap: 1rem;">
        <Avatar name="JD" size="xs" />
        <Avatar name="JD" size="sm" />
        <Avatar name="JD" size="md" />
        <Avatar name="JD" size="lg" />
        <Avatar name="JD" size="xl" />
        <Avatar name="JD" size="2xl" />
      </div>
    \`
  })
}`,...p.parameters?.docs?.source}}};d.parameters={...d.parameters,docs:{...d.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Avatar
    },
    template: \`
      <div style="display: flex; gap: 1rem;">
        <Avatar name="JD" shape="circle" />
        <Avatar name="JD" shape="rounded" />
        <Avatar name="JD" shape="square" />
      </div>
    \`
  })
}`,...d.parameters?.docs?.source}}};v.parameters={...v.parameters,docs:{...v.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Avatar
    },
    template: \`
      <div style="display: flex; gap: 1rem;">
        <Avatar name="JD" :show-status="true" status="online" />
        <Avatar name="JD" :show-status="true" status="offline" />
        <Avatar name="JD" :show-status="true" status="busy" />
        <Avatar name="JD" :show-status="true" status="away" />
      </div>
    \`
  })
}`,...v.parameters?.docs?.source}}};h.parameters={...h.parameters,docs:{...h.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Avatar
    },
    template: \`
      <div class="nw-avatar-group">
        <Avatar src="https://i.pravatar.cc/150?img=1" name="User 1" />
        <Avatar src="https://i.pravatar.cc/150?img=2" name="User 2" />
        <Avatar src="https://i.pravatar.cc/150?img=3" name="User 3" />
        <Avatar name="+5" />
      </div>
    \`
  })
}`,...h.parameters?.docs?.source}}};const H=["Default","WithImage","Initials","WithIcon","WithStatus","AllSizes","AllShapes","AllStatuses","AvatarGroup"];export{d as AllShapes,p as AllSizes,v as AllStatuses,h as AvatarGroup,c as Default,l as Initials,m as WithIcon,i as WithImage,u as WithStatus,H as __namedExportsOrder,R as default};
