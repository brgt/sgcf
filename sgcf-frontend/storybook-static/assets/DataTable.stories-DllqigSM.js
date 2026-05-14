import{d as q,g as D,h as A,c as t,b as s,a as y,n as c,F as w,e as x,r as C,o as n,j as E,t as S,f as I}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as j}from"./_plugin-vue_export-helper-DlAUqK2U.js";const L={class:"nw-data-table"},U={class:"nw-data-table__wrapper"},F={class:"nw-data-table__thead"},J=["onClick"],W={class:"nw-data-table__th-content"},z={key:0,class:"nw-data-table__sort-icon"},M={key:1,class:"i-carbon-chevron-sort"},O=["onClick"],K={key:0,class:"nw-data-table__loading"},G={key:1,class:"nw-data-table__empty"},R=q({__name:"DataTable",props:{columns:{},data:{},loading:{type:Boolean,default:!1},striped:{type:Boolean,default:!0},hoverable:{type:Boolean,default:!0},bordered:{type:Boolean,default:!0}},emits:["rowClick"],setup(a,{emit:i}){const h=a,$=i,u=D(null),m=D("asc"),B=A(()=>u.value?[...h.data].sort((l,o)=>{const e=l[u.value],p=o[u.value];if(e===p)return 0;const r=e>p?1:-1;return m.value==="asc"?r:-r}):h.data),V=l=>{l.sortable&&(u.value===l.key?m.value=m.value==="asc"?"desc":"asc":(u.value=l.key,m.value="asc"))},N=l=>{$("rowClick",l)};return(l,o)=>(n(),t("div",L,[s("div",U,[s("table",{class:c(["nw-data-table__table",{"nw-data-table__table--bordered":a.bordered}])},[s("thead",F,[s("tr",null,[(n(!0),t(w,null,x(a.columns,e=>(n(),t("th",{key:String(e.key),class:c(["nw-data-table__th",`nw-data-table__th--${e.align||"left"}`,{"nw-data-table__th--sortable":e.sortable}]),style:E({width:e.width}),onClick:p=>V(e)},[s("div",W,[s("span",null,S(e.label),1),e.sortable?(n(),t("span",z,[u.value===e.key?(n(),t("span",{key:0,class:c(m.value==="asc"?"i-carbon-chevron-up":"i-carbon-chevron-down")},null,2)):(n(),t("span",M))])):y("",!0)])],14,J))),128))])]),a.loading?y("",!0):(n(),t("tbody",{key:0,class:c(["nw-data-table__tbody",{"nw-data-table__tbody--striped":a.striped}])},[(n(!0),t(w,null,x(B.value,(e,p)=>(n(),t("tr",{key:p,class:c(["nw-data-table__tr",{"nw-data-table__tr--hoverable":a.hoverable}]),onClick:r=>N(e)},[(n(!0),t(w,null,x(a.columns,r=>(n(),t("td",{key:String(r.key),class:c(["nw-data-table__td",`nw-data-table__td--${r.align||"left"}`])},[C(l.$slots,`cell-${String(r.key)}`,{row:e,value:e[r.key]},()=>[I(S(e[r.key]),1)],!0)],2))),128))],10,O))),128))],2))],2),a.loading?(n(),t("div",K,[...o[0]||(o[0]=[s("div",{class:"nw-data-table__spinner"},null,-1),s("p",null,"Loading...",-1)])])):y("",!0),!a.loading&&a.data.length===0?(n(),t("div",G,[C(l.$slots,"empty",{},()=>[o[1]||(o[1]=s("div",{class:"i-carbon-inbox text-4xl text-gray-400 mb-2"},null,-1)),o[2]||(o[2]=s("p",{class:"text-gray-600"},"No data available",-1))],!0)])):y("",!0)])]))}}),d=j(R,[["__scopeId","data-v-383e0ba5"]]);R.__docgenInfo={exportName:"default",displayName:"DataTable",description:"",tags:{},props:[{name:"columns",required:!0,type:{name:"Array",elements:[{name:"Column",elements:[{name:"T"}]}]}},{name:"data",required:!0,type:{name:"Array",elements:[{name:"T"}]}},{name:"loading",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"false"}},{name:"striped",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"true"}},{name:"hoverable",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"true"}},{name:"bordered",required:!1,type:{name:"boolean"},defaultValue:{func:!1,value:"true"}}],events:[{name:"rowClick",type:{names:["T"]}}],slots:[{name:"`cell-${String(column.key)}`",scoped:!0,bindings:[{name:"name",title:"binding"},{name:"row",title:"binding"},{name:"value",title:"binding"}]},{name:"empty"}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/molecules/DataTable/DataTable.vue"]};const Q={title:"Design System/Molecules/DataTable",component:d,tags:["autodocs"]},T=[{id:1,name:"John Doe",email:"john@example.com",role:"Admin",status:"active"},{id:2,name:"Jane Smith",email:"jane@example.com",role:"User",status:"active"},{id:3,name:"Bob Johnson",email:"bob@example.com",role:"Manager",status:"inactive"},{id:4,name:"Alice Brown",email:"alice@example.com",role:"User",status:"active"},{id:5,name:"Charlie Wilson",email:"charlie@example.com",role:"Admin",status:"active"}],b=[{key:"id",label:"ID",sortable:!0,width:"80px"},{key:"name",label:"Name",sortable:!0},{key:"email",label:"Email",sortable:!0},{key:"role",label:"Role",sortable:!0},{key:"status",label:"Status",sortable:!0,align:"center"}],v={render:()=>({components:{DataTable:d},setup(){return{columns:b,users:T}},template:'<DataTable :columns="columns" :data="users" />'})},g={render:()=>({components:{DataTable:d},setup(){return{columns:b,users:T,handleRowClick:i=>{alert(`Clicked: ${i.name}`)}}},template:`
      <DataTable
        :columns="columns"
        :data="users"
        @row-click="handleRowClick"
      >
        <template #cell-status="{ value }">
          <span :class="['px-2 py-1 rounded-full text-xs font-medium', value === 'active' ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800']">
            {{ value }}
          </span>
        </template>
        <template #cell-role="{ value }">
          <span class="font-medium text-primary">{{ value }}</span>
        </template>
      </DataTable>
    `})},f={render:()=>({components:{DataTable:d},setup(){return{columns:b,users:[]}},template:'<DataTable :columns="columns" :data="users" :loading="true" />'})},k={render:()=>({components:{DataTable:d},setup(){return{columns:b,users:[]}},template:'<DataTable :columns="columns" :data="[]" />'})},_={render:()=>({components:{DataTable:d},setup(){const a=D(!1),i=D(T);return{columns:b,currentData:i,loading:a,loadData:()=>{a.value=!0,setTimeout(()=>{a.value=!1},2e3)}}},template:`
      <div>
        <button @click="loadData" class="mb-4 px-4 py-2 bg-primary text-white rounded-lg">
          Reload Data
        </button>
        <DataTable :columns="columns" :data="currentData" :loading="loading" />
      </div>
    `})};v.parameters={...v.parameters,docs:{...v.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      DataTable
    },
    setup() {
      return {
        columns,
        users
      };
    },
    template: \`<DataTable :columns="columns" :data="users" />\`
  })
}`,...v.parameters?.docs?.source}}};g.parameters={...g.parameters,docs:{...g.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      DataTable
    },
    setup() {
      const handleRowClick = (row: User) => {
        alert(\`Clicked: \${row.name}\`);
      };
      return {
        columns,
        users,
        handleRowClick
      };
    },
    template: \`
      <DataTable
        :columns="columns"
        :data="users"
        @row-click="handleRowClick"
      >
        <template #cell-status="{ value }">
          <span :class="['px-2 py-1 rounded-full text-xs font-medium', value === 'active' ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800']">
            {{ value }}
          </span>
        </template>
        <template #cell-role="{ value }">
          <span class="font-medium text-primary">{{ value }}</span>
        </template>
      </DataTable>
    \`
  })
}`,...g.parameters?.docs?.source}}};f.parameters={...f.parameters,docs:{...f.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      DataTable
    },
    setup() {
      return {
        columns,
        users: []
      };
    },
    template: \`<DataTable :columns="columns" :data="users" :loading="true" />\`
  })
}`,...f.parameters?.docs?.source}}};k.parameters={...k.parameters,docs:{...k.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      DataTable
    },
    setup() {
      return {
        columns,
        users: []
      };
    },
    template: \`<DataTable :columns="columns" :data="[]" />\`
  })
}`,...k.parameters?.docs?.source}}};_.parameters={..._.parameters,docs:{..._.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      DataTable
    },
    setup() {
      const loading = ref(false);
      const currentData = ref(users);
      const loadData = () => {
        loading.value = true;
        setTimeout(() => {
          loading.value = false;
        }, 2000);
      };
      return {
        columns,
        currentData,
        loading,
        loadData
      };
    },
    template: \`
      <div>
        <button @click="loadData" class="mb-4 px-4 py-2 bg-primary text-white rounded-lg">
          Reload Data
        </button>
        <DataTable :columns="columns" :data="currentData" :loading="loading" />
      </div>
    \`
  })
}`,..._.parameters?.docs?.source}}};const X=["Default","WithCustomCells","Loading","Empty","Interactive"];export{v as Default,k as Empty,_ as Interactive,f as Loading,g as WithCustomCells,X as __namedExportsOrder,Q as default};
