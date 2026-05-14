import{d as x,c as p,b as u,F as C,e as k,u as w,o as P,n as M,t as V,g as b}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as D}from"./_plugin-vue_export-helper-DlAUqK2U.js";const S={class:"nw-pagination"},q=["disabled"],B=["disabled","onClick"],F=["disabled"],v=x({__name:"Pagination",props:{currentPage:{},totalPages:{},maxVisible:{default:7}},emits:["update:currentPage"],setup(t,{emit:y}){const g=t,h=y,_=computed(()=>{const{currentPage:r,totalPages:e,maxVisible:n}=g,a=[];if(e<=n)for(let s=1;s<=e;s++)a.push(s);else{const s=Math.floor(n/2);let o=Math.max(1,r-s),i=Math.min(e,o+n-1);i-o<n-1&&(o=Math.max(1,i-n+1)),o>1&&(a.push(1),o>2&&a.push("..."));for(let d=o;d<=i;d++)a.push(d);i<e&&(i<e-1&&a.push("..."),a.push(e))}return a}),m=r=>{r>=1&&r<=g.totalPages&&r!==g.currentPage&&h("update:currentPage",r)};return(r,e)=>(P(),p("nav",S,[u("button",{disabled:t.currentPage===1,onClick:e[0]||(e[0]=n=>m(t.currentPage-1)),class:"nw-pagination__button"},[...e[2]||(e[2]=[u("span",{class:"i-carbon-chevron-left"},null,-1)])],8,q),(P(!0),p(C,null,k(w(_),(n,a)=>(P(),p("button",{key:a,class:M(["nw-pagination__button",{"nw-pagination__button--active":n===t.currentPage}]),disabled:n==="...",onClick:s=>typeof n=="number"?m(n):void 0},V(n),11,B))),128)),u("button",{disabled:t.currentPage===t.totalPages,onClick:e[1]||(e[1]=n=>m(t.currentPage+1)),class:"nw-pagination__button"},[...e[3]||(e[3]=[u("span",{class:"i-carbon-chevron-right"},null,-1)])],8,F)]))}}),f=D(v,[["__scopeId","data-v-6153d0b3"]]);v.__docgenInfo={exportName:"default",displayName:"Pagination",description:"",tags:{},props:[{name:"currentPage",required:!0,type:{name:"number"}},{name:"totalPages",required:!0,type:{name:"number"}},{name:"maxVisible",required:!1,type:{name:"number"},defaultValue:{func:!1,value:"7"}}],events:[{name:"update:currentPage",type:{names:["number"]}}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/molecules/Pagination/Pagination.vue"]};const E={title:"Design System/Molecules/Pagination",component:f,tags:["autodocs"]},c={render:()=>({components:{Pagination:f},setup(){return{currentPage:b(1)}},template:`
      <div>
        <Pagination v-model:current-page="currentPage" :total-pages="10" />
        <p class="mt-4">Current page: {{ currentPage }}</p>
      </div>
    `})},l={render:()=>({components:{Pagination:f},setup(){return{currentPage:b(15)}},template:'<Pagination v-model:current-page="currentPage" :total-pages="50" />'})};c.parameters={...c.parameters,docs:{...c.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Pagination
    },
    setup() {
      const currentPage = ref(1);
      return {
        currentPage
      };
    },
    template: \`
      <div>
        <Pagination v-model:current-page="currentPage" :total-pages="10" />
        <p class="mt-4">Current page: {{ currentPage }}</p>
      </div>
    \`
  })
}`,...c.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Pagination
    },
    setup() {
      const currentPage = ref(15);
      return {
        currentPage
      };
    },
    template: \`<Pagination v-model:current-page="currentPage" :total-pages="50" />\`
  })
}`,...l.parameters?.docs?.source}}};const I=["Default","ManyPages"];export{c as Default,l as ManyPages,I as __namedExportsOrder,E as default};
